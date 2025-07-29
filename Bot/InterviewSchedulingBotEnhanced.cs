using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using MediatR;
using InterviewBot.Bot.State;
using InterviewBot.Bot.Dialogs;
using InterviewBot.Bot.Cards;
using InterviewBot.Application.Interviews.Commands;
using InterviewSchedulingBot.Interfaces.Business;
using InterviewSchedulingBot.Interfaces.Integration;
using InterviewSchedulingBot.Interfaces;
using InterviewSchedulingBot.Services.Business;

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
        private readonly SlotQueryParser _slotQueryParser;
        private readonly ConversationalResponseGenerator _responseGenerator;
        private readonly InterviewBot.Domain.Interfaces.ISchedulingService _schedulingService;

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
            ILoggerFactory loggerFactory,
            SlotQueryParser slotQueryParser,
            ConversationalResponseGenerator responseGenerator,
            InterviewBot.Domain.Interfaces.ISchedulingService schedulingService)
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
            _slotQueryParser = slotQueryParser;
            _responseGenerator = responseGenerator;
            _schedulingService = schedulingService;
            
            // Setup dialogs with specific loggers
            _dialogs = new DialogSet(_accessors.DialogStateAccessor);
            _dialogs.Add(new ScheduleInterviewDialog(_accessors, _mediator, loggerFactory.CreateLogger<ScheduleInterviewDialog>()));
            _dialogs.Add(new ViewInterviewsDialog(_accessors, _mediator, loggerFactory.CreateLogger<ViewInterviewsDialog>()));
            _dialogs.Add(new FindSlotsDialog(_slotQueryParser, _schedulingService, _responseGenerator, _accessors, loggerFactory.CreateLogger<FindSlotsDialog>()));
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
            
            // Check if this is an adaptive card action
            if (turnContext.Activity.Value != null)
            {
                await HandleAdaptiveCardActionAsync(turnContext, cancellationToken);
                return;
            }
            
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
        
        private async Task HandleAdaptiveCardActionAsync(
            ITurnContext<IMessageActivity> turnContext, 
            CancellationToken cancellationToken)
        {
            try
            {
                var cardData = turnContext.Activity.Value as Newtonsoft.Json.Linq.JObject;
                var action = cardData?["action"]?.ToString();
                
                _logger.LogInformation("Handling adaptive card action: {Action}", action);
                
                // Get current dialog context
                var dialogContext = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
                var interviewState = await _accessors.InterviewStateAccessor.GetAsync(
                    turnContext, () => new InterviewState(), cancellationToken);
                
                switch (action)
                {
                    case "selectSlot":
                        await HandleSlotSelectionAsync(turnContext, cardData, interviewState, dialogContext, cancellationToken);
                        break;
                        
                    case "confirm":
                        await HandleConfirmationAsync(turnContext, interviewState, dialogContext, cancellationToken);
                        break;
                        
                    case "cancel":
                        await HandleCancellationAsync(turnContext, dialogContext, cancellationToken);
                        break;
                        
                    case "back":
                        await HandleBackActionAsync(turnContext, dialogContext, cancellationToken);
                        break;
                        
                    case "scheduleAnother":
                        await HandleScheduleAnotherAsync(turnContext, dialogContext, cancellationToken);
                        break;
                        
                    case "viewInterviews":
                        await HandleViewInterviewsAsync(turnContext, dialogContext, cancellationToken);
                        break;
                        
                    default:
                        await turnContext.SendActivityAsync(
                            MessageFactory.Text("Unknown action. Please try again."), 
                            cancellationToken);
                        break;
                }
                
                // Save state after handling action
                await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
                await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling adaptive card action");
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("Sorry, there was an error processing your selection. Please try again."), 
                    cancellationToken);
            }
        }
        
        private async Task HandleSlotSelectionAsync(
            ITurnContext turnContext,
            Newtonsoft.Json.Linq.JObject cardData,
            InterviewState interviewState,
            DialogContext dialogContext,
            CancellationToken cancellationToken)
        {
            var slotIndex = cardData["slotIndex"]?.ToObject<int>() ?? -1;
            
            if (slotIndex >= 0 && slotIndex < interviewState.SuggestedSlots.Count)
            {
                var selectedSlot = interviewState.SuggestedSlots[slotIndex];
                interviewState.SelectedSlot = selectedSlot.StartTime;
                interviewState.CurrentStep = "Confirming";
                
                // Show confirmation card
                var confirmationCard = AdaptiveCardHelper.CreateInterviewConfirmationCard(
                    interviewState.Title,
                    selectedSlot.StartTime,
                    interviewState.DurationMinutes,
                    interviewState.Participants,
                    selectedSlot.AvailableParticipants,
                    selectedSlot.TotalParticipants);
                
                var activity = MessageFactory.Attachment(confirmationCard);
                activity.Text = "Please confirm your interview details:";
                
                await turnContext.SendActivityAsync(activity, cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("Invalid slot selection. Please try again."), 
                    cancellationToken);
            }
        }
        
        private async Task HandleConfirmationAsync(
            ITurnContext turnContext,
            InterviewState interviewState,
            DialogContext dialogContext,
            CancellationToken cancellationToken)
        {
            try
            {
                // Create the interview using MediatR
                var command = new ScheduleInterviewCommand
                {
                    Title = interviewState.Title,
                    StartTime = interviewState.SelectedSlot!.Value,
                    Duration = TimeSpan.FromMinutes(interviewState.DurationMinutes),
                    ParticipantEmails = interviewState.Participants
                };
                
                var result = await _mediator.Send(command, cancellationToken);
                
                if (result.Success)
                {
                    // Show success card
                    var successCard = AdaptiveCardHelper.CreateSchedulingSuccessCard(
                        interviewState.Title,
                        interviewState.SelectedSlot.Value,
                        interviewState.DurationMinutes,
                        interviewState.Participants);
                    
                    var activity = MessageFactory.Attachment(successCard);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                    
                    // Clear the interview state
                    await _accessors.InterviewStateAccessor.DeleteAsync(turnContext, cancellationToken);
                    
                    _logger.LogInformation("Interview scheduled successfully: {InterviewId}", result.InterviewId);
                }
                else
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text($"❌ Failed to schedule interview: {result.ErrorMessage}"), 
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming interview");
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("❌ An error occurred while scheduling the interview. Please try again."), 
                    cancellationToken);
            }
        }
        
        private async Task HandleCancellationAsync(
            ITurnContext turnContext,
            DialogContext dialogContext,
            CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(
                MessageFactory.Text("❌ Interview scheduling cancelled."), 
                cancellationToken);
            
            // Clear state and end dialog
            await _accessors.InterviewStateAccessor.DeleteAsync(turnContext, cancellationToken);
            await dialogContext.CancelAllDialogsAsync(cancellationToken);
        }
        
        private async Task HandleBackActionAsync(
            ITurnContext turnContext,
            DialogContext dialogContext,
            CancellationToken cancellationToken)
        {
            var interviewState = await _accessors.InterviewStateAccessor.GetAsync(
                turnContext, () => new InterviewState(), cancellationToken);
            
            // Go back to slot selection by showing slots again
            if (interviewState.SuggestedSlots?.Any() == true)
            {
                var cardAttachment = AdaptiveCardHelper.CreateTimeSlotSelectionCard(
                    interviewState.SuggestedSlots, interviewState.Title);
                var activity = MessageFactory.Attachment(cardAttachment);
                activity.Text = "Please select a different time slot:";
                
                await turnContext.SendActivityAsync(activity, cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("No slots available to go back to. Please start a new scheduling session."), 
                    cancellationToken);
            }
        }
        
        private async Task HandleScheduleAnotherAsync(
            ITurnContext turnContext,
            DialogContext dialogContext,
            CancellationToken cancellationToken)
        {
            // Clear state and start new scheduling dialog
            await _accessors.InterviewStateAccessor.DeleteAsync(turnContext, cancellationToken);
            await dialogContext.BeginDialogAsync(nameof(ScheduleInterviewDialog), null, cancellationToken);
        }
        
        private async Task HandleViewInterviewsAsync(
            ITurnContext turnContext,
            DialogContext dialogContext,
            CancellationToken cancellationToken)
        {
            // Start view interviews dialog
            await dialogContext.BeginDialogAsync(nameof(ViewInterviewsDialog), null, cancellationToken);
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
            else if (IsSlotFindingIntent(messageText))
            {
                // Handle natural language slot finding queries
                await dialogContext.BeginDialogAsync(nameof(FindSlotsDialog), turnContext.Activity.Text, cancellationToken);
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
                // Check if this might be a natural language query by looking for time/day references
                if (ContainsTimeOrDayReference(messageText))
                {
                    _logger.LogInformation("Detected potential slot query: {Message}", messageText);
                    await dialogContext.BeginDialogAsync(nameof(FindSlotsDialog), turnContext.Activity.Text, cancellationToken);
                }
                else
                {
                    // Unknown intent - provide helpful guidance
                    await ShowUnknownIntentMessageAsync(turnContext, cancellationToken);
                }
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

        private bool IsSlotFindingIntent(string message)
        {
            var slotFindingKeywords = new[] { "find slots", "find time", "availability", "available", "free time", "when can", "show slots" };
            return slotFindingKeywords.Any(keyword => message.Contains(keyword));
        }

        private bool ContainsTimeOrDayReference(string message)
        {
            var timeReferences = new[] { 
                "morning", "afternoon", "evening", "today", "tomorrow", "monday", "tuesday", "wednesday", 
                "thursday", "friday", "saturday", "sunday", "next week", "this week", "next monday",
                "next tuesday", "next wednesday", "next thursday", "next friday", "am", "pm", 
                "time", "slot", "minute", "hour"
            };
            return timeReferences.Any(keyword => message.Contains(keyword));
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
                            "🔍 **Find Available Slots** *(NEW!)*\n" +
                            "   • Use natural language like:\n" +
                            "   • \"Find slots on Thursday afternoon\"\n" +
                            "   • \"Are there any slots next Monday?\"\n" +
                            "   • \"Show me morning availability tomorrow\"\n" +
                            "   • \"Find a 30-minute slot this week\"\n\n" +
                            "❌ **Cancel Interview** *(Coming Soon)*\n" +
                            "   • Type: `cancel`, `remove`\n" +
                            "   • Cancel or reschedule existing interviews\n\n" +
                            "**🤖 Natural Language Features:**\n" +
                            "• I understand conversational queries about time and availability\n" +
                            "• I can explain scheduling conflicts and suggest alternatives\n" +
                            "• I provide detailed availability information in human-readable format\n\n" +
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