namespace InterviewBot.Domain.Entities
{
    public class Participant
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Email { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public string GraphUserId { get; private set; } = string.Empty;
        
        public ICollection<InterviewParticipant> InterviewParticipants { get; private set; } = new List<InterviewParticipant>();
        public ICollection<AvailabilityRecord> AvailabilityRecords { get; private set; } = new List<AvailabilityRecord>();
        
        // Private constructor for EF Core
        private Participant() { }
        
        public Participant(string email, string name, string graphUserId)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            GraphUserId = graphUserId ?? throw new ArgumentNullException(nameof(graphUserId));
        }
        
        public void UpdateName(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
        
        public void UpdateGraphUserId(string graphUserId)
        {
            GraphUserId = graphUserId ?? throw new ArgumentNullException(nameof(graphUserId));
        }
    }
    
    public class InterviewParticipant
    {
        public Guid InterviewId { get; set; }
        public Interview Interview { get; set; } = null!;
        public Guid ParticipantId { get; set; }
        public Participant Participant { get; set; } = null!;
        public ParticipantRole Role { get; set; }
        public ParticipantStatus Status { get; set; }
        
        public void AcceptInvitation()
        {
            Status = ParticipantStatus.Accepted;
        }
        
        public void DeclineInvitation()
        {
            Status = ParticipantStatus.Declined;
        }
        
        public void MarkAsTentative()
        {
            Status = ParticipantStatus.Tentative;
        }
    }
    
    public enum ParticipantStatus
    {
        Pending,
        Accepted,
        Declined,
        Tentative
    }
}