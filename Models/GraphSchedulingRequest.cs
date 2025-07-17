using System.ComponentModel.DataAnnotations;

namespace InterviewSchedulingBot.Models
{
    public class GraphSchedulingRequest
    {
        [Required]
        public List<string> AttendeeEmails { get; set; } = new List<string>();

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [Range(15, 480)] // 15 minutes to 8 hours
        public int DurationMinutes { get; set; } = 60;

        public TimeSpan WorkingHoursStart { get; set; } = new TimeSpan(9, 0, 0); // 9 AM
        public TimeSpan WorkingHoursEnd { get; set; } = new TimeSpan(17, 0, 0); // 5 PM

        public List<DayOfWeek> WorkingDays { get; set; } = new List<DayOfWeek> 
        { 
            DayOfWeek.Monday, 
            DayOfWeek.Tuesday, 
            DayOfWeek.Wednesday, 
            DayOfWeek.Thursday, 
            DayOfWeek.Friday 
        };

        public string TimeZone { get; set; } = TimeZoneInfo.Local.Id;

        public int MaxSuggestions { get; set; } = 10;

        public int IntervalMinutes { get; set; } = 15; // For free/busy view

        public bool IsValid()
        {
            return AttendeeEmails.Count > 0 &&
                   AttendeeEmails.All(email => !string.IsNullOrEmpty(email) && email.Contains("@")) &&
                   StartDate < EndDate &&
                   StartDate >= DateTime.Now &&
                   DurationMinutes >= 15 &&
                   DurationMinutes <= 480 &&
                   WorkingHoursStart < WorkingHoursEnd &&
                   MaxSuggestions > 0 &&
                   IntervalMinutes > 0;
        }
    }
}