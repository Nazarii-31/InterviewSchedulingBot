namespace InterviewSchedulingBot.Models
{
    public class SchedulingResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<AvailableTimeSlot> AvailableSlots { get; set; } = new List<AvailableTimeSlot>();
        public List<string> AttendeeEmails { get; set; } = new List<string>();
        public int RequestedDurationMinutes { get; set; }
        public DateTime SearchStartDate { get; set; }
        public DateTime SearchEndDate { get; set; }
        public string TimeZone { get; set; } = TimeZoneInfo.Local.Id;

        public bool HasAvailableSlots => AvailableSlots.Count > 0;

        public string FormattedSlotsText
        {
            get
            {
                if (!HasAvailableSlots)
                    return "No available time slots found.";

                var slotsText = "Available time slots:\n";
                for (int i = 0; i < AvailableSlots.Count && i < 10; i++)
                {
                    var slot = AvailableSlots[i];
                    slotsText += $"{i + 1}. {slot}\n";
                }

                if (AvailableSlots.Count > 10)
                    slotsText += $"... and {AvailableSlots.Count - 10} more slots";

                return slotsText;
            }
        }

        public static SchedulingResponse CreateSuccess(List<AvailableTimeSlot> availableSlots, AvailabilityRequest request)
        {
            return new SchedulingResponse
            {
                IsSuccess = true,
                Message = $"Found {availableSlots.Count} available time slot(s)",
                AvailableSlots = availableSlots,
                AttendeeEmails = request.AttendeeEmails,
                RequestedDurationMinutes = request.DurationMinutes,
                SearchStartDate = request.StartDate,
                SearchEndDate = request.EndDate,
                TimeZone = request.TimeZone
            };
        }

        public static SchedulingResponse CreateFailure(string message, AvailabilityRequest request)
        {
            return new SchedulingResponse
            {
                IsSuccess = false,
                Message = message,
                AttendeeEmails = request.AttendeeEmails,
                RequestedDurationMinutes = request.DurationMinutes,
                SearchStartDate = request.StartDate,
                SearchEndDate = request.EndDate,
                TimeZone = request.TimeZone
            };
        }
    }
}