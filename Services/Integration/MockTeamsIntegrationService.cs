using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using InterviewSchedulingBot.Interfaces.Integration;
using InterviewSchedulingBot.Models;
using Microsoft.Extensions.Logging;
using InterviewSchedulingBot.Controllers.Api;

namespace InterviewSchedulingBot.Services.Mock
{
    /// <summary>
    /// Mock implementation of Teams integration service for testing
    /// Provides realistic mock data to simulate MS Teams API responses
    /// </summary>
    public class MockTeamsIntegrationService : ITeamsIntegrationService
    {
        private readonly ILogger<MockTeamsIntegrationService> _logger;
        private MockDataProvider _mockDataProvider;

        public MockTeamsIntegrationService(ILogger<MockTeamsIntegrationService> logger)
        {
            _logger = logger;
            _mockDataProvider = new MockDataProvider();
        }

        /// <summary>
        /// Update mock data for testing (allows frontend to sync with backend)
        /// </summary>
        public async Task UpdateMockDataAsync(MockDataRequest newMockData)
        {
            _logger.LogInformation("[MOCK] Updating mock data with {UserCount} users", newMockData.UserProfiles.Count);
            await Task.Delay(50); // Simulate processing delay
            _mockDataProvider.UpdateData(newMockData);
        }

        public async Task<ResourceResponse> SendMessageAsync(ITurnContext turnContext, string message)
        {
            _logger.LogInformation("[MOCK] Sending message: {Message}", message);
            
            await Task.Delay(100); // Simulate network delay
            
            return new ResourceResponse
            {
                Id = Guid.NewGuid().ToString()
            };
        }

        public async Task<ResourceResponse> SendAdaptiveCardAsync(ITurnContext turnContext, Attachment cardAttachment)
        {
            _logger.LogInformation("[MOCK] Sending adaptive card");
            
            await Task.Delay(150); // Simulate network delay
            
            return new ResourceResponse
            {
                Id = Guid.NewGuid().ToString()
            };
        }

        public async Task<TeamsUserInfo> GetUserInfoAsync(ITurnContext turnContext)
        {
            _logger.LogInformation("[MOCK] Getting user info from Teams context");
            
            await Task.Delay(80); // Simulate network delay
            
            return _mockDataProvider.GetMockTeamsUserInfo();
        }

        public async Task<AuthenticationResult> HandleAuthenticationAsync(ITurnContext turnContext, string userId)
        {
            _logger.LogInformation("[MOCK] Handling authentication for user {UserId}", userId);
            
            await Task.Delay(200); // Simulate authentication check delay
            
            return _mockDataProvider.GetMockAuthenticationResult(userId);
        }

        public async Task<Dictionary<string, List<BusyTimeSlot>>> GetCalendarAvailabilityAsync(
            ITurnContext turnContext, 
            List<string> userEmails, 
            DateTime startTime, 
            DateTime endTime)
        {
            _logger.LogInformation("[MOCK] Getting calendar availability for {UserCount} users from {StartTime} to {EndTime}", 
                userEmails.Count, startTime, endTime);
            
            await Task.Delay(300); // Simulate Graph API call delay
            
            return _mockDataProvider.GetMockCalendarAvailability(userEmails, startTime, endTime);
        }

        public async Task<WorkingHours> GetUserWorkingHoursAsync(ITurnContext turnContext, string userEmail)
        {
            _logger.LogInformation("[MOCK] Getting working hours for user {UserEmail}", userEmail);
            
            await Task.Delay(150); // Simulate Graph API call delay
            
            return _mockDataProvider.GetMockWorkingHours(userEmail);
        }

        public async Task<Dictionary<string, UserPresence>> GetUsersPresenceAsync(ITurnContext turnContext, List<string> userEmails)
        {
            _logger.LogInformation("[MOCK] Getting presence information for {UserCount} users", userEmails.Count);
            
            await Task.Delay(200); // Simulate Graph API call delay
            
            return _mockDataProvider.GetMockUsersPresence(userEmails);
        }

        public async Task<List<PersonInfo>> SearchPeopleAsync(ITurnContext turnContext, string searchQuery, int maxResults = 10)
        {
            _logger.LogInformation("[MOCK] Searching for people with query '{SearchQuery}'", searchQuery);
            
            await Task.Delay(250); // Simulate search delay
            
            return _mockDataProvider.GetMockPeopleSearchResults(searchQuery, maxResults);
        }
    }

    /// <summary>
    /// Provides realistic mock data for testing Teams API responses
    /// </summary>
    public class MockDataProvider
    {
        private readonly Random _random = new Random();
        private MockDataRequest? _customMockData;

        public void UpdateData(MockDataRequest newMockData)
        {
            _customMockData = newMockData;
        }

        public TeamsUserInfo GetMockTeamsUserInfo()
        {
            return new TeamsUserInfo
            {
                Id = "29:1q2w3e4r5t6y7u8i9o0p",
                Name = "John Doe",
                Email = "john.doe@contoso.com",
                TenantId = "12345678-1234-1234-1234-123456789012",
                TeamId = "19:abcd1234efgh5678ijkl@thread.tacv2",
                ChannelId = "19:wxyz9876mnop5432qrst@thread.tacv2"
            };
        }

        public AuthenticationResult GetMockAuthenticationResult(string userId)
        {
            // Simulate different authentication states
            var scenarios = new[]
            {
                new AuthenticationResult 
                { 
                    IsAuthenticated = true, 
                    AccessToken = "mock-access-token-" + Guid.NewGuid().ToString()[..8]
                },
                new AuthenticationResult 
                { 
                    IsAuthenticated = false, 
                    LoginUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id=mock&response_type=code"
                }
            };

            // 80% chance of being authenticated for testing
            return _random.NextDouble() < 0.8 ? scenarios[0] : scenarios[1];
        }

        public Dictionary<string, List<BusyTimeSlot>> GetMockCalendarAvailability(
            List<string> userEmails, 
            DateTime startTime, 
            DateTime endTime)
        {
            var result = new Dictionary<string, List<BusyTimeSlot>>();

            foreach (var userEmail in userEmails)
            {
                var busySlots = new List<BusyTimeSlot>();

                // Use custom mock data if available
                if (_customMockData != null)
                {
                    var userCalendar = _customMockData.CalendarAvailability.FirstOrDefault(ca => ca.UserEmail == userEmail);
                    if (userCalendar != null)
                    {
                        foreach (var slot in userCalendar.BusySlots)
                        {
                            // Parse with explicit format to handle timezone-aware dates consistently
                            if (DateTime.TryParse(slot.Start, null, System.Globalization.DateTimeStyles.RoundtripKind, out var start) && 
                                DateTime.TryParse(slot.End, null, System.Globalization.DateTimeStyles.RoundtripKind, out var end))
                            {
                                // Convert to UTC for consistent comparison
                                start = start.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(start, DateTimeKind.Local).ToUniversalTime() : start.ToUniversalTime();
                                end = end.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(end, DateTimeKind.Local).ToUniversalTime() : end.ToUniversalTime();
                                
                                var utcStartTime = startTime.ToUniversalTime();
                                var utcEndTime = endTime.ToUniversalTime();
                                
                                // Only include slots that overlap with the requested time range
                                if (start < utcEndTime && end > utcStartTime)
                                {
                                    busySlots.Add(new BusyTimeSlot
                                    {
                                        Start = start.ToLocalTime(), // Convert back to local for business logic
                                        End = end.ToLocalTime(),
                                        Status = slot.Status,
                                        Subject = slot.Subject
                                    });
                                }
                            }
                        }
                        result[userEmail] = busySlots.OrderBy(slot => slot.Start).ToList();
                        continue;
                    }
                }

                // Fallback to generated realistic busy time patterns
                var currentDate = startTime.Date;
                while (currentDate <= endTime.Date)
                {
                    // Skip weekends
                    if (currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        currentDate = currentDate.AddDays(1);
                        continue;
                    }

                    // Add some random meetings during work hours
                    var numberOfMeetings = _random.Next(0, 4); // 0-3 meetings per day
                    
                    for (int i = 0; i < numberOfMeetings; i++)
                    {
                        var meetingStart = currentDate.AddHours(_random.Next(9, 16)); // 9 AM to 4 PM
                        var meetingDuration = _random.Next(30, 120); // 30 minutes to 2 hours
                        var meetingEnd = meetingStart.AddMinutes(meetingDuration);

                        // Ensure meeting doesn't exceed working hours
                        if (meetingEnd.Hour > 17)
                        {
                            meetingEnd = currentDate.AddHours(17);
                        }

                        var meetingSubjects = new[]
                        {
                            "Team Meeting", "Code Review", "Product Planning", "Client Call", 
                            "Sprint Planning", "1:1 Meeting", "All Hands", "Training Session",
                            "Design Review", "Architecture Discussion", "Project Sync", "Performance Review"
                        };

                        busySlots.Add(new BusyTimeSlot
                        {
                            Start = meetingStart,
                            End = meetingEnd,
                            Status = _random.NextDouble() < 0.1 ? "Tentative" : "Busy", // 10% tentative
                            Subject = meetingSubjects[_random.Next(meetingSubjects.Length)]
                        });
                    }

                    currentDate = currentDate.AddDays(1);
                }

                result[userEmail] = busySlots.OrderBy(slot => slot.Start).ToList();
            }

            return result;
        }

        public WorkingHours GetMockWorkingHours(string userEmail)
        {
            // Use custom mock data if available
            if (_customMockData != null)
            {
                var customWorkingHours = _customMockData.WorkingHours.FirstOrDefault(wh => wh.UserEmail == userEmail);
                if (customWorkingHours != null)
                {
                    return new WorkingHours
                    {
                        TimeZone = customWorkingHours.TimeZone,
                        DaysOfWeek = customWorkingHours.DaysOfWeek,
                        StartTime = customWorkingHours.StartTime,
                        EndTime = customWorkingHours.EndTime
                    };
                }
            }

            // Fallback to simulated different working hour patterns
            var workingHourPatterns = new[]
            {
                new WorkingHours
                {
                    TimeZone = "Pacific Standard Time",
                    DaysOfWeek = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" },
                    StartTime = "09:00:00",
                    EndTime = "17:00:00"
                },
                new WorkingHours
                {
                    TimeZone = "Eastern Standard Time",
                    DaysOfWeek = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" },
                    StartTime = "08:00:00",
                    EndTime = "16:00:00"
                },
                new WorkingHours
                {
                    TimeZone = "GMT Standard Time",
                    DaysOfWeek = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" },
                    StartTime = "08:30:00",
                    EndTime = "17:30:00"
                }
            };

            // Select pattern based on email hash for consistency
            var index = Math.Abs(userEmail.GetHashCode()) % workingHourPatterns.Length;
            return workingHourPatterns[index];
        }

        public Dictionary<string, UserPresence> GetMockUsersPresence(List<string> userEmails)
        {
            var result = new Dictionary<string, UserPresence>();
            var presenceOptions = new[] { "Available", "Busy", "DoNotDisturb", "Away", "BeRightBack" };
            var activityOptions = new[] { "Available", "InACall", "InAMeeting", "Busy", "Away" };

            foreach (var userEmail in userEmails)
            {
                var presenceIndex = Math.Abs(userEmail.GetHashCode()) % presenceOptions.Length;
                var activityIndex = Math.Abs(userEmail.GetHashCode()) % activityOptions.Length;

                result[userEmail] = new UserPresence
                {
                    Availability = presenceOptions[presenceIndex],
                    Activity = activityOptions[activityIndex],
                    LastModifiedDateTime = DateTime.UtcNow.AddMinutes(-_random.Next(1, 60)).ToString("O")
                };
            }

            return result;
        }

        public List<PersonInfo> GetMockPeopleSearchResults(string searchQuery, int maxResults)
        {
            var mockPeople = new List<PersonInfo>
            {
                new PersonInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = "Alice Johnson",
                    EmailAddresses = new List<string> { "alice.johnson@contoso.com" },
                    JobTitle = "Senior Software Engineer",
                    Department = "Engineering"
                },
                new PersonInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = "Bob Smith",
                    EmailAddresses = new List<string> { "bob.smith@contoso.com" },
                    JobTitle = "Product Manager",
                    Department = "Product"
                },
                new PersonInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = "Carol Davis",
                    EmailAddresses = new List<string> { "carol.davis@contoso.com" },
                    JobTitle = "UX Designer",
                    Department = "Design"
                },
                new PersonInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = "David Wilson",
                    EmailAddresses = new List<string> { "david.wilson@contoso.com" },
                    JobTitle = "Engineering Manager",
                    Department = "Engineering"
                },
                new PersonInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = "Eva Brown",
                    EmailAddresses = new List<string> { "eva.brown@contoso.com" },
                    JobTitle = "HR Business Partner",
                    Department = "Human Resources"
                }
            };

            // Filter by search query (simple contains search)
            var filteredResults = mockPeople
                .Where(person => 
                    person.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    person.JobTitle.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    person.Department.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    person.EmailAddresses.Any(email => email.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)))
                .Take(maxResults)
                .ToList();

            return filteredResults;
        }
    }
}