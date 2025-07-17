using Microsoft.Graph;
using Microsoft.Graph.Models;
using InterviewSchedulingBot.Models;
using Azure.Identity;
using Microsoft.Graph.Authentication;
using Microsoft.Kiota.Abstractions.Authentication;
using InterviewSchedulingBot.Interfaces;

namespace InterviewSchedulingBot.Services
{
    public class TokenProvider : IAccessTokenProvider
    {
        private readonly string _accessToken;

        public TokenProvider(string accessToken)
        {
            _accessToken = accessToken;
        }

        public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_accessToken);
        }

        public AllowedHostsValidator AllowedHostsValidator { get; } = new AllowedHostsValidator();
    }

    public class GraphCalendarService : IGraphCalendarService
    {
        private readonly IConfiguration _configuration;
        private readonly IAuthenticationService _authService;
        private GraphServiceClient? _appOnlyGraphClient;

        public GraphCalendarService(IConfiguration configuration, IAuthenticationService authService)
        {
            _configuration = configuration;
            _authService = authService;
        }

        private GraphServiceClient GetAppOnlyGraphServiceClient()
        {
            if (_appOnlyGraphClient == null)
            {
                // Get configuration values
                var clientId = _configuration["GraphApi:ClientId"];
                var clientSecret = _configuration["GraphApi:ClientSecret"];
                var tenantId = _configuration["GraphApi:TenantId"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(tenantId))
                {
                    throw new InvalidOperationException("Graph API configuration is missing. Please check appsettings.json");
                }

                // Create authentication provider using Azure.Identity
                var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                
                // Create Graph client
                _appOnlyGraphClient = new GraphServiceClient(credential);
            }

            return _appOnlyGraphClient;
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

        // User-authenticated methods (delegated permissions)
        public async Task<string> CreateInterviewEventAsync(SchedulingRequest request, string userId)
        {
            try
            {
                var graphClient = await GetUserGraphServiceClientAsync(userId);

                var newEvent = new Event
                {
                    Subject = request.Title,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = $"<p>Interview scheduled between {request.InterviewerEmail} and {request.CandidateEmail}</p>" +
                                 $"<p>Duration: {request.DurationMinutes} minutes</p>" +
                                 $"<p>Notes: {request.Notes}</p>"
                    },
                    Start = new DateTimeTimeZone
                    {
                        DateTime = request.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = TimeZoneInfo.Local.Id
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = request.StartTime.AddMinutes(request.DurationMinutes).ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = TimeZoneInfo.Local.Id
                    },
                    Attendees = new List<Attendee>
                    {
                        new Attendee
                        {
                            EmailAddress = new EmailAddress
                            {
                                Address = request.InterviewerEmail,
                                Name = "Interviewer"
                            },
                            Type = AttendeeType.Required
                        },
                        new Attendee
                        {
                            EmailAddress = new EmailAddress
                            {
                                Address = request.CandidateEmail,
                                Name = "Candidate"
                            },
                            Type = AttendeeType.Required
                        }
                    },
                    IsOnlineMeeting = true,
                    OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness
                };

                // Create the event in the user's calendar (delegated permission)
                var createdEvent = await graphClient.Me
                    .Calendar
                    .Events
                    .PostAsync(newEvent);

                return createdEvent?.Id ?? "Event created but ID not available";
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create calendar event: {ex.Message}", ex);
            }
        }

        public async Task<List<Event>> GetAvailableTimeSlotsAsync(string userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var graphClient = await GetUserGraphServiceClientAsync(userId);

                var events = await graphClient.Me
                    .Calendar
                    .Events
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = 
                            $"start/dateTime ge '{startDate:yyyy-MM-ddTHH:mm:ss.fffK}' and end/dateTime le '{endDate:yyyy-MM-ddTHH:mm:ss.fffK}'";
                    });

                return events?.Value?.ToList() ?? new List<Event>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve calendar events: {ex.Message}", ex);
            }
        }

        public async Task<bool> UpdateEventAsync(string eventId, string userId, SchedulingRequest updatedRequest)
        {
            try
            {
                var graphClient = await GetUserGraphServiceClientAsync(userId);

                var eventUpdate = new Event
                {
                    Subject = updatedRequest.Title,
                    Start = new DateTimeTimeZone
                    {
                        DateTime = updatedRequest.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = TimeZoneInfo.Local.Id
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = updatedRequest.StartTime.AddMinutes(updatedRequest.DurationMinutes).ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = TimeZoneInfo.Local.Id
                    }
                };

                await graphClient.Me
                    .Calendar
                    .Events[eventId]
                    .PatchAsync(eventUpdate);

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to update calendar event: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteEventAsync(string eventId, string userId)
        {
            try
            {
                var graphClient = await GetUserGraphServiceClientAsync(userId);

                await graphClient.Me
                    .Calendar
                    .Events[eventId]
                    .DeleteAsync();

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete calendar event: {ex.Message}", ex);
            }
        }

        public async Task<Dictionary<string, List<AvailableTimeSlot>>> GetFreeBusyInformationAsync(List<string> attendeeEmails, DateTime startDate, DateTime endDate, string userId)
        {
            try
            {
                var graphClient = await GetUserGraphServiceClientAsync(userId);
                var result = new Dictionary<string, List<AvailableTimeSlot>>();

                // Initialize result dictionary
                foreach (var email in attendeeEmails)
                {
                    result[email] = new List<AvailableTimeSlot>();
                }

                // For now, let's use a simpler approach - get calendar events for each attendee
                // and convert them to busy time slots
                foreach (var email in attendeeEmails)
                {
                    try
                    {
                        // Get the user's calendar events
                        var events = await graphClient.Users[email]
                            .Calendar
                            .Events
                            .GetAsync(requestConfiguration =>
                            {
                                requestConfiguration.QueryParameters.Filter = 
                                    $"start/dateTime ge '{startDate:yyyy-MM-ddTHH:mm:ss.fffK}' and end/dateTime le '{endDate:yyyy-MM-ddTHH:mm:ss.fffK}'";
                                requestConfiguration.QueryParameters.Select = new[] { "start", "end", "showAs" };
                            });

                        if (events?.Value != null)
                        {
                            var busySlots = new List<AvailableTimeSlot>();
                            
                            foreach (var eventItem in events.Value)
                            {
                                if (eventItem.Start?.DateTime != null && eventItem.End?.DateTime != null)
                                {
                                    // Parse the date strings
                                    if (DateTime.TryParse(eventItem.Start.DateTime, out var eventStart) &&
                                        DateTime.TryParse(eventItem.End.DateTime, out var eventEnd))
                                    {
                                        // Only consider events that show as busy
                                        if (eventItem.ShowAs == FreeBusyStatus.Busy || 
                                            eventItem.ShowAs == FreeBusyStatus.Oof ||
                                            eventItem.ShowAs == FreeBusyStatus.Tentative)
                                        {
                                            busySlots.Add(new AvailableTimeSlot(eventStart, eventEnd));
                                        }
                                    }
                                }
                            }

                            result[email] = MergeConsecutiveTimeSlots(busySlots);
                        }
                    }
                    catch (Exception ex)
                    {
                        // If we can't get calendar for this user, treat as completely free
                        // Log the error but continue processing other attendees
                        System.Diagnostics.Debug.WriteLine($"Could not get calendar for {email}: {ex.Message}");
                        result[email] = new List<AvailableTimeSlot>();
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve free/busy information: {ex.Message}", ex);
            }
        }

        private List<AvailableTimeSlot> MergeConsecutiveTimeSlots(List<AvailableTimeSlot> timeSlots)
        {
            if (timeSlots.Count <= 1)
                return timeSlots;

            var merged = new List<AvailableTimeSlot>();
            var sorted = timeSlots.OrderBy(ts => ts.StartTime).ToList();
            
            var current = sorted[0];
            
            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];
                
                if (current.EndTime >= next.StartTime)
                {
                    // Merge overlapping or consecutive slots
                    current = new AvailableTimeSlot(current.StartTime, 
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

        public async Task<string> BookMeetingFromSuggestionAsync(InterviewSchedulingBot.Models.MeetingTimeSuggestion suggestion, List<string> attendeeEmails, string meetingTitle, string userId)
        {
            try
            {
                var graphClient = await GetUserGraphServiceClientAsync(userId);

                if (suggestion.MeetingTimeSlot?.Start?.DateTime == null || 
                    suggestion.MeetingTimeSlot?.End?.DateTime == null)
                {
                    throw new ArgumentException("Invalid meeting time data in suggestion");
                }

                var startTime = DateTime.Parse(suggestion.MeetingTimeSlot.Start.DateTime);
                var endTime = DateTime.Parse(suggestion.MeetingTimeSlot.End.DateTime);

                var newEvent = new Event
                {
                    Subject = meetingTitle,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = $"<p><strong>Meeting scheduled via AI-driven scheduling</strong></p>" +
                                 $"<p><strong>Attendees:</strong> {string.Join(", ", attendeeEmails)}</p>" +
                                 $"<p><strong>Duration:</strong> {(endTime - startTime).TotalMinutes} minutes</p>" +
                                 $"<p><strong>AI Confidence:</strong> {suggestion.Confidence * 100:F0}%</p>" +
                                 $"<p><strong>Scheduling Reason:</strong> {suggestion.SuggestionReason}</p>" +
                                 $"<p><em>This meeting was automatically scheduled using Microsoft Graph AI-driven scheduling algorithms.</em></p>"
                    },
                    Start = new DateTimeTimeZone
                    {
                        DateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = suggestion.MeetingTimeSlot.Start.TimeZone ?? "UTC"
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = suggestion.MeetingTimeSlot.End.TimeZone ?? "UTC"
                    },
                    Attendees = attendeeEmails.Select(email => new Attendee
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = email,
                            Name = email.Split('@')[0] // Use part before @ as name
                        },
                        Type = AttendeeType.Required
                    }).ToList(),
                    IsOnlineMeeting = true,
                    OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness,
                    // Explicitly request that invitations be sent
                    ResponseRequested = true
                };

                // Create the event in the user's calendar (delegated permission)
                // Microsoft Graph automatically sends invitations when creating calendar events with attendees
                var createdEvent = await graphClient.Me
                    .Calendar
                    .Events
                    .PostAsync(newEvent);

                if (createdEvent?.Id == null)
                {
                    throw new InvalidOperationException("Event was created but no ID was returned");
                }

                // Log successful booking for debugging
                System.Diagnostics.Debug.WriteLine($"Successfully booked meeting: {createdEvent.Id}");
                System.Diagnostics.Debug.WriteLine($"Meeting: {meetingTitle} at {startTime:yyyy-MM-dd HH:mm} - {endTime:HH:mm}");
                System.Diagnostics.Debug.WriteLine($"Attendees: {string.Join(", ", attendeeEmails)}");

                return createdEvent.Id;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to book meeting from suggestion: {ex.Message}", ex);
            }
        }

        // App-only authentication methods (for backward compatibility)
        public async Task<string> CreateInterviewEventAppOnlyAsync(SchedulingRequest request)
        {
            try
            {
                var graphClient = GetAppOnlyGraphServiceClient();

                var newEvent = new Event
                {
                    Subject = request.Title,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = $"<p>Interview scheduled between {request.InterviewerEmail} and {request.CandidateEmail}</p>" +
                                 $"<p>Duration: {request.DurationMinutes} minutes</p>" +
                                 $"<p>Notes: {request.Notes}</p>"
                    },
                    Start = new DateTimeTimeZone
                    {
                        DateTime = request.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = TimeZoneInfo.Local.Id
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = request.StartTime.AddMinutes(request.DurationMinutes).ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = TimeZoneInfo.Local.Id
                    },
                    Attendees = new List<Attendee>
                    {
                        new Attendee
                        {
                            EmailAddress = new EmailAddress
                            {
                                Address = request.InterviewerEmail,
                                Name = "Interviewer"
                            },
                            Type = AttendeeType.Required
                        },
                        new Attendee
                        {
                            EmailAddress = new EmailAddress
                            {
                                Address = request.CandidateEmail,
                                Name = "Candidate"
                            },
                            Type = AttendeeType.Required
                        }
                    },
                    IsOnlineMeeting = true,
                    OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness
                };

                // Create the event in the organizer's calendar
                var createdEvent = await graphClient.Users[request.InterviewerEmail]
                    .Calendar
                    .Events
                    .PostAsync(newEvent);

                return createdEvent?.Id ?? "Event created but ID not available";
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create calendar event: {ex.Message}", ex);
            }
        }

        public async Task<List<Event>> GetAvailableTimeSlotsAppOnlyAsync(string userEmail, DateTime startDate, DateTime endDate)
        {
            try
            {
                var graphClient = GetAppOnlyGraphServiceClient();

                var events = await graphClient.Users[userEmail]
                    .Calendar
                    .Events
                    .GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = 
                            $"start/dateTime ge '{startDate:yyyy-MM-ddTHH:mm:ss.fffK}' and end/dateTime le '{endDate:yyyy-MM-ddTHH:mm:ss.fffK}'";
                    });

                return events?.Value?.ToList() ?? new List<Event>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve calendar events: {ex.Message}", ex);
            }
        }

        public async Task<bool> UpdateEventAppOnlyAsync(string eventId, string userEmail, SchedulingRequest updatedRequest)
        {
            try
            {
                var graphClient = GetAppOnlyGraphServiceClient();

                var eventUpdate = new Event
                {
                    Subject = updatedRequest.Title,
                    Start = new DateTimeTimeZone
                    {
                        DateTime = updatedRequest.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = TimeZoneInfo.Local.Id
                    },
                    End = new DateTimeTimeZone
                    {
                        DateTime = updatedRequest.StartTime.AddMinutes(updatedRequest.DurationMinutes).ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                        TimeZone = TimeZoneInfo.Local.Id
                    }
                };

                await graphClient.Users[userEmail]
                    .Calendar
                    .Events[eventId]
                    .PatchAsync(eventUpdate);

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to update calendar event: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteEventAppOnlyAsync(string eventId, string userEmail)
        {
            try
            {
                var graphClient = GetAppOnlyGraphServiceClient();

                await graphClient.Users[userEmail]
                    .Calendar
                    .Events[eventId]
                    .DeleteAsync();

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete calendar event: {ex.Message}", ex);
            }
        }
    }
}