namespace InterviewBot.Domain.Entities
{
    public class Interview
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Title { get; private set; } = string.Empty;
        public DateTime StartTime { get; private set; }
        public TimeSpan Duration { get; private set; }
        public InterviewStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        
        public ICollection<InterviewParticipant> InterviewParticipants { get; private set; } = new List<InterviewParticipant>();
        
        // Private constructor for EF Core
        private Interview() { }
        
        public Interview(string title, DateTime startTime, TimeSpan duration)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            StartTime = startTime;
            Duration = duration;
            Status = InterviewStatus.Planned;
        }
        
        public void Schedule(DateTime startTime)
        {
            if (Status != InterviewStatus.Planned)
                throw new InvalidOperationException("Can only schedule planned interviews");
                
            StartTime = startTime;
            Status = InterviewStatus.Scheduled;
        }
        
        public void Cancel(string reason)
        {
            if (Status == InterviewStatus.Completed || Status == InterviewStatus.Cancelled)
                throw new InvalidOperationException("Cannot cancel completed or already cancelled interview");
                
            Status = InterviewStatus.Cancelled;
        }
        
        public void AddParticipant(Participant participant, ParticipantRole role)
        {
            if (participant == null)
                throw new ArgumentNullException(nameof(participant));
                
            var existingParticipant = InterviewParticipants
                .FirstOrDefault(ip => ip.ParticipantId == participant.Id);
                
            if (existingParticipant != null)
                throw new InvalidOperationException("Participant already added to interview");
                
            InterviewParticipants.Add(new InterviewParticipant
            {
                InterviewId = Id,
                ParticipantId = participant.Id,
                Role = role,
                Status = ParticipantStatus.Pending
            });
        }
        
        public void RemoveParticipant(Participant participant)
        {
            if (participant == null)
                throw new ArgumentNullException(nameof(participant));
                
            var participantToRemove = InterviewParticipants
                .FirstOrDefault(ip => ip.ParticipantId == participant.Id);
                
            if (participantToRemove != null)
            {
                InterviewParticipants.Remove(participantToRemove);
            }
        }
        
        public void Complete()
        {
            if (Status != InterviewStatus.Scheduled)
                throw new InvalidOperationException("Can only complete scheduled interviews");
                
            Status = InterviewStatus.Completed;
        }
    }
    
    public enum InterviewStatus
    {
        Planned,
        Scheduled,
        Completed,
        Cancelled
    }
    
    public enum ParticipantRole
    {
        Interviewer,
        Candidate,
        Coordinator
    }
}