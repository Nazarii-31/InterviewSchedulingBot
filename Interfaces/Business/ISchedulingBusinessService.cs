using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces.Integration;

namespace InterviewSchedulingBot.Interfaces.Business
{
    /// <summary>
    /// Interface for pure business logic of interview scheduling
    /// Contains no integration concerns - only business rules and algorithms
    /// </summary>
    public interface ISchedulingBusinessService
    {
        /// <summary>
        /// Find optimal interview time slots based on business rules
        /// </summary>
        /// <param name="request">Scheduling requirements</param>
        /// <returns>Business-validated time suggestions</returns>
        Task<SchedulingBusinessResult> FindOptimalInterviewSlotsAsync(SchedulingBusinessRequest request);

        /// <summary>
        /// Validate interview scheduling requirements according to business rules
        /// </summary>
        /// <param name="request">Scheduling request to validate</param>
        /// <returns>Validation result with any errors or warnings</returns>
        Task<ValidationResult> ValidateSchedulingRequestAsync(SchedulingBusinessRequest request);

        /// <summary>
        /// Apply business rules to filter and rank time suggestions
        /// </summary>
        /// <param name="suggestions">Raw time suggestions</param>
        /// <param name="businessContext">Business context for filtering</param>
        /// <returns>Filtered and ranked suggestions</returns>
        Task<List<BusinessRankedTimeSlot>> ApplyBusinessRulesAsync(
            List<CalendarMeetingTimeSuggestion> suggestions, 
            BusinessSchedulingContext businessContext);

        /// <summary>
        /// Calculate scheduling conflicts and their business impact
        /// </summary>
        /// <param name="proposedTime">Proposed meeting time</param>
        /// <param name="participantSchedules">Participant availability data</param>
        /// <returns>Conflict analysis with business impact assessment</returns>
        Task<ConflictAnalysis> AnalyzeSchedulingConflictsAsync(
            DateTime proposedTime, 
            TimeSpan duration,
            Dictionary<string, List<BusyTimeSlot>> participantSchedules);

        /// <summary>
        /// Generate alternative scheduling options based on conflicts
        /// </summary>
        /// <param name="originalRequest">Original scheduling request</param>
        /// <param name="conflicts">Identified conflicts</param>
        /// <returns>Alternative scheduling options</returns>
        Task<List<AlternativeOption>> GenerateAlternativeOptionsAsync(
            SchedulingBusinessRequest originalRequest, 
            ConflictAnalysis conflicts);

        /// <summary>
        /// Apply interview-specific business logic (duration, buffer times, etc.)
        /// </summary>
        /// <param name="baseSchedule">Base schedule options</param>
        /// <param name="interviewType">Type of interview (technical, behavioral, etc.)</param>
        /// <returns>Interview-optimized schedule</returns>
        Task<InterviewScheduleResult> OptimizeForInterviewTypeAsync(
            List<CalendarMeetingTimeSuggestion> baseSchedule, 
            InterviewType interviewType);
    }

    public class SchedulingBusinessRequest
    {
        public List<string> ParticipantEmails { get; set; } = new();
        public int DurationMinutes { get; set; }
        public DateTime EarliestDate { get; set; }
        public DateTime LatestDate { get; set; }
        public InterviewType InterviewType { get; set; } = InterviewType.General;
        public Priority Priority { get; set; } = Priority.Normal;
        public WorkingHoursConfig? WorkingHours { get; set; }
        public List<BusinessConstraint> Constraints { get; set; } = new();
        public string? RequesterId { get; set; }
        public string? Department { get; set; }
    }

    public class SchedulingBusinessResult
    {
        public List<BusinessRankedTimeSlot> RecommendedSlots { get; set; } = new();
        public List<BusinessRankedTimeSlot> AlternativeSlots { get; set; } = new();
        public ValidationResult Validation { get; set; } = new();
        public BusinessInsights Insights { get; set; } = new();
        public string? RecommendationReasoning { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = new();
        public List<ValidationWarning> Warnings { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
    }

    public class ValidationError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Field { get; set; }
        public Severity Severity { get; set; } = Severity.Error;
    }

    public class ValidationWarning
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Field { get; set; }
        public string? Suggestion { get; set; }
    }

    public class BusinessRankedTimeSlot
    {
        public CalendarMeetingTimeSuggestion TimeSlot { get; set; } = new();
        public double BusinessScore { get; set; } // 0.0 to 100.0
        public BusinessRanking Ranking { get; set; } = new();
        public List<string> BusinessReasons { get; set; } = new();
        public List<BusinessConstraint> SatisfiedConstraints { get; set; } = new();
        public List<BusinessConstraint> ViolatedConstraints { get; set; } = new();
    }

    public class BusinessRanking
    {
        public double AvailabilityScore { get; set; }
        public double TimingScore { get; set; }
        public double ParticipantPreferenceScore { get; set; }
        public double BusinessRulesScore { get; set; }
        public double OverallScore { get; set; }
    }

    public class BusinessSchedulingContext
    {
        public string OrganizationId { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public Dictionary<string, object> OrganizationPolicies { get; set; } = new();
        public List<BusinessConstraint> GlobalConstraints { get; set; } = new();
        public WorkingHoursConfig OrganizationWorkingHours { get; set; } = new();
        public Dictionary<string, UserPreferences> ParticipantPreferences { get; set; } = new();
    }

    public class ConflictAnalysis
    {
        public bool HasConflicts { get; set; }
        public List<SchedulingConflict> Conflicts { get; set; } = new();
        public BusinessImpactLevel ImpactLevel { get; set; }
        public string? ImpactDescription { get; set; }
        public List<string> AffectedParticipants { get; set; } = new();
        public List<string> MitigationSuggestions { get; set; } = new();
    }

    public class SchedulingConflict
    {
        public string ParticipantEmail { get; set; } = string.Empty;
        public ConflictType Type { get; set; }
        public DateTime ConflictStart { get; set; }
        public DateTime ConflictEnd { get; set; }
        public string? ConflictDescription { get; set; }
        public ConflictSeverity Severity { get; set; }
        public bool CanBeResolved { get; set; }
    }

    public class AlternativeOption
    {
        public DateTime ProposedTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> AvailableParticipants { get; set; } = new();
        public List<string> UnavailableParticipants { get; set; } = new();
        public string? AlternativeReason { get; set; }
        public double FeasibilityScore { get; set; }
    }

    public class InterviewScheduleResult
    {
        public List<BusinessRankedTimeSlot> OptimizedSlots { get; set; } = new();
        public InterviewSpecificInsights Insights { get; set; } = new();
        public List<string> InterviewOptimizations { get; set; } = new();
    }

    public class BusinessInsights
    {
        public double AverageAvailability { get; set; }
        public List<string> BestTimeWindows { get; set; } = new();
        public List<string> ChallengingPeriods { get; set; } = new();
        public Dictionary<string, string> ParticipantInsights { get; set; } = new();
        public List<string> SchedulingTips { get; set; } = new();
    }

    public class InterviewSpecificInsights
    {
        public TimeSpan RecommendedBufferTime { get; set; }
        public List<string> InterviewPreparationTips { get; set; } = new();
        public Dictionary<string, string> RoleSpecificConsiderations { get; set; } = new();
    }

    public class BusinessConstraint
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ConstraintType Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool IsMandatory { get; set; }
        public int Priority { get; set; } = 1;
    }

    public class UserPreferences
    {
        public WorkingHoursConfig? PreferredWorkingHours { get; set; }
        public List<DayOfWeek> PreferredDays { get; set; } = new();
        public List<TimeRange> PreferredTimeRanges { get; set; } = new();
        public List<TimeRange> BlockedTimeRanges { get; set; } = new();
        public int MinimumNoticeHours { get; set; } = 24;
        public bool AllowBackToBackMeetings { get; set; } = false;
    }

    public class TimeRange
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string? Description { get; set; }
    }

    public enum InterviewType
    {
        General,
        Technical,
        Behavioral,
        Panel,
        Phone,
        Video,
        OnSite,
        Final
    }

    public enum BusinessImpactLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }

    public enum ConflictType
    {
        HardConflict,     // Existing meeting
        SoftConflict,     // Preference violation
        BusinessRule,     // Policy violation
        Availability      // Outside working hours
    }

    public enum ConflictSeverity
    {
        Minor,
        Moderate,
        Major,
        Critical
    }

    public enum ConstraintType
    {
        WorkingHours,
        BufferTime,
        MaxMeetingsPerDay,
        NoBackToBack,
        RequiredAttendee,
        TimeZone,
        BusinessPolicy
    }

    public enum Severity
    {
        Info,
        Warning,
        Error,
        Critical
    }
}