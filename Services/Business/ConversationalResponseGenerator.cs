using InterviewBot.Domain.Entities;
using InterviewSchedulingBot.Services.Integration;
using System.Globalization;

namespace InterviewSchedulingBot.Services.Business
{
    public class ConversationalResponseGenerator
    {
        private readonly IOpenWebUIClient _openWebUIClient;
        private readonly ILogger<ConversationalResponseGenerator> _logger;
        
        public ConversationalResponseGenerator(
            IOpenWebUIClient openWebUIClient, 
            ILogger<ConversationalResponseGenerator> logger)
        {
            _openWebUIClient = openWebUIClient;
            _logger = logger;
        }
        
        public async Task<string> GenerateSlotResponseAsync(
            List<RankedTimeSlot> slots, 
            SlotQueryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Generating slot response for {SlotCount} slots", slots.Count);
                
                if (!slots.Any())
                {
                    return await GenerateNoSlotsResponseAsync(criteria, cancellationToken);
                }

                // Group slots by day
                var slotsByDay = slots
                    .GroupBy(s => s.StartTime.Date)
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.ToList());
                
                var context = new
                {
                    Query = new
                    {
                        TimeOfDay = criteria.TimeOfDay != null 
                            ? $"{criteria.TimeOfDay.Start:hh\\:mm} - {criteria.TimeOfDay.End:hh\\:mm}"
                            : "Any time",
                        SpecificDay = criteria.SpecificDay,
                        RelativeDay = criteria.RelativeDay,
                        StartDate = criteria.StartDate.ToString("yyyy-MM-dd"),
                        EndDate = criteria.EndDate.ToString("yyyy-MM-dd"),
                        DurationMinutes = criteria.DurationMinutes,
                        Participants = criteria.ParticipantEmails
                    },
                    SlotCount = slots.Count,
                    DayCount = slotsByDay.Count,
                    SlotsByDay = slotsByDay.Select(kv => new
                    {
                        Date = kv.Key.ToString("yyyy-MM-dd"),
                        DayOfWeek = kv.Key.DayOfWeek.ToString(),
                        Slots = kv.Value.Select(s => new
                        {
                            StartTime = s.StartTime.ToString("HH:mm"),
                            EndTime = s.EndTime.ToString("HH:mm"),
                            Score = s.Score,
                            AvailableParticipants = s.AvailableParticipants,
                            TotalParticipants = s.TotalParticipants
                        }).ToList()
                    }).ToList(),
                    BestSlot = slots.OrderByDescending(s => s.Score).FirstOrDefault()
                };
                
                var prompt = "Generate a conversational response about available interview time slots";
                var response = await _openWebUIClient.GenerateResponseAsync(prompt, context, cancellationToken);
                
                // If Open WebUI is not available, create a fallback response
                if (string.IsNullOrWhiteSpace(response) || response.Contains("fallback"))
                {
                    return GenerateFallbackSlotResponse(slots, criteria, slotsByDay);
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating slot response");
                return GenerateFallbackSlotResponse(slots, criteria, slots.GroupBy(s => s.StartTime.Date).ToDictionary(g => g.Key, g => g.ToList()));
            }
        }
        
        public async Task<string> GenerateConflictResponseAsync(
            List<string> participants,
            Dictionary<string, List<TimeSlot>> participantAvailability,
            SlotQueryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Generating conflict response for {ParticipantCount} participants", participants.Count);
                
                // Create a structure that shows which participants have conflicts
                var conflicts = new List<object>();
                
                foreach (var participant in participants)
                {
                    if (!participantAvailability.ContainsKey(participant) || 
                        !participantAvailability[participant].Any())
                    {
                        conflicts.Add(new
                        {
                            Participant = participant,
                            FullyBooked = true,
                            ConflictingTimes = new List<string>()
                        });
                        continue;
                    }
                    
                    // Identify free time gaps
                    var availableSlots = participantAvailability[participant];
                    var busyPeriods = GetBusyPeriods(
                        availableSlots, criteria.StartDate, criteria.EndDate);
                    
                    if (busyPeriods.Any())
                    {
                        conflicts.Add(new
                        {
                            Participant = participant,
                            FullyBooked = false,
                            ConflictingTimes = busyPeriods.Select(bp => 
                                $"{bp.Start:yyyy-MM-dd HH:mm} - {bp.End:HH:mm}").ToList()
                        });
                    }
                }
                
                var context = new
                {
                    Query = new
                    {
                        TimeOfDay = criteria.TimeOfDay != null 
                            ? $"{criteria.TimeOfDay.Start:hh\\:mm} - {criteria.TimeOfDay.End:hh\\:mm}"
                            : "Any time",
                        SpecificDay = criteria.SpecificDay,
                        RelativeDay = criteria.RelativeDay,
                        StartDate = criteria.StartDate.ToString("yyyy-MM-dd"),
                        EndDate = criteria.EndDate.ToString("yyyy-MM-dd"),
                        DurationMinutes = criteria.DurationMinutes
                    },
                    Participants = participants,
                    Conflicts = conflicts,
                    SuggestedAlternativeTimes = FindAlternativeTimes(participantAvailability, criteria)
                };
                
                var prompt = "Generate a conversational response explaining scheduling conflicts and suggesting alternatives";
                var response = await _openWebUIClient.GenerateResponseAsync(prompt, context, cancellationToken);
                
                // If Open WebUI is not available, create a fallback response
                if (string.IsNullOrWhiteSpace(response) || response.Contains("fallback"))
                {
                    return GenerateFallbackConflictResponse(participants, conflicts, criteria);
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating conflict response");
                return "I couldn't find suitable slots that match your criteria. Could you try with different dates or times?";
            }
        }

        private async Task<string> GenerateNoSlotsResponseAsync(SlotQueryCriteria criteria, CancellationToken cancellationToken = default)
        {
            var timeConstraint = criteria.TimeOfDay != null 
                ? $" in the {criteria.TimeOfDay.Start:hh\\:mm}-{criteria.TimeOfDay.End:hh\\:mm} time range"
                : "";
            
            var dayConstraint = !string.IsNullOrEmpty(criteria.SpecificDay) 
                ? $" on {criteria.SpecificDay}"
                : !string.IsNullOrEmpty(criteria.RelativeDay)
                    ? $" {criteria.RelativeDay}"
                    : "";

            return $"I couldn't find any available {criteria.DurationMinutes}-minute slots{dayConstraint}{timeConstraint}. " +
                   "Would you like me to:\n" +
                   "• Try a different time range or day\n" +
                   "• Look for shorter meeting durations\n" +
                   "• Check availability for the following week";
        }

        private string GenerateFallbackSlotResponse(
            List<RankedTimeSlot> slots, 
            SlotQueryCriteria criteria, 
            Dictionary<DateTime, List<RankedTimeSlot>> slotsByDay)
        {
            if (!slots.Any())
                return "I couldn't find any available slots matching your criteria.";

            var response = new List<string>();
            var bestSlot = slots.OrderByDescending(s => s.Score).First();
            var englishCulture = CultureInfo.GetCultureInfo("en-US");
            
            // Filter slots based on specific day request if provided
            if (!string.IsNullOrEmpty(criteria.SpecificDay))
            {
                var requestedDayOfWeek = ParseDayOfWeek(criteria.SpecificDay);
                if (requestedDayOfWeek.HasValue)
                {
                    slotsByDay = slotsByDay
                        .Where(kvp => kvp.Key.DayOfWeek == requestedDayOfWeek.Value)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    
                    slots = slotsByDay.SelectMany(kvp => kvp.Value).ToList();
                    
                    if (!slots.Any())
                    {
                        return $"I couldn't find any available slots on {criteria.SpecificDay}. Would you like me to check other days?";
                    }
                }
            }
            
            response.Add($"🎯 **Great news!** I found {slots.Count} available slot{(slots.Count > 1 ? "s" : "")} for your {criteria.DurationMinutes}-minute meeting.");
            
            if (slotsByDay.Count == 1)
            {
                var day = slotsByDay.Keys.First();
                var dayName = day.ToString("dddd", englishCulture);
                var dateStr = day.ToString("MMM dd", englishCulture);
                response.Add($"\n📅 **{dayName}, {dateStr}:**");
                
                foreach (var slot in slotsByDay[day].OrderBy(s => s.StartTime))
                {
                    var startTimeStr = slot.StartTime.ToString("h:mm tt", englishCulture);
                    var endTimeStr = slot.EndTime.ToString("h:mm tt", englishCulture);
                    var availabilityInfo = GetAvailabilityInfo(slot);
                    response.Add($"   • {startTimeStr} - {endTimeStr}{availabilityInfo}");
                }
            }
            else
            {
                response.Add($"\n📅 **Available across {slotsByDay.Count} days:**");
                
                foreach (var dayGroup in slotsByDay.OrderBy(kvp => kvp.Key))
                {
                    var dayName = dayGroup.Key.ToString("dddd", englishCulture);
                    var dateStr = dayGroup.Key.ToString("MMM dd", englishCulture);
                    response.Add($"\n**{dayName}, {dateStr}:**");
                    foreach (var slot in dayGroup.Value.Take(3).OrderBy(s => s.StartTime))
                    {
                        var startTimeStr = slot.StartTime.ToString("h:mm tt", englishCulture);
                        var endTimeStr = slot.EndTime.ToString("h:mm tt", englishCulture);
                        var availabilityInfo = GetAvailabilityInfo(slot);
                        response.Add($"   • {startTimeStr} - {endTimeStr}{availabilityInfo}");
                    }
                }
            }
            
            var bestSlotDay = bestSlot.StartTime.ToString("dddd", englishCulture);
            var bestSlotDate = bestSlot.StartTime.ToString("MMM dd", englishCulture);
            var bestSlotTime = bestSlot.StartTime.ToString("h:mm tt", englishCulture);
            response.Add($"\n⭐ **Best recommendation:** {bestSlotDay}, {bestSlotDate} at {bestSlotTime} " +
                        $"(Score: {bestSlot.Score:F0})");
            
            response.Add("\nWould you like me to schedule one of these slots or find different options?");
            
            return string.Join("", response);
        }

        private string GetAvailabilityInfo(RankedTimeSlot slot)
        {
            if (slot.TotalParticipants == 0)
                return "";
                
            if (slot.AvailableParticipants == slot.TotalParticipants)
            {
                return " ✅ All participants available";
            }
            else if (slot.AvailableParticipants > 0)
            {
                var availableInfo = $" ⚠️ {slot.AvailableParticipants}/{slot.TotalParticipants} participants available";
                
                if (slot.UnavailableParticipants.Any())
                {
                    var conflicts = slot.UnavailableParticipants
                        .Select(p => $"{p.Email} ({p.ConflictReason})")
                        .Take(2); // Limit to first 2 to avoid too much text
                    
                    availableInfo += $"\n     Unavailable: {string.Join(", ", conflicts)}";
                    
                    if (slot.UnavailableParticipants.Count > 2)
                    {
                        availableInfo += $" and {slot.UnavailableParticipants.Count - 2} others";
                    }
                }
                
                return availableInfo;
            }
            else
            {
                return " ❌ No participants available";
            }
        }

        private DayOfWeek? ParseDayOfWeek(string dayName)
        {
            return dayName?.ToLowerInvariant() switch
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

        private string GenerateFallbackConflictResponse(
            List<string> participants, 
            List<object> conflicts,
            SlotQueryCriteria criteria)
        {
            var response = new List<string>
            {
                "❌ **Scheduling Conflicts Found**\n"
            };

            var timeConstraint = criteria.TimeOfDay != null 
                ? $" during {criteria.TimeOfDay.Start:hh\\:mm}-{criteria.TimeOfDay.End:hh\\:mm}"
                : "";

            response.Add($"I couldn't find a suitable {criteria.DurationMinutes}-minute slot{timeConstraint} that works for everyone.\n");
            
            response.Add("**💡 Suggestions:**");
            response.Add("• Try a different time of day");
            response.Add("• Consider a shorter meeting duration");
            response.Add("• Look at the following week");
            response.Add("• Schedule with a subset of participants");
            
            return string.Join("\n", response);
        }
        
        private List<(DateTime Start, DateTime End)> GetBusyPeriods(
            List<TimeSlot> availableSlots, 
            DateTime startDate, 
            DateTime endDate)
        {
            // This is a simplified implementation
            // In a real scenario, this would analyze gaps in availability
            var busyPeriods = new List<(DateTime Start, DateTime End)>();
            
            // Create dummy busy periods for demonstration
            var current = startDate.Date.AddHours(9);
            while (current < endDate.Date.AddHours(17))
            {
                // Check if this time overlaps with any available slot
                var isAvailable = availableSlots.Any(slot => 
                    slot.StartTime <= current && slot.EndTime >= current.AddHours(1));
                
                if (!isAvailable)
                {
                    busyPeriods.Add((current, current.AddHours(1)));
                }
                
                current = current.AddHours(1);
            }
            
            return busyPeriods;
        }
        
        private List<string> FindAlternativeTimes(
            Dictionary<string, List<TimeSlot>> participantAvailability,
            SlotQueryCriteria criteria)
        {
            var alternatives = new List<string>();
            
            // Simple alternative time suggestions
            var nextDay = criteria.StartDate.AddDays(1);
            alternatives.Add($"{nextDay:yyyy-MM-dd} at 10:00");
            alternatives.Add($"{nextDay:yyyy-MM-dd} at 14:00");
            
            var nextWeek = criteria.StartDate.AddDays(7);
            alternatives.Add($"{nextWeek:yyyy-MM-dd} at {criteria.StartDate:HH:mm}");
            
            return alternatives;
        }
    }
}