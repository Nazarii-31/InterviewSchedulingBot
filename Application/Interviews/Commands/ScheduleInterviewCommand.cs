using MediatR;
using InterviewBot.Application.DTOs;
using InterviewBot.Domain.Interfaces;
using InterviewBot.Domain.Entities;

namespace InterviewBot.Application.Interviews.Commands
{
    public class ScheduleInterviewCommand : IRequest<Guid>
    {
        public string Title { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<ParticipantDto> Participants { get; set; } = new List<ParticipantDto>();
    }
    
    public class ScheduleInterviewHandler : IRequestHandler<ScheduleInterviewCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICalendarService _calendarService;
        
        public ScheduleInterviewHandler(IUnitOfWork unitOfWork, ICalendarService calendarService)
        {
            _unitOfWork = unitOfWork;
            _calendarService = calendarService;
        }
        
        public async Task<Guid> Handle(ScheduleInterviewCommand request, CancellationToken cancellationToken)
        {
            // Create Interview entity
            var interview = new Interview(request.Title, request.StartTime, request.Duration);
            
            // Add participants
            foreach (var participantDto in request.Participants)
            {
                var participant = await _unitOfWork.Participants.GetByEmailAsync(participantDto.Email);
                if (participant == null)
                {
                    participant = new Participant(participantDto.Email, participantDto.Name, string.Empty);
                    await _unitOfWork.Participants.AddAsync(participant);
                }
                
                if (Enum.TryParse<ParticipantRole>(participantDto.Role, out var role))
                {
                    interview.AddParticipant(participant, role);
                }
            }
            
            // Save to database
            await _unitOfWork.Interviews.AddAsync(interview);
            var success = await _unitOfWork.SaveChangesAsync();
            
            if (success)
            {
                // Create calendar event
                try
                {
                    await _calendarService.CreateMeetingAsync(interview);
                }
                catch (Exception)
                {
                    // Log but don't fail the interview creation
                    // Consider implementing compensating transaction here
                }
                
                return interview.Id;
            }
            
            throw new InvalidOperationException("Failed to schedule interview");
        }
    }
}