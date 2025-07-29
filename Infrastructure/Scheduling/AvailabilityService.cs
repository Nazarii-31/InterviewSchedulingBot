using InterviewBot.Domain.Entities;
using InterviewBot.Domain.Interfaces;

namespace InterviewBot.Infrastructure.Scheduling
{
    public class AvailabilityService : IAvailabilityService
    {
        private readonly ICalendarService _calendarService;
        private readonly IAvailabilityRepository _availabilityRepository;
        private readonly ILogger<AvailabilityService> _logger;
        
        public AvailabilityService(
            ICalendarService calendarService,
            IAvailabilityRepository availabilityRepository,
            ILogger<AvailabilityService> logger)
        {
            _calendarService = calendarService;
            _availabilityRepository = availabilityRepository;
            _logger = logger;
        }
        
        public async Task<List<TimeSlot>> FindCommonAvailabilityAsync(
            List<string> participantIds, 
            DateTime startDate, 
            DateTime endDate,
            int durationMinutes,
            int minRequiredParticipants)
        {
            try
            {
                var allAvailability = await GetParticipantAvailabilityAsync(participantIds, startDate, endDate);
                
                var commonSlots = FindOverlappingFreeTime(allAvailability, durationMinutes, minRequiredParticipants);
                
                _logger.LogInformation("Found {Count} common availability slots for {ParticipantCount} participants", 
                    commonSlots.Count, participantIds.Count);
                
                return commonSlots;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding common availability for participants");
                return new List<TimeSlot>();
            }
        }
        
        public async Task<Dictionary<string, List<TimeSlot>>> GetParticipantAvailabilityAsync(
            List<string> participantIds,
            DateTime startDate,
            DateTime endDate)
        {
            var result = new Dictionary<string, List<TimeSlot>>();
            
            foreach (var participantId in participantIds)
            {
                try
                {
                    // Try to get cached availability first
                    var lastUpdate = await _availabilityRepository.GetLastUpdateTimeAsync(participantId);
                    var cached = await _availabilityRepository.GetAvailabilityAsync(participantId, startDate, endDate);
                    
                    // If we have recent data (less than 1 hour old), use it
                    if (lastUpdate.HasValue && lastUpdate.Value > DateTime.UtcNow.AddHours(-1) && cached.Any())
                    {
                        result[participantId] = cached;
                        _logger.LogDebug("Using cached availability for participant {ParticipantId}", participantId);
                    }
                    else
                    {
                        // Otherwise, fetch fresh data from calendar service
                        var fresh = await _calendarService.GetAvailabilityAsync(participantId, startDate, endDate);
                        result[participantId] = fresh;
                        
                        // Cache the fresh data
                        try
                        {
                            await _availabilityRepository.StoreAvailabilityAsync(participantId, fresh, startDate, endDate);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to cache availability for participant {ParticipantId}", participantId);
                        }
                        
                        _logger.LogDebug("Fetched fresh availability for participant {ParticipantId}", participantId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting availability for participant {ParticipantId}", participantId);
                    result[participantId] = new List<TimeSlot>();
                }
            }
            
            return result;
        }
        
        private List<TimeSlot> FindOverlappingFreeTime(
            Dictionary<string, List<TimeSlot>> allAvailability,
            int durationMinutes,
            int minRequiredParticipants)
        {
            var commonSlots = new List<TimeSlot>();
            
            if (allAvailability.Count == 0)
                return commonSlots;
            
            // Get all unique time points where availability changes
            var timePoints = new SortedSet<DateTime>();
            
            foreach (var participantSlots in allAvailability.Values)
            {
                foreach (var slot in participantSlots)
                {
                    timePoints.Add(slot.StartTime);
                    timePoints.Add(slot.EndTime);
                }
            }
            
            var timePointsList = timePoints.ToList();
            
            // Check each consecutive pair of time points
            for (int i = 0; i < timePointsList.Count - 1; i++)
            {
                var start = timePointsList[i];
                var end = timePointsList[i + 1];
                
                // Check if this interval is long enough
                if ((end - start).TotalMinutes < durationMinutes)
                    continue;
                
                // Count how many participants are available during this interval
                var availableCount = 0;
                foreach (var participantSlots in allAvailability.Values)
                {
                    if (participantSlots.Any(slot => slot.StartTime <= start && slot.EndTime >= end))
                    {
                        availableCount++;
                    }
                }
                
                // If enough participants are available, add this as a common slot
                if (availableCount >= minRequiredParticipants)
                {
                    commonSlots.Add(new TimeSlot
                    {
                        StartTime = start,
                        EndTime = end
                    });
                }
            }
            
            return commonSlots;
        }
    }
}