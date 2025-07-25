using Microsoft.Identity.Client;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System.Collections.Concurrent;
using InterviewSchedulingBot.Interfaces;

namespace InterviewSchedulingBot.Services
{

    public class AuthenticationService : IAuthenticationService
    {
        private readonly IConfiguration _configuration;
        private readonly IConfidentialClientApplication? _msalClient;
        private readonly ConcurrentDictionary<string, UserTokenInfo> _tokenStorage;
        private readonly bool _isConfigured;

        public AuthenticationService(IConfiguration configuration)
        {
            _configuration = configuration;
            _tokenStorage = new ConcurrentDictionary<string, UserTokenInfo>();
            
            var clientId = _configuration["Authentication:ClientId"];
            var clientSecret = _configuration["Authentication:ClientSecret"];
            var authority = _configuration["Authentication:Authority"]?.Replace("{tenant}", _configuration["Authentication:TenantId"]);

            // Check if authentication is configured
            _isConfigured = !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret) && !string.IsNullOrEmpty(authority);

            if (_isConfigured)
            {
                _msalClient = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority(authority)
                    .Build();
            }
            else
            {
                _msalClient = null;
            }
        }

        public async Task<string?> GetAccessTokenAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId) || !_isConfigured || _msalClient == null)
            {
                return null;
            }

            if (!_tokenStorage.TryGetValue(userId, out var tokenInfo))
            {
                return null;
            }

            // Check if token is still valid (with 5-minute buffer)
            if (tokenInfo.ExpiresOn.HasValue && tokenInfo.ExpiresOn.Value.AddMinutes(-5) > DateTimeOffset.UtcNow)
            {
                return tokenInfo.AccessToken;
            }

            // Try to refresh the token if we have a refresh token
            if (!string.IsNullOrEmpty(tokenInfo.RefreshToken))
            {
                try
                {
                    var scopes = _configuration.GetSection("Authentication:Scopes").Get<string[]>() ?? new[] { "https://graph.microsoft.com/.default" };
                    
                    // Try to get account by identifier instead of enumerating all accounts
                    IAccount? account = null;
                    try
                    {
                        var accounts = await _msalClient.GetAccountsAsync();
                        account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier.Contains(userId));
                    }
                    catch (MsalException)
                    {
                        // If we can't get accounts, account will remain null and we'll proceed to clear tokens
                    }
                    
                    if (account != null)
                    {
                        var result = await _msalClient.AcquireTokenSilent(scopes, account).ExecuteAsync();
                        
                        await StoreTokenAsync(userId, result.AccessToken, null, result.ExpiresOn);
                        return result.AccessToken;
                    }
                }
                catch (MsalException ex)
                {
                    // Silent token acquisition failed, user needs to sign in again
                    await ClearTokenAsync(userId);
                    
                    // Log the error for debugging purposes
                    System.Diagnostics.Debug.WriteLine($"Token refresh failed for user {userId}: {ex.Message}");
                }
            }

            // Token is expired and couldn't be refreshed
            await ClearTokenAsync(userId);
            return null;
        }

        public async Task StoreTokenAsync(string userId, string accessToken, string? refreshToken = null, DateTimeOffset? expiresOn = null)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(accessToken))
            {
                throw new ArgumentException("UserId and AccessToken cannot be null or empty");
            }

            var tokenInfo = new UserTokenInfo
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresOn = expiresOn,
                StoredAt = DateTimeOffset.UtcNow
            };

            _tokenStorage.AddOrUpdate(userId, tokenInfo, (key, oldValue) => tokenInfo);
            await Task.CompletedTask;
        }

        public async Task<bool> IsUserAuthenticatedAsync(string userId)
        {
            var token = await GetAccessTokenAsync(userId);
            return !string.IsNullOrEmpty(token);
        }

        public async Task ClearTokenAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            _tokenStorage.TryRemove(userId, out _);
            
            // Also remove from MSAL cache if authentication is configured
            if (_isConfigured && _msalClient != null)
            {
                try
                {
                    // Using GetAccountsAsync is deprecated but still functional for cache cleanup
                    // In production, consider implementing proper account identifier tracking
                    var accounts = await _msalClient.GetAccountsAsync();
                    var account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier.Contains(userId));
                    if (account != null)
                    {
                        await _msalClient.RemoveAsync(account);
                    }
                }
                catch (MsalException ex)
                {
                    // Ignore errors when clearing cache but log for debugging
                    System.Diagnostics.Debug.WriteLine($"Failed to clear MSAL cache for user {userId}: {ex.Message}");
                }
            }
        }

        public string GetAuthorizationUrl(string userId, string conversationId)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(conversationId))
            {
                throw new ArgumentException("UserId and ConversationId cannot be null or empty");
            }

            if (!_isConfigured || _msalClient == null)
            {
                throw new InvalidOperationException("Authentication is not configured. Please check appsettings.json");
            }

            try
            {
                var scopes = _configuration.GetSection("Authentication:Scopes").Get<string[]>() ?? new[] { "https://graph.microsoft.com/.default" };
                var redirectUri = _configuration["Authentication:RedirectUri"];
                
                if (string.IsNullOrEmpty(redirectUri))
                {
                    throw new InvalidOperationException("RedirectUri is not configured in appsettings.json");
                }
                
                // Create state parameter to include user and conversation information
                var state = $"{userId}|{conversationId}";
                
                var authUrl = _msalClient
                    .GetAuthorizationRequestUrl(scopes)
                    .WithRedirectUri(redirectUri)
                    .WithExtraQueryParameters($"state={Uri.EscapeDataString(state)}")
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                return authUrl.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to generate authorization URL: {ex.Message}", ex);
            }
        }

        private class UserTokenInfo
        {
            public string AccessToken { get; set; } = string.Empty;
            public string? RefreshToken { get; set; }
            public DateTimeOffset? ExpiresOn { get; set; }
            public DateTimeOffset StoredAt { get; set; }
        }
    }
}