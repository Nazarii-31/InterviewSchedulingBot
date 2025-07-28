namespace InterviewBot.Application.DTOs
{
    public class InterviewDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<ParticipantDto> Participants { get; set; } = new List<ParticipantDto>();
    }
    
    public class ParticipantDto
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
    
    public class TimeSlotDto
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double Score { get; set; }
        public int AvailableParticipants { get; set; }
        public int TotalParticipants { get; set; }
    }
    
    public class SchedulingRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public List<string> ParticipantEmails { get; set; } = new List<string>();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DurationMinutes { get; set; } = 60;
        public int MaxResults { get; set; } = 5;
    }
}