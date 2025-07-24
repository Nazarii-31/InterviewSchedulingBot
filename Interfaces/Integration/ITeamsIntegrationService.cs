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
        /// Teams provides direct access to user's Outlook calendar data
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
    }

    public class TeamsUserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
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
}