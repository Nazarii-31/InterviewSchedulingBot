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
        /// Create a Teams meeting link for scheduled interviews
        /// </summary>
        /// <param name="meetingRequest">Meeting details</param>
        /// <returns>Teams meeting URL</returns>
        Task<string> CreateTeamsMeetingAsync(MeetingRequest meetingRequest);
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