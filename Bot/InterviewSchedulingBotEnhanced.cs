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
        private readonly IAIResponseService _aiResponseService;
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
            IAIResponseService aiResponseService,
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
            _aiResponseService = aiResponseService;
            _schedulingService = schedulingService;
            
            // Setup dialogs with specific loggers
            _dialogs = new DialogSet(_accessors.DialogStateAccessor);
            _dialogs.Add(new ScheduleInterviewDialog(_accessors, _mediator, loggerFactory.CreateLogger<ScheduleInterviewDialog>()));
            _dialogs.Add(new ViewInterviewsDialog(_accessors, _mediator, loggerFactory.CreateLogger<ViewInterviewsDialog>()));
            _dialogs.Add(new FindSlotsDialog(_aiResponseService, _schedulingService, _accessors, loggerFactory.CreateLogger<FindSlotsDialog>()));
        }

        protected override async Task OnMembersAddedAsync(
            IList<ChannelAccount> membersAdded,
            ITurnContext<IConversationUpdateActivity> turnContext,
            CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    // Generate AI-driven welcome message
                    var welcomeMessage = await _aiResponseService.GenerateWelcomeMessageAsync(
                        member.Name ?? "there", 
                        cancellationToken);
                    
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeMessage), cancellationToken);
                    
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
            
            // Route based on intent - check for specific commands first
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
            else if (ContainsTimeOrDayReference(messageText))
            {
                // Check if this might be a natural language query by looking for time/day references
                _logger.LogInformation("Detected potential slot query: {Message}", messageText);
                await dialogContext.BeginDialogAsync(nameof(FindSlotsDialog), turnContext.Activity.Text, cancellationToken);
            }
            else
            {
                // For ANY other message, use AI response service instead of showing unknown intent
                _logger.LogInformation("Processing general message with AI: {Message}", messageText);
                await HandleGeneralMessageWithAIAsync(turnContext, cancellationToken);
            }
        }
        
        private async Task HandleGeneralMessageWithAIAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            try
            {
                // Get conversation context
                var userProfile = await _accessors.UserProfileAccessor.GetAsync(
                    turnContext, () => new UserProfile(), cancellationToken);
                
                // Create context for AI response
                var conversationContext = new InterviewSchedulingBot.Services.Business.ConversationContext
                {
                    PreviousMessages = userProfile.ConversationHistory?.TakeLast(5).ToList() ?? new List<string>(),
                    CurrentIntent = "general_conversation"
                };
                
                // Generate AI response for the general message
                var response = await _aiResponseService.GenerateResponseAsync(
                    new AIResponseRequest
                    {
                        ResponseType = "general_response",
                        UserQuery = turnContext.Activity.Text ?? "",
                        Context = new { 
                            UserName = turnContext.Activity.From.Name ?? "there",
                            ConversationContext = conversationContext,
                            IsGeneralQuery = true
                        }
                    },
                    cancellationToken);
                
                await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
                
                // Update conversation history
                userProfile.ConversationHistory ??= new List<string>();
                userProfile.ConversationHistory.Add($"User: {turnContext.Activity.Text}");
                userProfile.ConversationHistory.Add($"Bot: {response}");
                
                // Keep only last 10 messages to avoid memory bloat
                if (userProfile.ConversationHistory.Count > 10)
                {
                    userProfile.ConversationHistory = userProfile.ConversationHistory.TakeLast(10).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling general message with AI");
                
                // Fallback to a helpful response
                var fallbackResponse = "I'm here to help with interview scheduling! You can ask me to find time slots, schedule meetings, or check availability. For example, try 'Find slots tomorrow morning' or 'Schedule an interview next week'.";
                await turnContext.SendActivityAsync(MessageFactory.Text(fallbackResponse), cancellationToken);
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
            var greetingMessage = await _aiResponseService.GenerateResponseAsync(
                new AIResponseRequest
                {
                    ResponseType = "greeting_message",
                    Context = new { UserName = turnContext.Activity.From.Name ?? "there" }
                },
                cancellationToken);
            
            await turnContext.SendActivityAsync(MessageFactory.Text(greetingMessage), cancellationToken);
        }
        
        private async Task ShowHelpMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var helpMessage = await _aiResponseService.GenerateHelpMessageAsync(
                "general_help", 
                cancellationToken);
            
            await turnContext.SendActivityAsync(MessageFactory.Text(helpMessage), cancellationToken);
        }
        
        private async Task ShowUnknownIntentMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var unknownMessage = await _aiResponseService.GenerateErrorMessageAsync(
                "unknown_intent", 
                "User message unclear", 
                cancellationToken);
            
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