using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Interfaces
{
    public interface IGraphSchedulingService
    {
        Task<GraphSchedulingResponse> FindOptimalMeetingTimesAsync(GraphSchedulingRequest request, string userId);
    }
}