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
    private readonly TimeSpan _askOnceExpiration = TimeSpan.FromMinutes(15);
        
        public ConversationStateManager(IMemoryCache cache, ILogger<ConversationStateManager> logger)
        {
            _cache = cache;
            _logger = logger;
        }
        
    public Task<List<MessageRecord>> GetHistoryAsync(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                _logger.LogWarning("Attempted to get history with null conversationId");
        return Task.FromResult(new List<MessageRecord>());
            }
            
            var cacheKey = $"conversation_{conversationId}";
        var list = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = _cacheExpiration;
                return new List<MessageRecord>();
        }) ?? new List<MessageRecord>();
        return Task.FromResult(list);
        }
        
    public Task AddToHistoryAsync(string conversationId, MessageRecord message)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                _logger.LogWarning("Attempted to add message to null conversationId");
        return Task.CompletedTask;
            }
            
            var cacheKey = $"conversation_{conversationId}";
        var history = GetHistoryAsync(conversationId).GetAwaiter().GetResult();
            
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
        return Task.CompletedTask;
        }
        
        public Task<bool> HasSentWelcomeMessageAsync(string conversationId)
        {
            var history = GetHistoryAsync(conversationId).GetAwaiter().GetResult();
            return Task.FromResult(history.Any(m => m.IsFromBot && m.Text.Contains("I'm your AI-powered Interview Scheduling assistant")));
        }

        public Task<SlotQueryCriteria?> GetLastQueryCriteriaAsync(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                _logger.LogWarning("Attempted to get query criteria with null conversationId");
                return Task.FromResult<SlotQueryCriteria?>(null);
            }

            var cacheKey = $"query_criteria_{conversationId}";
            return Task.FromResult(_cache.Get<SlotQueryCriteria>(cacheKey));
        }

        public Task SetLastQueryCriteriaAsync(string conversationId, SlotQueryCriteria criteria)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                _logger.LogWarning("Attempted to set query criteria with null conversationId");
                return Task.CompletedTask;
            }

            var cacheKey = $"query_criteria_{conversationId}";
            _cache.Set(cacheKey, criteria, _cacheExpiration);
            
            _logger.LogInformation("Stored query criteria for conversation {ConversationId}: {Criteria}", 
                conversationId, criteria.ToString());
            return Task.CompletedTask;
        }

        public Task ClearQueryCriteriaAsync(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                _logger.LogWarning("Attempted to clear query criteria with null conversationId");
                return Task.CompletedTask;
            }

            var cacheKey = $"query_criteria_{conversationId}";
            _cache.Remove(cacheKey);
            
            _logger.LogInformation("Cleared query criteria for conversation {ConversationId}", conversationId);
            return Task.CompletedTask;
        }

        // Prevent re-asking on identical content
        public Task<bool> ShouldAskClarificationOnceAsync(string conversationId, string contentHash)
            => ShouldAskOnceAsync(conversationId, "clarification", contentHash);

        public Task<bool> ShouldPromptParticipantsOnceAsync(string conversationId, string contentHash)
            => ShouldAskOnceAsync(conversationId, "participants", contentHash);

        private Task<bool> ShouldAskOnceAsync(string conversationId, string kind, string contentHash)
        {
            if (string.IsNullOrEmpty(conversationId) || string.IsNullOrEmpty(contentHash))
            {
                return Task.FromResult(true);
            }
            var key = $"askonce:{kind}:{conversationId}:{contentHash}";
            if (_cache.TryGetValue<bool>(key, out var alreadyAsked) && alreadyAsked)
            {
                _logger.LogInformation("Skipping repeated {Kind} ask for same content in conversation {ConversationId}", kind, conversationId);
                return Task.FromResult(false);
            }
            _cache.Set(key, true, _askOnceExpiration);
            return Task.FromResult(true);
        }
    }

    public class MessageRecord
    {
        public string Text { get; set; } = string.Empty;
        public bool IsFromBot { get; set; }
        public DateTime Timestamp { get; set; }
    }
}