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
using InterviewBot.Models;
using InterviewBot.Services;
using System.Globalization;
using System.Text.RegularExpressions;

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
        private readonly ICleanOpenWebUIClient _cleanOpenWebUIClient;
        private readonly IConversationStore _conversationStore;
        private readonly ConversationStateManager _stateManager;
        private readonly SlotQueryParser _slotQueryParser;
        private readonly ConversationalResponseGenerator _conversationalResponseGenerator;
        private readonly DeterministicSlotRecommendationService _deterministicSlotService;
        private readonly TimeSlotResponseFormatter _timeSlotFormatter;

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
            ICleanOpenWebUIClient cleanOpenWebUIClient,
            IConversationStore conversationStore,
            ConversationStateManager stateManager,
            SlotQueryParser slotQueryParser,
            ConversationalResponseGenerator conversationalResponseGenerator,
            DeterministicSlotRecommendationService deterministicSlotService,
            TimeSlotResponseFormatter timeSlotFormatter)
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
            _cleanOpenWebUIClient = cleanOpenWebUIClient;
            _conversationStore = conversationStore;
            _stateManager = stateManager;
            _slotQueryParser = slotQueryParser;
            _conversationalResponseGenerator = conversationalResponseGenerator;
            _deterministicSlotService = deterministicSlotService;
            _timeSlotFormatter = timeSlotFormatter;
            
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
                    var welcomeParameters = new MeetingParameters
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
            
            _logger.LogInformation("Received message: {Message} in conversation {ConversationId}", 
                userMessage, conversationId);
            
            // Handle null or empty messages
            if (string.IsNullOrEmpty(userMessage))
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("I didn't receive any message. Please try again."), cancellationToken);
                return;
            }
            
            // Show typing indicator
            await turnContext.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);
            
            try
            {
                // Get previous conversation context
                var previousCriteria = await _stateManager.GetLastQueryCriteriaAsync(conversationId);
                
                // Parse the current query with context from previous conversation
                var currentCriteria = await _slotQueryParser.ParseQueryAsync(userMessage, previousCriteria, cancellationToken);
                
                if (currentCriteria != null)
                {
                    // Store the updated criteria for future use
                    await _stateManager.SetLastQueryCriteriaAsync(conversationId, currentCriteria);
                    
                    _logger.LogInformation("Stored slot criteria for conversation {ConversationId}: {Criteria}", 
                        conversationId, currentCriteria.ToString());
                }
                
                // Fallback to original parameter extraction if SlotQueryParser fails
                var parameters = await _cleanOpenWebUIClient.ExtractParametersAsync(userMessage);
                
                // Check if this is a slot request with emails - use deterministic handler
                if ((userMessage.Contains("slot") || userMessage.Contains("schedule") || userMessage.Contains("time") || userMessage.Contains("meeting")) 
                    && ExtractEmailsFromMessage(userMessage).Any())
                {
                    await HandleSlotRequestAsync(turnContext, userMessage, cancellationToken);
                    return;
                }
                
                // If we have SlotQueryCriteria, use it to enhance parameters
                if (currentCriteria != null)
                {
                    parameters = new MeetingParameters
                    {
                        Participants = currentCriteria.ParticipantEmails,
                        Duration = currentCriteria.DurationMinutes,
                        TimeFrame = currentCriteria.TimeOfDay != null ? 
                            $"{GetTimeOfDayName(currentCriteria.TimeOfDay)} slots" : 
                            parameters.TimeFrame
                    };
                }
                
                // Generate response using extracted parameters
                var response = await GenerateResponseAsync(parameters, userMessage, currentCriteria);
                
                // Add to conversation history
                await _stateManager.AddToHistoryAsync(
                    conversationId, 
                    new MessageRecord { Text = userMessage, IsFromBot = false, Timestamp = DateTime.UtcNow });
                    
                await _stateManager.AddToHistoryAsync(
                    conversationId,
                    new MessageRecord { Text = response, IsFromBot = true, Timestamp = DateTime.UtcNow });
                
                await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message in conversation {ConversationId}", conversationId);
                await turnContext.SendActivityAsync(MessageFactory.Text("I encountered an error processing your request. Please try again."), cancellationToken);
            }
        }

        private string GetTimeOfDayName(TimeOfDayRange timeOfDay)
        {
            // Morning: 8:00 AM to 12:00 PM
            if (timeOfDay.Start.Hours >= 8 && timeOfDay.End.Hours <= 12)
                return "morning";
            
            // Afternoon: 12:00 PM to 5:00 PM
            if (timeOfDay.Start.Hours >= 12 && timeOfDay.End.Hours <= 17)
                return "afternoon";
            
            // Evening: 5:00 PM to 8:00 PM
            if (timeOfDay.Start.Hours >= 17 && timeOfDay.End.Hours <= 20)
                return "evening";
            
            return string.Empty;
        }
        private async Task<string> GenerateWelcomeResponseAsync(MeetingParameters parameters)
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

        private async Task<string> GenerateResponseAsync(MeetingParameters parameters, string originalMessage)
        {
            return await GenerateResponseAsync(parameters, originalMessage, null);
        }

        private async Task<string> GenerateResponseAsync(MeetingParameters parameters, string originalMessage, SlotQueryCriteria? criteria)
        {
            try
            {
                // If participants are specified, find available slots
                if (parameters.Participants.Any())
                {
                    var slots = await FindSlotsAsync(parameters);
                    return await GenerateSlotsResponseAsync(slots, parameters, originalMessage);
                }
                
                // Otherwise generate a general AI response
                var prompt = $"The user said: '{originalMessage}'. Generate a helpful response for an interview scheduling assistant.";
                var context = new { userMessage = originalMessage, extractedParameters = parameters };
                
                var response = await _openWebUIClient.GenerateResponseAsync(prompt, context);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate response using OpenWebUI API");
                
                return $"⚠️ **System Error**: Unable to connect to AI service to process your request.\n\n" +
                       $"**Error Details**: {ex.Message}\n\n" +
                       "Please contact your system administrator to resolve this issue.";
            }
        }

        private async Task<List<InterviewSchedulingBot.Models.TimeSlot>> FindSlotsAsync(MeetingParameters parameters)
        {
            try
            {
                _logger.LogInformation("Finding available slots for {ParticipantCount} participants: {Participants}", 
                    parameters.Participants.Count, string.Join(", ", parameters.Participants));
                
                var startDate = ParseTimeFrame(parameters.TimeFrame);
                var endDate = startDate.AddDays(7);
                
                _logger.LogInformation("Scanning calendars from {StartDate} to {EndDate}", startDate, endDate);
                
                // Use the actual scheduling service to find real availability
                var availabilityRequest = new InterviewBot.Application.Availability.Queries.FindOptimalSlotsQuery
                {
                    ParticipantEmails = parameters.Participants,
                    StartDate = startDate,
                    EndDate = endDate,
                    Duration = TimeSpan.FromMinutes(parameters.Duration),
                    MaxResults = 10
                };
                
                var rankedSlots = await _mediator.Send(availabilityRequest);
                
                var slots = rankedSlots.Select(rankedSlot => new InterviewSchedulingBot.Models.TimeSlot
                {
                    StartTime = rankedSlot.StartTime,
                    EndTime = rankedSlot.EndTime,
                    AvailabilityScore = rankedSlot.Score,
                    AvailableParticipants = rankedSlot.AvailableParticipantEmails,
                    TotalParticipants = parameters.Participants.Count
                }).ToList();
                
                _logger.LogInformation("Found {SlotCount} available slots after scanning participant calendars", slots.Count);
                
                return slots;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding available slots");
                
                // Return empty list instead of mock data to indicate the failure
                return new List<InterviewSchedulingBot.Models.TimeSlot>();
            }
        }

        private async Task<string> GenerateSlotsResponseAsync(List<InterviewSchedulingBot.Models.TimeSlot> slots, MeetingParameters parameters, string originalMessage)
        {
            try
            {
                if (!slots.Any())
                {
                    return "I couldn't find any available slots matching your criteria.";
                }

                // Convert TimeSlot to RankedTimeSlot for the response generator
                var rankedSlots = slots.Select(slot => new RankedTimeSlot
                {
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Score = slot.AvailabilityScore,
                    AvailableParticipants = slot.AvailableParticipants.Count,
                    TotalParticipants = slot.TotalParticipants,
                    UnavailableParticipants = new List<ParticipantConflict>()
                }).ToList();

                // Parse time frame to determine criteria
                var startDate = ParseTimeFrame(parameters.TimeFrame);
                var endDate = startDate.AddDays(7);
                
                // Determine relative day from original message
                var relativeDay = ExtractRelativeDay(originalMessage, parameters.TimeFrame);
                var specificDay = ExtractSpecificDay(originalMessage);
                
                var criteria = new SlotQueryCriteria
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    ParticipantEmails = parameters.Participants,
                    DurationMinutes = parameters.Duration,
                    RelativeDay = relativeDay,
                    SpecificDay = specificDay
                };

                // Use the enhanced conversational response generator
                return await _conversationalResponseGenerator.GenerateSlotResponseAsync(rankedSlots, criteria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate slots response using OpenWebUI API");
                
                return $"⚠️ **System Error**: Unable to connect to AI service to format the scheduling response.\n\n" +
                       $"**Error Details**: {ex.Message}\n\n" +
                       "Please contact your system administrator to resolve this issue.";
            }
        }

        private DateTime ParseTimeFrame(string timeFrame)
        {
            var today = DateTime.Today;
            
            return timeFrame.ToLowerInvariant() switch
            {
                "tomorrow" => today.AddDays(1),
                "next week" => today.AddDays(7 - (int)today.DayOfWeek + 1), // Next Monday
                "next month" => today.AddMonths(1),
                "monday" => GetNextWeekday(today, DayOfWeek.Monday),
                "tuesday" => GetNextWeekday(today, DayOfWeek.Tuesday),
                "wednesday" => GetNextWeekday(today, DayOfWeek.Wednesday),
                "thursday" => GetNextWeekday(today, DayOfWeek.Thursday),
                "friday" => GetNextWeekday(today, DayOfWeek.Friday),
                _ => today.AddDays(1) // Default to tomorrow
            };
        }

        private DateTime GetNextWeekday(DateTime start, DayOfWeek day)
        {
            var daysUntilTargetDay = ((int)day - (int)start.DayOfWeek + 7) % 7;
            if (daysUntilTargetDay == 0) daysUntilTargetDay = 7; // If today is the target day, get next week
            return start.AddDays(daysUntilTargetDay);
        }
        
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
                    await turnContext.SendActivityAsync(MessageFactory.Text("Invalid card action received."), cancellationToken);
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
        

        private async Task HandleHelpCommandAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var helpMessage = "I can help you find available time slots for interviews and meetings! Here's what I can do:\n\n" +
                             "• Find available time slots using natural language\n" +
                             "• Check calendar availability for multiple participants\n" +
                             "• Suggest optimal meeting times\n" +
                             "• Analyze scheduling conflicts and suggest alternatives\n\n" +
                             "Just ask me in plain English, like:\n" +
                             "• 'Find slots tomorrow morning'\n" +
                             "• 'When are we available for 90 minutes next week?'\n" +
                             "• 'Show me availability for John and Sarah on Friday'\n" +
                             "• 'Check if we can meet Tuesday afternoon'";
            
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

        private string ExtractRelativeDay(string originalMessage, string timeFrame)
        {
            var lowerMessage = originalMessage.ToLowerInvariant();
            
            if (lowerMessage.Contains("tomorrow"))
                return "tomorrow";
            else if (lowerMessage.Contains("next week"))
                return "next week";
            else if (lowerMessage.Contains("next month"))
                return "next month";
            
            return timeFrame;
        }

        private string? ExtractSpecificDay(string originalMessage)
        {
            var lowerMessage = originalMessage.ToLowerInvariant();
            
            if (lowerMessage.Contains("monday"))
                return "monday";
            else if (lowerMessage.Contains("tuesday"))
                return "tuesday";
            else if (lowerMessage.Contains("wednesday"))
                return "wednesday";
            else if (lowerMessage.Contains("thursday"))
                return "thursday";
            else if (lowerMessage.Contains("friday"))
                return "friday";
            else if (lowerMessage.Contains("saturday"))
                return "saturday";
            else if (lowerMessage.Contains("sunday"))
                return "sunday";
            
            return null;
        }

        private string GenerateFormattedSlotResponse(List<RankedTimeSlot> slots, SlotQueryCriteria criteria)
        {
            if (!slots.Any())
                return "I couldn't find any available slots matching your criteria.";

            var response = new List<string>();
            var englishCulture = CultureInfo.GetCultureInfo("en-US");
            
            // Ensure all slots start at quarter hours (00, 15, 30, 45)
            var quarterHourSlots = slots.Where(slot => slot.StartTime.Minute % 15 == 0).ToList();
            
            // Group slots by day
            var slotsByDay = quarterHourSlots
                .GroupBy(s => s.StartTime.Date)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            // Add information about the requested time period
            var timeRangeInfo = GetRequestedTimeRangeInfo(criteria);
            
            response.Add($"Here are the available {criteria.DurationMinutes}-minute time slots{timeRangeInfo}:");
            
            // Display all days with their slots
            foreach (var dayGroup in slotsByDay.OrderBy(kvp => kvp.Key))
            {
                var day = dayGroup.Key;
                var dayName = day.ToString("dddd", englishCulture);
                var dateStr = day.ToString("dd.MM.yyyy", englishCulture);
                
                // Add day header with format "Monday [04.08.2025]"
                response.Add($"\n\n{dayName} [{dateStr}]");
                
                // Add all slots for this day
                var daySlots = dayGroup.Value.OrderBy(s => s.StartTime).ToList();
                foreach (var slot in daySlots)
                {
                    var startTimeStr = slot.StartTime.ToString("HH:mm", englishCulture);
                    var endTimeStr = slot.EndTime.ToString("HH:mm", englishCulture);
                    response.Add($"\n- {startTimeStr} - {endTimeStr}");
                }
            }
            
            response.Add("\n\nPlease let me know which time slot works best for you.");
            
            return string.Join("", response);
        }

        private string GetRequestedTimeRangeInfo(SlotQueryCriteria criteria)
        {
            var timeInfo = "";
            var englishCulture = CultureInfo.GetCultureInfo("en-US");
            
            // Add specific day information with exact date
            if (!string.IsNullOrEmpty(criteria.SpecificDay))
            {
                // Find the actual date for the specific day
                var requestedDayOfWeek = ParseDayOfWeek(criteria.SpecificDay);
                if (requestedDayOfWeek.HasValue)
                {
                    var today = DateTime.Today;
                    var daysUntilRequested = ((int)requestedDayOfWeek.Value - (int)today.DayOfWeek + 7) % 7;
                    if (daysUntilRequested == 0 && DateTime.Now.Hour >= 17) daysUntilRequested = 7; // If it's the same day but late, assume next week
                    var requestedDate = today.AddDays(daysUntilRequested);
                    timeInfo += $" for {criteria.SpecificDay} [{requestedDate.ToString("dd.MM.yyyy", englishCulture)}]";
                }
                else
                {
                    timeInfo += $" for {criteria.SpecificDay}";
                }
            }
            else if (!string.IsNullOrEmpty(criteria.RelativeDay))
            {
                if (criteria.RelativeDay.ToLower().Contains("tomorrow"))
                {
                    // For "tomorrow", format as "Monday [04.08.2025]" (no mention of "tomorrow")
                    var tomorrow = criteria.StartDate;
                    var dayName = tomorrow.ToString("dddd", englishCulture);
                    timeInfo += $" for {dayName} [{tomorrow.ToString("dd.MM.yyyy", englishCulture)}]";
                }
                else if (criteria.RelativeDay.ToLower().Contains("week"))
                {
                    timeInfo += $" for {criteria.RelativeDay} [{criteria.StartDate.ToString("dd.MM.yyyy", englishCulture)} - {criteria.EndDate.ToString("dd.MM.yyyy", englishCulture)}]";
                }
                else
                {
                    timeInfo += $" for {criteria.RelativeDay} [{criteria.StartDate.ToString("dd.MM.yyyy", englishCulture)}]";
                }
            }
            else if (criteria.StartDate != DateTime.MinValue && criteria.EndDate != DateTime.MinValue)
            {
                if (criteria.StartDate.Date == criteria.EndDate.Date)
                {
                    timeInfo += $" for [{criteria.StartDate.ToString("dd.MM.yyyy", englishCulture)}]";
                }
                else
                {
                    timeInfo += $" between [{criteria.StartDate.ToString("dd.MM.yyyy", englishCulture)}] and [{criteria.EndDate.ToString("dd.MM.yyyy", englishCulture)}]";
                }
            }
            
            return timeInfo;
        }

        private DayOfWeek? ParseDayOfWeek(string dayName)
        {
            return dayName?.ToLowerInvariant() switch
            {
                "monday" => DayOfWeek.Monday,
                "tuesday" => DayOfWeek.Tuesday,
                "wednesday" => DayOfWeek.Wednesday,
                "thursday" => DayOfWeek.Thursday,
                "friday" => DayOfWeek.Friday,
                "saturday" => DayOfWeek.Saturday,
                "sunday" => DayOfWeek.Sunday,
                _ => null
            };
        }

        // New methods for handling slot requests with deterministic behavior
        private async Task HandleSlotRequestAsync(ITurnContext<IMessageActivity> turnContext, string message, CancellationToken cancellationToken)
        {
            // Extract emails
            var emails = ExtractEmailsFromMessage(message);
            if (!emails.Any())
            {
                await turnContext.SendActivityAsync("I couldn't find any email addresses in your request. Please include participant emails.");
                return;
            }
            
            // Extract duration
            int duration = ExtractDurationFromMessage(message);
            
            // Extract date range
            var (startDate, endDate) = ExtractDateRangeFromMessage(message);
            
            // Generate slots using deterministic service
            var slots = _deterministicSlotService.GenerateConsistentTimeSlots(startDate, endDate, duration, emails);
            
            // Format response
            string response = _timeSlotFormatter.FormatResponse(slots, duration, startDate, endDate);
            
            await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
        }

        private List<string> ExtractEmailsFromMessage(string message)
        {
            // Use regex to extract emails
            var emails = new List<string>();
            var regex = new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
            var matches = regex.Matches(message);
            foreach (Match match in matches)
            {
                emails.Add(match.Value);
            }
            return emails;
        }

        private int ExtractDurationFromMessage(string message)
        {
            // Use regex to extract duration
            var regex = new Regex(@"(\d+)\s*(?:min|mins|minutes)");
            var match = regex.Match(message);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int duration))
            {
                return duration;
            }
            return 60; // Default to 60 minutes
        }

        private (DateTime start, DateTime end) ExtractDateRangeFromMessage(string message)
        {
            DateTime now = DateTime.Now;
            DateTime tomorrow = DateFormattingService.GetNextBusinessDay(now);
            
            // Default to tomorrow
            DateTime start = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, 9, 0, 0);
            DateTime end = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, 17, 0, 0);
            
            // Handle specific time ranges
            if (message.Contains("tomorrow"))
            {
                // Already set to tomorrow
            }
            else if (message.Contains("next week"))
            {
                // Find next Monday
                DateTime nextMonday = now.Date;
                while (nextMonday.DayOfWeek != DayOfWeek.Monday || nextMonday.Date <= now.Date)
                    nextMonday = nextMonday.AddDays(1);
                
                start = new DateTime(nextMonday.Year, nextMonday.Month, nextMonday.Day, 9, 0, 0);
                end = new DateTime(nextMonday.AddDays(4).Year, nextMonday.AddDays(4).Month, nextMonday.AddDays(4).Day, 17, 0, 0);
            }
            
            // Handle time of day
            if (message.Contains("morning"))
            {
                start = new DateTime(start.Year, start.Month, start.Day, 9, 0, 0);
                end = new DateTime(end.Year, end.Month, end.Day, 12, 0, 0);
            }
            else if (message.Contains("afternoon"))
            {
                start = new DateTime(start.Year, start.Month, start.Day, 13, 0, 0);
                end = new DateTime(end.Year, end.Month, end.Day, 17, 0, 0);
            }
            
            return (start, end);
        }
    }
}