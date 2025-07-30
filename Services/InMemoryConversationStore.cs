using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces;
using System.Collections.Concurrent;

namespace InterviewSchedulingBot.Services
{
    public class InMemoryConversationStore : IConversationStore
    {
        private readonly ConcurrentDictionary<string, List<MessageHistoryItem>> _conversations = new();
        private readonly int _maxHistoryPerConversation = 50; // Limit to prevent memory bloat

        public Task<List<MessageHistoryItem>> GetHistoryAsync(string userId, string conversationId)
        {
            var key = $"{userId}:{conversationId}";
            return Task.FromResult(_conversations.GetValueOrDefault(key, new List<MessageHistoryItem>()));
        }

        public Task SaveHistoryAsync(string userId, string conversationId, List<MessageHistoryItem> history)
        {
            var key = $"{userId}:{conversationId}";
            
            // Limit history size to prevent memory issues
            if (history.Count > _maxHistoryPerConversation)
            {
                history = history.TakeLast(_maxHistoryPerConversation).ToList();
            }
            
            _conversations[key] = history;
            return Task.CompletedTask;
        }

        public Task ClearHistoryAsync(string userId, string conversationId)
        {
            var key = $"{userId}:{conversationId}";
            _conversations.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }
}