using System.ComponentModel.DataAnnotations;

namespace InterviewSchedulingBot.Models
{
    public class BookingRequest
    {
        [Required]
        public MeetingTimeSuggestion SelectedSuggestion { get; set; } = new MeetingTimeSuggestion();

        [Required]
        public List<string> AttendeeEmails { get; set; } = new List<string>();

        [Required]
        public string MeetingTitle { get; set; } = string.Empty;

        public string MeetingDescription { get; set; } = string.Empty;

        public bool IsValid()
        {
            return SelectedSuggestion != null &&
                   AttendeeEmails.Count > 0 &&
                   AttendeeEmails.All(email => !string.IsNullOrEmpty(email) && email.Contains("@")) &&
                   !string.IsNullOrEmpty(MeetingTitle);
        }
    }
}