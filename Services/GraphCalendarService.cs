using Microsoft.Graph;
using Microsoft.Graph.Models;
using InterviewSchedulingBot.Models;
using Azure.Identity;

namespace InterviewSchedulingBot.Services
{
    public class GraphCalendarService
    {
        private readonly IConfiguration _configuration;
        private GraphServiceClient? _graphClient;

        public GraphCalendarService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private GraphServiceClient GetGraphServiceClient()
        {
            if (_graphClient == null)
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
                _graphClient = new GraphServiceClient(credential);
            }

            return _graphClient;
        }

        public async Task<string> CreateInterviewEventAsync(SchedulingRequest request)
        {
            try
            {
                var graphClient = GetGraphServiceClient();

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

        public async Task<List<Event>> GetAvailableTimeSlotsAsync(string userEmail, DateTime startDate, DateTime endDate)
        {
            try
            {
                var graphClient = GetGraphServiceClient();

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

        public async Task<bool> UpdateEventAsync(string eventId, string userEmail, SchedulingRequest updatedRequest)
        {
            try
            {
                var graphClient = GetGraphServiceClient();

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

        public async Task<bool> DeleteEventAsync(string eventId, string userEmail)
        {
            try
            {
                var graphClient = GetGraphServiceClient();

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