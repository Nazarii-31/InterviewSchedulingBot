using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Services
{
    public class MockCalendarGenerator
    {
        private readonly ILogger<MockCalendarGenerator> _logger;
        private readonly Random _random;
        
        // Dictionary to store generated calendars for consistency
        private static readonly Dictionary<string, List<CalendarEvent>> _userCalendars = new();
        
        // Default users with their email addresses
        private readonly List<string> _defaultUsers = new()
        {
            "john.doe@company.com",
            "jane.smith@company.com",
            "alex.wilson@company.com", 
            "maria.garcia@company.com",
            "david.brown@company.com"
        };
        
        public MockCalendarGenerator(ILogger<MockCalendarGenerator> logger)
        {
            _logger = logger;
            _random = new Random(42); // Fixed seed for reproducibility
        }
        
        public List<string> GetAllUsers() => _userCalendars.Keys.ToList();
        
        public List<CalendarEvent> GetUserCalendar(string email)
        {
            if (_userCalendars.TryGetValue(email, out var calendar))
            {
                return calendar;
            }
            
            return new List<CalendarEvent>();
        }
        
        public void GenerateCalendars(int numberOfUsers, double busynessLevel)
        {
            _userCalendars.Clear(); // Clear existing calendars
            
            // Ensure we have at least 1 user and at most the number of default users
            numberOfUsers = Math.Max(1, Math.Min(numberOfUsers, _defaultUsers.Count));
            
            // Clamp busyness level between 0.1 (10% busy) and 0.9 (90% busy)
            busynessLevel = Math.Max(0.1, Math.Min(0.9, busynessLevel));
            
            _logger.LogInformation("Generating mock calendars for {UserCount} users with busyness level {BusynessLevel:P0}",
                numberOfUsers, busynessLevel);
            
            // Generate calendars for the specified number of users
            for (int i = 0; i < numberOfUsers; i++)
            {
                var email = _defaultUsers[i];
                _userCalendars[email] = GenerateUserCalendar(email, busynessLevel);
                _logger.LogInformation("Generated calendar for {User} with {EventCount} events", 
                    email, _userCalendars[email].Count);
            }
        }
        
        public List<CalendarEvent> GetEventsInRange(string email, DateTime start, DateTime end)
        {
            if (!_userCalendars.TryGetValue(email, out var calendar))
            {
                return new List<CalendarEvent>();
            }
            
            return calendar.Where(e => e.EndTime > start && e.StartTime < end).ToList();
        }
        
        private List<CalendarEvent> GenerateUserCalendar(string email, double busynessLevel)
        {
            var events = new List<CalendarEvent>();
            var today = DateTime.Today;
            
            // Generate events for the next 14 days
            for (int day = 0; day < 14; day++)
            {
                var date = today.AddDays(day);
                
                // Skip weekends
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }
                
                // Calculate number of events based on busyness level
                // Higher busyness means more events per day
                int maxEventsPerDay = 8; // Maximum events in a business day
                int minEventsPerDay = 1; // Minimum events in a business day
                
                int eventsPerDay = (int)Math.Round(minEventsPerDay + (maxEventsPerDay - minEventsPerDay) * busynessLevel);
                
                // Keep track of busy times to avoid overlaps
                var busyTimes = new List<(DateTime start, DateTime end)>();
                
                for (int i = 0; i < eventsPerDay; i++)
                {
                    // Try a few times to find a non-overlapping slot
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        // Generate time between 9 AM and 5 PM
                        int hour = _random.Next(9, 17);
                        int minute = _random.Next(0, 4) * 15; // 0, 15, 30, 45
                        
                        // Duration between 30 and 120 minutes
                        int durationMinutes = new[] { 30, 45, 60, 90, 120 }[_random.Next(5)];
                        
                        var startTime = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0);
                        var endTime = startTime.AddMinutes(durationMinutes);
                        
                        // Check if this overlaps with any existing event
                        bool overlaps = busyTimes.Any(busy => 
                            (startTime < busy.end && endTime > busy.start));
                            
                        // If no overlap, create the event
                        if (!overlaps)
                        {
                            var eventId = Guid.NewGuid().ToString();
                            var title = GetRandomEventTitle();
                            var attendees = GetRandomAttendees(email);
                            
                            var calendarEvent = new CalendarEvent
                            {
                                Id = eventId,
                                Title = title,
                                StartTime = startTime,
                                EndTime = endTime,
                                Organizer = email,
                                Attendees = attendees
                            };
                            
                            events.Add(calendarEvent);
                            busyTimes.Add((startTime, endTime));
                            break;
                        }
                    }
                }
            }
            
            return events.OrderBy(e => e.StartTime).ToList();
        }
        
        private string GetRandomEventTitle()
        {
            var titles = new[]
            {
                "Team Meeting",
                "Project Review",
                "Client Call",
                "1:1 with Manager",
                "Interview with Candidate",
                "Product Demo",
                "Sprint Planning",
                "Retrospective",
                "Training Session",
                "Department Sync",
                "Lunch & Learn",
                "Status Update"
            };
            
            return titles[_random.Next(titles.Length)];
        }
        
        private List<string> GetRandomAttendees(string organizer)
        {
            var attendees = new List<string> { organizer };
            
            // Add 1-3 other random attendees
            int additionalCount = _random.Next(1, 4);
            var otherUsers = _userCalendars.Keys.Where(u => u != organizer).ToList();
            
            for (int i = 0; i < additionalCount && i < otherUsers.Count; i++)
            {
                int index = _random.Next(otherUsers.Count);
                attendees.Add(otherUsers[index]);
                otherUsers.RemoveAt(index);
            }
            
            return attendees;
        }
    }
    
    public class CalendarEvent
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Organizer { get; set; } = string.Empty;
        public List<string> Attendees { get; set; } = new List<string>();
    }
}