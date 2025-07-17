using Microsoft.Graph.Models;

namespace InterviewSchedulingBot.Models
{
    public class MeetingTimeSuggestion
    {
        public MeetingTimeSlot MeetingTimeSlot { get; set; } = new MeetingTimeSlot();
        public double Confidence { get; set; }
        public string SuggestionReason { get; set; } = string.Empty;
    }

    public class MeetingTimeSlot
    {
        public DateTimeTimeZone Start { get; set; } = new DateTimeTimeZone();
        public DateTimeTimeZone End { get; set; } = new DateTimeTimeZone();
    }
}