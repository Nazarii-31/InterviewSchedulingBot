using Microsoft.Identity.Client;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System.Collections.Concurrent;

namespace InterviewSchedulingBot.Services
{
    public interface IAuthenticationService
    {
        Task<string?> GetAccessTokenAsync(string userId);
        Task StoreTokenAsync(string userId, string accessToken, string? refreshToken = null, DateTimeOffset? expiresOn = null);
        Task<bool> IsUserAuthenticatedAsync(string userId);
        Task ClearTokenAsync(string userId);
        string GetAuthorizationUrl(string userId, string conversationId);
    }

    public class AuthenticationService : IAuthenticationService
    {
        private readonly IConfiguration _configuration;
        private readonly IConfidentialClientApplication _msalClient;
        private readonly ConcurrentDictionary<string, UserTokenInfo> _tokenStorage;

        public AuthenticationService(IConfiguration configuration)
        {
            _configuration = configuration;
            _tokenStorage = new ConcurrentDictionary<string, UserTokenInfo>();
            
            var clientId = _configuration["Authentication:ClientId"];
            var clientSecret = _configuration["Authentication:ClientSecret"];
            var authority = _configuration["Authentication:Authority"]?.Replace("{tenant}", _configuration["Authentication:TenantId"]);

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(authority))
            {
                throw new InvalidOperationException("Authentication configuration is missing. Please check appsettings.json");
            }

            _msalClient = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(authority)
                .Build();
        }

        public async Task<string?> GetAccessTokenAsync(string userId)
        {
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
                    
                    var accounts = await _msalClient.GetAccountsAsync();
                    var account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier.Contains(userId));
                    
                    if (account != null)
                    {
                        var result = await _msalClient.AcquireTokenSilent(scopes, account).ExecuteAsync();
                        
                        await StoreTokenAsync(userId, result.AccessToken, null, result.ExpiresOn);
                        return result.AccessToken;
                    }
                }
                catch (MsalException)
                {
                    // Silent token acquisition failed, user needs to sign in again
                    await ClearTokenAsync(userId);
                }
            }

            return null;
        }

        public async Task StoreTokenAsync(string userId, string accessToken, string? refreshToken = null, DateTimeOffset? expiresOn = null)
        {
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
            _tokenStorage.TryRemove(userId, out _);
            
            // Also remove from MSAL cache
            try
            {
                var accounts = await _msalClient.GetAccountsAsync();
                var account = accounts.FirstOrDefault(a => a.HomeAccountId.Identifier.Contains(userId));
                if (account != null)
                {
                    await _msalClient.RemoveAsync(account);
                }
            }
            catch (MsalException)
            {
                // Ignore errors when clearing cache
            }
        }

        public string GetAuthorizationUrl(string userId, string conversationId)
        {
            var scopes = _configuration.GetSection("Authentication:Scopes").Get<string[]>() ?? new[] { "https://graph.microsoft.com/.default" };
            var redirectUri = _configuration["Authentication:RedirectUri"];
            
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

        private class UserTokenInfo
        {
            public string AccessToken { get; set; } = string.Empty;
            public string? RefreshToken { get; set; }
            public DateTimeOffset? ExpiresOn { get; set; }
            public DateTimeOffset StoredAt { get; set; }
        }
    }
}