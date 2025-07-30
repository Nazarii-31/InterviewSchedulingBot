using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using InterviewBot.Bot.State;
using InterviewBot.Domain.Entities;
using InterviewBot.Domain.Interfaces;
using InterviewSchedulingBot.Services.Business;

namespace InterviewBot.Bot.Dialogs
{
    public class FindSlotsDialog : ComponentDialog
    {
        private readonly IAIResponseService _aiResponseService;
        private readonly ISchedulingService _schedulingService;
        private readonly BotStateAccessors _accessors;
        private readonly ILogger<FindSlotsDialog> _logger;
        
        public FindSlotsDialog(
            IAIResponseService aiResponseService,
            ISchedulingService schedulingService,
            BotStateAccessors accessors,
            ILogger<FindSlotsDialog> logger)
            : base(nameof(FindSlotsDialog))
        {
            _aiResponseService = aiResponseService;
            _schedulingService = schedulingService;
            _accessors = accessors;
            _logger = logger;
            
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                ParseQueryStepAsync,
                FindSlotsStepAsync,
                PresentResultsStepAsync,
                HandleFollowUpStepAsync
            }));
            
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            
            InitialDialogId = nameof(WaterfallDialog);
        }
        
        private async Task<DialogTurnResult> ParseQueryStepAsync(
            WaterfallStepContext stepContext, 
            CancellationToken cancellationToken)
        {
            // Get user's query from the activity or dialog state
            var query = stepContext.Options as string ?? stepContext.Context.Activity.Text;
            
            _logger.LogInformation("Processing natural language slot query: {Query}", query);
            
            // Show typing indicator
            await stepContext.Context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);
            
            try
            {
                // Create conversation context
                var conversationContext = new InterviewSchedulingBot.Services.Business.ConversationContext
                {
                    PreviousMessages = new List<string> { query },
                    CurrentIntent = "find_slots",
                    ParticipantIds = new List<string> { stepContext.Context.Activity.From.Id }
                };

                // Parse the query using AI
                var parseResult = await _aiResponseService.ParseUserQueryAsync(query, conversationContext, cancellationToken);
                
                if (!parseResult.Success || parseResult.Criteria == null)
                {
                    var errorMessage = await _aiResponseService.GenerateErrorMessageAsync(
                        "query_parsing_failed", 
                        $"Query: {query}", 
                        cancellationToken);
                    
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text(errorMessage),
                        cancellationToken: cancellationToken);
                    
                    return await stepContext.EndDialogAsync(null, cancellationToken);
                }
                
                // Store criteria in dialog state
                stepContext.Values["criteria"] = parseResult.Criteria;
                
                _logger.LogInformation("Successfully parsed criteria with confidence: {Confidence}", parseResult.ConfidenceScore);
                
                return await stepContext.NextAsync(cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing query: {Query}", query);
                
                var errorMessage = await _aiResponseService.GenerateErrorMessageAsync(
                    "processing_error", 
                    "Query parsing failed unexpectedly", 
                    cancellationToken);
                
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text(errorMessage),
                    cancellationToken: cancellationToken);
                
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
        
        private async Task<DialogTurnResult> FindSlotsStepAsync(
            WaterfallStepContext stepContext, 
            CancellationToken cancellationToken)
        {
            var criteria = (SlotQueryCriteria)stepContext.Values["criteria"];
            
            _logger.LogInformation("Finding slots with criteria: {Criteria}", criteria);
            
            // Show typing indicator
            await stepContext.Context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);
            
            try
            {
                // Use the scheduling service to find optimal slots
                var participantIds = criteria.ParticipantEmails.Any() 
                    ? criteria.ParticipantEmails 
                    : new List<string> { stepContext.Context.Activity.From.Id }; // Default to current user
                
                var slots = await _schedulingService.FindOptimalSlotsAsync(
                    participantIds,
                    criteria.StartDate,
                    criteria.EndDate,
                    criteria.DurationMinutes,
                    10); // Max 10 results
                
                // Filter slots based on time of day constraints
                if (criteria.TimeOfDay != null)
                {
                    slots = slots.Where(slot => criteria.TimeOfDay.Contains(slot.StartTime)).ToList();
                }
                
                stepContext.Values["slots"] = slots;
                stepContext.Values["participantIds"] = participantIds;
                
                _logger.LogInformation("Found {SlotCount} slots matching criteria", slots.Count);
                
                return await stepContext.NextAsync(cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding slots");
                
                var errorMessage = await _aiResponseService.GenerateErrorMessageAsync(
                    "slot_search_failed", 
                    "Unable to search for available slots", 
                    cancellationToken);
                
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text(errorMessage),
                    cancellationToken: cancellationToken);
                
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
        
        private async Task<DialogTurnResult> PresentResultsStepAsync(
            WaterfallStepContext stepContext, 
            CancellationToken cancellationToken)
        {
            var criteria = (SlotQueryCriteria)stepContext.Values["criteria"];
            var slots = (List<RankedTimeSlot>)stepContext.Values["slots"];
            var participantIds = (List<string>)stepContext.Values["participantIds"];
            
            _logger.LogInformation("Presenting {SlotCount} slots to user", slots.Count);
            
            // Show typing indicator
            await stepContext.Context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing }, cancellationToken);
            
            try
            {
                if (!slots.Any())
                {
                    // Generate AI-driven conflict explanation
                    var participantAvailability = await GetMockParticipantAvailabilityAsync(
                        participantIds,
                        criteria.StartDate,
                        criteria.EndDate);
                    
                    var conflictResponse = await _aiResponseService.GenerateConflictExplanationAsync(
                        participantIds,
                        participantAvailability,
                        criteria,
                        cancellationToken);
                    
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text(conflictResponse),
                        cancellationToken);
                    
                    return await stepContext.EndDialogAsync(null, cancellationToken);
                }
                else
                {
                    // Convert RankedTimeSlot to AvailableSlot for AI service
                    var availableSlots = slots.Select(s => new AvailableSlot
                    {
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Score = (int)s.Score,
                        AvailableParticipants = s.AvailableParticipants,
                        TotalParticipants = s.TotalParticipants,
                        ParticipantEmails = participantIds
                    }).ToList();

                    // Generate AI-driven slot suggestions
                    var slotResponse = await _aiResponseService.GenerateSlotSuggestionsAsync(
                        availableSlots,
                        criteria,
                        cancellationToken);
                    
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text(slotResponse),
                        cancellationToken);
                    
                    // Store slots for potential scheduling
                    var interviewState = await _accessors.InterviewStateAccessor.GetAsync(
                        stepContext.Context, () => new InterviewState(), cancellationToken);
                    
                    interviewState.SuggestedSlots = slots.Select(s => new RankedTimeSlot
                    {
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Score = s.Score,
                        AvailableParticipants = s.AvailableParticipants,
                        TotalParticipants = s.TotalParticipants
                    }).ToList();
                    
                    interviewState.DurationMinutes = criteria.DurationMinutes;
                    interviewState.Participants = participantIds;
                    
                    // Generate AI-driven follow-up question
                    var followUpQuestion = await _aiResponseService.GenerateFollowUpQuestionAsync(
                        "slot_suggestions_presented",
                        new List<string> { "schedule", "find_different_options", "get_more_details" },
                        cancellationToken);
                    
                    return await stepContext.PromptAsync(
                        nameof(TextPrompt),
                        new PromptOptions
                        {
                            Prompt = MessageFactory.Text(followUpQuestion)
                        },
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error presenting results");
                
                var errorMessage = await _aiResponseService.GenerateErrorMessageAsync(
                    "result_presentation_failed", 
                    "Error formatting slot results", 
                    cancellationToken);
                
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text(errorMessage),
                    cancellationToken: cancellationToken);
                
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
        
        private async Task<DialogTurnResult> HandleFollowUpStepAsync(
            WaterfallStepContext stepContext, 
            CancellationToken cancellationToken)
        {
            var response = (stepContext.Result as string)?.ToLowerInvariant() ?? "";
            
            _logger.LogInformation("Handling follow-up response: {Response}", response);
            
            try
            {
                // Use AI to generate contextual responses based on user input
                if (response.Contains("schedule") || response.Contains("book"))
                {
                    var confirmationMessage = await _aiResponseService.GenerateConfirmationMessageAsync(
                        "schedule_slot",
                        new { UserResponse = response, Action = "scheduling" },
                        cancellationToken);
                    
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text(confirmationMessage),
                        cancellationToken: cancellationToken);
                }
                else if (response.Contains("different") || response.Contains("other") || response.Contains("more"))
                {
                    var alternativeMessage = await _aiResponseService.GenerateFollowUpQuestionAsync(
                        "user_wants_alternatives",
                        new List<string> { "different_day", "different_time", "different_duration", "different_participants" },
                        cancellationToken);
                    
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text(alternativeMessage),
                        cancellationToken: cancellationToken);
                }
                else if (response.Contains("details") || response.Contains("more info"))
                {
                    var detailsMessage = await _aiResponseService.GenerateResponseAsync(
                        new AIResponseRequest
                        {
                            ResponseType = "slot_details_explanation",
                            UserQuery = response,
                            Context = new { RequestType = "more_details", Context = "slot_suggestions" }
                        },
                        cancellationToken);
                    
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text(detailsMessage),
                        cancellationToken: cancellationToken);
                }
                else
                {
                    // Let AI handle any other type of response
                    var generalResponse = await _aiResponseService.GenerateResponseAsync(
                        new AIResponseRequest
                        {
                            ResponseType = "general_follow_up",
                            UserQuery = response,
                            Context = new { PreviousAction = "slot_suggestions", UserResponse = response }
                        },
                        cancellationToken);
                    
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text(generalResponse),
                        cancellationToken: cancellationToken);
                }
                
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling follow-up response: {Response}", response);
                
                var errorMessage = await _aiResponseService.GenerateErrorMessageAsync(
                    "follow_up_handling_failed",
                    $"User response: {response}",
                    cancellationToken);
                
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text(errorMessage),
                    cancellationToken: cancellationToken);
                
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
        
        private async Task<Dictionary<string, List<TimeSlot>>> GetMockParticipantAvailabilityAsync(
            List<string> participants,
            DateTime startDate,
            DateTime endDate)
        {
            // This creates mock availability data for demonstration
            // In a real implementation, this would query the actual availability service
            await Task.Delay(1); // Simulate async call
            
            var result = new Dictionary<string, List<TimeSlot>>();
            
            foreach (var participant in participants)
            {
                var slots = new List<TimeSlot>();
                
                // Create some mock available time slots
                var current = startDate.Date.AddHours(9); // Start at 9 AM
                
                while (current.Date <= endDate.Date)
                {
                    // Add morning slot (9-12)
                    slots.Add(new TimeSlot
                    {
                        StartTime = current,
                        EndTime = current.AddHours(3)
                    });
                    
                    // Add afternoon slot (13-17) with some gaps for realism
                    if (current.DayOfWeek != DayOfWeek.Wednesday) // Simulate Wednesday afternoon conflicts
                    {
                        slots.Add(new TimeSlot
                        {
                            StartTime = current.AddHours(4), // 1 PM
                            EndTime = current.AddHours(8)    // 5 PM
                        });
                    }
                    
                    current = current.AddDays(1);
                }
                
                result[participant] = slots;
            }
            
            return result;
        }
    }
}