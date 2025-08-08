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
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Models;
using InterviewBot.Domain.Entities;
using System.Globalization;
using InterviewBot.Services;
using InterviewBot.Models;
using IntegrationMeetingParameters = InterviewSchedulingBot.Services.Integration.MeetingParameters;
using IntegrationICleanOpenWebUIClient = InterviewSchedulingBot.Services.Integration.ICleanOpenWebUIClient;

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
                private readonly IntegrationICleanOpenWebUIClient _webUIClient;
        private readonly IConversationStore _conversationStore;
        private readonly ConversationStateManager _stateManager;
        private readonly SlotQueryParser _slotQueryParser;
        private readonly ConversationalResponseGenerator _conversationalResponseGenerator;
        private readonly InterviewBot.Services.SlotRecommendationService _slotRecommendationService;
        private readonly InterviewBot.Services.SlotResponseFormatter _responseFormatter;
        private readonly InterviewSchedulingBot.Services.Business.IInterviewSchedulingService _newSchedulingService;

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
            IntegrationICleanOpenWebUIClient webUIClient,
            IConversationStore conversationStore,
            ConversationStateManager stateManager,
            SlotQueryParser slotQueryParser,
            ConversationalResponseGenerator conversationalResponseGenerator,
            InterviewBot.Services.SlotRecommendationService slotRecommendationService,
            InterviewBot.Services.SlotResponseFormatter responseFormatter,
            InterviewSchedulingBot.Services.Business.IInterviewSchedulingService newSchedulingService)
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
            _webUIClient = webUIClient;
            _conversationStore = conversationStore;
            _stateManager = stateManager;
            _slotQueryParser = slotQueryParser;
            _conversationalResponseGenerator = conversationalResponseGenerator;
            _slotRecommendationService = slotRecommendationService;
            _responseFormatter = responseFormatter;
            _newSchedulingService = newSchedulingService;
            
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
            var conversationId = turnContext.Activity.Conversation.Id;
            
            // Check if we've already sent a welcome message to avoid duplicates
            if (await _stateManager.HasSentWelcomeMessageAsync(conversationId))
            {
                _logger.LogInformation("Welcome message already sent to conversation {ConversationId}", conversationId);
                return;
            }
            
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    // Generate AI-powered welcome message instead of hardcoded one
                    var welcomeParameters = new IntegrationMeetingParameters
                    {
                        Duration = 60,
                        TimeFrame = "general_welcome",
                        Participants = new List<string>()
                    };
                    
                    var welcomeMessage = await GenerateWelcomeResponseAsync(welcomeParameters);
                    
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeMessage), cancellationToken);
                    
                    // Record that we sent the welcome message
                    await _stateManager.AddToHistoryAsync(
                        conversationId,
                        new MessageRecord { Text = welcomeMessage, IsFromBot = true, Timestamp = DateTime.UtcNow });
                }
            }
        }

        protected override async Task OnMessageActivityAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var conversationId = turnContext.Activity.Conversation.Id;
            var userMessage = turnContext.Activity.Text?.Trim();
            var contentHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(userMessage ?? "")));
            
            _logger.LogInformation("Received message: {Message} in conversation {ConversationId}", 
                userMessage, conversationId);
            
            // Handle null or empty messages
            if (string.IsNullOrEmpty(userMessage))
            {
                // AI-generated short nudge for empty input
                var prompt = "The user sent an empty message. Generate a brief, polite English prompt asking them to type their scheduling request.";
                var context = new { type = "empty_message" };
                var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, context);
                await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
                return;
            }
            
            // Show typing indicator
            await turnContext.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);
            
            try
            {
                // Extract parameters via Clean client
                var aiParams = await _webUIClient.ExtractParametersAsync(userMessage!, DateTime.UtcNow, _configuration, (string?)null);

                // If range includes weekend, ask model to adjust to business days automatically
                if (aiParams.needClarification == null &&
                    (aiParams.startDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ||
                     aiParams.endDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday))
                {
                    _logger.LogInformation("Weekend detected in AI range; requesting business-day adjustment");
                    aiParams = await _webUIClient.ExtractParametersAsync(
                        userMessage!, DateTime.UtcNow, _configuration,
                        "Weekends are not allowed. Adjust startDate and endDate to Monday–Friday only and return one JSON object.");
                }

                // If the user asked for a week but AI collapsed to a single day, request a reinterpretation once
                if (aiParams.needClarification == null &&
                    aiParams.startDate.Date == aiParams.endDate.Date &&
                    userMessage!.IndexOf("week", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _logger.LogInformation("Single-day extraction for 'week' intent; requesting reinterpretation");
                    aiParams = await _webUIClient.ExtractParametersAsync(
                        userMessage!, DateTime.UtcNow, _configuration,
                        "Your previous output seems to collapse the requested 'week' into one day. Return the full requested span as business days, with correct startDate and endDate. Return only one JSON object.");
                }

                // Clarification branch (ask once per content hash)
                if (aiParams.needClarification != null)
                {
                    if (await _stateManager.ShouldAskClarificationOnceAsync(conversationId, contentHash))
                    {
                        string question;
                        if (aiParams.needClarification is InterviewSchedulingBot.Services.Integration.AiClarification clar)
                            question = clar.question;
                        else
                        {
                            var prop = aiParams.needClarification.GetType().GetProperty("question");
                            question = prop?.GetValue(aiParams.needClarification)?.ToString() ?? "Could you clarify your request?";
                        }
                        await turnContext.SendActivityAsync(MessageFactory.Text(question), cancellationToken);
                        await _stateManager.AddToHistoryAsync(conversationId, new MessageRecord { Text = userMessage!, IsFromBot = false, Timestamp = DateTime.UtcNow });
                        await _stateManager.AddToHistoryAsync(conversationId, new MessageRecord { Text = question, IsFromBot = true, Timestamp = DateTime.UtcNow });
                    }
                    return;
                }

                // Participants required (ask once per content hash)
                if (aiParams.participantEmails == null || aiParams.participantEmails.Count == 0)
                {
                    if (await _stateManager.ShouldPromptParticipantsOnceAsync(conversationId, contentHash))
                    {
                        var msg = "Please share the participant email addresses to check availability.";
                        await turnContext.SendActivityAsync(MessageFactory.Text(msg), cancellationToken);
                        await _stateManager.AddToHistoryAsync(conversationId, new MessageRecord { Text = userMessage!, IsFromBot = false, Timestamp = DateTime.UtcNow });
                        await _stateManager.AddToHistoryAsync(conversationId, new MessageRecord { Text = msg, IsFromBot = true, Timestamp = DateTime.UtcNow });
                    }
                    return;
                }

                // Duration fallback
                var duration = aiParams.durationMinutes ?? int.Parse(_configuration["Scheduling:DefaultDurationMinutes"] ?? "60");

                // Build day set via business service (no calendar semantics here)
                var selector = aiParams.daysSelector ?? new InterviewSchedulingBot.Services.Integration.DaysSelector { mode = "fullRange", n = null, daysOfWeek = new() };
                var slots = await _newSchedulingService.GenerateTimeSlotsAsync(
                    aiParams.startDate, aiParams.endDate, duration,
                    aiParams.participantEmails, aiParams.timeOfDay ?? "all", selector);

                // Mark recommended
                // reuse internal helper via reflection is complex; call existing formatting pipeline via AI client
                var ctx = new InterviewSchedulingBot.Services.Integration.MeetingContext
                {
                    AvailableSlots = slots,
                    Parameters = new InterviewSchedulingBot.Services.Integration.EnhancedMeetingParameters
                    {
                        StartDate = aiParams.startDate,
                        EndDate = aiParams.endDate,
                        DurationMinutes = duration,
                        ParticipantEmails = aiParams.participantEmails,
                        TimeOfDay = aiParams.timeOfDay ?? "all"
                    },
                    OriginalRequest = userMessage!
                };

                var formatted = await _webUIClient.FormatSlotsAsync(ctx, _configuration);

                await _stateManager.AddToHistoryAsync(conversationId, new MessageRecord { Text = userMessage!, IsFromBot = false, Timestamp = DateTime.UtcNow });
                await _stateManager.AddToHistoryAsync(conversationId, new MessageRecord { Text = formatted, IsFromBot = true, Timestamp = DateTime.UtcNow });
                await turnContext.SendActivityAsync(MessageFactory.Text(formatted), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message in conversation {ConversationId}", conversationId);
                // AI-generated graceful error
                var prompt = "We hit an internal error while handling a scheduling request. Write a short, friendly English apology and ask the user to try again in a moment.";
                var context = new { type = "error", details = ex.Message };
                var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, context);
                await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
            }
        }

    // Legacy helpers removed to enforce AI-first path
        private async Task<string> GenerateWelcomeResponseAsync(IntegrationMeetingParameters parameters)
        {
            try
            {
                // Use OpenWebUI to generate a dynamic welcome message
                var prompt = @"Generate a professional welcome message for an AI-powered interview scheduling assistant. 
                              Include: greeting, what you can help with (finding time slots, scheduling meetings, calendar management), 
                              mention natural language support, and ask how you can help today. Keep it warm but professional.";
                
                var context = new { type = "welcome", capabilities = new[] { "time_slots", "scheduling", "calendar_management" } };
                
                var response = await _openWebUIClient.GenerateResponseAsync(prompt, context);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate welcome response using OpenWebUI API");
                
                // Send detailed error message to user about API connectivity
                return $"⚠️ **System Error**: Unable to connect to AI service. Please check that OpenWebUI is properly configured and accessible.\n\n" +
                       $"**Error Details**: {ex.Message}\n\n" +
                       "Please contact your system administrator to resolve this issue.";
            }
        }

    // Legacy GenerateResponseAsync removed; bot now delegates to AI-first scheduling service

    // Legacy FindSlotsAsync removed

    // Legacy GenerateSlotsResponseAsync removed

    // Legacy manual date parsing removed
        
    private async Task HandleAdaptiveCardActionAsync(
            ITurnContext<IMessageActivity> turnContext, 
            CancellationToken cancellationToken)
        {
            try
            {
                var cardData = turnContext.Activity.Value as Newtonsoft.Json.Linq.JObject;
                var action = cardData?["action"]?.ToString();
                
                if (cardData == null)
                {
                    _logger.LogWarning("Received card action with null card data");
                    var prompt = "Generate a short English message telling the user that the action couldn't be processed and to try again.";
                    var ctx = new { type = "card_action_error" };
                    var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, ctx);
                    await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
                    return;
                }
                
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
                    {
                        var prompt = "Write a very short English message: 'Unknown action. Please try again.'";
                        var ctx = new { type = "unknown_action" };
                        var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, ctx);
                        await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
                        break;
                    }
                }
                
                // Save state after handling action
                await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
                await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling adaptive card action");
                var prompt = "Apologize briefly in English and ask the user to try their selection again due to an internal error.";
                var ctx = new { type = "card_action_exception", error = ex.Message };
                var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, ctx);
                await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
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
                var prompt = "Generate a short English heading inviting the user to confirm interview details (one sentence).";
                var ctx = new { type = "confirm_heading" };
                var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, ctx);
                activity.Text = aiText;
                await turnContext.SendActivityAsync(activity, cancellationToken);
            }
            else
            {
                var prompt = "Write a brief English message: the selected time slot is invalid; ask the user to pick another.";
                var ctx = new { type = "invalid_slot" };
                var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, ctx);
                await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
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
                    var prompt = "Compose a concise English error message that the interview could not be scheduled and include this reason in quotes: '" + result.ErrorMessage + "'.";
                    var ctx = new { type = "schedule_failed", reason = result.ErrorMessage };
                    var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, ctx);
                    await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming interview");
                var prompt = "Write a short English apology that scheduling failed due to an internal error and ask the user to try again.";
                var ctx = new { type = "schedule_exception", error = ex.Message };
                var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, ctx);
                await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
            }
        }
        
        private async Task HandleCancellationAsync(
            ITurnContext turnContext,
            DialogContext dialogContext,
            CancellationToken cancellationToken)
        {
            var prompt = "Write a short English confirmation that interview scheduling has been cancelled.";
            var ctx = new { type = "cancelled" };
            var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, ctx);
            await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
            
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
                var prompt = "Write a short English sentence asking the user to choose a different time slot.";
                var ctx = new { type = "choose_different_slot" };
                var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, ctx);
                activity.Text = aiText;
                await turnContext.SendActivityAsync(activity, cancellationToken);
            }
            else
            {
                var prompt = "Tell the user in English that there are no previous slots to return to and they should start a new scheduling session.";
                var ctx = new { type = "no_slots_to_go_back" };
                var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, ctx);
                await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
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
        

        private async Task HandleHelpCommandAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var prompt = "Generate a concise English help message for an interview scheduling bot. List 3-4 example queries and keep it under 80 words.";
            var context = new { type = "help" };
            var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, context);
            await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
        }

        private async Task HandleLogoutAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            await _authService.ClearTokenAsync(userId);
            var prompt = "Write a short English confirmation that the user has been logged out from the scheduling bot and must authenticate again for calendar features.";
            var context = new { type = "logout" };
            var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, context);
            await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
        }
        
        private async Task HandleAuthenticationAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var prompt = "Explain in one short paragraph (English) that authentication is required before scheduling and that a sign-in link will follow.";
            var context = new { type = "auth_required" };
            var aiText = await _openWebUIClient.GenerateResponseAsync(prompt, context);
            await turnContext.SendActivityAsync(MessageFactory.Text(aiText), cancellationToken);
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
        
    // RemoveBotMentions retained if needed elsewhere; not used in AI-first flow
        
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

    // Legacy keyword extraction removed

    // Legacy manual slot formatting removed (AI formatter is used)

    // Legacy range header generation removed

    // Legacy day parsing removed

    // Dedicated handler removed; all messages use AI-first pipeline
        
    // Legacy fallback removed

    // Legacy email extraction removed

    // Legacy duration extraction removed

    // Legacy range extraction removed
        
    // Legacy business-day utilities removed
    }
}