namespace InterviewSchedulingBot.Models
{
    /// <summary>
    /// Models for integration layer operations
    /// Shared between Teams and calendar integrations
    /// </summary>
    
    public class CalendarMeetingTimeSuggestion
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double Confidence { get; set; } // 0.0 to 100.0
        public string? Reason { get; set; }
        public List<string> AvailableAttendees { get; set; } = new();
        public List<string> ConflictingAttendees { get; set; } = new();
    }

    public class WorkingHoursConfig
    {
        public TimeSpan StartTime { get; set; } = new(9, 0, 0); // 9 AM
        public TimeSpan EndTime { get; set; } = new(17, 0, 0); // 5 PM
        public List<DayOfWeek> WorkingDays { get; set; } = new() { 
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
            DayOfWeek.Thursday, DayOfWeek.Friday 
        };
        public string TimeZone { get; set; } = "UTC";
    }

    public class BusyTimeSlot
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Status { get; set; } = "Busy"; // Busy, Tentative, OutOfOffice, etc.
        public string? Subject { get; set; }
    }
}