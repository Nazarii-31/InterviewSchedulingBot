using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Interfaces
{
    /// <summary>
    /// Interface for core scheduling logic to find common availability
    /// </summary>
    public interface ICoreSchedulingLogic
    {
        /// <summary>
        /// Finds common available time slots for a list of participants
        /// </summary>
        /// <param name="participantEmails">List of participant email addresses</param>
        /// <param name="durationMinutes">Required meeting duration in minutes</param>
        /// <param name="startDate">Start date for availability search</param>
        /// <param name="endDate">End date for availability search</param>
        /// <param name="userId">User ID for authentication</param>
        /// <param name="workingHoursStart">Working hours start time (default: 9 AM)</param>
        /// <param name="workingHoursEnd">Working hours end time (default: 5 PM)</param>
        /// <param name="workingDays">Working days (default: Monday-Friday)</param>
        /// <param name="timeZone">Time zone (default: system local)</param>
        /// <returns>List of available time slots</returns>
        Task<List<AvailableTimeSlot>> FindCommonAvailabilityAsync(
            List<string> participantEmails,
            int durationMinutes,
            DateTime startDate,
            DateTime endDate,
            string userId,
            TimeSpan? workingHoursStart = null,
            TimeSpan? workingHoursEnd = null,
            List<DayOfWeek>? workingDays = null,
            string? timeZone = null);
    }
}