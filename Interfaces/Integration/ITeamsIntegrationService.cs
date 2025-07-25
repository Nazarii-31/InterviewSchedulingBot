using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Interfaces.Integration
{
    /// <summary>
    /// Interface for Teams-specific integration operations
    /// Provides abstraction for Teams bot interactions and messaging
    /// </summary>
    public interface ITeamsIntegrationService
    {
        /// <summary>
        /// Send a message to a Teams conversation
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <param name="message">Message to send</param>
        /// <returns>Resource response from Teams</returns>
        Task<ResourceResponse> SendMessageAsync(ITurnContext turnContext, string message);

        /// <summary>
        /// Send an adaptive card to a Teams conversation
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <param name="cardAttachment">Adaptive card attachment</param>
        /// <returns>Resource response from Teams</returns>
        Task<ResourceResponse> SendAdaptiveCardAsync(ITurnContext turnContext, Attachment cardAttachment);

        /// <summary>
        /// Get user information from Teams context
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <returns>Teams user information</returns>
        Task<TeamsUserInfo> GetUserInfoAsync(ITurnContext turnContext);

        /// <summary>
        /// Handle authentication flow for Teams users
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <param name="userId">User identifier</param>
        /// <returns>Authentication result with login URL if needed</returns>
        Task<AuthenticationResult> HandleAuthenticationAsync(ITurnContext turnContext, string userId);

        /// <summary>
        /// Get calendar availability through Teams built-in calendar access
        /// Teams provides direct access to user's Outlook calendar data via Microsoft Graph
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <param name="userEmails">List of user emails to check availability</param>
        /// <param name="startTime">Start time for availability check</param>
        /// <param name="endTime">End time for availability check</param>
        /// <returns>Dictionary of user email to their busy time slots</returns>
        Task<Dictionary<string, List<BusyTimeSlot>>> GetCalendarAvailabilityAsync(
            ITurnContext turnContext, 
            List<string> userEmails, 
            DateTime startTime, 
            DateTime endTime);

        /// <summary>
        /// Get user's working hours and time zone preferences
        /// Essential for respecting user availability preferences when scheduling
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <param name="userEmail">User email to get working hours for</param>
        /// <returns>Working hours configuration</returns>
        Task<WorkingHours> GetUserWorkingHoursAsync(ITurnContext turnContext, string userEmail);



        /// <summary>
        /// Get user presence information for real-time availability
        /// Helps determine if users are currently available for immediate scheduling
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <param name="userEmails">List of user emails to check presence</param>
        /// <returns>Dictionary of user email to presence information</returns>
        Task<Dictionary<string, UserPresence>> GetUsersPresenceAsync(ITurnContext turnContext, List<string> userEmails);

        /// <summary>
        /// Search for people in the organization
        /// Useful for finding interview participants and stakeholders
        /// </summary>
        /// <param name="turnContext">Bot turn context</param>
        /// <param name="searchQuery">Search query (name, email, etc.)</param>
        /// <param name="maxResults">Maximum number of results to return</param>
        /// <returns>List of people matching the search criteria</returns>
        Task<List<PersonInfo>> SearchPeopleAsync(ITurnContext turnContext, string searchQuery, int maxResults = 10);
    }

    public class TeamsUserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
    }

    public class AuthenticationResult
    {
        public bool IsAuthenticated { get; set; }
        public string? LoginUrl { get; set; }
        public string? AccessToken { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class MeetingRequest
    {
        public string Subject { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<string> AttendeeEmails { get; set; } = new();
        public string? Description { get; set; }
        public string OrganizerEmail { get; set; } = string.Empty;
    }

    public class WorkingHours
    {
        public string TimeZone { get; set; } = string.Empty;
        public List<string> DaysOfWeek { get; set; } = new();
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
    }



    public class UserPresence
    {
        public string Availability { get; set; } = string.Empty;
        public string Activity { get; set; } = string.Empty;
        public string LastModifiedDateTime { get; set; } = string.Empty;
    }

    public class PersonInfo
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> EmailAddresses { get; set; } = new();
        public string JobTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }
}