using Microsoft.Graph;
using Microsoft.Graph.Models;
using InterviewSchedulingBot.Models;
using Azure.Identity;
using Microsoft.Graph.Authentication;
using Microsoft.Kiota.Abstractions.Authentication;

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

        public AllowedHostsValidator? AllowedHostsValidator { get; }
    }

    public class GraphCalendarService
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