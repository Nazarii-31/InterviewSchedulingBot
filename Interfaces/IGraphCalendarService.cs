using Microsoft.Graph.Models;
using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Interfaces
{
    public interface IGraphCalendarService
    {
        // User-authenticated methods (delegated permissions)
        Task<string> CreateInterviewEventAsync(SchedulingRequest request, string userId);
        Task<List<Event>> GetAvailableTimeSlotsAsync(string userId, DateTime startDate, DateTime endDate);
        Task<bool> UpdateEventAsync(string eventId, string userId, SchedulingRequest updatedRequest);
        Task<bool> DeleteEventAsync(string eventId, string userId);

        // App-only authentication methods (for backward compatibility)
        Task<string> CreateInterviewEventAppOnlyAsync(SchedulingRequest request);
        Task<List<Event>> GetAvailableTimeSlotsAppOnlyAsync(string userEmail, DateTime startDate, DateTime endDate);
        Task<bool> UpdateEventAppOnlyAsync(string eventId, string userEmail, SchedulingRequest updatedRequest);
        Task<bool> DeleteEventAppOnlyAsync(string eventId, string userEmail);
    }
}