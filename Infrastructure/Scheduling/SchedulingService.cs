using InterviewBot.Domain.Entities;
using InterviewBot.Domain.Interfaces;

namespace InterviewBot.Infrastructure.Scheduling
{
    public class SchedulingService : ISchedulingService
    {
        private readonly IAvailabilityService _availabilityService;
        private readonly OptimalSlotFinder _slotFinder;
        private readonly ILogger<SchedulingService> _logger;
        
        public SchedulingService(
            IAvailabilityService availabilityService,
            OptimalSlotFinder slotFinder,
            ILogger<SchedulingService> logger)
        {
            _availabilityService = availabilityService;
            _slotFinder = slotFinder;
            _logger = logger;
        }
        
        public async Task<List<RankedTimeSlot>> FindOptimalSlotsAsync(
            List<string> participantIds,
            DateTime startDate,
            DateTime endDate,
            int durationMinutes,
            int maxResults)
        {
            try
            {
                _logger.LogInformation("Finding optimal slots for {ParticipantCount} participants from {StartDate} to {EndDate}", 
                    participantIds.Count, startDate, endDate);
                
                // Get availability for all participants
                var participantAvailability = await _availabilityService.GetParticipantAvailabilityAsync(
                    participantIds, startDate, endDate);
                
                // Find and rank optimal slots
                var requirements = new InterviewRequirements
                {
                    DurationMinutes = durationMinutes,
                    PreferredTimeOfDay = TimeSpan.FromHours(10), // 10 AM preference
                    WorkingHoursStart = TimeSpan.FromHours(9),   // 9 AM
                    WorkingHoursEnd = TimeSpan.FromHours(17),    // 5 PM
                    MaxResults = maxResults
                };
                
                var rankedSlots = _slotFinder.FindOptimalSlots(participantAvailability, requirements);
                
                _logger.LogInformation("Found {Count} optimal slots", rankedSlots.Count);
                
                return rankedSlots.Take(maxResults).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding optimal slots");
                return new List<RankedTimeSlot>();
            }
        }
    }
    
    public class OptimalSlotFinder
    {
        private readonly ILogger<OptimalSlotFinder> _logger;
        
        public OptimalSlotFinder(ILogger<OptimalSlotFinder> logger)
        {
            _logger = logger;
        }
        
        public List<RankedTimeSlot> FindOptimalSlots(
            Dictionary<string, List<TimeSlot>> participantAvailability,
            InterviewRequirements requirements)
        {
            var rankedSlots = new List<RankedTimeSlot>();
            var totalParticipants = participantAvailability.Count;
            
            // Generate deterministic quarter-hour aligned slots
            var allPotentialSlots = GenerateQuarterHourAlignedSlots(participantAvailability, requirements);
            
            // Evaluate each potential slot
            foreach (var start in allPotentialSlots)
            {
                var end = start.AddMinutes(requirements.DurationMinutes);
                
                // Count available participants for this slot and track who they are
                var availableParticipantIds = new List<string>();
                var unavailableParticipants = new List<ParticipantConflict>();
                
                foreach (var participantEntry in participantAvailability)
                {
                    var participantId = participantEntry.Key;
                    var participantSlots = participantEntry.Value;
                    
                    bool isAvailable = participantSlots.Any(slot => slot.StartTime <= start && slot.EndTime >= end);
                    
                    if (isAvailable)
                    {
                        availableParticipantIds.Add(participantId);
                    }
                    else
                    {
                        // Find conflicting meetings for this participant
                        var conflictingSlot = participantSlots
                            .FirstOrDefault(slot => start < slot.EndTime && end > slot.StartTime);
                        
                        var conflictReason = conflictingSlot != null 
                            ? $"Meeting until {conflictingSlot.EndTime:HH:mm}"
                            : "Not available";
                            
                        unavailableParticipants.Add(new ParticipantConflict
                        {
                            Email = participantId,
                            ConflictReason = conflictReason,
                            ConflictStartTime = conflictingSlot?.StartTime,
                            ConflictEndTime = conflictingSlot?.EndTime
                        });
                    }
                }
                
                var availableParticipants = availableParticipantIds.Count;
                
                // Only consider slots where at least half the participants are available
                if (availableParticipants >= Math.Max(1, totalParticipants / 2))
                {
                    var score = CalculateEnhancedSlotScore(start, end, availableParticipants, totalParticipants, requirements);
                    
                    rankedSlots.Add(new RankedTimeSlot
                    {
                        StartTime = start,
                        EndTime = end,
                        Score = score,
                        AvailableParticipants = availableParticipants,
                        TotalParticipants = totalParticipants,
                        AvailableParticipantEmails = availableParticipantIds,
                        UnavailableParticipants = unavailableParticipants
                    });
                }
            }
            
            // Sort by score (highest first) and return top results
            return rankedSlots
                .OrderByDescending(slot => slot.Score)
                .ThenBy(slot => slot.StartTime) // Secondary sort for deterministic results
                .Take(requirements.MaxResults)
                .ToList();
        }

        /// <summary>
        /// Generates deterministic quarter-hour aligned slots for consistent results
        /// </summary>
        private List<DateTime> GenerateQuarterHourAlignedSlots(
            Dictionary<string, List<TimeSlot>> participantAvailability,
            InterviewRequirements requirements)
        {
            var allPotentialSlots = new HashSet<DateTime>();
            
            foreach (var participantSlots in participantAvailability.Values)
            {
                foreach (var slot in participantSlots)
                {
                    // Generate quarter-hour aligned potential meeting start times
                    var current = AlignToQuarterHour(slot.StartTime);
                    var slotEnd = slot.EndTime;
                    
                    while (current.AddMinutes(requirements.DurationMinutes) <= slotEnd)
                    {
                        // Only add if the slot is within working hours
                        if (IsWithinWorkingHours(current, requirements))
                        {
                            allPotentialSlots.Add(current);
                        }
                        current = current.AddMinutes(15); // Move to next quarter hour
                    }
                }
            }
            
            return allPotentialSlots.OrderBy(s => s).ToList();
        }

        /// <summary>
        /// Aligns a DateTime to the nearest quarter hour (00, 15, 30, 45)
        /// </summary>
        private DateTime AlignToQuarterHour(DateTime dateTime)
        {
            var minutes = dateTime.Minute;
            var alignedMinutes = (minutes / 15) * 15; // Round down to nearest quarter hour
            
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 
                               dateTime.Hour, alignedMinutes, 0, dateTime.Kind);
        }

        /// <summary>
        /// Checks if the time slot is within standard working hours
        /// </summary>
        private bool IsWithinWorkingHours(DateTime startTime, InterviewRequirements requirements)
        {
            // Skip weekends
            if (startTime.DayOfWeek == DayOfWeek.Saturday || startTime.DayOfWeek == DayOfWeek.Sunday)
                return false;
            
            var timeOfDay = startTime.TimeOfDay;
            return timeOfDay >= requirements.WorkingHoursStart && timeOfDay <= requirements.WorkingHoursEnd;
        }

        private double CalculateEnhancedSlotScore(
            DateTime start,
            DateTime end,
            int availableParticipants,
            int totalParticipants,
            InterviewRequirements requirements)
        {
            double score = 0;
            
            // Base score: percentage of participants available (0-100)
            score += (double)availableParticipants / totalParticipants * 100;
            
            // Bonus for full participant availability
            if (availableParticipants == totalParticipants)
            {
                score += 25; // Extra bonus for full availability
            }
            
            // Bonus for working hours (0-20 points)
            var timeOfDay = start.TimeOfDay;
            if (timeOfDay >= requirements.WorkingHoursStart && timeOfDay <= requirements.WorkingHoursEnd)
            {
                score += 20;
            }
            
            // Bonus for preferred time of day (0-15 points)
            var distanceFromPreferred = Math.Abs((timeOfDay - requirements.PreferredTimeOfDay).TotalHours);
            if (distanceFromPreferred <= 2) // Within 2 hours of preferred time
            {
                score += 15 * (1 - distanceFromPreferred / 2);
            }
            
            // Bonus for morning meetings (9 AM - 12 PM) for productivity
            if (timeOfDay >= TimeSpan.FromHours(9) && timeOfDay < TimeSpan.FromHours(12))
            {
                score += 10;
            }
            
            // Bonus for quarter-hour alignment (should always be true now, but keeping for clarity)
            if (start.Minute % 15 == 0)
            {
                score += 5;
            }
            
            // Penalty for late afternoon meetings (after 4 PM)
            if (timeOfDay >= TimeSpan.FromHours(16))
            {
                score -= 5;
            }
            
            // Penalty for weekends (-15 points)
            if (start.DayOfWeek == DayOfWeek.Saturday || start.DayOfWeek == DayOfWeek.Sunday)
            {
                score -= 15;
            }
            
            // Bonus for mid-week days (Tuesday-Thursday) (+5 points)
            if (start.DayOfWeek >= DayOfWeek.Tuesday && start.DayOfWeek <= DayOfWeek.Thursday)
            {
                score += 5;
            }
            
            return Math.Max(0, score); // Ensure score is not negative
        }
    }
    
    public class InterviewRequirements
    {
        public int DurationMinutes { get; set; } = 30;
        public TimeSpan PreferredTimeOfDay { get; set; } = TimeSpan.FromHours(10);
        public TimeSpan WorkingHoursStart { get; set; } = TimeSpan.FromHours(9);
        public TimeSpan WorkingHoursEnd { get; set; } = TimeSpan.FromHours(17);
        public int MaxResults { get; set; } = 5;
    }
}
