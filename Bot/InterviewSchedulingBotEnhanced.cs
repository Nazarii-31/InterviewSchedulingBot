using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using MediatR;
using InterviewBot.Bot.State;
using InterviewBot.Bot.Dialogs;
using InterviewSchedulingBot.Interfaces.Business;
using InterviewSchedulingBot.Interfaces.Integration;
using InterviewSchedulingBot.Interfaces;

namespace InterviewBot.Bot
{
    public class InterviewSchedulingBotEnhanced : TeamsActivityHandler
    {
        private readonly IAuthenticationService _authService;
        private readonly ISchedulingBusinessService _schedulingBusinessService;
        private readonly ITeamsIntegrationService _teamsIntegrationService;
        private readonly IMediator _mediator;
        private readonly IConfiguration _configuration;
        private readonly BotState _conversationState;
        private readonly BotState _userState;
        private readonly BotStateAccessors _accessors;
        private readonly DialogSet _dialogs;
        private readonly ILogger<InterviewSchedulingBotEnhanced> _logger;

        public InterviewSchedulingBotEnhanced(
            IAuthenticationService authService, 
            ISchedulingBusinessService schedulingBusinessService,
            ITeamsIntegrationService teamsIntegrationService,
            IMediator mediator,
            IConfiguration configuration,
            ConversationState conversationState,
            UserState userState,
            BotStateAccessors accessors,
            ILogger<InterviewSchedulingBotEnhanced> logger,
            ILoggerFactory loggerFactory)
        {
            _authService = authService;
            _schedulingBusinessService = schedulingBusinessService;
            _teamsIntegrationService = teamsIntegrationService;
            _mediator = mediator;
            _configuration = configuration;
            _conversationState = conversationState;
            _userState = userState;
            _accessors = accessors;
            _logger = logger;
            
            // Setup dialogs with specific loggers
            _dialogs = new DialogSet(_accessors.DialogStateAccessor);
            _dialogs.Add(new ScheduleInterviewDialog(_accessors, _mediator, loggerFactory.CreateLogger<ScheduleInterviewDialog>()));
            _dialogs.Add(new ViewInterviewsDialog(_accessors, _mediator, loggerFactory.CreateLogger<ViewInterviewsDialog>()));
        }

        protected override async Task OnMembersAddedAsync(
            IList<ChannelAccount> membersAdded,
            ITurnContext<IConversationUpdateActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var welcomeText = "🤖 **Welcome to the Interview Scheduling Bot!**\n\n" +
                "I can help you efficiently schedule interviews using our advanced Clean Architecture system:\n\n" +
                "✅ **Smart Scheduling** - Find optimal times for all participants\n" +
                "✅ **Calendar Integration** - Automatic calendar invites and Teams meetings\n" +
                "✅ **Conflict Detection** - Avoid scheduling conflicts\n" +
                "✅ **Multi-participant Support** - Handle complex scheduling scenarios\n\n" +
                "**What I can help you with:**\n" +
                "🗓️ **Schedule Interview** - Find and book optimal interview times\n" +
                "📅 **View Interviews** - See your upcoming interviews\n" +
                "❌ **Cancel Interviews** - Cancel or reschedule meetings\n" +
                "🔍 **Find Availability** - Check participant availability\n\n" +
                "**Quick Commands:**\n" +
                "• Type **'schedule'** or **'book'** to schedule a new interview\n" +
                "• Type **'view'** or **'list'** to see your interviews\n" +
                "• Type **'help'** for more options\n\n" +
                "Let's get started! What would you like to do?";

            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText), cancellationToken);
                    
                    // Initialize user profile
                    var userProfile = await _accessors.UserProfileAccessor.GetAsync(
                        turnContext, () => new UserProfile(), cancellationToken);
                    userProfile.LastActivity = DateTime.UtcNow;
                    
                    _logger.LogInformation("New user added to conversation: {UserId}", member.Id);
                }
            }
        }

        protected override async Task OnMessageActivityAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Received message: {Message}", turnContext.Activity.Text);
            
            // Update user activity
            var userProfile = await _accessors.UserProfileAccessor.GetAsync(
                turnContext, () => new UserProfile(), cancellationToken);
            userProfile.LastActivity = DateTime.UtcNow;
            
            // Create dialog context
            var dialogContext = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
            var results = await dialogContext.ContinueDialogAsync(cancellationToken);
            
            // If no active dialog, analyze message and route to appropriate dialog
            if (results.Status == DialogTurnStatus.Empty)
            {
                await RouteMessageAsync(dialogContext, turnContext, cancellationToken);
            }
            
            // Save state
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
        }
        
        private async Task RouteMessageAsync(
            DialogContext dialogContext, 
            ITurnContext turnContext, 
            CancellationToken cancellationToken)
        {
            var messageText = turnContext.Activity.Text?.ToLowerInvariant() ?? string.Empty;
            
            // Remove bot mentions if present
            messageText = RemoveBotMentions(messageText, turnContext);
            
            // Route based on intent
            if (IsSchedulingIntent(messageText))
            {
                await dialogContext.BeginDialogAsync(nameof(ScheduleInterviewDialog), null, cancellationToken);
            }
            else if (IsViewingIntent(messageText))
            {
                await dialogContext.BeginDialogAsync(nameof(ViewInterviewsDialog), null, cancellationToken);
            }
            else if (IsHelpIntent(messageText))
            {
                await ShowHelpMessageAsync(turnContext, cancellationToken);
            }
            else if (IsGreetingIntent(messageText))
            {
                await ShowGreetingMessageAsync(turnContext, cancellationToken);
            }
            else
            {
                // Unknown intent - provide helpful guidance
                await ShowUnknownIntentMessageAsync(turnContext, cancellationToken);
            }
        }
        
        private bool IsSchedulingIntent(string message)
        {
            var schedulingKeywords = new[] { "schedule", "book", "create", "plan", "arrange", "set up", "new interview", "meeting" };
            return schedulingKeywords.Any(keyword => message.Contains(keyword));
        }
        
        private bool IsViewingIntent(string message)
        {
            var viewingKeywords = new[] { "view", "list", "show", "see", "upcoming", "my interviews", "calendar" };
            return viewingKeywords.Any(keyword => message.Contains(keyword));
        }
        
        private bool IsHelpIntent(string message)
        {
            var helpKeywords = new[] { "help", "what can you do", "commands", "options", "how", "?" };
            return helpKeywords.Any(keyword => message.Contains(keyword));
        }
        
        private bool IsGreetingIntent(string message)
        {
            var greetingKeywords = new[] { "hello", "hi", "hey", "good morning", "good afternoon", "good evening", "start" };
            return greetingKeywords.Any(keyword => message.Contains(keyword));
        }
        
        private string RemoveBotMentions(string message, ITurnContext turnContext)
        {
            // Remove @mentions of the bot
            var botName = turnContext.Activity.Recipient?.Name;
            if (!string.IsNullOrEmpty(botName))
            {
                message = message.Replace($"@{botName.ToLowerInvariant()}", "").Trim();
            }
            return message;
        }
        
        private async Task ShowGreetingMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var greetingMessage = "👋 Hello! I'm your Interview Scheduling Assistant.\n\n" +
                                "I can help you:\n" +
                "🗓️ **Schedule interviews** with optimal time finding\n" +
                "📅 **View your upcoming interviews**\n" +
                "❌ **Manage your interview calendar**\n\n" +
                "What would you like to do today?";
            
            await turnContext.SendActivityAsync(MessageFactory.Text(greetingMessage), cancellationToken);
        }
        
        private async Task ShowHelpMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var helpMessage = "🆘 **Help - Interview Scheduling Bot**\n\n" +
                            "**Available Commands:**\n\n" +
                            "📝 **Schedule Interview**\n" +
                            "   • Type: `schedule`, `book`, `create interview`\n" +
                            "   • I'll guide you through scheduling a new interview\n\n" +
                            "📅 **View Interviews**\n" +
                            "   • Type: `view`, `list`, `my interviews`\n" +
                            "   • See your upcoming scheduled interviews\n\n" +
                            "❌ **Cancel Interview** *(Coming Soon)*\n" +
                            "   • Type: `cancel`, `remove`\n" +
                            "   • Cancel or reschedule existing interviews\n\n" +
                            "🔍 **Find Availability** *(Coming Soon)*\n" +
                            "   • Type: `availability`, `free time`\n" +
                            "   • Check when participants are available\n\n" +
                            "**Tips:**\n" +
                            "• I understand natural language - just tell me what you want to do!\n" +
                            "• I can handle multiple participants and complex scheduling\n" +
                            "• All meetings include automatic Teams links\n" +
                            "• Calendar invites are sent automatically\n\n" +
                            "Ready to get started? Just type what you'd like to do!";
            
            await turnContext.SendActivityAsync(MessageFactory.Text(helpMessage), cancellationToken);
        }
        
        private async Task ShowUnknownIntentMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var unknownMessage = "🤔 I'm not sure what you'd like to do.\n\n" +
                               "**I can help you with:**\n" +
                               "🗓️ **Schedule an interview** - type `schedule` or `book`\n" +
                               "📅 **View your interviews** - type `view` or `list`\n" +
                               "🆘 **Get help** - type `help`\n\n" +
                               "Or just describe what you want to do in your own words!";
            
            await turnContext.SendActivityAsync(MessageFactory.Text(unknownMessage), cancellationToken);
        }
        
        protected override async Task OnEventActivityAsync(
            ITurnContext<IEventActivity> turnContext,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Event activity received: {EventType}", turnContext.Activity.Name);
            await base.OnEventActivityAsync(turnContext, cancellationToken);
        }
        
        protected override async Task OnUnrecognizedActivityTypeAsync(
            ITurnContext turnContext,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Unrecognized activity type: {ActivityType}", turnContext.Activity.Type);
            await base.OnUnrecognizedActivityTypeAsync(turnContext, cancellationToken);
        }
    }
}