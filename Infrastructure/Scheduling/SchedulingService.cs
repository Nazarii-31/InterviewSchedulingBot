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
            
            // Get all unique time points where availability changes
            var timePoints = new SortedSet<DateTime>();
            
            foreach (var participantSlots in participantAvailability.Values)
            {
                foreach (var slot in participantSlots)
                {
                    timePoints.Add(slot.StartTime);
                    timePoints.Add(slot.EndTime);
                }
            }
            
            var timePointsList = timePoints.ToList();
            var totalParticipants = participantAvailability.Count;
            
            // Check each consecutive pair of time points for potential slots
            for (int i = 0; i < timePointsList.Count - 1; i++)
            {
                var start = timePointsList[i];
                var end = start.AddMinutes(requirements.DurationMinutes);
                
                // Make sure the slot doesn't exceed the next time point boundary
                if (i + 1 < timePointsList.Count && end > timePointsList[i + 1])
                    continue;
                
                // Count available participants for this slot and track who they are
                var availableParticipantIds = new List<string>();
                var unavailableParticipants = new List<ParticipantConflict>();
                
                foreach (var participantEntry in participantAvailability)
                {
                    var participantId = participantEntry.Key;
                    var participantSlots = participantEntry.Value;
                    
                    if (participantSlots.Any(slot => slot.StartTime <= start && slot.EndTime >= end))
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
                    var score = CalculateSlotScore(start, end, availableParticipants, totalParticipants, requirements);
                    
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
                .Take(requirements.MaxResults)
                .ToList();
        }
        
        private double CalculateSlotScore(
            DateTime start,
            DateTime end,
            int availableParticipants,
            int totalParticipants,
            InterviewRequirements requirements)
        {
            double score = 0;
            
            // Base score: percentage of participants available (0-100)
            score += (double)availableParticipants / totalParticipants * 100;
            
            // Bonus for working hours (0-20 points)
            var timeOfDay = start.TimeOfDay;
            if (timeOfDay >= requirements.WorkingHoursStart && timeOfDay <= requirements.WorkingHoursEnd)
            {
                score += 20;
            }
            
            // Bonus for preferred time of day (0-10 points)
            var distanceFromPreferred = Math.Abs((timeOfDay - requirements.PreferredTimeOfDay).TotalHours);
            if (distanceFromPreferred <= 2) // Within 2 hours of preferred time
            {
                score += 10 * (1 - distanceFromPreferred / 2);
            }
            
            // Penalty for weekends (-10 points)
            if (start.DayOfWeek == DayOfWeek.Saturday || start.DayOfWeek == DayOfWeek.Sunday)
            {
                score -= 10;
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
        public int DurationMinutes { get; set; } = 60;
        public TimeSpan PreferredTimeOfDay { get; set; } = TimeSpan.FromHours(10);
        public TimeSpan WorkingHoursStart { get; set; } = TimeSpan.FromHours(9);
        public TimeSpan WorkingHoursEnd { get; set; } = TimeSpan.FromHours(17);
        public int MaxResults { get; set; } = 5;
    }
}