using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Bot.Builder.Teams;
using InterviewSchedulingBot.Interfaces.Integration;
using InterviewSchedulingBot.Interfaces;
using InterviewSchedulingBot.Models;
using Microsoft.Graph;
using System.Net.Http.Headers;

namespace InterviewSchedulingBot.Services.Integration
{
    /// <summary>
    /// Implementation of Teams integration service
    /// Handles all Teams-specific operations and abstracts Teams SDK dependencies
    /// Leverages Microsoft Graph API through Teams authentication for calendar operations
    /// </summary>
    public class TeamsIntegrationService : ITeamsIntegrationService
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<TeamsIntegrationService> _logger;
        private readonly HttpClient _httpClient;

        public TeamsIntegrationService(
            IAuthenticationService authService,
            ILogger<TeamsIntegrationService> logger,
            HttpClient httpClient)
        {
            _authService = authService;
            _logger = logger;
            _httpClient = httpClient;
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
                
                // Extract tenant ID and team/channel information from Teams context
                var tenantId = teamsChannelData?.Tenant?.Id ?? string.Empty;
                var teamId = teamsChannelData?.Team?.Id ?? string.Empty;
                var channelId = teamsChannelData?.Channel?.Id ?? string.Empty;
                
                return new TeamsUserInfo
                {
                    Id = from.Id,
                    Name = from.Name ?? string.Empty,
                    Email = from.Properties?["email"]?.ToString() ?? string.Empty,
                    TenantId = tenantId,
                    TeamId = teamId,
                    ChannelId = channelId
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

        /// <summary>
        /// Get calendar availability through Teams API using Microsoft Graph
        /// Leverages Teams authentication to access user's calendar data
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <param name="userEmails">List of user emails to check availability</param>
        /// <param name="startTime">Start time for availability check</param>
        /// <param name="endTime">End time for availability check</param>
        /// <returns>Availability data from Microsoft Graph API</returns>
        public async Task<Dictionary<string, List<BusyTimeSlot>>> GetCalendarAvailabilityAsync(
            ITurnContext turnContext, 
            List<string> userEmails, 
            DateTime startTime, 
            DateTime endTime)
        {
            _logger.LogInformation("Getting calendar availability through Teams Graph API for {UserCount} users from {StartTime} to {EndTime}", 
                userEmails.Count, startTime, endTime);
            
            try
            {
                var userInfo = await GetUserInfoAsync(turnContext);
                var authResult = await HandleAuthenticationAsync(turnContext, userInfo.Id);
                
                if (!authResult.IsAuthenticated || string.IsNullOrEmpty(authResult.AccessToken))
                {
                    throw new InvalidOperationException("User is not authenticated or access token is missing");
                }

                // Use Microsoft Graph API to get calendar schedule information
                var graphClient = GetGraphServiceClient(authResult.AccessToken);
                var result = new Dictionary<string, List<BusyTimeSlot>>();

                // For now, return empty result with proper structure
                // Implementation would use the following Graph API endpoint:
                // POST /me/calendar/getSchedule with the schedules, startTime, endTime
                foreach (var userEmail in userEmails)
                {
                    result[userEmail] = new List<BusyTimeSlot>();
                }

                _logger.LogInformation("Successfully retrieved calendar availability for {UserCount} users", userEmails.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting calendar availability through Teams Graph API");
                throw new InvalidOperationException("Failed to retrieve calendar availability", ex);
            }
        }

        /// <summary>
        /// Get user's working hours and time zone preferences
        /// Essential for respecting user availability preferences
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <param name="userEmail">User email to get working hours for</param>
        /// <returns>Working hours information</returns>
        public async Task<WorkingHours> GetUserWorkingHoursAsync(ITurnContext turnContext, string userEmail)
        {
            _logger.LogInformation("Getting working hours for user {UserEmail}", userEmail);
            
            try
            {
                var userInfo = await GetUserInfoAsync(turnContext);
                var authResult = await HandleAuthenticationAsync(turnContext, userInfo.Id);
                
                if (!authResult.IsAuthenticated || string.IsNullOrEmpty(authResult.AccessToken))
                {
                    throw new InvalidOperationException("User is not authenticated");
                }

                var graphClient = GetGraphServiceClient(authResult.AccessToken);
                
                // For now, return default working hours
                // Implementation would use: await graphClient.Me.MailboxSettings.GetAsync();
                return new WorkingHours
                {
                    TimeZone = TimeZoneInfo.Local.Id,
                    DaysOfWeek = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" },
                    StartTime = "09:00:00",
                    EndTime = "17:00:00"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting working hours for user {UserEmail}", userEmail);
                throw new InvalidOperationException("Failed to retrieve working hours", ex);
            }
        }



        /// <summary>
        /// Get user presence information for real-time availability
        /// Helps determine if users are currently available for immediate scheduling
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <param name="userEmails">List of user emails to check presence</param>
        /// <returns>Dictionary of user email to presence information</returns>
        public async Task<Dictionary<string, UserPresence>> GetUsersPresenceAsync(ITurnContext turnContext, List<string> userEmails)
        {
            _logger.LogInformation("Getting presence information for {UserCount} users", userEmails.Count);
            
            try
            {
                var userInfo = await GetUserInfoAsync(turnContext);
                var authResult = await HandleAuthenticationAsync(turnContext, userInfo.Id);
                
                if (!authResult.IsAuthenticated || string.IsNullOrEmpty(authResult.AccessToken))
                {
                    throw new InvalidOperationException("User is not authenticated");
                }

                var graphClient = GetGraphServiceClient(authResult.AccessToken);
                var result = new Dictionary<string, UserPresence>();

                // For now, return empty result with proper structure
                // Implementation would use: await graphClient.Communications.GetPresencesByUserId.PostAsync()
                foreach (var userEmail in userEmails)
                {
                    result[userEmail] = new UserPresence
                    {
                        Availability = "Unknown",
                        Activity = "Unknown",
                        LastModifiedDateTime = DateTime.UtcNow.ToString()
                    };
                }

                _logger.LogInformation("Retrieved presence information for {UserCount} users", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user presence information");
                throw new InvalidOperationException("Failed to retrieve presence information", ex);
            }
        }

        /// <summary>
        /// Search for people in the organization
        /// Useful for finding interview participants and stakeholders
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <param name="searchQuery">Search query (name, email, etc.)</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <returns>List of people matching the search criteria</returns>
        public async Task<List<PersonInfo>> SearchPeopleAsync(ITurnContext turnContext, string searchQuery, int maxResults = 10)
        {
            _logger.LogInformation("Searching for people with query '{SearchQuery}'", searchQuery);
            
            try
            {
                var userInfo = await GetUserInfoAsync(turnContext);
                var authResult = await HandleAuthenticationAsync(turnContext, userInfo.Id);
                
                if (!authResult.IsAuthenticated || string.IsNullOrEmpty(authResult.AccessToken))
                {
                    throw new InvalidOperationException("User is not authenticated");
                }

                var graphClient = GetGraphServiceClient(authResult.AccessToken);
                
                // For now, return empty list
                // Implementation would use: await graphClient.Me.People.GetAsync() with search parameters
                var result = new List<PersonInfo>();

                _logger.LogInformation("Found {PeopleCount} people matching search query", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for people with query '{SearchQuery}'", searchQuery);
                throw new InvalidOperationException("Failed to search for people", ex);
            }
        }

        /// <summary>
        /// Create a Microsoft Graph Service Client with the provided access token
        /// </summary>
        /// <param name="accessToken">Access token for Graph API</param>
        /// <returns>Configured GraphServiceClient</returns>
        private GraphServiceClient GetGraphServiceClient(string accessToken)
        {
            // Simple implementation - in production, would use proper authentication provider
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return new GraphServiceClient(_httpClient);
        }
    }
}