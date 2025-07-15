using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Interfaces
{
    public interface ISchedulingService
    {
        Task<SchedulingResponse> FindAvailableTimeSlotsAsync(AvailabilityRequest request, string userId);
        List<AvailableTimeSlot> GetCommonAvailability(Dictionary<string, List<AvailableTimeSlot>> attendeeBusyTimes, AvailabilityRequest request);
        List<AvailableTimeSlot> FilterByWorkingHours(List<AvailableTimeSlot> timeSlots, AvailabilityRequest request);
        List<AvailableTimeSlot> GenerateWorkingHourSlots(DateTime startDate, DateTime endDate, AvailabilityRequest request);
    }
}