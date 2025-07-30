using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using InterviewBot.Domain.Entities;

namespace InterviewBot.Bot.State
{
    public class BotStateAccessors
    {
        public IStatePropertyAccessor<DialogState> DialogStateAccessor { get; }
        public IStatePropertyAccessor<InterviewState> InterviewStateAccessor { get; }
        public IStatePropertyAccessor<UserProfile> UserProfileAccessor { get; }
        
        public BotStateAccessors(ConversationState conversationState, UserState userState)
        {
            DialogStateAccessor = conversationState.CreateProperty<DialogState>(nameof(DialogState));
            InterviewStateAccessor = conversationState.CreateProperty<InterviewState>(nameof(InterviewState));
            UserProfileAccessor = userState.CreateProperty<UserProfile>(nameof(UserProfile));
        }
    }
    
    public class InterviewState
    {
        public string Title { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int DurationMinutes { get; set; } = 60;
        public List<string> Participants { get; set; } = new List<string>();
        public List<RankedTimeSlot> SuggestedSlots { get; set; } = new List<RankedTimeSlot>();
        public DateTime? SelectedSlot { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
    }
    
    public class UserProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GraphUserId { get; set; } = string.Empty;
        public bool IsAuthenticated { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public List<string> ConversationHistory { get; set; } = new List<string>();
    }
}