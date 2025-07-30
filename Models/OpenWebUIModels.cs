using System.Collections.Generic;

namespace InterviewSchedulingBot.Models
{
    public class OpenWebUIRequest
    {
        public string Query { get; set; } = string.Empty;
        public OpenWebUIRequestType Type { get; set; }
        public int MaxTokens { get; set; } = 500;
        public double Temperature { get; set; } = 0.7;
        public int Timeout { get; set; } = 30000; // 30 seconds timeout
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
    }

    public class OpenWebUIResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string GeneratedText { get; set; } = string.Empty;
        public List<string> Participants { get; set; } = new List<string>();
        public DateRange? DateRange { get; set; }
        public string TimeOfDay { get; set; } = string.Empty;
        public int? Duration { get; set; }
        public int? MinRequiredParticipants { get; set; }
        public string SpecificDay { get; set; } = string.Empty;
        public string RelativeDay { get; set; } = string.Empty;
        public int TokensUsed { get; set; }
        public double ProcessingTime { get; set; } // in seconds
    }

    public class MessageHistoryItem
    {
        public string Message { get; set; } = string.Empty;
        public bool IsFromBot { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ConflictDetail
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Participant { get; set; } = string.Empty;
    }

    public class TimeSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double AvailabilityScore { get; set; }
        public List<string> AvailableParticipants { get; set; } = new List<string>();
        public int TotalParticipants { get; set; }
    }

    public class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public enum OpenWebUIRequestType
    {
        SlotQuery,
        ConflictAnalysis,
        ResponseGeneration
    }

    // New models for intent recognition and slot finding
    public class IntentResponse
    {
        public string TopIntent { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public Dictionary<string, object> Entities { get; set; } = new Dictionary<string, object>();
    }

    public class SlotParameters
    {
        public List<string>? Participants { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? DurationMinutes { get; set; }
        public string? TimeOfDay { get; set; }
        public string? SpecificDay { get; set; }
        
        public bool HasMinimumRequiredInfo()
        {
            // Need at least some time indication (start date, time of day, or specific day)
            return StartDate.HasValue || !string.IsNullOrEmpty(TimeOfDay) || !string.IsNullOrEmpty(SpecificDay);
        }
    }

    public class SlotRequest
    {
        public List<string>? Participants { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? DurationMinutes { get; set; }
        public string? TimeOfDay { get; set; }
        public string? SpecificDay { get; set; }
    }

    // New models for specialized parameter extraction assistant
    public class ParameterExtractionResponse
    {
        public bool IsSlotRequest { get; set; }
        public ParameterExtractionData? Parameters { get; set; }
        public string? Message { get; set; } // For non-slot requests
        public string? SuggestedResponse { get; set; }
    }

    public class ParameterExtractionData
    {
        public int Duration { get; set; } = 60; // Default 60 minutes
        public TimeRangeData TimeRange { get; set; } = new TimeRangeData();
        public List<string> Participants { get; set; } = new List<string>();
    }

    public class TimeRangeData
    {
        public string Type { get; set; } = "specific_day"; // "specific_day", "this_week", "next_week", "date_range"
        public string? StartDate { get; set; } // Format: "2025-07-31"
        public string? EndDate { get; set; } // Format: "2025-07-31"  
        public string? TimeOfDay { get; set; } // "morning", "afternoon", "evening", "all_day", or null
    }

    // For compatibility with existing code
    public class TimeFrameData : TimeRangeData
    {
    }
}