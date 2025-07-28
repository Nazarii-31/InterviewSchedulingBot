using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using MediatR;
using InterviewBot.Bot.State;
using InterviewBot.Application.Availability.Queries;
using InterviewBot.Application.Interviews.Commands;
using InterviewBot.Application.DTOs;
using InterviewBot.Domain.Entities;

namespace InterviewBot.Bot.Dialogs
{
    public class ScheduleInterviewDialog : ComponentDialog
    {
        private readonly BotStateAccessors _accessors;
        private readonly IMediator _mediator;
        private readonly ILogger<ScheduleInterviewDialog> _logger;
        
        public ScheduleInterviewDialog(
            BotStateAccessors accessors,
            IMediator mediator,
            ILogger<ScheduleInterviewDialog> logger) 
            : base(nameof(ScheduleInterviewDialog))
        {
            _accessors = accessors;
            _mediator = mediator;
            _logger = logger;
            
            // Add dialog prompts
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new DateTimePrompt(nameof(DateTimePrompt)));
            AddDialog(new NumberPrompt<int>(nameof(NumberPrompt<int>)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            
            // Add waterfall dialog with steps
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                AskTitleStepAsync,
                AskParticipantsStepAsync,
                AskDateRangeStepAsync,
                AskDurationStepAsync,
                FindAvailableSlotsStepAsync,
                ShowAvailableSlotsStepAsync,
                ProcessSlotSelectionStepAsync,
                ConfirmInterviewStepAsync,
                FinalStepAsync
            }));
            
            InitialDialogId = nameof(WaterfallDialog);
        }
        
        private async Task<DialogTurnResult> AskTitleStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var promptOptions = new PromptOptions 
            { 
                Prompt = MessageFactory.Text("📝 Please enter a title for the interview:"),
                RetryPrompt = MessageFactory.Text("Please provide a valid title for the interview.")
            };
            
            return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
        }
        
        private async Task<DialogTurnResult> AskParticipantsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var interviewState = await _accessors.InterviewStateAccessor.GetAsync(
                stepContext.Context, () => new InterviewState(), cancellationToken);
            interviewState.Title = (string)stepContext.Result;
            interviewState.CurrentStep = "Participants";
            
            var promptOptions = new PromptOptions 
            { 
                Prompt = MessageFactory.Text("👥 Please enter the email addresses of participants (comma separated):\n\nExample: john@company.com, jane@company.com"),
                RetryPrompt = MessageFactory.Text("Please provide valid email addresses separated by commas.")
            };
            
            return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
        }
        
        private async Task<DialogTurnResult> AskDateRangeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var interviewState = await _accessors.InterviewStateAccessor.GetAsync(
                stepContext.Context, () => new InterviewState(), cancellationToken);
                
            // Parse participants
            var participantsText = (string)stepContext.Result;
            var participants = participantsText.Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p) && p.Contains('@'))
                .ToList();
                
            if (!participants.Any())
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("❌ No valid email addresses found. Please try again."), 
                    cancellationToken);
                return await stepContext.ReplaceDialogAsync(nameof(ScheduleInterviewDialog), null, cancellationToken);
            }
            
            interviewState.Participants = participants;
            interviewState.CurrentStep = "DateRange";
            
            var promptOptions = new PromptOptions 
            { 
                Prompt = MessageFactory.Text($"📅 When would you like to schedule this interview?\n\nPlease provide a date range (e.g., 'tomorrow' or 'next week' or 'January 15 to January 20'):\n\n👥 Participants: {string.Join(", ", participants)}"),
                RetryPrompt = MessageFactory.Text("Please provide a valid date or date range.")
            };
            
            return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
        }
        
        private async Task<DialogTurnResult> AskDurationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var interviewState = await _accessors.InterviewStateAccessor.GetAsync(
                stepContext.Context, () => new InterviewState(), cancellationToken);
                
            // Parse date range (simplified - in real implementation would use NLP or date parsing)
            var dateText = (string)stepContext.Result;
            interviewState.StartDate = DateTime.Today.AddDays(1); // Default to tomorrow
            interviewState.EndDate = DateTime.Today.AddDays(7);   // Default to next week
            interviewState.CurrentStep = "Duration";
            
            var choices = new List<Choice>
            {
                new Choice("30 minutes") { Value = "30" },
                new Choice("45 minutes") { Value = "45" },
                new Choice("60 minutes (1 hour)") { Value = "60" },
                new Choice("90 minutes (1.5 hours)") { Value = "90" },
                new Choice("120 minutes (2 hours)") { Value = "120" }
            };
            
            var promptOptions = new PromptOptions 
            { 
                Prompt = MessageFactory.Text("⏱️ How long should the interview be?"),
                Choices = choices,
                RetryPrompt = MessageFactory.Text("Please select a valid duration.")
            };
            
            return await stepContext.PromptAsync(nameof(ChoicePrompt), promptOptions, cancellationToken);
        }
        
        private async Task<DialogTurnResult> FindAvailableSlotsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var interviewState = await _accessors.InterviewStateAccessor.GetAsync(
                stepContext.Context, () => new InterviewState(), cancellationToken);
                
            var choice = (FoundChoice)stepContext.Result;
            interviewState.DurationMinutes = int.Parse(choice.Value);
            interviewState.CurrentStep = "FindingSlots";
            
            // Show typing indicator
            var typingActivity = new Activity { Type = ActivityTypes.Typing };
            await stepContext.Context.SendActivityAsync(typingActivity, cancellationToken);
            
            await stepContext.Context.SendActivityAsync(
                MessageFactory.Text("🔍 Finding optimal interview times..."), 
                cancellationToken);
            
            try
            {
                // Use MediatR to find optimal slots
                var query = new FindOptimalSlotsQuery
                {
                    ParticipantEmails = interviewState.Participants,
                    StartDate = interviewState.StartDate!.Value,
                    EndDate = interviewState.EndDate!.Value,
                    Duration = TimeSpan.FromMinutes(interviewState.DurationMinutes),
                    MaxResults = 5
                };
                
                var optimalSlots = await _mediator.Send(query, cancellationToken);
                interviewState.SuggestedSlots = optimalSlots;
                
                _logger.LogInformation("Found {SlotCount} optimal slots for interview", optimalSlots.Count);
                
                if (!optimalSlots.Any())
                {
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text("❌ No available time slots found for all participants. You may need to:\n\n" +
                                          "• Extend the date range\n" +
                                          "• Reduce the number of participants\n" +
                                          "• Try a shorter duration"), 
                        cancellationToken);
                    return await stepContext.EndDialogAsync(null, cancellationToken);
                }
                
                return await stepContext.NextAsync(null, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding optimal slots");
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("❌ An error occurred while finding available times. Please try again later."), 
                    cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
        
        private async Task<DialogTurnResult> ShowAvailableSlotsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var interviewState = await _accessors.InterviewStateAccessor.GetAsync(
                stepContext.Context, () => new InterviewState(), cancellationToken);
                
            var slots = interviewState.SuggestedSlots;
            interviewState.CurrentStep = "SelectingSlot";
            
            var choices = new List<Choice>();
            var messageText = "✅ Found available time slots! Please select your preferred option:\n\n";
            
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var timeStr = slot.StartTime.ToString("ddd, MMM dd 'at' h:mm tt");
                var availabilityStr = $"{slot.AvailableParticipants}/{slot.TotalParticipants} participants available";
                var scoreStr = $"Score: {slot.Score:F1}";
                
                messageText += $"**{i + 1}.** {timeStr}\n   📊 {availabilityStr} | {scoreStr}\n\n";
                
                choices.Add(new Choice($"{i + 1}. {timeStr}") { Value = i.ToString() });
            }
            
            var promptOptions = new PromptOptions 
            { 
                Prompt = MessageFactory.Text(messageText),
                Choices = choices,
                RetryPrompt = MessageFactory.Text("Please select a valid time slot option.")
            };
            
            return await stepContext.PromptAsync(nameof(ChoicePrompt), promptOptions, cancellationToken);
        }
        
        private async Task<DialogTurnResult> ProcessSlotSelectionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var interviewState = await _accessors.InterviewStateAccessor.GetAsync(
                stepContext.Context, () => new InterviewState(), cancellationToken);
                
            var choice = (FoundChoice)stepContext.Result;
            var selectedIndex = int.Parse(choice.Value);
            var selectedSlot = interviewState.SuggestedSlots[selectedIndex];
            
            interviewState.SelectedSlot = selectedSlot.StartTime;
            interviewState.CurrentStep = "Confirming";
            
            var confirmText = $"📅 **Interview Summary**\n\n" +
                             $"**Title:** {interviewState.Title}\n" +
                             $"**Date & Time:** {selectedSlot.StartTime:ddd, MMM dd, yyyy 'at' h:mm tt}\n" +
                             $"**Duration:** {interviewState.DurationMinutes} minutes\n" +
                             $"**Participants:** {string.Join(", ", interviewState.Participants)}\n" +
                             $"**Availability:** {selectedSlot.AvailableParticipants}/{selectedSlot.TotalParticipants} participants\n\n" +
                             "Would you like to schedule this interview?";
            
            var promptOptions = new PromptOptions 
            { 
                Prompt = MessageFactory.Text(confirmText),
                RetryPrompt = MessageFactory.Text("Please answer yes or no.")
            };
            
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), promptOptions, cancellationToken);
        }
        
        private async Task<DialogTurnResult> ConfirmInterviewStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var confirmed = (bool)stepContext.Result;
            
            if (!confirmed)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("❌ Interview scheduling cancelled. Feel free to start over anytime!"), 
                    cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            
            var interviewState = await _accessors.InterviewStateAccessor.GetAsync(
                stepContext.Context, () => new InterviewState(), cancellationToken);
                
            interviewState.CurrentStep = "Scheduling";
            
            // Show typing indicator
            await stepContext.Context.SendActivityAsync(
                new Activity { Type = ActivityTypes.Typing }, 
                cancellationToken);
                
            await stepContext.Context.SendActivityAsync(
                MessageFactory.Text("📝 Scheduling your interview..."), 
                cancellationToken);
            
            try
            {
                // Use MediatR to schedule the interview
                var command = new ScheduleInterviewCommand
                {
                    Title = interviewState.Title,
                    StartTime = interviewState.SelectedSlot!.Value,
                    Duration = TimeSpan.FromMinutes(interviewState.DurationMinutes),
                    Participants = interviewState.Participants.Select(email => new ParticipantDto
                    {
                        Email = email,
                        Name = email, // In real implementation, we'd resolve names
                        Role = "Interviewer" // Default role
                    }).ToList()
                };
                
                var interviewId = await _mediator.Send(command, cancellationToken);
                
                _logger.LogInformation("Successfully scheduled interview {InterviewId}", interviewId);
                
                return await stepContext.NextAsync(interviewId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling interview");
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("❌ Failed to schedule the interview. Please try again later."), 
                    cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
        
        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var interviewId = (Guid)stepContext.Result;
            var interviewState = await _accessors.InterviewStateAccessor.GetAsync(
                stepContext.Context, () => new InterviewState(), cancellationToken);
            
            var successMessage = $"✅ **Interview Successfully Scheduled!**\n\n" +
                               $"**Interview ID:** `{interviewId}`\n" +
                               $"**Title:** {interviewState.Title}\n" +
                               $"**Date & Time:** {interviewState.SelectedSlot:ddd, MMM dd, yyyy 'at' h:mm tt}\n" +
                               $"**Duration:** {interviewState.DurationMinutes} minutes\n\n" +
                               $"📧 Calendar invitations will be sent to all participants.\n" +
                               $"🔗 Teams meeting link will be included in the invitation.\n\n" +
                               $"Need help with anything else? Just ask!";
            
            await stepContext.Context.SendActivityAsync(
                MessageFactory.Text(successMessage), 
                cancellationToken);
            
            // Clear interview state
            await _accessors.InterviewStateAccessor.DeleteAsync(stepContext.Context, cancellationToken);
            
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}