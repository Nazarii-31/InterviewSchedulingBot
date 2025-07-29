using InterviewBot.Domain.Entities;

namespace InterviewBot.Domain.Interfaces
{
    public interface ICalendarService
    {
        Task<List<TimeSlot>> GetAvailabilityAsync(string userId, DateTime start, DateTime end);
        Task CreateMeetingAsync(Interview interview);
        Task CancelMeetingAsync(Guid interviewId);
    }
    
    public interface IAvailabilityService
    {
        Task<List<TimeSlot>> FindCommonAvailabilityAsync(
            List<string> participantIds, 
            DateTime startDate, 
            DateTime endDate,
            int durationMinutes,
            int minRequiredParticipants);
            
        Task<Dictionary<string, List<TimeSlot>>> GetParticipantAvailabilityAsync(
            List<string> participantIds,
            DateTime startDate,
            DateTime endDate);
    }
    
    public interface ISchedulingService
    {
        Task<List<RankedTimeSlot>> FindOptimalSlotsAsync(
            List<string> participantIds,
            DateTime startDate,
            DateTime endDate,
            int durationMinutes,
            int maxResults);
    }
    
    public interface ITelemetryService
    {
        void TrackAvailabilityLookup(string userId, int participantCount, TimeSpan duration);
        void TrackInterviewScheduled(string userId, Guid interviewId, int participantCount);
        void TrackException(Exception ex, string? userId = null, string? operation = null);
    }
}