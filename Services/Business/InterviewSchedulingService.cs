using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using InterviewSchedulingBot.Services.Integration;
using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Services.Business
{
    public interface IInterviewSchedulingService
    {
        Task<List<TimeSlot>> GenerateTimeSlotsAsync(
            DateTime startDate,
            DateTime endDate,
            int durationMinutes,
            List<string> participantEmails);
            
        Task<string> ProcessSchedulingRequestAsync(string userMessage);

        // New span-wide API for data shaping without NL semantics
        Task<List<TimeSlot>> GenerateTimeSlotsAsync(
            DateTime start,
            DateTime end,
            int durationMinutes,
            IReadOnlyList<string> participantEmails,
            string timeOfDay,
            InterviewSchedulingBot.Services.Integration.DaysSelector selector);
    }
    
    public class InterviewSchedulingService : IInterviewSchedulingService
    {
        private readonly ILogger<InterviewSchedulingService> _logger;
        private readonly IConfiguration _configuration;
                private readonly InterviewSchedulingBot.Services.Integration.ICleanOpenWebUIClient _cleanOpenWebUIClient;
        private readonly TimeSpan _workdayStart;
        private readonly TimeSpan _workdayEnd;
        private readonly int _slotIntervalMinutes;
        private readonly Random _random = new Random();
        
        public InterviewSchedulingService(
            ILogger<InterviewSchedulingService> logger,
            IConfiguration configuration,
            InterviewSchedulingBot.Services.Integration.ICleanOpenWebUIClient cleanOpenWebUIClient)
        {
            _logger = logger;
            _configuration = configuration;
            _cleanOpenWebUIClient = cleanOpenWebUIClient;
            
            // Read configuration
            var startTimeStr = configuration["Scheduling:WorkingHours:StartTime"] ?? "09:00";
            var endTimeStr = configuration["Scheduling:WorkingHours:EndTime"] ?? "17:00";
            _slotIntervalMinutes = int.Parse(configuration["Scheduling:SlotIntervalMinutes"] ?? "30");
            
            _workdayStart = TimeSpan.Parse(startTimeStr);
            _workdayEnd = TimeSpan.Parse(endTimeStr);
        }
        
        public Task<List<TimeSlot>> GenerateTimeSlotsAsync(
            DateTime startDate,
            DateTime endDate,
            int durationMinutes,
            List<string> participantEmails)
        {
            var slots = new List<TimeSlot>();
            
            _logger.LogInformation("Generating time slots from {StartDate} to {EndDate} for {Duration} minutes with participants: {Participants}", 
                startDate, endDate, durationMinutes, string.Join(", ", participantEmails));
            
            // Generate slots for each day in the range
            for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
            {
                // Skip weekends
                if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday)
                {
                    _logger.LogInformation("Skipping weekend day: {Day}", day);
                    continue;
                }
                
                // Determine start and end times for this day
                var dayStartTime = day.Date.Add(_workdayStart);
                var dayEndTime = day.Date.Add(_workdayEnd);
                
                // Adjust for time of day preference
                if (startDate.Date == day && startDate.TimeOfDay > TimeSpan.Zero)
                {
                    dayStartTime = new DateTime(
                        day.Year, day.Month, day.Day,
                        startDate.Hour, startDate.Minute, 0);
                }
                
                if (endDate.Date == day && endDate.TimeOfDay > TimeSpan.Zero)
                {
                    dayEndTime = new DateTime(
                        day.Year, day.Month, day.Day,
                        endDate.Hour, endDate.Minute, 0);
                }
                
                // Ensure dayStartTime is not before workday start
                if (dayStartTime.TimeOfDay < _workdayStart)
                {
                    dayStartTime = day.Date.Add(_workdayStart);
                }
                
                // Ensure dayEndTime is not after workday end
                if (dayEndTime.TimeOfDay > _workdayEnd)
                {
                    dayEndTime = day.Date.Add(_workdayEnd);
                }
                
                _logger.LogInformation("Generating slots for {Day} from {StartTime} to {EndTime}", 
                    day.ToString("yyyy-MM-dd"), dayStartTime.ToString("HH:mm"), dayEndTime.ToString("HH:mm"));
                
                // Generate slots for the day
                for (var slotStart = dayStartTime;
                     slotStart.AddMinutes(durationMinutes) <= dayEndTime;
                     slotStart = slotStart.AddMinutes(_slotIntervalMinutes))
                {
                    var slotEnd = slotStart.AddMinutes(durationMinutes);
                    
                    var slot = new TimeSlot
                    {
                        StartTime = slotStart,
                        EndTime = slotEnd,
                        AvailableParticipants = new List<string>(),
                        TotalParticipants = participantEmails.Count,
                    };
                    
                    // Generate mock availability data
                    foreach (var email in participantEmails)
                    {
                        // Mock availability based on email+time hash for consistency
                        bool isAvailable = IsParticipantAvailable(email, slotStart);
                        
                        if (isAvailable)
                        {
                            slot.AvailableParticipants.Add(email);
                        }
                        // For unavailable participants, we don't need to track them separately
                        // since we can calculate from TotalParticipants - AvailableParticipants.Count
                    }
                    
                    // Calculate score based on availability
                    slot.AvailabilityScore = CalculateScore(slot, participantEmails.Count);
                    
                    slots.Add(slot);
                }
            }
            
            _logger.LogInformation("Generated {SlotCount} total slots", slots.Count);
            
            // Ensure we have at least some slots by adding fallback if needed
            if (!slots.Any())
            {
                _logger.LogWarning("No slots generated, adding fallback slots");
                slots = GenerateFallbackSlots(startDate, durationMinutes, participantEmails);
            }
            
            // For multi-day requests, ensure we have good distribution across days
            if (startDate.Date != endDate.Date && slots.Count > 0)
            {
                var distinctDays = slots.Select(s => s.StartTime.Date).Distinct().Count();
                var requestedDays = (endDate.Date - startDate.Date).Days + 1;
                
                _logger.LogInformation("Slots generated for {DistinctDays} out of {RequestedDays} requested days", 
                    distinctDays, requestedDays);
                
                // If we only have slots for a few days but requested more, try to add more
                if (distinctDays < Math.Min(requestedDays, 3))
                {
                    _logger.LogInformation("Adding more slots to improve day distribution");
                    // The existing slot generation should already handle this,
                    // but we can log when distribution is poor
                }
            }
            
            // Limit results but ensure multi-day distribution
            int maxResults = int.Parse(_configuration["Scheduling:MaxSlotsToShow"] ?? "10");
            List<TimeSlot> resultSlots;
            
            if (startDate.Date == endDate.Date)
            {
                // Single day - just take top slots by score
                resultSlots = slots
                    .OrderByDescending(s => s.AvailabilityScore)
                    .Take(maxResults)
                    .ToList();
            }
            else
            {
                // Multi-day - try to get slots from different days
                resultSlots = new List<TimeSlot>();
                var slotsByDay = slots
                    .GroupBy(s => s.StartTime.Date)
                    .OrderBy(g => g.Key)
                    .ToList();
                
                int slotsPerDay = Math.Max(1, maxResults / Math.Max(slotsByDay.Count, 1));
                
                foreach (var dayGroup in slotsByDay)
                {
                    var daySlots = dayGroup
                        .OrderByDescending(s => s.AvailabilityScore)
                        .Take(slotsPerDay)
                        .ToList();
                    resultSlots.AddRange(daySlots);
                }
                
                // If we still have room, add more slots from best days
                if (resultSlots.Count < maxResults)
                {
                    var remainingSlots = slots
                        .Except(resultSlots)
                        .OrderByDescending(s => s.AvailabilityScore)
                        .Take(maxResults - resultSlots.Count);
                    resultSlots.AddRange(remainingSlots);
                }
            }
                
            _logger.LogInformation("Returning top {ResultCount} slots", resultSlots.Count);
            
            return Task.FromResult(resultSlots);
        }
        
        public async Task<string> ProcessSchedulingRequestAsync(string userMessage)
        {
            try
            {
                _logger.LogInformation("Processing scheduling request (AI-first): {Message}", userMessage);

                // Step 1: AI extraction
                var aiResult = await _cleanOpenWebUIClient.ExtractParametersAsync(userMessage, DateTime.UtcNow, _configuration);

                // Step 1a: Clarification needed
                if (aiResult.needClarification != null)
                {
                    if (aiResult.needClarification is InterviewSchedulingBot.Services.Integration.AiClarification clar)
                        return clar.question;
                    // Fallback: dynamic object scenario
                    var questionProp = aiResult.needClarification.GetType().GetProperty("question");
                    var question = questionProp?.GetValue(aiResult.needClarification)?.ToString() ?? "Could you clarify your request?";
                    return question;
                }

                // Step 2: Validate participants
                if (aiResult.participantEmails == null || aiResult.participantEmails.Count == 0)
                {
                    return "Please share the participant email addresses to check availability.";
                }

                // Step 3: Basic range sanity & weekend correction via follow-up if needed
                bool needsWeekendCorrection = aiResult.startDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                    || aiResult.endDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                if (needsWeekendCorrection)
                {
                    _logger.LogInformation("Detected weekend in AI range; requesting correction");
                    aiResult = await _cleanOpenWebUIClient.ExtractParametersAsync(userMessage, DateTime.UtcNow, _configuration, "Weekends are not allowed by config; adjust to business days only.");
                    if (aiResult.needClarification != null)
                    {
                        return "Could you clarify a business-day range (Mon-Fri only)?";
                    }
                }

                // Detect likely truncation (single day but user said 'week')
                if (aiResult.startDate.Date == aiResult.endDate.Date && userMessage.ToLowerInvariant().Contains("week"))
                {
                    _logger.LogInformation("Single-day AI output with 'week' keyword; requesting reinterpretation");
                    aiResult = await _cleanOpenWebUIClient.ExtractParametersAsync(userMessage, DateTime.UtcNow, _configuration, "Your output seems to ignore the requested span; please re-interpret and return JSON again.");
                    if (aiResult.needClarification != null)
                        return "Could you confirm the exact business-day span you want?";
                }

                // Step 4: Determine duration
                int duration = aiResult.durationMinutes ?? int.Parse(_configuration["Scheduling:DefaultDurationMinutes"] ?? "60");

                // Step 5: Derive final day set based on daysSelector (algorithmic filtering only)
                var effectiveEnd = aiResult.endDate;
                var effectiveStart = aiResult.startDate;
                var distinctBusinessDays = EnumerateBusinessDays(effectiveStart, effectiveEnd).ToList();
                if (aiResult.daysSelector != null)
                {
                    if (aiResult.daysSelector.mode == "firstN" && aiResult.daysSelector.n.HasValue && aiResult.daysSelector.n.Value > 0)
                    {
                        distinctBusinessDays = distinctBusinessDays.Take(aiResult.daysSelector.n.Value).ToList();
                        if (distinctBusinessDays.Any())
                        {
                            effectiveStart = distinctBusinessDays.First();
                            effectiveEnd = distinctBusinessDays.Last();
                        }
                    }
                    else if (aiResult.daysSelector.mode == "specificDays" && aiResult.daysSelector.daysOfWeek.Any())
                    {
                        var wanted = new HashSet<string>(aiResult.daysSelector.daysOfWeek, StringComparer.OrdinalIgnoreCase);
                        distinctBusinessDays = distinctBusinessDays
                            .Where(d => wanted.Contains(d.ToString("ddd")))
                            .ToList();
                        if (distinctBusinessDays.Any())
                        {
                            effectiveStart = distinctBusinessDays.First();
                            effectiveEnd = distinctBusinessDays.Last();
                        }
                        else
                        {
                            return "I couldn’t match the specified days to business days. Please rephrase or specify valid weekdays (Mon-Fri).";
                        }
                    }
                }

                // Step 6: Generate slots across all selected days (timeOfDay filtering in method)
                var slots = await GenerateTimeSlotsWithFiltersAsync(
                    distinctBusinessDays,
                    duration,
                    aiResult.participantEmails,
                    aiResult.timeOfDay ?? "all");

                MarkRecommendedSlots(slots);

                // Step 7: Format via AI
                var context = new InterviewSchedulingBot.Services.Integration.MeetingContext
                {
                    AvailableSlots = slots,
                    Parameters = new InterviewSchedulingBot.Services.Integration.EnhancedMeetingParameters
                    {
                        StartDate = effectiveStart,
                        EndDate = effectiveEnd,
                        DurationMinutes = duration,
                        ParticipantEmails = aiResult.participantEmails,
                        TimeOfDay = aiResult.timeOfDay ?? "all"
                    },
                    OriginalRequest = userMessage
                };

                var formatted = await _cleanOpenWebUIClient.FormatSlotsAsync(context, _configuration);
                return formatted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI-first scheduling pipeline error for: {Message}", userMessage);
                return "I encountered an internal issue while processing your scheduling request. Please try again.";
            }
        }

        // New: Span-wide generation honoring selector and timeOfDay
        public async Task<List<TimeSlot>> GenerateTimeSlotsAsync(
            DateTime start,
            DateTime end,
            int durationMinutes,
            IReadOnlyList<string> participantEmails,
            string timeOfDay,
            InterviewSchedulingBot.Services.Integration.DaysSelector selector)
        {
            var businessDays = EnumerateBusinessDays(start, end).ToList();

            if (selector != null)
            {
                if (selector.mode == "firstN" && selector.n.HasValue && selector.n.Value > 0)
                {
                    businessDays = businessDays.Take(selector.n.Value).ToList();
                }
                else if (selector.mode == "specificDays" && selector.daysOfWeek != null && selector.daysOfWeek.Any())
                {
                    var wanted = new HashSet<string>(selector.daysOfWeek, StringComparer.OrdinalIgnoreCase);
                    businessDays = businessDays.Where(d => {
                        var code = System.Globalization.CultureInfo.GetCultureInfo("en-US").DateTimeFormat.GetAbbreviatedDayName(d.DayOfWeek);
                        // Ensure Mon/Tue/Wed/Thu/Fri format (trim trailing '.')
                        code = code.Replace(".", string.Empty);
                        // Normalize to 3-letter English code
                        code = code switch { "Thu" => "Thu", "Tue" => "Tue", "Wed" => "Wed", "Mon" => "Mon", "Fri" => "Fri", _ => code };
                        return wanted.Contains(code);
                    }).ToList();
                }
            }

            var slots = await GenerateTimeSlotsWithFiltersAsync(businessDays, durationMinutes, participantEmails.ToList(), timeOfDay ?? "all");
            MarkRecommendedSlots(slots);
            return slots;
        }

        private IEnumerable<DateTime> EnumerateBusinessDays(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            {
                if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                yield return d;
            }
        }

        private async Task<List<TimeSlot>> GenerateTimeSlotsWithFiltersAsync(
            List<DateTime> businessDays,
            int durationMinutes,
            List<string> participantEmails,
            string timeOfDay)
        {
            // Convert to start-end range for reuse of existing generator per day
            var allSlots = new List<TimeSlot>();
            foreach (var day in businessDays)
            {
                var daySlots = await GenerateTimeSlotsAsync(day, day, durationMinutes, participantEmails);
                allSlots.AddRange(daySlots);
            }

            if (timeOfDay == "morning" || timeOfDay == "afternoon")
            {
                var workStart = _workdayStart;
                var workEnd = _workdayEnd;
                var spanMinutes = (workEnd - workStart).TotalMinutes;
                var midPoint = workStart + TimeSpan.FromMinutes(spanMinutes / 2.0);
                if (timeOfDay == "morning")
                {
                    allSlots = allSlots.Where(s => s.StartTime.TimeOfDay < midPoint).ToList();
                }
                else
                {
                    allSlots = allSlots.Where(s => s.StartTime.TimeOfDay >= midPoint).ToList();
                }
            }
            return allSlots;
        }
        
    // Removed legacy parameter conversion in AI-first pipeline
        
        private bool IsParticipantAvailable(string email, DateTime slotStart)
        {
            // Deterministic availability based on email and time
            int hash = (email + slotStart.ToString("yyyyMMddHHmm")).GetHashCode();
            
            // Make 80% of slots available
            return Math.Abs(hash % 100) < 80;
        }
        
        private double CalculateScore(TimeSlot slot, int totalParticipants)
        {
            // Base score on availability percentage
            double availabilityScore = (double)slot.AvailableParticipants.Count / totalParticipants;
            
            // Boost morning slots slightly
            double timeBonus = 0;
            int hour = slot.StartTime.Hour;
            
            if (hour >= 9 && hour <= 11)
            {
                timeBonus = 0.05; // Morning bonus
            }
            else if (hour >= 14 && hour <= 15)
            {
                timeBonus = 0.03; // Early afternoon bonus
            }
            
            // Add a small random factor for variety
            double randomFactor = _random.NextDouble() * 0.05;
            
            return availabilityScore * 0.9 + timeBonus + randomFactor;
        }
        
        private void MarkRecommendedSlots(List<TimeSlot> slots)
        {
            // Group by day
            var slotsByDay = slots.GroupBy(s => s.StartTime.Date);
            
            foreach (var dayGroup in slotsByDay)
            {
                // Find best slot of the day and boost its score slightly to make it stand out
                var bestSlot = dayGroup.OrderByDescending(s => s.AvailabilityScore).FirstOrDefault();
                if (bestSlot != null && bestSlot.AvailabilityScore < 95.0)
                {
                    bestSlot.AvailabilityScore = Math.Min(100.0, bestSlot.AvailabilityScore + 5.0);
                }
            }
        }
        
        private List<TimeSlot> GenerateFallbackSlots(
            DateTime startDate,
            int durationMinutes,
            List<string> participantEmails)
        {
            // Ensure we're using a business day
            while (startDate.DayOfWeek == DayOfWeek.Saturday || 
                   startDate.DayOfWeek == DayOfWeek.Sunday)
            {
                startDate = startDate.AddDays(1);
            }
            
            var slots = new List<TimeSlot>();
            
            // Add morning slot
            var morningSlot = new TimeSlot
            {
                StartTime = startDate.Date.AddHours(10),
                EndTime = startDate.Date.AddHours(10).AddMinutes(durationMinutes),
                AvailabilityScore = 95.0,
                AvailableParticipants = new List<string>(participantEmails),
                TotalParticipants = participantEmails.Count
            };
            slots.Add(morningSlot);
            
            // Add afternoon slot
            var afternoonSlot = new TimeSlot
            {
                StartTime = startDate.Date.AddHours(14),
                EndTime = startDate.Date.AddHours(14).AddMinutes(durationMinutes),
                AvailabilityScore = 85.0,
                AvailableParticipants = new List<string>(participantEmails),
                TotalParticipants = participantEmails.Count
            };
            slots.Add(afternoonSlot);
            
            return slots;
        }
        
        private InterviewSchedulingBot.Services.Integration.EnhancedMeetingParameters CreateMockParameters(string userMessage)
        {
            var now = DateTime.Now;
            
            // Extract basic date references from message for mock response
            var lowerMessage = userMessage.ToLowerInvariant();
            var startDate = now.Date;
            
            if (lowerMessage.Contains("tomorrow"))
            {
                startDate = GetNextBusinessDay(now);
            }
            else if (lowerMessage.Contains("monday"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Monday);
            }
            else if (lowerMessage.Contains("tuesday"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Tuesday);
            }
            else if (lowerMessage.Contains("wednesday"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Wednesday);
            }
            else if (lowerMessage.Contains("thursday"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Thursday);
            }
            else if (lowerMessage.Contains("friday"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Friday);
            }
            else if (lowerMessage.Contains("next week"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Monday);
            }
            else
            {
                startDate = GetNextBusinessDay(now);
            }
            
            // Extract duration if present
            int duration = 60;
            var durationMatch = System.Text.RegularExpressions.Regex.Match(
                userMessage, 
                @"(\d+)\s*(?:min|mins|minutes)");
                
            if (durationMatch.Success && int.TryParse(durationMatch.Groups[1].Value, out int parsedDuration))
            {
                duration = parsedDuration;
            }
            
            // Extract emails from message
            var emails = new List<string>();
            var emailMatches = System.Text.RegularExpressions.Regex.Matches(
                userMessage, 
                @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
                
            foreach (System.Text.RegularExpressions.Match match in emailMatches)
            {
                emails.Add(match.Value);
            }
            
            // Do NOT add default emails if none found (policy: never invent participants)
            
            // Determine time of day
            string timeOfDay = "all";
            if (lowerMessage.Contains("morning"))
            {
                timeOfDay = "morning";
            }
            else if (lowerMessage.Contains("afternoon"))
            {
                timeOfDay = "afternoon";
            }
            
            return new InterviewSchedulingBot.Services.Integration.EnhancedMeetingParameters
            {
                StartDate = startDate,
                EndDate = startDate,
                DurationMinutes = duration,
                ParticipantEmails = emails,
                TimeOfDay = timeOfDay
            };
        }
        
        private DateTime GetNextBusinessDay(DateTime date)
        {
            var nextDay = date.AddDays(1);
            while (nextDay.DayOfWeek == DayOfWeek.Saturday || nextDay.DayOfWeek == DayOfWeek.Sunday)
            {
                nextDay = nextDay.AddDays(1);
            }
            return nextDay;
        }
        
        private DateTime GetNextSpecificDay(DateTime start, DayOfWeek targetDay)
        {
            int daysToAdd = ((int)targetDay - (int)start.DayOfWeek + 7) % 7;
            if (daysToAdd == 0) daysToAdd = 7; // If today is the target day, get next week
            return start.AddDays(daysToAdd);
        }
    }
}
