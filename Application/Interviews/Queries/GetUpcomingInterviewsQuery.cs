using MediatR;
using InterviewBot.Application.DTOs;
using InterviewBot.Domain.Interfaces;

namespace InterviewBot.Application.Interviews.Queries
{
    public class GetUpcomingInterviewsQuery : IRequest<List<InterviewDto>>
    {
        public string ParticipantEmail { get; set; } = string.Empty;
        public DateTime From { get; set; } = DateTime.Today;
        public DateTime To { get; set; } = DateTime.Today.AddDays(30);
    }
    
    public class GetUpcomingInterviewsHandler : IRequestHandler<GetUpcomingInterviewsQuery, List<InterviewDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        
        public GetUpcomingInterviewsHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        
        public async Task<List<InterviewDto>> Handle(GetUpcomingInterviewsQuery request, CancellationToken cancellationToken)
        {
            var participant = await _unitOfWork.Participants.GetByEmailAsync(request.ParticipantEmail);
            if (participant == null)
            {
                return new List<InterviewDto>();
            }
            
            var interviews = await _unitOfWork.Interviews.GetByParticipantAsync(participant.Id);
            
            var interviewDtos = interviews
                .Where(i => i.StartTime >= request.From && i.StartTime <= request.To)
                .Select(i => new InterviewDto
                {
                    Id = i.Id,
                    Title = i.Title,
                    StartTime = i.StartTime,
                    Duration = i.Duration,
                    Status = i.Status.ToString(),
                    Participants = i.InterviewParticipants.Select(ip => new ParticipantDto
                    {
                        Email = ip.Participant.Email,
                        Name = ip.Participant.Name,
                        Role = ip.Role.ToString(),
                        Status = ip.Status.ToString()
                    }).ToList()
                })
                .ToList();
            
            return interviewDtos;
        }
    }
}