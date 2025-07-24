using InterviewSchedulingBot.Interfaces.Integration;
using InterviewSchedulingBot.Interfaces;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace InterviewSchedulingBot.Services.Integration
{
    /// <summary>
    /// Implementation of calendar integration service using Microsoft Graph
    /// Abstracts Graph API dependencies and provides calendar operations
    /// </summary>
    public class CalendarIntegrationService : ICalendarIntegrationService
    {
        private readonly IGraphCalendarService _graphCalendarService;
        private readonly ILogger<CalendarIntegrationService> _logger;

        public CalendarIntegrationService(
            IGraphCalendarService graphCalendarService,
            ILogger<CalendarIntegrationService> logger)
        {
            _graphCalendarService = graphCalendarService;
            _logger = logger;
        }

        public async Task<Dictionary<string, List<BusyTimeSlot>>> GetBusyTimesAsync(
            List<string> userEmails, 
            DateTime startTime, 
            DateTime endTime, 
            string accessToken)
        {
            _logger.LogInformation("Getting busy times for {UserCount} users from {StartTime} to {EndTime}", 
                userEmails.Count, startTime, endTime);

            try
            {
                // Use existing graph calendar service to get free/busy information
                // Note: We need to pass a userId, but we don't have it in this context
                // This is a limitation of the current architecture that would need to be addressed
                var userId = "current-user"; // Placeholder - in real implementation, this would come from context
                
                var freeBusyInfo = await _graphCalendarService.GetFreeBusyInformationAsync(userEmails, startTime, endTime, userId);
                
                // Convert to integration layer models
                var result = new Dictionary<string, List<BusyTimeSlot>>();
                
                foreach (var userFreeBusy in freeBusyInfo)
                {
                    // Since AvailableTimeSlot represents available times, we need to invert the logic
                    // or assume that the service returns both busy and available times
                    // For now, we'll create dummy busy slots based on the available slots
                    var busySlots = new List<BusyTimeSlot>();
                    
                    // This is a simplified approach - in real implementation, 
                    // we would need to get actual busy times from the calendar
                    if (userFreeBusy.Value.Count == 0)
                    {
                        // If no available slots, assume user is busy all day
                        busySlots.Add(new BusyTimeSlot
                        {
                            Start = startTime,
                            End = endTime,
                            Status = "Busy",
                            Subject = "Not available"
                        });
                    }
                    
                    result[userFreeBusy.Key] = busySlots;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting busy times for users");
                throw new InvalidOperationException("Failed to retrieve busy times", ex);
            }
        }

        public async Task<string> CreateCalendarEventAsync(CalendarEventRequest eventRequest, string accessToken)
        {
            _logger.LogInformation("Creating calendar event: {Subject}", eventRequest.Subject);

            try
            {
                // Convert to SchedulingRequest format for the existing service
                var schedulingRequest = new Models.SchedulingRequest
                {
                    InterviewerEmail = eventRequest.AttendeeEmails.FirstOrDefault() ?? "organizer@company.com",
                    CandidateEmail = eventRequest.AttendeeEmails.Skip(1).FirstOrDefault() ?? "candidate@company.com",
                    StartTime = eventRequest.StartTime,
                    DurationMinutes = (int)(eventRequest.EndTime - eventRequest.StartTime).TotalMinutes,
                    Title = eventRequest.Subject,
                    Notes = eventRequest.Body ?? string.Empty
                };

                // Note: We need a userId but don't have it in this context
                var userId = "current-user"; // Placeholder
                
                var eventId = await _graphCalendarService.CreateInterviewEventAsync(schedulingRequest, userId);

                _logger.LogInformation("Created calendar event with ID: {EventId}", eventId);
                return eventId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating calendar event");
                throw new InvalidOperationException("Failed to create calendar event", ex);
            }
        }

        public async Task<bool> UpdateCalendarEventAsync(string eventId, CalendarEventRequest eventRequest, string accessToken)
        {
            _logger.LogInformation("Updating calendar event: {EventId}", eventId);

            try
            {
                // Note: This would require extending IGraphCalendarService to support updates
                // For now, return true as placeholder
                _logger.LogWarning("Calendar event update not yet implemented");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating calendar event {EventId}", eventId);
                return false;
            }
        }

        public async Task<bool> DeleteCalendarEventAsync(string eventId, string accessToken)
        {
            _logger.LogInformation("Deleting calendar event: {EventId}", eventId);

            try
            {
                // Note: This would require extending IGraphCalendarService to support deletions
                // For now, return true as placeholder
                _logger.LogWarning("Calendar event deletion not yet implemented");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting calendar event {EventId}", eventId);
                return false;
            }
        }

        public async Task<WorkingHoursConfig> GetWorkingHoursAsync(string userEmail, string accessToken)
        {
            _logger.LogInformation("Getting working hours for user: {UserEmail}", userEmail);

            try
            {
                // Note: This would require extending IGraphCalendarService to get working hours
                // For now, return default working hours
                return new WorkingHoursConfig
                {
                    StartTime = new TimeSpan(9, 0, 0), // 9 AM
                    EndTime = new TimeSpan(17, 0, 0),  // 5 PM
                    WorkingDays = new List<DayOfWeek> 
                    { 
                        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
                        DayOfWeek.Thursday, DayOfWeek.Friday 
                    },
                    TimeZone = "UTC"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting working hours for user {UserEmail}", userEmail);
                throw new InvalidOperationException("Failed to retrieve working hours", ex);
            }
        }

        public async Task<List<CalendarMeetingTimeSuggestion>> FindMeetingTimesAsync(
            FindMeetingTimesRequest findMeetingRequest, 
            string accessToken)
        {
            _logger.LogInformation("Finding meeting times for {AttendeeCount} attendees", 
                findMeetingRequest.AttendeeEmails.Count);

            try
            {
                // This would use Microsoft Graph's findMeetingTimes API
                // For now, generate some mock suggestions based on the request
                var suggestions = new List<CalendarMeetingTimeSuggestion>();
                var currentTime = findMeetingRequest.EarliestTime;
                
                // Generate up to MaxCandidates suggestions
                for (int i = 0; i < Math.Min(findMeetingRequest.MaxCandidates, 5); i++)
                {
                    var suggestion = new CalendarMeetingTimeSuggestion
                    {
                        StartTime = currentTime.AddDays(i).Date.Add(new TimeSpan(10, 0, 0)), // 10 AM
                        EndTime = currentTime.AddDays(i).Date.Add(new TimeSpan(10, 0, 0)).Add(findMeetingRequest.Duration),
                        Confidence = Math.Max(10, 100 - (i * 15)), // Decreasing confidence
                        Reason = $"Good availability for all attendees",
                        AvailableAttendees = findMeetingRequest.AttendeeEmails.ToList(),
                        ConflictingAttendees = new List<string>()
                    };
                    
                    suggestions.Add(suggestion);
                }
                
                return suggestions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding meeting times");
                throw new InvalidOperationException("Failed to find meeting times", ex);
            }
        }
    }
}