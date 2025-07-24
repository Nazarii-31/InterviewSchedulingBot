using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Interfaces.Integration
{
    /// <summary>
    /// Interface for calendar integration operations
    /// Provides abstraction for calendar services (Microsoft Graph, Exchange, etc.)
    /// </summary>
    public interface ICalendarIntegrationService
    {
        /// <summary>
        /// Get busy times for a list of users within a date range
        /// </summary>
        /// <param name="userEmails">List of user email addresses</param>
        /// <param name="startTime">Start time for availability check</param>
        /// <param name="endTime">End time for availability check</param>
        /// <param name="accessToken">Authentication token</param>
        /// <returns>Dictionary of user email to their busy time slots</returns>
        Task<Dictionary<string, List<BusyTimeSlot>>> GetBusyTimesAsync(
            List<string> userEmails, 
            DateTime startTime, 
            DateTime endTime, 
            string accessToken);

        /// <summary>
        /// Create a calendar event
        /// </summary>
        /// <param name="eventRequest">Event details</param>
        /// <param name="accessToken">Authentication token</param>
        /// <returns>Created event ID</returns>
        Task<string> CreateCalendarEventAsync(CalendarEventRequest eventRequest, string accessToken);

        /// <summary>
        /// Update an existing calendar event
        /// </summary>
        /// <param name="eventId">Event identifier</param>
        /// <param name="eventRequest">Updated event details</param>
        /// <param name="accessToken">Authentication token</param>
        /// <returns>Success status</returns>
        Task<bool> UpdateCalendarEventAsync(string eventId, CalendarEventRequest eventRequest, string accessToken);

        /// <summary>
        /// Delete a calendar event
        /// </summary>
        /// <param name="eventId">Event identifier</param>
        /// <param name="accessToken">Authentication token</param>
        /// <returns>Success status</returns>
        Task<bool> DeleteCalendarEventAsync(string eventId, string accessToken);

        /// <summary>
        /// Get user's working hours configuration
        /// </summary>
        /// <param name="userEmail">User email address</param>
        /// <param name="accessToken">Authentication token</param>
        /// <returns>Working hours configuration</returns>
        Task<WorkingHoursConfig> GetWorkingHoursAsync(string userEmail, string accessToken);

        /// <summary>
        /// Find meeting times using calendar service's built-in functionality
        /// </summary>
        /// <param name="findMeetingRequest">Meeting time search criteria</param>
        /// <param name="accessToken">Authentication token</param>
        /// <returns>Suggested meeting times</returns>
        Task<List<CalendarMeetingTimeSuggestion>> FindMeetingTimesAsync(FindMeetingTimesRequest findMeetingRequest, string accessToken);
    }

    public class BusyTimeSlot
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Status { get; set; } = "Busy"; // Busy, Tentative, OutOfOffice, etc.
        public string? Subject { get; set; }
    }

    public class CalendarEventRequest
    {
        public string Subject { get; set; } = string.Empty;
        public string? Body { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<string> AttendeeEmails { get; set; } = new();
        public string Location { get; set; } = string.Empty;
        public bool IsOnlineMeeting { get; set; }
        public string? TimeZone { get; set; }
    }

    public class WorkingHoursConfig
    {
        public TimeSpan StartTime { get; set; } = new(9, 0, 0); // 9 AM
        public TimeSpan EndTime { get; set; } = new(17, 0, 0); // 5 PM
        public List<DayOfWeek> WorkingDays { get; set; } = new() { 
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
            DayOfWeek.Thursday, DayOfWeek.Friday 
        };
        public string TimeZone { get; set; } = "UTC";
    }

    public class FindMeetingTimesRequest
    {
        public List<string> AttendeeEmails { get; set; } = new();
        public TimeSpan Duration { get; set; }
        public DateTime EarliestTime { get; set; }
        public DateTime LatestTime { get; set; }
        public int MaxCandidates { get; set; } = 20;
        public WorkingHoursConfig? WorkingHours { get; set; }
    }

    public class CalendarMeetingTimeSuggestion
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double Confidence { get; set; } // 0.0 to 100.0
        public string? Reason { get; set; }
        public List<string> AvailableAttendees { get; set; } = new();
        public List<string> ConflictingAttendees { get; set; } = new();
    }
}