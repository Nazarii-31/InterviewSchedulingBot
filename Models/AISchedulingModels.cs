using System.ComponentModel.DataAnnotations;

namespace InterviewSchedulingBot.Models
{
    /// <summary>
    /// Model representing historical scheduling data for AI analysis
    /// </summary>
    public class SchedulingHistoryEntry
    {
        public string UserId { get; set; } = string.Empty;
        public List<string> AttendeeEmails { get; set; } = new();
        public DateTime ScheduledTime { get; set; }
        public int DurationMinutes { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan TimeOfDay { get; set; }
        public double UserSatisfactionScore { get; set; } // 0-1 scale
        public bool WasRescheduled { get; set; }
        public string? ReschedulingReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<string> ConflictingEmails { get; set; } = new();
        public string TimeZone { get; set; } = "UTC";
        public bool MeetingCompleted { get; set; }
    }

    /// <summary>
    /// Model representing user preferences learned from behavior
    /// </summary>
    public class UserPreferences
    {
        public string UserId { get; set; } = string.Empty;
        public List<DayOfWeek> PreferredDays { get; set; } = new();
        public List<TimeSpan> PreferredTimes { get; set; } = new();
        public int PreferredDurationMinutes { get; set; } = 60;
        public double MorningPreference { get; set; } = 0.5; // 0-1 scale
        public double AfternoonPreference { get; set; } = 0.5;
        public double EveningPreference { get; set; } = 0.1;
        public Dictionary<string, double> AttendeeAffinityScores { get; set; } = new();
        public Dictionary<DayOfWeek, double> DayPreferenceScores { get; set; } = new();
        public TimeSpan OptimalStartTime { get; set; } = new TimeSpan(10, 0, 0);
        public TimeSpan OptimalEndTime { get; set; } = new TimeSpan(15, 0, 0);
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public int TotalScheduledMeetings { get; set; }
        public double AverageReschedulingRate { get; set; }
    }

    /// <summary>
    /// Model for AI-driven time slot prediction
    /// </summary>
    public class TimeSlotPrediction
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double PredictedSuccessRate { get; set; } // 0-1 scale
        public double UserPreferenceScore { get; set; } // 0-1 scale
        public double AttendeeCompatibilityScore { get; set; } // 0-1 scale
        public double HistoricalSuccessScore { get; set; } // 0-1 scale
        public double OverallConfidence { get; set; } // 0-1 scale
        public string PredictionReason { get; set; } = string.Empty;
        public List<string> ContributingFactors { get; set; } = new();
        public Dictionary<string, double> FeatureScores { get; set; } = new();
        public double ConflictProbability { get; set; } // 0-1 scale
        public bool IsOptimalSlot { get; set; }
    }

    /// <summary>
    /// Model representing scheduling patterns learned from historical data
    /// </summary>
    public class SchedulingPattern
    {
        public string PatternId { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int FrequencyCount { get; set; }
        public double SuccessRate { get; set; } // 0-1 scale
        public double AverageUserSatisfaction { get; set; } // 0-1 scale
        public List<string> CommonAttendees { get; set; } = new();
        public int AverageDurationMinutes { get; set; }
        public double ReschedulingRate { get; set; } // 0-1 scale
        public DateTime LastOccurrence { get; set; }
        public string PatternType { get; set; } = "Regular"; // Regular, Recurring, Seasonal
        public Dictionary<string, object> PatternMetadata { get; set; } = new();
    }

    /// <summary>
    /// Request model for AI scheduling with enhanced parameters
    /// </summary>
    public class AISchedulingRequest
    {
        [Required]
        public List<string> AttendeeEmails { get; set; } = new();
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Range(15, 480)]
        public int DurationMinutes { get; set; } = 60;
        
        [Required]
        public DateTime StartDate { get; set; }
        
        [Required]
        public DateTime EndDate { get; set; }
        
        public bool UseLearningAlgorithm { get; set; } = true;
        public bool UseHistoricalData { get; set; } = true;
        public bool UseUserPreferences { get; set; } = true;
        public bool UseAttendeePatterns { get; set; } = true;
        public double MinimumConfidenceThreshold { get; set; } = 0.5;
        public int MaxSuggestions { get; set; } = 10;
        public List<DayOfWeek> PreferredDays { get; set; } = new();
        public TimeSpan? PreferredStartTime { get; set; }
        public TimeSpan? PreferredEndTime { get; set; }
        public string TimeZone { get; set; } = "UTC";
        public bool OptimizeForProductivity { get; set; } = true;
        public bool ConsiderReschedulingHistory { get; set; } = true;
        public Dictionary<string, object> AdditionalPreferences { get; set; } = new();
        
        public bool IsValid()
        {
            return AttendeeEmails.Count > 0 && 
                   !string.IsNullOrEmpty(UserId) && 
                   DurationMinutes >= 15 && 
                   DurationMinutes <= 480 && 
                   StartDate < EndDate;
        }
    }

    /// <summary>
    /// Response model for AI scheduling with detailed insights
    /// </summary>
    public class AISchedulingResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<TimeSlotPrediction> PredictedTimeSlots { get; set; } = new();
        public UserPreferences? UserPreferences { get; set; }
        public List<SchedulingPattern> RelevantPatterns { get; set; } = new();
        public Dictionary<string, double> AttendeeCompatibilityScores { get; set; } = new();
        public AISchedulingRequest? OriginalRequest { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        public double ProcessingTimeMs { get; set; }
        public Dictionary<string, object> AIInsights { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public double OverallConfidence { get; set; }
        public string AlgorithmVersion { get; set; } = "1.0";
        
        public static AISchedulingResponse CreateSuccess(
            List<TimeSlotPrediction> predictions, 
            AISchedulingRequest originalRequest,
            UserPreferences? preferences = null)
        {
            return new AISchedulingResponse
            {
                IsSuccess = true,
                Message = "AI scheduling completed successfully",
                PredictedTimeSlots = predictions,
                OriginalRequest = originalRequest,
                UserPreferences = preferences,
                OverallConfidence = predictions.Any() ? predictions.Average(p => p.OverallConfidence) : 0.0
            };
        }
        
        public static AISchedulingResponse CreateFailure(string message, AISchedulingRequest originalRequest)
        {
            return new AISchedulingResponse
            {
                IsSuccess = false,
                Message = message,
                OriginalRequest = originalRequest
            };
        }
        
        public bool HasPredictions => PredictedTimeSlots.Count > 0;
        public TimeSlotPrediction? BestPrediction => PredictedTimeSlots.OrderByDescending(p => p.OverallConfidence).FirstOrDefault();
    }
}