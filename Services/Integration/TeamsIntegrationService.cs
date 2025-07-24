using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Bot.Builder.Teams;
using InterviewSchedulingBot.Interfaces.Integration;
using InterviewSchedulingBot.Interfaces;

namespace InterviewSchedulingBot.Services.Integration
{
    /// <summary>
    /// Implementation of Teams integration service
    /// Handles all Teams-specific operations and abstracts Teams SDK dependencies
    /// </summary>
    public class TeamsIntegrationService : ITeamsIntegrationService
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<TeamsIntegrationService> _logger;

        public TeamsIntegrationService(
            IAuthenticationService authService,
            ILogger<TeamsIntegrationService> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        public async Task<ResourceResponse> SendMessageAsync(ITurnContext turnContext, string message)
        {
            _logger.LogInformation("Sending message to Teams conversation");
            
            var messageActivity = MessageFactory.Text(message);
            return await turnContext.SendActivityAsync(messageActivity);
        }

        public async Task<ResourceResponse> SendAdaptiveCardAsync(ITurnContext turnContext, Attachment cardAttachment)
        {
            _logger.LogInformation("Sending adaptive card to Teams conversation");
            
            var messageActivity = MessageFactory.Attachment(cardAttachment);
            return await turnContext.SendActivityAsync(messageActivity);
        }

        public async Task<TeamsUserInfo> GetUserInfoAsync(ITurnContext turnContext)
        {
            _logger.LogInformation("Getting user info from Teams context");
            
            try
            {
                // Get Teams-specific user information
                var teamsChannelData = turnContext.Activity.GetChannelData<TeamsChannelData>();
                var from = turnContext.Activity.From;
                
                // Extract tenant ID from the Teams context
                var tenantId = teamsChannelData?.Tenant?.Id ?? string.Empty;
                
                return new TeamsUserInfo
                {
                    Id = from.Id,
                    Name = from.Name ?? string.Empty,
                    Email = from.Properties?["email"]?.ToString() ?? string.Empty,
                    TenantId = tenantId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user info from Teams context");
                return new TeamsUserInfo
                {
                    Id = turnContext.Activity.From.Id,
                    Name = turnContext.Activity.From.Name ?? "Unknown User"
                };
            }
        }

        public async Task<AuthenticationResult> HandleAuthenticationAsync(ITurnContext turnContext, string userId)
        {
            _logger.LogInformation("Handling authentication for user {UserId}", userId);
            
            try
            {
                // Check if user is already authenticated
                var isAuthenticated = await _authService.IsUserAuthenticatedAsync(userId);
                
                if (isAuthenticated)
                {
                    var accessToken = await _authService.GetAccessTokenAsync(userId);
                    return new AuthenticationResult
                    {
                        IsAuthenticated = true,
                        AccessToken = accessToken
                    };
                }
                
                // Generate login URL for OAuth flow
                var conversationId = turnContext.Activity.Conversation.Id;
                var loginUrl = _authService.GetAuthorizationUrl(userId, conversationId);
                
                return new AuthenticationResult
                {
                    IsAuthenticated = false,
                    LoginUrl = loginUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling authentication for user {UserId}", userId);
                return new AuthenticationResult
                {
                    IsAuthenticated = false,
                    ErrorMessage = "Authentication error occurred"
                };
            }
        }

        public async Task<string> CreateTeamsMeetingAsync(MeetingRequest meetingRequest)
        {
            _logger.LogInformation("Creating Teams meeting for {Subject}", meetingRequest.Subject);
            
            try
            {
                // This would integrate with Microsoft Graph to create a Teams meeting
                // For now, return a placeholder URL
                // In a real implementation, this would call Graph API's /me/onlineMeetings endpoint
                
                var meetingId = Guid.NewGuid().ToString("N")[..8];
                var teamsMeetingUrl = $"https://teams.microsoft.com/l/meetup-join/{meetingId}";
                
                _logger.LogInformation("Created Teams meeting with URL: {MeetingUrl}", teamsMeetingUrl);
                return teamsMeetingUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Teams meeting");
                throw new InvalidOperationException("Failed to create Teams meeting", ex);
            }
        }
    }
}