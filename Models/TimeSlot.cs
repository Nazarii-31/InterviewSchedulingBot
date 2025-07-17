namespace InterviewSchedulingBot.Models
{
    public class AvailableTimeSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int DurationMinutes => (int)(EndTime - StartTime).TotalMinutes;
        public string TimeZone { get; set; } = TimeZoneInfo.Local.Id;

        public AvailableTimeSlot(DateTime startTime, DateTime endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }

        public bool OverlapsWith(AvailableTimeSlot other)
        {
            return StartTime < other.EndTime && EndTime > other.StartTime;
        }

        public bool CanAccommodate(int durationMinutes)
        {
            return DurationMinutes >= durationMinutes;
        }

        public override string ToString()
        {
            return $"{StartTime:yyyy-MM-dd HH:mm} - {EndTime:HH:mm} ({DurationMinutes}min)";
        }
    }
}