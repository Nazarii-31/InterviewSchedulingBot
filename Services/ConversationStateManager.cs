using Microsoft.Extensions.Caching.Memory;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Services.Business;

namespace InterviewSchedulingBot.Services
{
    public class ConversationStateManager
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<ConversationStateManager> _logger;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);
        
        public ConversationStateManager(IMemoryCache cache, ILogger<ConversationStateManager> logger)
        {
            _cache = cache;
            _logger = logger;
        }
        
        public async Task<List<MessageRecord>> GetHistoryAsync(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                _logger.LogWarning("Attempted to get history with null conversationId");
                return new List<MessageRecord>();
            }
            
            var cacheKey = $"conversation_{conversationId}";
            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = _cacheExpiration;
                return new List<MessageRecord>();
            }) ?? new List<MessageRecord>();
        }
        
        public async Task AddToHistoryAsync(string conversationId, MessageRecord message)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                _logger.LogWarning("Attempted to add message to null conversationId");
                return;
            }
            
            var cacheKey = $"conversation_{conversationId}";
            var history = await GetHistoryAsync(conversationId);
            
            // Check if this message already exists to prevent duplicates
            if (!history.Any(m => m.Text == message.Text && m.IsFromBot == message.IsFromBot))
            {
                history.Add(message);
                _cache.Set(cacheKey, history, _cacheExpiration);
            }
            else
            {
                _logger.LogWarning("Duplicate message detected and prevented");
            }
        }
        
        public async Task<bool> HasSentWelcomeMessageAsync(string conversationId)
        {
            var history = await GetHistoryAsync(conversationId);
            return history.Any(m => m.IsFromBot && m.Text.Contains("I'm your AI-powered Interview Scheduling assistant"));
        }

        public async Task<SlotQueryCriteria?> GetLastQueryCriteriaAsync(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                _logger.LogWarning("Attempted to get query criteria with null conversationId");
                return null;
            }

            var cacheKey = $"query_criteria_{conversationId}";
            return _cache.Get<SlotQueryCriteria>(cacheKey);
        }

        public async Task SetLastQueryCriteriaAsync(string conversationId, SlotQueryCriteria criteria)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                _logger.LogWarning("Attempted to set query criteria with null conversationId");
                return;
            }

            var cacheKey = $"query_criteria_{conversationId}";
            _cache.Set(cacheKey, criteria, _cacheExpiration);
            
            _logger.LogInformation("Stored query criteria for conversation {ConversationId}: {Criteria}", 
                conversationId, criteria.ToString());
        }

        public async Task ClearQueryCriteriaAsync(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                _logger.LogWarning("Attempted to clear query criteria with null conversationId");
                return;
            }

            var cacheKey = $"query_criteria_{conversationId}";
            _cache.Remove(cacheKey);
            
            _logger.LogInformation("Cleared query criteria for conversation {ConversationId}", conversationId);
        }
    }

    public class MessageRecord
    {
        public string Text { get; set; } = string.Empty;
        public bool IsFromBot { get; set; }
        public DateTime Timestamp { get; set; }
    }
}