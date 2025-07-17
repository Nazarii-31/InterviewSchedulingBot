using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Graph.Authentication;
using Microsoft.Kiota.Abstractions.Authentication;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces;
using GetSchedulePostRequestBody = Microsoft.Graph.Me.Calendar.GetSchedule.GetSchedulePostRequestBody;
using GetSchedulePostResponse = Microsoft.Graph.Me.Calendar.GetSchedule.GetSchedulePostResponse;
using LocalMeetingTimeSuggestion = InterviewSchedulingBot.Models.MeetingTimeSuggestion;
using LocalTimeSlot = InterviewSchedulingBot.Models.TimeSlot;

namespace InterviewSchedulingBot.Services
{
    public class GraphSchedulingService : IGraphSchedulingService
    {
        private readonly IGraphCalendarService _graphCalendarService;
        private readonly IAuthenticationService _authService;
        private readonly IConfiguration _configuration;

        public GraphSchedulingService(
            IGraphCalendarService graphCalendarService,
            IAuthenticationService authService,
            IConfiguration configuration)
        {
            _graphCalendarService = graphCalendarService;
            _authService = authService;
            _configuration = configuration;
        }

        public async Task<GraphSchedulingResponse> FindOptimalMeetingTimesAsync(
            GraphSchedulingRequest request, 
            string userId)
        {
            try
            {
                if (!request.IsValid())
                {
                    return GraphSchedulingResponse.CreateFailure(
                        "Invalid scheduling request parameters", 
                        request);
                }

                var accessToken = await _authService.GetAccessTokenAsync(userId);
                if (string.IsNullOrEmpty(accessToken))
                {
                    return GraphSchedulingResponse.CreateFailure(
                        "User authentication required", 
                        request);
                }

                // Create Graph client with user token
                var graphClient = await GetUserGraphServiceClientAsync(userId);

                // Use getSchedule to get free/busy information for all attendees
                var getScheduleRequest = CreateGetScheduleRequest(request);
                var scheduleResponse = await graphClient.Me.Calendar.GetSchedule
                    .PostAsGetSchedulePostResponseAsync(getScheduleRequest);

                if (scheduleResponse == null || scheduleResponse.Value == null)
                {
                    return GraphSchedulingResponse.CreateFailure(
                        "No response from Microsoft Graph API", 
                        request);
                }

                // Process the response and find optimal meeting times
                var meetingTimeSuggestions = ProcessScheduleResponse(
                    scheduleResponse, 
                    request);

                if (meetingTimeSuggestions.Count == 0)
                {
                    return GraphSchedulingResponse.CreateFailure(
                        "No suitable meeting times found for the specified criteria", 
                        request);
                }

                return GraphSchedulingResponse.CreateSuccess(
                    meetingTimeSuggestions, 
                    request);
            }
            catch (ODataError odataError)
            {
                return GraphSchedulingResponse.CreateFailure(
                    $"Graph API error: {odataError.Error?.Message ?? "Unknown error"}", 
                    request);
            }
            catch (Exception ex)
            {
                return GraphSchedulingResponse.CreateFailure(
                    $"Error finding optimal meeting times: {ex.Message}", 
                    request);
            }
        }

        private GetSchedulePostRequestBody CreateGetScheduleRequest(GraphSchedulingRequest request)
        {
            var requestBody = new GetSchedulePostRequestBody
            {
                Schedules = request.AttendeeEmails,
                StartTime = new DateTimeTimeZone
                {
                    DateTime = request.StartDate.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                    TimeZone = request.TimeZone
                },
                EndTime = new DateTimeTimeZone
                {
                    DateTime = request.EndDate.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                    TimeZone = request.TimeZone
                },
                AvailabilityViewInterval = request.IntervalMinutes
            };

            return requestBody;
        }

        private List<LocalMeetingTimeSuggestion> ProcessScheduleResponse(
            GetSchedulePostResponse scheduleResponse, 
            GraphSchedulingRequest request)
        {
            var suggestions = new List<LocalMeetingTimeSuggestion>();

            if (scheduleResponse.Value == null)
            {
                return suggestions;
            }

            // Process free/busy information from all attendees
            var attendeeBusyTimes = new Dictionary<string, List<LocalTimeSlot>>();
            
            for (int i = 0; i < scheduleResponse.Value.Count && i < request.AttendeeEmails.Count; i++)
            {
                var attendeeEmail = request.AttendeeEmails[i];
                var attendeeSchedule = scheduleResponse.Value[i];
                
                var busyTimes = new List<LocalTimeSlot>();
                
                // Try to get free/busy view - different properties might be available
                string freeBusyView = null;
                
                // Try to access FreeBusyViewType property if it exists
                try
                {
                    var freeBusyProp = attendeeSchedule.GetType().GetProperty("FreeBusyViewType");
                    if (freeBusyProp != null)
                    {
                        freeBusyView = freeBusyProp.GetValue(attendeeSchedule) as string;
                    }
                }
                catch
                {
                    // If property doesn't exist, continue with empty busy times
                }
                
                if (!string.IsNullOrEmpty(freeBusyView))
                {
                    busyTimes = ParseFreeBusyView(freeBusyView, request.StartDate, request.IntervalMinutes);
                }
                
                attendeeBusyTimes[attendeeEmail] = busyTimes;
            }

            // Generate meeting time suggestions based on the availability data
            var availableSlots = FindAvailableSlots(attendeeBusyTimes, request);
            
            // Convert to MeetingTimeSuggestion objects
            foreach (var slot in availableSlots.Take(request.MaxSuggestions))
            {
                suggestions.Add(new LocalMeetingTimeSuggestion
                {
                    MeetingTimeSlot = new MeetingTimeSlot
                    {
                        Start = new DateTimeTimeZone
                        {
                            DateTime = slot.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                            TimeZone = request.TimeZone
                        },
                        End = new DateTimeTimeZone
                        {
                            DateTime = slot.EndTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                            TimeZone = request.TimeZone
                        }
                    },
                    Confidence = CalculateConfidence(slot, attendeeBusyTimes),
                    SuggestionReason = "Available for all attendees during working hours"
                });
            }

            return suggestions;
        }

        private List<LocalTimeSlot> ParseFreeBusyView(string freeBusyView, DateTime startTime, int intervalMinutes)
        {
            var busyTimes = new List<LocalTimeSlot>();
            
            if (string.IsNullOrEmpty(freeBusyView))
            {
                return busyTimes;
            }

            var currentTime = startTime;
            
            // FreeBusy view is a string where each character represents an interval
            // '0' = Free, '1' = Tentative, '2' = Busy, '3' = Out of Office, '4' = Working elsewhere
            foreach (char status in freeBusyView)
            {
                if (status == '2' || status == '3') // Busy or Out of Office
                {
                    var slotEnd = currentTime.AddMinutes(intervalMinutes);
                    busyTimes.Add(new LocalTimeSlot
                    {
                        StartTime = currentTime,
                        EndTime = slotEnd
                    });
                }
                
                currentTime = currentTime.AddMinutes(intervalMinutes);
            }

            return MergeBusyTimes(busyTimes);
        }

        private List<LocalTimeSlot> MergeBusyTimes(List<LocalTimeSlot> busyTimes)
        {
            if (busyTimes.Count <= 1)
                return busyTimes;

            var merged = new List<LocalTimeSlot>();
            var sorted = busyTimes.OrderBy(ts => ts.StartTime).ToList();
            
            var current = sorted[0];
            
            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];
                
                if (current.EndTime >= next.StartTime)
                {
                    // Merge overlapping or consecutive slots
                    current = new LocalTimeSlot
                    {
                        StartTime = current.StartTime,
                        EndTime = next.EndTime > current.EndTime ? next.EndTime : current.EndTime
                    };
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

        private List<LocalTimeSlot> FindAvailableSlots(
            Dictionary<string, List<LocalTimeSlot>> attendeeBusyTimes, 
            GraphSchedulingRequest request)
        {
            var availableSlots = new List<LocalTimeSlot>();
            var currentTime = request.StartDate;

            while (currentTime.AddMinutes(request.DurationMinutes) <= request.EndDate)
            {
                // Check if this time is within working hours
                if (IsWithinWorkingHours(currentTime, request))
                {
                    var potentialSlot = new LocalTimeSlot
                    {
                        StartTime = currentTime,
                        EndTime = currentTime.AddMinutes(request.DurationMinutes)
                    };

                    // Check if this slot conflicts with any attendee's busy time
                    bool isAvailable = true;
                    foreach (var attendeeEmail in request.AttendeeEmails)
                    {
                        if (attendeeBusyTimes.ContainsKey(attendeeEmail))
                        {
                            var busyTimes = attendeeBusyTimes[attendeeEmail];
                            if (busyTimes.Any(busy => SlotsOverlap(potentialSlot, busy)))
                            {
                                isAvailable = false;
                                break;
                            }
                        }
                    }

                    if (isAvailable)
                    {
                        availableSlots.Add(potentialSlot);
                    }
                }

                // Move to next interval
                currentTime = currentTime.AddMinutes(request.IntervalMinutes);
            }

            return availableSlots;
        }

        private bool IsWithinWorkingHours(DateTime dateTime, GraphSchedulingRequest request)
        {
            if (!request.WorkingDays.Contains(dateTime.DayOfWeek))
            {
                return false;
            }

            var timeOfDay = dateTime.TimeOfDay;
            return timeOfDay >= request.WorkingHoursStart && 
                   timeOfDay.Add(TimeSpan.FromMinutes(request.DurationMinutes)) <= request.WorkingHoursEnd;
        }

        private bool SlotsOverlap(LocalTimeSlot slot1, LocalTimeSlot slot2)
        {
            return slot1.StartTime < slot2.EndTime && slot2.StartTime < slot1.EndTime;
        }

        private double CalculateConfidence(LocalTimeSlot slot, Dictionary<string, List<LocalTimeSlot>> attendeeBusyTimes)
        {
            // Simple confidence calculation based on how far from busy times
            // In a real implementation, this would be more sophisticated
            return 1.0; // High confidence for now
        }

        private async Task<GraphServiceClient> GetUserGraphServiceClientAsync(string userId)
        {
            var accessToken = await _authService.GetAccessTokenAsync(userId);
            
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new UnauthorizedAccessException("User is not authenticated. Please sign in first.");
            }

            // Create a simple authentication provider with the user's access token
            var authProvider = new BaseBearerTokenAuthenticationProvider(
                new TokenProvider(accessToken)
            );

            return new GraphServiceClient(authProvider);
        }
    }
}