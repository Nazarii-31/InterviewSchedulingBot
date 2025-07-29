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
        private readonly SlotQueryParser _queryParser;
        private readonly ISchedulingService _schedulingService;
        private readonly ConversationalResponseGenerator _responseGenerator;
        private readonly BotStateAccessors _accessors;
        private readonly ILogger<FindSlotsDialog> _logger;
        
        public FindSlotsDialog(
            SlotQueryParser queryParser,
            ISchedulingService schedulingService,
            ConversationalResponseGenerator responseGenerator,
            BotStateAccessors accessors,
            ILogger<FindSlotsDialog> logger)
            : base(nameof(FindSlotsDialog))
        {
            _queryParser = queryParser;
            _schedulingService = schedulingService;
            _responseGenerator = responseGenerator;
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
                // Parse the query to extract criteria
                var criteria = await _queryParser.ParseQueryAsync(query);
                
                if (criteria == null)
                {
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text("🤔 I'm sorry, I couldn't understand your query. Please try again with a different format.\n\n" +
                                           "**Examples:**\n" +
                                           "• \"Find slots on Thursday afternoon\"\n" +
                                           "• \"Are there any slots next Monday?\"\n" +
                                           "• \"Show me morning availability tomorrow\"\n" +
                                           "• \"Find a 30-minute slot this week\""),
                        cancellationToken: cancellationToken);
                    
                    return await stepContext.EndDialogAsync(null, cancellationToken);
                }
                
                // Store criteria in dialog state
                stepContext.Values["criteria"] = criteria;
                
                _logger.LogInformation("Successfully parsed criteria: {Criteria}", criteria);
                
                return await stepContext.NextAsync(cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing query: {Query}", query);
                
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("❌ I encountered an error while processing your request. Please try again with a simpler query."),
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
                
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("❌ I'm sorry, I encountered an error while looking for available slots. Please try again later."),
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
                    // Generate conflict explanation using mock participant availability
                    var participantAvailability = await GetMockParticipantAvailabilityAsync(
                        participantIds,
                        criteria.StartDate,
                        criteria.EndDate);
                    
                    var conflictResponse = await _responseGenerator.GenerateConflictResponseAsync(
                        participantIds,
                        participantAvailability,
                        criteria);
                    
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text(conflictResponse),
                        cancellationToken);
                    
                    return await stepContext.EndDialogAsync(null, cancellationToken);
                }
                else
                {
                    // Generate slot response
                    var slotResponse = await _responseGenerator.GenerateSlotResponseAsync(
                        slots,
                        criteria);
                    
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
                    
                    return await stepContext.PromptAsync(
                        nameof(TextPrompt),
                        new PromptOptions
                        {
                            Prompt = MessageFactory.Text("Would you like me to schedule one of these slots, find different options, or need more details?")
                        },
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error presenting results");
                
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("I found some available slots but encountered an error while formatting the results. Please try the 'schedule interview' command for guided scheduling."),
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
            
            if (response.Contains("schedule") || response.Contains("book"))
            {
                // Start the scheduling dialog with the suggested slots
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("Great! Let me help you schedule one of these slots. Please use the **'schedule interview'** command to continue with the guided scheduling process, or tell me which specific time you prefer."),
                    cancellationToken: cancellationToken);
            }
            else if (response.Contains("different") || response.Contains("other") || response.Contains("more"))
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("I'd be happy to find different options! Please provide new criteria such as:\n" +
                                       "• A different day or time range\n" +
                                       "• Different meeting duration\n" +
                                       "• Specific participants to include\n\n" +
                                       "Just describe what you're looking for in natural language."),
                    cancellationToken: cancellationToken);
            }
            else if (response.Contains("details") || response.Contains("more info"))
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("Here are more details about the available slots:\n\n" +
                                       "• **Scores** represent overall suitability based on participant availability, preferred times, and business hours\n" +
                                       "• **Availability ratio** shows how many participants are free vs. total participants\n" +
                                       "• **Best recommendations** are highlighted based on optimal timing and availability\n\n" +
                                       "Would you like me to schedule one of these slots or find different options?"),
                    cancellationToken: cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("Got it! Let me know if you need help with:\n" +
                                       "• Scheduling one of these slots\n" +
                                       "• Finding different time options\n" +
                                       "• Any other scheduling needs"),
                    cancellationToken: cancellationToken);
            }
            
            return await stepContext.EndDialogAsync(null, cancellationToken);
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