using Microsoft.Extensions.Caching.Memory;
using InterviewBot.Domain.Entities;
using InterviewBot.Domain.Interfaces;

namespace InterviewBot.Infrastructure.Caching
{
    public class CachedAvailabilityService : IAvailabilityService
    {
        private readonly IAvailabilityService _baseService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachedAvailabilityService> _logger;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);
        
        public CachedAvailabilityService(
            IAvailabilityService baseService,
            IMemoryCache cache,
            ILogger<CachedAvailabilityService> logger)
        {
            _baseService = baseService;
            _cache = cache;
            _logger = logger;
        }
        
        public async Task<List<TimeSlot>> FindCommonAvailabilityAsync(
            List<string> participantIds, 
            DateTime startDate, 
            DateTime endDate,
            int durationMinutes,
            int minRequiredParticipants)
        {
            var cacheKey = GenerateCacheKey("common", participantIds, startDate, endDate, durationMinutes, minRequiredParticipants);
            
            if (_cache.TryGetValue(cacheKey, out List<TimeSlot>? cachedResult))
            {
                _logger.LogDebug("Cache hit for common availability: {CacheKey}", cacheKey);
                return cachedResult ?? new List<TimeSlot>();
            }
            
            _logger.LogDebug("Cache miss for common availability: {CacheKey}", cacheKey);
            var result = await _baseService.FindCommonAvailabilityAsync(
                participantIds, startDate, endDate, durationMinutes, minRequiredParticipants);
            
            _cache.Set(cacheKey, result, _cacheExpiry);
            return result;
        }
        
        public async Task<Dictionary<string, List<TimeSlot>>> GetParticipantAvailabilityAsync(
            List<string> participantIds,
            DateTime startDate,
            DateTime endDate)
        {
            var result = new Dictionary<string, List<TimeSlot>>();
            var uncachedParticipants = new List<string>();
            
            // Check cache for each participant
            foreach (var participantId in participantIds)
            {
                var cacheKey = GenerateCacheKey("participant", new List<string> { participantId }, startDate, endDate);
                
                if (_cache.TryGetValue(cacheKey, out List<TimeSlot>? cachedAvailability))
                {
                    result[participantId] = cachedAvailability ?? new List<TimeSlot>();
                    _logger.LogDebug("Cache hit for participant availability: {ParticipantId}", participantId);
                }
                else
                {
                    uncachedParticipants.Add(participantId);
                }
            }
            
            // Fetch uncached participants
            if (uncachedParticipants.Any())
            {
                _logger.LogDebug("Cache miss for {Count} participants", uncachedParticipants.Count);
                var uncachedResult = await _baseService.GetParticipantAvailabilityAsync(
                    uncachedParticipants, startDate, endDate);
                
                // Cache and merge results
                foreach (var kvp in uncachedResult)
                {
                    var cacheKey = GenerateCacheKey("participant", new List<string> { kvp.Key }, startDate, endDate);
                    _cache.Set(cacheKey, kvp.Value, _cacheExpiry);
                    result[kvp.Key] = kvp.Value;
                }
            }
            
            return result;
        }
        
        private string GenerateCacheKey(
            string type, 
            List<string> participantIds, 
            DateTime startDate, 
            DateTime endDate,
            int durationMinutes = 0,
            int minParticipants = 0)
        {
            var participants = string.Join(",", participantIds.OrderBy(p => p));
            var key = $"availability:{type}:{participants}:{startDate:yyyy-MM-dd}:{endDate:yyyy-MM-dd}";
            
            if (durationMinutes > 0)
                key += $":{durationMinutes}";
            if (minParticipants > 0)
                key += $":{minParticipants}";
                
            return key;
        }
        
        public void ClearCache()
        {
            _logger.LogInformation("Clearing availability cache");
            
            // Clear cache entries (this is a simplified approach)
            // In a production system, you might want more granular cache invalidation
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0); // Remove all entries
            }
        }
        
        public void ClearCacheForParticipant(string participantId)
        {
            _logger.LogInformation("Clearing cache for participant: {ParticipantId}", participantId);
            
            // This would require a more sophisticated cache implementation
            // that tracks keys by participant for production use
        }
    }
}