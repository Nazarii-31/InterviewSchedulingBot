using MediatR;
using InterviewBot.Application.DTOs;
using InterviewBot.Domain.Interfaces;
using InterviewBot.Domain.Entities;

namespace InterviewBot.Application.Interviews.Commands
{
    public class ScheduleInterviewCommand : IRequest<ScheduleInterviewResult>
    {
        public string Title { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> ParticipantEmails { get; set; } = new List<string>();
    }
    
    public class ScheduleInterviewResult
    {
        public bool Success { get; set; }
        public Guid InterviewId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
    
    public class ScheduleInterviewHandler : IRequestHandler<ScheduleInterviewCommand, ScheduleInterviewResult>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICalendarService _calendarService;
        
        public ScheduleInterviewHandler(IUnitOfWork unitOfWork, ICalendarService calendarService)
        {
            _unitOfWork = unitOfWork;
            _calendarService = calendarService;
        }
        
        public async Task<ScheduleInterviewResult> Handle(ScheduleInterviewCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Create Interview entity
                var interview = new Interview(request.Title, request.StartTime, request.Duration);
                
                // Add participants
                foreach (var email in request.ParticipantEmails)
                {
                    var participant = await _unitOfWork.Participants.GetByEmailAsync(email);
                    if (participant == null)
                    {
                        participant = new Participant(email, email, string.Empty);
                        await _unitOfWork.Participants.AddAsync(participant);
                    }
                    
                    // Default to Interviewer role, can be enhanced later
                    interview.AddParticipant(participant, ParticipantRole.Interviewer);
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
                    catch (Exception ex)
                    {
                        // Log but don't fail the interview creation
                        // Consider implementing compensating transaction here
                        return new ScheduleInterviewResult
                        {
                            Success = true,
                            InterviewId = interview.Id,
                            ErrorMessage = $"Interview created but calendar invite failed: {ex.Message}"
                        };
                    }
                    
                    return new ScheduleInterviewResult
                    {
                        Success = true,
                        InterviewId = interview.Id
                    };
                }
                
                return new ScheduleInterviewResult
                {
                    Success = false,
                    ErrorMessage = "Failed to save interview to database"
                };
            }
            catch (Exception ex)
            {
                return new ScheduleInterviewResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}