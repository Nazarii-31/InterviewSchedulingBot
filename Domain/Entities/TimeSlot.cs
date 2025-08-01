namespace InterviewBot.Domain.Entities
{
    public class TimeSlot
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Guid AvailabilityRecordId { get; set; }
        public AvailabilityRecord AvailabilityRecord { get; set; } = null!;
        
        public TimeSpan Duration => EndTime - StartTime;
        
        public bool OverlapsWith(TimeSlot other)
        {
            return StartTime < other.EndTime && EndTime > other.StartTime;
        }
        
        public bool ContainsTime(DateTime time)
        {
            return time >= StartTime && time < EndTime;
        }
    }
    
    public class AvailabilityRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ParticipantId { get; set; }
        public Participant Participant { get; set; } = null!;
        public DateTime Date { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        public ICollection<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
        
        public void AddTimeSlot(DateTime startTime, DateTime endTime)
        {
            if (startTime >= endTime)
                throw new ArgumentException("Start time must be before end time");
                
            var timeSlot = new TimeSlot
            {
                StartTime = startTime,
                EndTime = endTime,
                AvailabilityRecordId = Id
            };
            
            TimeSlots.Add(timeSlot);
            LastUpdated = DateTime.UtcNow;
        }
        
        public void ClearTimeSlots()
        {
            TimeSlots.Clear();
            LastUpdated = DateTime.UtcNow;
        }
        
        public bool IsAvailableAt(DateTime startTime, TimeSpan duration)
        {
            var endTime = startTime.Add(duration);
            return TimeSlots.Any(ts => ts.StartTime <= startTime && ts.EndTime >= endTime);
        }
    }
    
    public class RankedTimeSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double Score { get; set; }
        public int AvailableParticipants { get; set; }
        public int TotalParticipants { get; set; }
        public List<string> AvailableParticipantEmails { get; set; } = new List<string>();
        public List<ParticipantConflict> UnavailableParticipants { get; set; } = new List<ParticipantConflict>();
        
        public TimeSpan Duration => EndTime - StartTime;
        public double AvailabilityPercentage => TotalParticipants > 0 ? (double)AvailableParticipants / TotalParticipants * 100 : 0;
    }

    public class ParticipantConflict
    {
        public string Email { get; set; } = "";
        public string ConflictReason { get; set; } = "";
        public DateTime? ConflictStartTime { get; set; }
        public DateTime? ConflictEndTime { get; set; }
    }
}