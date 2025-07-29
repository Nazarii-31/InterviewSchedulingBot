using MediatR;
using InterviewBot.Domain.Entities;
using InterviewBot.Domain.Interfaces;

namespace InterviewBot.Application.Availability.Queries
{
    public class FindOptimalSlotsQuery : IRequest<List<RankedTimeSlot>>
    {
        public List<string> ParticipantEmails { get; set; } = new List<string>();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan Duration { get; set; }
        public int MaxResults { get; set; } = 5;
    }
    
    public class FindOptimalSlotsHandler : IRequestHandler<FindOptimalSlotsQuery, List<RankedTimeSlot>>
    {
        private readonly ISchedulingService _schedulingService;
        private readonly IUnitOfWork _unitOfWork;
        
        public FindOptimalSlotsHandler(ISchedulingService schedulingService, IUnitOfWork unitOfWork)
        {
            _schedulingService = schedulingService;
            _unitOfWork = unitOfWork;
        }
        
        public async Task<List<RankedTimeSlot>> Handle(FindOptimalSlotsQuery request, CancellationToken cancellationToken)
        {
            // Get or create participants from emails
            var participantIds = new List<string>();
            
            foreach (var email in request.ParticipantEmails)
            {
                var participant = await _unitOfWork.Participants.GetByEmailAsync(email);
                if (participant == null)
                {
                    // Create new participant
                    participant = new Participant(email, email, string.Empty);
                    await _unitOfWork.Participants.AddAsync(participant);
                    await _unitOfWork.SaveChangesAsync();
                }
                participantIds.Add(participant.Id.ToString());
            }
            
            // Call scheduling service to find optimal slots
            var optimalSlots = await _schedulingService.FindOptimalSlotsAsync(
                participantIds,
                request.StartDate,
                request.EndDate,
                (int)request.Duration.TotalMinutes,
                request.MaxResults);
            
            return optimalSlots;
        }
    }
}