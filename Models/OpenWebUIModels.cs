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
}