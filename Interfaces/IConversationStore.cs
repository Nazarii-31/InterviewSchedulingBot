using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Interfaces
{
    public interface IConversationStore
    {
        Task<List<MessageHistoryItem>> GetHistoryAsync(string userId, string conversationId);
        Task SaveHistoryAsync(string userId, string conversationId, List<MessageHistoryItem> history);
        Task ClearHistoryAsync(string userId, string conversationId);
    }
}