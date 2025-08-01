using InterviewSchedulingBot.Services.Integration;
using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Services.Business
{
    public class SlotQueryParser
    {
        private readonly IOpenWebUIClient _openWebUIClient;
        private readonly ILogger<SlotQueryParser> _logger;
        
        public SlotQueryParser(IOpenWebUIClient openWebUIClient, ILogger<SlotQueryParser> logger)
        {
            _openWebUIClient = openWebUIClient;
            _logger = logger;
        }
        
        public async Task<SlotQueryCriteria?> ParseQueryAsync(string query, CancellationToken cancellationToken = default)
        {
            return await ParseQueryAsync(query, null, cancellationToken);
        }

        public async Task<SlotQueryCriteria?> ParseQueryAsync(string query, SlotQueryCriteria? previousCriteria = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Parsing slot query: {Query}", query);
                
                var response = await _openWebUIClient.ProcessQueryAsync(
                    query, OpenWebUIRequestType.SlotQuery, cancellationToken);
                
                if (!response.Success)
                {
                    _logger.LogWarning("Failed to parse slot query: {Message}", response.Message);
                    return null;
                }
                
                // Parse the response into slot criteria, merging with previous criteria if available
                var criteria = new SlotQueryCriteria
                {
                    DurationMinutes = response.Duration ?? previousCriteria?.DurationMinutes ?? 30,
                    ParticipantEmails = response.Participants?.Count > 0 ? response.Participants : (previousCriteria?.ParticipantEmails ?? new List<string>()),
                    MinRequiredParticipants = response.MinRequiredParticipants ?? previousCriteria?.MinRequiredParticipants ?? 0,
                    SpecificDay = response.SpecificDay ?? previousCriteria?.SpecificDay,
                    RelativeDay = response.RelativeDay ?? previousCriteria?.RelativeDay
                };

                // Parse date range
                if (response.DateRange != null)
                {
                    criteria.StartDate = response.DateRange.Start;
                    criteria.EndDate = response.DateRange.End;
                }
                else if (previousCriteria != null && (!string.IsNullOrEmpty(previousCriteria.SpecificDay) || !string.IsNullOrEmpty(previousCriteria.RelativeDay)))
                {
                    // Use previous date range if no new date specified
                    criteria.StartDate = previousCriteria.StartDate;
                    criteria.EndDate = previousCriteria.EndDate;
                    criteria.SpecificDay = previousCriteria.SpecificDay;
                    criteria.RelativeDay = previousCriteria.RelativeDay;
                }
                else
                {
                    // Set defaults based on relative or specific day
                    SetDateRangeFromDayReference(criteria);
                }

                // Parse time of day - prioritize new time, fallback to previous if available
                var newTimeOfDay = ParseTimeOfDay(response.TimeOfDay);
                criteria.TimeOfDay = newTimeOfDay ?? previousCriteria?.TimeOfDay;
                
                _logger.LogInformation("Successfully parsed slot query criteria: {Criteria}", criteria);
                return criteria;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing slot query");
                return null;
            }
        }
        
        private void SetDateRangeFromDayReference(SlotQueryCriteria criteria)
        {
            var today = DateTime.Today;
            
            if (!string.IsNullOrEmpty(criteria.RelativeDay))
            {
                switch (criteria.RelativeDay.ToLowerInvariant())
                {
                    case "tomorrow":
                        var tomorrow = today.AddDays(1);
                        // Skip weekends - if tomorrow is Saturday or Sunday, move to Monday
                        while (tomorrow.DayOfWeek == DayOfWeek.Saturday || tomorrow.DayOfWeek == DayOfWeek.Sunday)
                        {
                            tomorrow = tomorrow.AddDays(1);
                        }
                        criteria.StartDate = tomorrow;
                        criteria.EndDate = tomorrow;
                        break;
                    case "next week":
                        // Next week starts from the upcoming Monday
                        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
                        if (daysUntilMonday == 0) daysUntilMonday = 7; // If today is Monday, go to next Monday
                        var nextWeekStart = today.AddDays(daysUntilMonday);
                        criteria.StartDate = nextWeekStart;
                        criteria.EndDate = nextWeekStart.AddDays(4); // Monday to Friday
                        break;
                    default:
                        criteria.StartDate = today;
                        criteria.EndDate = today.AddDays(7);
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(criteria.SpecificDay))
            {
                var targetDay = ParseDayOfWeek(criteria.SpecificDay);
                if (targetDay.HasValue)
                {
                    var daysUntilTarget = ((int)targetDay.Value - (int)today.DayOfWeek + 7) % 7;
                    if (daysUntilTarget == 0) daysUntilTarget = 7; // Next occurrence
                    
                    var targetDate = today.AddDays(daysUntilTarget);
                    criteria.StartDate = targetDate;
                    criteria.EndDate = targetDate;
                }
                else
                {
                    criteria.StartDate = today;
                    criteria.EndDate = today.AddDays(7);
                }
            }
            else
            {
                // Default to next 7 days
                criteria.StartDate = today;
                criteria.EndDate = today.AddDays(7);
            }
        }

        private DayOfWeek? ParseDayOfWeek(string dayName)
        {
            return dayName.ToLowerInvariant() switch
            {
                "monday" => DayOfWeek.Monday,
                "tuesday" => DayOfWeek.Tuesday,
                "wednesday" => DayOfWeek.Wednesday,
                "thursday" => DayOfWeek.Thursday,
                "friday" => DayOfWeek.Friday,
                "saturday" => DayOfWeek.Saturday,
                "sunday" => DayOfWeek.Sunday,
                _ => null
            };
        }
        
        private TimeOfDayRange? ParseTimeOfDay(string? timeOfDay)
        {
            if (string.IsNullOrEmpty(timeOfDay))
                return null;
                
            return timeOfDay.ToLowerInvariant() switch
            {
                "morning" => new TimeOfDayRange { Start = new TimeSpan(8, 0, 0), End = new TimeSpan(12, 0, 0) },
                "afternoon" => new TimeOfDayRange { Start = new TimeSpan(12, 0, 0), End = new TimeSpan(17, 0, 0) },
                "evening" => new TimeOfDayRange { Start = new TimeSpan(17, 0, 0), End = new TimeSpan(20, 0, 0) },
                _ => null
            };
        }
    }
    
    public class SlotQueryCriteria
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeOfDayRange? TimeOfDay { get; set; }
        public List<string> ParticipantEmails { get; set; } = new();
        public int MinRequiredParticipants { get; set; }
        public int DurationMinutes { get; set; } = 30;
        public string? SpecificDay { get; set; }
        public string? RelativeDay { get; set; }

        public override string ToString()
        {
            var parts = new List<string>();
            
            if (StartDate == EndDate)
                parts.Add($"Date: {StartDate:yyyy-MM-dd}");
            else
                parts.Add($"Date Range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}");
            
            if (TimeOfDay != null)
                parts.Add($"Time: {TimeOfDay.Start:hh\\:mm} - {TimeOfDay.End:hh\\:mm}");
            
            parts.Add($"Duration: {DurationMinutes} minutes");
            
            if (ParticipantEmails.Any())
                parts.Add($"Participants: {string.Join(", ", ParticipantEmails)}");
            
            return string.Join(", ", parts);
        }
    }
    
    public class TimeOfDayRange
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }

        public bool Contains(TimeSpan time)
        {
            return time >= Start && time <= End;
        }

        public bool Contains(DateTime dateTime)
        {
            return Contains(dateTime.TimeOfDay);
        }
    }
}