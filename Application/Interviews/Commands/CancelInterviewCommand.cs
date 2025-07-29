using MediatR;
using InterviewBot.Domain.Interfaces;

namespace InterviewBot.Application.Interviews.Commands
{
    public class CancelInterviewCommand : IRequest<bool>
    {
        public Guid InterviewId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
    
    public class CancelInterviewHandler : IRequestHandler<CancelInterviewCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICalendarService _calendarService;
        
        public CancelInterviewHandler(IUnitOfWork unitOfWork, ICalendarService calendarService)
        {
            _unitOfWork = unitOfWork;
            _calendarService = calendarService;
        }
        
        public async Task<bool> Handle(CancelInterviewCommand request, CancellationToken cancellationToken)
        {
            var interview = await _unitOfWork.Interviews.GetByIdAsync(request.InterviewId);
            if (interview == null)
            {
                return false;
            }
            
            interview.Cancel(request.Reason);
            await _unitOfWork.Interviews.UpdateAsync(interview);
            
            var success = await _unitOfWork.SaveChangesAsync();
            
            if (success)
            {
                // Cancel calendar meeting
                try
                {
                    await _calendarService.CancelMeetingAsync(request.InterviewId);
                }
                catch (Exception)
                {
                    // Log but don't fail the cancellation
                }
            }
            
            return success;
        }
    }
}