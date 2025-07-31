using Microsoft.Extensions.Logging;

namespace InterviewSchedulingBot.Services.Business
{
    /// <summary>
    /// Clean, data-driven Mock Data Generator that matches the UI requirements exactly
    /// </summary>
    public interface ICleanMockDataGenerator
    {
        void ResetToDefault();
        void GenerateRandomData();
        void RegenerateCalendarEvents();
        string ExportData();
        MockDataSettings GetCurrentSettings();
        void UpdateSettings(MockDataSettings settings);
        List<UserProfile> GetUserProfiles();
        void GenerateCalendarRange(int durationDays, MeetingDensity density);
    }

    public class CleanMockDataGenerator : ICleanMockDataGenerator
    {
        private readonly ILogger<CleanMockDataGenerator> _logger;
        private MockDataSettings _currentSettings;
        private List<UserProfile> _userProfiles;
        private List<CalendarEvent> _calendarEvents;

        // Data-driven user templates instead of hard-coded logic
        private static readonly UserProfileTemplate[] UserTemplates = {
            new("John Doe", "john.doe@company.com", "Senior Software Engineer", "Engineering", "Pacific Standard Time", "09:00:00", "17:00:00"),
            new("Jane Smith", "jane.smith@company.com", "Product Manager", "Product", "Eastern Standard Time", "08:00:00", "16:00:00"),
            new("Alex Wilson", "alex.wilson@company.com", "Engineering Manager", "Engineering", "Pacific Standard Time", "08:30:00", "17:30:00"),
            new("Maria Garcia", "maria.garcia@company.com", "UX Designer", "Design", "Central Standard Time", "09:30:00", "17:30:00"),
            new("David Brown", "david.brown@company.com", "DevOps Engineer", "Engineering", "Eastern Standard Time", "08:00:00", "16:00:00")
        };

        // Data-driven meeting types instead of conditional logic
        private static readonly MeetingTemplate[] MeetingTemplates = {
            new("Team Meeting", 60, 3),
            new("1:1 with Manager", 30, 2),
            new("Client Call", 90, 2),
            new("Product Demo", 60, 4),
            new("Sprint Planning", 120, 5),
            new("Project Review", 45, 3),
            new("Lunch & Learn", 90, 4),
            new("All Hands", 60, 8)
        };

        public CleanMockDataGenerator(ILogger<CleanMockDataGenerator> logger)
        {
            _logger = logger;
            _currentSettings = CreateDefaultSettings();
            _userProfiles = new List<UserProfile>();
            _calendarEvents = new List<CalendarEvent>();
            ResetToDefault();
        }

        public void ResetToDefault()
        {
            _currentSettings = CreateDefaultSettings();
            GenerateUserProfiles();
            GenerateCalendarRange(_currentSettings.CalendarRangeDays, _currentSettings.MeetingDensity);
            _logger.LogInformation("Reset mock data to default settings");
        }

        public void GenerateRandomData()
        {
            var random = new Random();
            
            // Use data-driven approach instead of complex conditionals
            _currentSettings.CalendarRangeDays = random.Next(1, 31);
            _currentSettings.MeetingDensity = (MeetingDensity)random.Next(0, 3);
            
            GenerateUserProfiles();
            GenerateCalendarRange(_currentSettings.CalendarRangeDays, _currentSettings.MeetingDensity);
            _logger.LogInformation("Generated random mock data");
        }

        public void RegenerateCalendarEvents()
        {
            GenerateCalendarRange(_currentSettings.CalendarRangeDays, _currentSettings.MeetingDensity);
            _logger.LogInformation("Regenerated calendar events");
        }

        public string ExportData()
        {
            var exportData = new
            {
                Settings = _currentSettings,
                UserProfiles = _userProfiles,
                CalendarEvents = _calendarEvents,
                ExportedAt = DateTime.UtcNow
            };
            
            return System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }

        public MockDataSettings GetCurrentSettings() => _currentSettings;

        public void UpdateSettings(MockDataSettings settings)
        {
            _currentSettings = settings;
            GenerateCalendarRange(settings.CalendarRangeDays, settings.MeetingDensity);
        }

        public List<UserProfile> GetUserProfiles() => _userProfiles.ToList();

        public void GenerateCalendarRange(int durationDays, MeetingDensity density)
        {
            _calendarEvents.Clear();
            
            var eventsPerDay = density switch
            {
                MeetingDensity.Low => 1,
                MeetingDensity.Medium => 2,
                MeetingDensity.High => 4,
                _ => 2
            };

            var random = new Random();
            var startDate = DateTime.Today;

            for (int day = 0; day < durationDays; day++)
            {
                var currentDate = startDate.AddDays(day);
                
                // Skip weekends using pattern matching
                if (IsWeekend(currentDate)) continue;

                foreach (var user in _userProfiles)
                {
                    var dailyEvents = GenerateDailyEvents(user, currentDate, eventsPerDay, random);
                    _calendarEvents.AddRange(dailyEvents);
                }
            }
        }

        private static bool IsWeekend(DateTime date) => 
            date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        private MockDataSettings CreateDefaultSettings() => new()
        {
            CalendarRangeDays = 7,
            MeetingDensity = MeetingDensity.Medium
        };

        private void GenerateUserProfiles()
        {
            _userProfiles.Clear();
            
            // Use LINQ and data-driven approach instead of loops
            _userProfiles = UserTemplates
                .Take(_currentSettings.UserCount)
                .Select(template => new UserProfile
                {
                    Name = template.Name,
                    Email = template.Email,
                    JobTitle = template.JobTitle,
                    Department = template.Department,
                    TimeZone = template.TimeZone,
                    WorkingHours = new CleanWorkingHours
                    {
                        StartTime = template.StartTime,
                        EndTime = template.EndTime,
                        DaysOfWeek = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" }
                    }
                })
                .ToList();
        }

        private List<CalendarEvent> GenerateDailyEvents(UserProfile user, DateTime date, int maxEvents, Random random)
        {
            var events = new List<CalendarEvent>();
            var eventCount = random.Next(0, maxEvents + 1);

            for (int i = 0; i < eventCount; i++)
            {
                var template = MeetingTemplates[random.Next(MeetingTemplates.Length)];
                var startHour = random.Next(9, 17); // Business hours
                var startTime = date.AddHours(startHour);
                
                events.Add(new CalendarEvent
                {
                    Title = template.Title,
                    StartTime = startTime,
                    EndTime = startTime.AddMinutes(template.DurationMinutes),
                    Attendees = GenerateAttendees(user, template.AttendeeCount, random),
                    UserEmail = user.Email
                });
            }

            return events;
        }

        private List<string> GenerateAttendees(UserProfile organizer, int count, Random random)
        {
            var attendees = new List<string> { organizer.Email };
            
            var otherUsers = _userProfiles
                .Where(u => u.Email != organizer.Email)
                .OrderBy(u => random.Next())
                .Take(count - 1)
                .Select(u => u.Email);
                
            attendees.AddRange(otherUsers);
            return attendees;
        }
    }

    // Clean data models
    public class MockDataSettings
    {
        public int UserCount { get; set; } = 3;
        public int CalendarRangeDays { get; set; } = 7;
        public MeetingDensity MeetingDensity { get; set; } = MeetingDensity.Medium;
    }

    public enum MeetingDensity
    {
        Low,
        Medium, 
        High
    }

    public class UserProfile
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string JobTitle { get; set; } = "";
        public string Department { get; set; } = "";
        public string TimeZone { get; set; } = "";
        public CleanWorkingHours WorkingHours { get; set; } = new();
    }

    public class CleanWorkingHours
    {
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public string[] DaysOfWeek { get; set; } = Array.Empty<string>();
    }

    public class CalendarEvent
    {
        public string Title { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<string> Attendees { get; set; } = new();
        public string UserEmail { get; set; } = "";
    }

    // Data templates instead of hard-coded logic
    public record UserProfileTemplate(
        string Name,
        string Email, 
        string JobTitle,
        string Department,
        string TimeZone,
        string StartTime,
        string EndTime);

    public record MeetingTemplate(
        string Title,
        int DurationMinutes,
        int AttendeeCount);
}