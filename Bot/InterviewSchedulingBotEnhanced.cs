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
using InterviewSchedulingBot.Services.Integration;
using InterviewSchedulingBot.Models;

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
        private readonly IOpenWebUIClient _openWebUIClient;
        private readonly IConversationStore _conversationStore;

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
            InterviewBot.Domain.Interfaces.ISchedulingService schedulingService,
            IOpenWebUIClient openWebUIClient,
            IConversationStore conversationStore)
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
            _openWebUIClient = openWebUIClient;
            _conversationStore = conversationStore;
            
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
            var userId = turnContext.Activity.From.Id;
            var userMessage = turnContext.Activity.Text?.Trim();
            
            _logger.LogInformation("Received message: {Message}", userMessage);
            
            // Check if this is an adaptive card action
            if (turnContext.Activity.Value != null)
            {
                await HandleAdaptiveCardActionAsync(turnContext, cancellationToken);
                return;
            }
            
            // Check authentication first
            var isAuthenticated = await _authService.IsUserAuthenticatedAsync(userId);
            if (!isAuthenticated)
            {
                await HandleAuthenticationAsync(turnContext, cancellationToken);
                return;
            }
            
            // Show typing indicator
            await turnContext.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);
            
            try
            {
                var conversationId = turnContext.Activity.Conversation.Id;
                var history = await _conversationStore.GetHistoryAsync(userId, conversationId);
                
                // Add current message to history
                history.Add(new InterviewSchedulingBot.Models.MessageHistoryItem { Message = userMessage, IsFromBot = false, Timestamp = DateTime.UtcNow });
                
                // First, use OpenWebUI to determine the intent (DEFAULT processor)
                var intentResponse = await _openWebUIClient.RecognizeIntentAsync(userMessage, history);
                
                switch (intentResponse.TopIntent)
                {
                    case "FindSlots":
                        // Extract slot finding parameters and process
                        var slotParams = await _openWebUIClient.ExtractSlotParametersAsync(userMessage, history);
                        await ProcessSlotFindingAsync(turnContext, slotParams, history, cancellationToken);
                        break;
                        
                    case "Help":
                        await HandleHelpCommandAsync(turnContext, cancellationToken);
                        break;
                        
                    case "Logout":
                        await HandleLogoutAsync(turnContext, cancellationToken);
                        break;
                        
                    default:
                        // For any other intent, get a general response from OpenWebUI
                        var response = await _openWebUIClient.GetDirectResponseAsync(userMessage, conversationId, history);
                        await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
                        
                        // Add bot response to history
                        history.Add(new InterviewSchedulingBot.Models.MessageHistoryItem { Message = response, IsFromBot = true, Timestamp = DateTime.UtcNow });
                        break;
                }
                
                // Save updated history
                await _conversationStore.SaveHistoryAsync(userId, conversationId, history);
                
                // Update user activity and save state
                var userProfile = await _accessors.UserProfileAccessor.GetAsync(
                    turnContext, () => new UserProfile(), cancellationToken);
                userProfile.LastActivity = DateTime.UtcNow;
                
                await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
                await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("I encountered an issue processing your request. Please try again."), 
                    cancellationToken);
            }
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
        
        
        private async Task ProcessSlotFindingAsync(
            ITurnContext turnContext,
            SlotParameters parameters,
            List<InterviewSchedulingBot.Models.MessageHistoryItem> history,
            CancellationToken cancellationToken)
        {
            try
            {
                // If we don't have enough parameters, ask for missing info
                if (!parameters.HasMinimumRequiredInfo())
                {
                    var clarificationResponse = await _openWebUIClient.GenerateClarificationAsync(parameters, history);
                    await turnContext.SendActivityAsync(MessageFactory.Text(clarificationResponse), cancellationToken);
                    
                    // Add bot response to history
                    history.Add(new InterviewSchedulingBot.Models.MessageHistoryItem { Message = clarificationResponse, IsFromBot = true, Timestamp = DateTime.UtcNow });
                    return;
                }
                
                // Prepare parameters for slot finding
                var participantIds = parameters.Participants ?? new List<string>();
                var startDate = parameters.StartDate ?? DateTime.Today;
                var endDate = parameters.EndDate ?? startDate.AddDays(14);
                var durationMinutes = parameters.DurationMinutes ?? 60;
                
                // If specific day is mentioned, adjust the date range
                if (!string.IsNullOrEmpty(parameters.SpecificDay) && !parameters.StartDate.HasValue)
                {
                    var dayOfWeek = Enum.Parse<DayOfWeek>(parameters.SpecificDay, true);
                    startDate = GetNextWeekday(DateTime.Today, dayOfWeek);
                    endDate = startDate.AddDays(1);
                }
                
                // Use the scheduling service to find optimal slots
                var rankedSlots = await _schedulingService.FindOptimalSlotsAsync(
                    participantIds,
                    startDate,
                    endDate,
                    durationMinutes,
                    10); // Max 10 results
                    
                string slotResponse;
                if (rankedSlots.Any())
                {
                    // Convert RankedTimeSlot to TimeSlot for formatting
                    var timeSlots = rankedSlots.Select(rs => new InterviewSchedulingBot.Models.TimeSlot
                    {
                        StartTime = rs.StartTime,
                        EndTime = rs.EndTime,
                        AvailabilityScore = rs.Score,
                        AvailableParticipants = new List<string>(), // Would need participant names
                        TotalParticipants = rs.TotalParticipants
                    }).ToList();
                    
                    // Generate formatted response with actual slots using OpenWebUI
                    slotResponse = await _openWebUIClient.FormatAvailableSlotsAsync(timeSlots, parameters);
                }
                else
                {
                    // Generate no slots response
                    slotResponse = await _openWebUIClient.GenerateNoSlotsResponseAsync(parameters, history);
                }
                
                await turnContext.SendActivityAsync(MessageFactory.Text(slotResponse), cancellationToken);
                
                // Add bot response to history
                history.Add(new InterviewSchedulingBot.Models.MessageHistoryItem { Message = slotResponse, IsFromBot = true, Timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding slots");
                var errorMessage = "I encountered an issue while finding available slots. Please try again with more specific details.";
                await turnContext.SendActivityAsync(MessageFactory.Text(errorMessage), cancellationToken);
                
                // Add bot response to history
                history.Add(new InterviewSchedulingBot.Models.MessageHistoryItem { Message = errorMessage, IsFromBot = true, Timestamp = DateTime.UtcNow });
            }
        }

        private static DateTime GetNextWeekday(DateTime start, DayOfWeek day)
        {
            // Calculate days to add to get to the next occurrence of the specified day
            int daysToAdd = ((int)day - (int)start.DayOfWeek + 7) % 7;
            if (daysToAdd == 0) daysToAdd = 7; // If it's the same day, get next week
            return start.AddDays(daysToAdd);
        }

        private async Task HandleHelpCommandAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var helpMessage = "I can help you find available time slots for interviews and meetings! Here's what I can do:\n\n" +
                             "• Find available time slots using natural language\n" +
                             "• Check calendar availability for multiple participants\n" +
                             "• Suggest optimal meeting times\n\n" +
                             "Just ask me in plain English, like:\n" +
                             "• 'Find slots tomorrow morning'\n" +
                             "• 'When can we meet for 90 minutes next week?'\n" +
                             "• 'Show me availability for John and Sarah on Friday'";
            
            await turnContext.SendActivityAsync(MessageFactory.Text(helpMessage), cancellationToken);
        }

        private async Task HandleLogoutAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            await _authService.ClearTokenAsync(userId);
            
            var logoutMessage = "You have been logged out successfully. You'll need to authenticate again to use scheduling features.";
            await turnContext.SendActivityAsync(MessageFactory.Text(logoutMessage), cancellationToken);
        }
        
        private async Task HandleAuthenticationAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var authMessage = "I need to authenticate you before we can proceed with scheduling. Please log in to continue.";
            await turnContext.SendActivityAsync(MessageFactory.Text(authMessage), cancellationToken);
            
            // In a real implementation, this would redirect to authentication flow
            _logger.LogInformation("User authentication required for: {UserId}", turnContext.Activity.From.Id);
        }
        
        private async Task ProcessSchedulingCommandAsync(ITurnContext<IMessageActivity> turnContext, string message, CancellationToken cancellationToken)
        {
            // Create dialog context for scheduling operations
            var dialogContext = await _dialogs.CreateContextAsync(turnContext, cancellationToken);
            var results = await dialogContext.ContinueDialogAsync(cancellationToken);
            
            // If no active dialog, start appropriate scheduling dialog
            if (results.Status == DialogTurnStatus.Empty)
            {
                var lowerMessage = message.ToLowerInvariant();
                
                if (lowerMessage.Contains("find slots") || lowerMessage.Contains("available"))
                {
                    await dialogContext.BeginDialogAsync(nameof(FindSlotsDialog), message, cancellationToken);
                }
                else if (lowerMessage.Contains("schedule") || lowerMessage.Contains("book") || lowerMessage.Contains("create"))
                {
                    await dialogContext.BeginDialogAsync(nameof(ScheduleInterviewDialog), null, cancellationToken);
                }
                else
                {
                    // Default to slot finding for scheduling-related queries
                    await dialogContext.BeginDialogAsync(nameof(FindSlotsDialog), message, cancellationToken);
                }
            }
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