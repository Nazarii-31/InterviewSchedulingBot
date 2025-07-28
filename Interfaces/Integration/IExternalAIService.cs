using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Interfaces.Integration
{
    /// <summary>
    /// Interface for external AI service integration
    /// Provides abstraction for AI providers (Azure OpenAI, OpenAI, etc.)
    /// </summary>
    public interface IExternalAIService
    {
        /// <summary>
        /// Get AI-powered scheduling suggestions
        /// </summary>
        /// <param name="request">Scheduling context and requirements</param>
        /// <returns>AI-generated scheduling suggestions</returns>
        Task<AISchedulingSuggestion> GetSchedulingSuggestionsAsync(AISchedulingRequest request);

        /// <summary>
        /// Analyze meeting patterns to improve future scheduling
        /// </summary>
        /// <param name="historicalData">Past meeting data</param>
        /// <returns>Pattern analysis results</returns>
        Task<MeetingPatternAnalysis> AnalyzeMeetingPatternsAsync(List<HistoricalMeeting> historicalData);

        /// <summary>
        /// Generate natural language responses for scheduling conversations
        /// </summary>
        /// <param name="context">Conversation context</param>
        /// <param name="userMessage">User's message</param>
        /// <returns>AI-generated response</returns>
        Task<string> GenerateConversationalResponseAsync(ConversationContext context, string userMessage);

        /// <summary>
        /// Extract meeting requirements from natural language input
        /// </summary>
        /// <param name="naturalLanguageInput">User's natural language request</param>
        /// <returns>Structured meeting requirements</returns>
        Task<MeetingRequirements> ExtractMeetingRequirementsAsync(string naturalLanguageInput);

        /// <summary>
        /// Score and rank meeting time suggestions based on various factors
        /// </summary>
        /// <param name="suggestions">Available time suggestions</param>
        /// <param name="context">Scheduling context</param>
        /// <returns>Ranked suggestions with scores</returns>
        Task<List<RankedMeetingTimeSuggestion>> RankMeetingTimesAsync(
            List<CalendarMeetingTimeSuggestion> suggestions, 
            AISchedulingContext context);
    }

    public class AISchedulingRequest
    {
        public List<string> ParticipantEmails { get; set; } = new();
        public int DurationMinutes { get; set; }
        public DateTime PreferredStartDate { get; set; }
        public DateTime PreferredEndDate { get; set; }
        public string? MeetingPurpose { get; set; }
        public Priority Priority { get; set; } = Priority.Normal;
        public List<TimePreference>? TimePreferences { get; set; }
        public string? SpecialRequirements { get; set; }
    }

    public class AISchedulingSuggestion
    {
        public List<RankedMeetingTimeSuggestion> Suggestions { get; set; } = new();
        public string? Reasoning { get; set; }
        public double ConfidenceScore { get; set; }
        public List<string> Considerations { get; set; } = new();
    }

    public class MeetingPatternAnalysis
    {
        public Dictionary<DayOfWeek, double> PreferredDays { get; set; } = new();
        public Dictionary<int, double> PreferredHours { get; set; } = new(); // Hour of day (0-23)
        public TimeSpan AverageMeetingDuration { get; set; }
        public List<string> CommonMeetingTypes { get; set; } = new();
        public Dictionary<string, int> FrequentAttendees { get; set; } = new();
    }

    public class ConversationContext
    {
        public string UserId { get; set; } = string.Empty;
        public List<ConversationMessage> MessageHistory { get; set; } = new();
        public SchedulingState CurrentState { get; set; } = SchedulingState.Initial;
        public Dictionary<string, object> SessionData { get; set; } = new();
    }

    public class ConversationMessage
    {
        public string Role { get; set; } = string.Empty; // "user" or "assistant"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class MeetingRequirements
    {
        public int DurationMinutes { get; set; }
        public List<string> ParticipantNames { get; set; } = new();
        public List<string> ParticipantEmails { get; set; } = new();
        public DateTime? PreferredDate { get; set; }
        public TimeSpan? PreferredTime { get; set; }
        public string? Subject { get; set; }
        public string? Location { get; set; }
        public Priority Priority { get; set; } = Priority.Normal;
        public bool RequiresRoom { get; set; }
        public string? SpecialRequirements { get; set; }
    }

    public class AISchedulingContext
    {
        public string UserId { get; set; } = string.Empty;
        public MeetingRequirements Requirements { get; set; } = new();
        public List<HistoricalMeeting> UserHistory { get; set; } = new();
        public Dictionary<string, object> Preferences { get; set; } = new();
    }

    public class RankedMeetingTimeSuggestion
    {
        public CalendarMeetingTimeSuggestion Suggestion { get; set; } = new();
        public double AIScore { get; set; } // 0.0 to 100.0
        public string? AIReasoning { get; set; }
        public List<string> PositiveFactors { get; set; } = new();
        public List<string> NegativeFactors { get; set; } = new();
    }

    public class HistoricalMeeting
    {
        public string Id { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<string> Attendees { get; set; } = new();
        public string Subject { get; set; } = string.Empty;
        public bool WasSuccessful { get; set; }
        public string? FeedbackNotes { get; set; }
    }

    public class TimePreference
    {
        public DayOfWeek? PreferredDay { get; set; }
        public TimeSpan? PreferredStartTime { get; set; }
        public TimeSpan? PreferredEndTime { get; set; }
        public string? Description { get; set; }
    }

    public enum Priority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Urgent = 4
    }

    public enum SchedulingState
    {
        Initial,
        CollectingParticipants,
        CollectingDuration,
        CollectingDateRange,
        CollectingPreferences,
        GeneratingSuggestions,
        PresentingSuggestions,
        ConfirmingSelection,
        BookingMeeting,
        Completed
    }
}