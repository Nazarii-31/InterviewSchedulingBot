using System.ComponentModel.DataAnnotations;

namespace InterviewSchedulingBot.Models
{
    public class SchedulingRequest
    {
        [Required]
        [EmailAddress]
        public string InterviewerEmail { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string CandidateEmail { get; set; } = string.Empty;

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        [Range(15, 480)] // 15 minutes to 8 hours
        public int DurationMinutes { get; set; } = 60;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Notes { get; set; } = string.Empty;

        public string? EventId { get; set; }

        public SchedulingRequestStatus Status { get; set; } = SchedulingRequestStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Helper properties
        public DateTime EndTime => StartTime.AddMinutes(DurationMinutes);

        public string FormattedDuration
        {
            get
            {
                var hours = DurationMinutes / 60;
                var minutes = DurationMinutes % 60;
                
                if (hours > 0 && minutes > 0)
                    return $"{hours}h {minutes}m";
                else if (hours > 0)
                    return $"{hours}h";
                else
                    return $"{minutes}m";
            }
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(InterviewerEmail) &&
                   !string.IsNullOrEmpty(CandidateEmail) &&
                   !string.IsNullOrEmpty(Title) &&
                   StartTime > DateTime.Now &&
                   DurationMinutes >= 15 &&
                   DurationMinutes <= 480;
        }

        public override string ToString()
        {
            return $"Interview: {Title} - {InterviewerEmail} & {CandidateEmail} on {StartTime:yyyy-MM-dd HH:mm} ({FormattedDuration})";
        }
    }

    public enum SchedulingRequestStatus
    {
        Pending,
        Scheduled,
        Cancelled,
        Completed,
        Failed
    }
}