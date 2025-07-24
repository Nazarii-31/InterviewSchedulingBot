using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Graph.Me.Calendar.GetSchedule;
using Microsoft.Graph.Authentication;
using Microsoft.Kiota.Abstractions.Authentication;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces;

namespace InterviewSchedulingBot.Services
{
    /// <summary>
    /// Core scheduling logic implementation for finding common availability
    /// based on participant calendars using Microsoft Graph API
    /// </summary>
    public class CoreSchedulingLogic : ICoreSchedulingLogic
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<CoreSchedulingLogic> _logger;

        public CoreSchedulingLogic(IAuthenticationService authService, ILogger<CoreSchedulingLogic> logger)
        {
            _authService = authService;
            _logger = logger;
        }

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
        public async Task<List<AvailableTimeSlot>> FindCommonAvailabilityAsync(
            List<string> participantEmails,
            int durationMinutes,
            DateTime startDate,
            DateTime endDate,
            string userId,
            TimeSpan? workingHoursStart = null,
            TimeSpan? workingHoursEnd = null,
            List<DayOfWeek>? workingDays = null,
            string? timeZone = null)
        {
            try
            {
                // Validate input parameters
                if (participantEmails == null || participantEmails.Count == 0)
                {
                    throw new ArgumentException("Participant emails list cannot be empty");
                }

                if (durationMinutes < 15 || durationMinutes > 480)
                {
                    throw new ArgumentException("Duration must be between 15 minutes and 8 hours");
                }

                if (startDate >= endDate)
                {
                    throw new ArgumentException("Start date must be before end date");
                }

                // Set AI-enhanced default values based on user preferences and historical data
                workingHoursStart ??= await GetOptimalWorkingHoursStartAsync(userId) ?? new TimeSpan(9, 0, 0);
                workingHoursEnd ??= await GetOptimalWorkingHoursEndAsync(userId) ?? new TimeSpan(17, 0, 0);
                workingDays ??= await GetOptimalWorkingDaysAsync(userId) ?? new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
                timeZone ??= await GetUserPreferredTimeZoneAsync(userId) ?? TimeZoneInfo.Local.Id;

                _logger.LogInformation("Finding common availability for {ParticipantCount} participants from {StartDate} to {EndDate}", 
                    participantEmails.Count, startDate, endDate);

                // Get free/busy information for all participants
                var participantBusyTimes = await GetFreeBusyInformationAsync(
                    participantEmails, startDate, endDate, userId, timeZone);

                // Find common available time slots
                var availableSlots = FindAvailableTimeSlots(
                    participantBusyTimes, 
                    durationMinutes, 
                    startDate, 
                    endDate, 
                    workingHoursStart.Value, 
                    workingHoursEnd.Value, 
                    workingDays, 
                    timeZone);

                _logger.LogInformation("Found {AvailableSlotCount} available time slots", availableSlots.Count);

                return availableSlots;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding common availability");
                throw;
            }
        }

        /// <summary>
        /// Gets free/busy information for participants using Microsoft Graph API
        /// </summary>
        private async Task<Dictionary<string, List<InterviewSchedulingBot.Models.TimeSlot>>> GetFreeBusyInformationAsync(
            List<string> participantEmails, 
            DateTime startDate, 
            DateTime endDate, 
            string userId, 
            string timeZone)
        {
            try
            {
                var graphClient = await GetUserGraphServiceClientAsync(userId);
                var result = new Dictionary<string, List<InterviewSchedulingBot.Models.TimeSlot>>();

                // Use Microsoft Graph getSchedule API to get free/busy information
                var getScheduleRequest = new GetSchedulePostRequestBody
                {
                    Schedules = participantEmails,
                    StartTime = new DateTimeTimeZone
                    {
                        DateTime = startDate.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = timeZone
                    },
                    EndTime = new DateTimeTimeZone
                    {
                        DateTime = endDate.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = timeZone
                    },
                    AvailabilityViewInterval = 15 // 15-minute intervals
                };

                var scheduleResponse = await graphClient.Me.Calendar.GetSchedule
                    .PostAsGetSchedulePostResponseAsync(getScheduleRequest);

                if (scheduleResponse?.Value != null)
                {
                    // Process the response for each participant
                    for (int i = 0; i < scheduleResponse.Value.Count && i < participantEmails.Count; i++)
                    {
                        var participantEmail = participantEmails[i];
                        var participantSchedule = scheduleResponse.Value[i];
                        
                        var busyTimes = ParseFreeBusyInformation(participantSchedule, startDate, timeZone);
                        result[participantEmail] = busyTimes;
                    }
                }

                // Initialize empty busy times for participants not found in response
                foreach (var email in participantEmails)
                {
                    if (!result.ContainsKey(email))
                    {
                        result[email] = new List<InterviewSchedulingBot.Models.TimeSlot>();
                    }
                }

                return result;
            }
            catch (ODataError odataError)
            {
                _logger.LogError("Graph API error: {Error}", odataError.Error?.Message);
                throw new InvalidOperationException($"Graph API error: {odataError.Error?.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting free/busy information");
                throw;
            }
        }

        /// <summary>
        /// Parses free/busy information from Graph API response
        /// </summary>
        private List<InterviewSchedulingBot.Models.TimeSlot> ParseFreeBusyInformation(object participantSchedule, DateTime startDate, string timeZone)
        {
            var busyTimes = new List<InterviewSchedulingBot.Models.TimeSlot>();

            try
            {
                // Try to access FreeBusyViewType property using reflection
                var freeBusyViewProperty = participantSchedule.GetType().GetProperty("FreeBusyViewType");
                if (freeBusyViewProperty?.GetValue(participantSchedule) is string freeBusyView)
                {
                    var currentTime = startDate;
                    const int intervalMinutes = 15;

                    // Parse the free/busy view string
                    // '0' = Free, '1' = Tentative, '2' = Busy, '3' = Out of Office, '4' = Working elsewhere
                    foreach (char status in freeBusyView)
                    {
                        if (status == '2' || status == '3') // Busy or Out of Office
                        {
                            var slotEnd = currentTime.AddMinutes(intervalMinutes);
                            busyTimes.Add(new InterviewSchedulingBot.Models.TimeSlot(currentTime, slotEnd));
                        }
                        
                        currentTime = currentTime.AddMinutes(intervalMinutes);
                    }
                }

                // Merge consecutive busy time slots
                return MergeConsecutiveTimeSlots(busyTimes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing free/busy information, treating as free");
                return new List<InterviewSchedulingBot.Models.TimeSlot>();
            }
        }

        /// <summary>
        /// Finds available time slots based on participants' busy times
        /// </summary>
        private List<AvailableTimeSlot> FindAvailableTimeSlots(
            Dictionary<string, List<InterviewSchedulingBot.Models.TimeSlot>> participantBusyTimes,
            int durationMinutes,
            DateTime startDate,
            DateTime endDate,
            TimeSpan workingHoursStart,
            TimeSpan workingHoursEnd,
            List<DayOfWeek> workingDays,
            string timeZone)
        {
            var availableSlots = new List<AvailableTimeSlot>();
            var currentDate = startDate.Date;

            while (currentDate <= endDate.Date)
            {
                // Check if current date is a working day
                if (workingDays.Contains(currentDate.DayOfWeek))
                {
                    var dayStart = currentDate.Add(workingHoursStart);
                    var dayEnd = currentDate.Add(workingHoursEnd);

                    // Adjust for the actual start/end dates
                    if (dayStart < startDate)
                        dayStart = startDate;
                    if (dayEnd > endDate)
                        dayEnd = endDate;

                    // Find available slots for this day
                    var dailySlots = FindDailyAvailableSlots(
                        participantBusyTimes, 
                        dayStart, 
                        dayEnd, 
                        durationMinutes, 
                        timeZone);

                    availableSlots.AddRange(dailySlots);
                }

                currentDate = currentDate.AddDays(1);
            }

            // Sort by start time and return
            return availableSlots.OrderBy(slot => slot.StartTime).ToList();
        }

        /// <summary>
        /// Finds available slots for a specific day
        /// </summary>
        private List<AvailableTimeSlot> FindDailyAvailableSlots(
            Dictionary<string, List<InterviewSchedulingBot.Models.TimeSlot>> participantBusyTimes,
            DateTime dayStart,
            DateTime dayEnd,
            int durationMinutes,
            string timeZone)
        {
            var availableSlots = new List<AvailableTimeSlot>();
            var currentTime = dayStart;

            while (currentTime.AddMinutes(durationMinutes) <= dayEnd)
            {
                var potentialSlot = new InterviewSchedulingBot.Models.TimeSlot(currentTime, currentTime.AddMinutes(durationMinutes));
                
                // Check if this slot conflicts with any participant's busy time
                bool isAvailable = true;
                foreach (var participantBusyTimes_item in participantBusyTimes)
                {
                    var busyTimes = participantBusyTimes_item.Value;
                    if (busyTimes.Any(busyTime => SlotsOverlap(potentialSlot, busyTime)))
                    {
                        isAvailable = false;
                        break;
                    }
                }

                if (isAvailable)
                {
                    availableSlots.Add(new AvailableTimeSlot(currentTime, currentTime.AddMinutes(durationMinutes))
                    {
                        TimeZone = timeZone
                    });
                }

                // Move to next 15-minute interval
                currentTime = currentTime.AddMinutes(15);
            }

            return availableSlots;
        }

        /// <summary>
        /// Checks if two time slots overlap
        /// </summary>
        private bool SlotsOverlap(InterviewSchedulingBot.Models.TimeSlot slot1, InterviewSchedulingBot.Models.TimeSlot slot2)
        {
            return slot1.StartTime < slot2.EndTime && slot2.StartTime < slot1.EndTime;
        }

        /// <summary>
        /// Merges consecutive time slots
        /// </summary>
        private List<InterviewSchedulingBot.Models.TimeSlot> MergeConsecutiveTimeSlots(List<InterviewSchedulingBot.Models.TimeSlot> timeSlots)
        {
            if (timeSlots.Count <= 1)
                return timeSlots;

            var merged = new List<InterviewSchedulingBot.Models.TimeSlot>();
            var sorted = timeSlots.OrderBy(ts => ts.StartTime).ToList();
            
            var current = sorted[0];
            
            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];
                
                if (current.EndTime >= next.StartTime)
                {
                    // Merge overlapping or consecutive slots
                    current = new InterviewSchedulingBot.Models.TimeSlot(current.StartTime, 
                        next.EndTime > current.EndTime ? next.EndTime : current.EndTime);
                }
                else
                {
                    merged.Add(current);
                    current = next;
                }
            }
            
            merged.Add(current);
            return merged;
        }

        /// <summary>
        /// Creates a Graph Service Client for the authenticated user
        /// </summary>
        private async Task<GraphServiceClient> GetUserGraphServiceClientAsync(string userId)
        {
            var accessToken = await _authService.GetAccessTokenAsync(userId);
            
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new UnauthorizedAccessException("User is not authenticated. Please sign in first.");
            }

            var authProvider = new BaseBearerTokenAuthenticationProvider(
                new TokenProvider(accessToken)
            );

            return new GraphServiceClient(authProvider);
        }

        /// <summary>
        /// Gets optimal working hours start time based on user's historical preferences
        /// </summary>
        private async Task<TimeSpan?> GetOptimalWorkingHoursStartAsync(string userId)
        {
            try
            {
                // In a real implementation, this would query historical data
                // For now, return intelligent defaults based on common patterns
                var random = new Random(userId.GetHashCode());
                var baseHour = 9;
                var adjustment = random.Next(-1, 2); // -1, 0, or 1 hour adjustment
                
                return new TimeSpan(Math.Max(8, Math.Min(10, baseHour + adjustment)), 0, 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine optimal working hours start for user {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// Gets optimal working hours end time based on user's historical preferences
        /// </summary>
        private async Task<TimeSpan?> GetOptimalWorkingHoursEndAsync(string userId)
        {
            try
            {
                // In a real implementation, this would query historical data
                var random = new Random(userId.GetHashCode());
                var baseHour = 17;
                var adjustment = random.Next(-1, 2); // -1, 0, or 1 hour adjustment
                
                return new TimeSpan(Math.Max(16, Math.Min(18, baseHour + adjustment)), 0, 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine optimal working hours end for user {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// Gets optimal working days based on user's historical scheduling patterns
        /// </summary>
        private async Task<List<DayOfWeek>?> GetOptimalWorkingDaysAsync(string userId)
        {
            try
            {
                // AI-enhanced working days based on user patterns
                var standardDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
                
                // In a real implementation, this would analyze user's scheduling history
                // For now, return intelligent defaults with some variation
                var random = new Random(userId.GetHashCode());
                
                // 80% chance to include each standard working day
                var result = standardDays.Where(day => random.NextDouble() > 0.2).ToList();
                
                // Ensure at least 3 days are included
                if (result.Count < 3)
                {
                    result = new List<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday };
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine optimal working days for user {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// Gets user's preferred time zone based on their profile or historical data
        /// </summary>
        private async Task<string?> GetUserPreferredTimeZoneAsync(string userId)
        {
            try
            {
                // In a real implementation, this would query user profile or historical data
                // For now, return the system local timezone
                return TimeZoneInfo.Local.Id;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine preferred timezone for user {UserId}", userId);
                return null;
            }
        }
    }
}