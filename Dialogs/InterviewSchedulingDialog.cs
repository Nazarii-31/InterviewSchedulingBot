using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Interfaces;
using System.Text.RegularExpressions;

namespace InterviewSchedulingBot.Dialogs
{
    public class InterviewSchedulingDialog : ComponentDialog
    {
        private readonly ISchedulingService _schedulingService;
        private readonly IGraphSchedulingService _graphSchedulingService;
        private readonly IConfiguration _configuration;

        // Define the dialog steps
        private const string WaterfallDialog = "mainWaterfallDialog";
        private const string ChoicePrompt = "choicePrompt";
        private const string TextPrompt = "textPrompt";
        private const string ConfirmPrompt = "confirmPrompt";

        public InterviewSchedulingDialog(
            ISchedulingService schedulingService,
            IGraphSchedulingService graphSchedulingService,
            IConfiguration configuration) 
            : base(nameof(InterviewSchedulingDialog))
        {
            _schedulingService = schedulingService;
            _graphSchedulingService = graphSchedulingService;
            _configuration = configuration;

            // Define the main waterfall dialog and its related components
            AddDialog(new WaterfallDialog(WaterfallDialog, new WaterfallStep[]
            {
                GreetingStepAsync,
                SchedulingTypeStepAsync,
                CollectAttendeesStepAsync,
                CollectDurationStepAsync,
                FindAvailableSlotsStepAsync,
                PresentSlotsStepAsync,
                ConfirmSelectionStepAsync,
                FinalStepAsync
            }));

            AddDialog(new ChoicePrompt(ChoicePrompt));
            AddDialog(new TextPrompt(TextPrompt, ValidateEmailsAsync));
            AddDialog(new TextPrompt("durationPrompt", ValidateDurationAsync));
            AddDialog(new ChoicePrompt("slotSelectionPrompt"));
            AddDialog(new ConfirmPrompt(ConfirmPrompt));

            // The initial child Dialog to run.
            InitialDialogId = WaterfallDialog;
        }

        private async Task<DialogTurnResult> GreetingStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var greetingMessage = "👋 **Welcome to the Interview Scheduling Assistant!**\n\n" +
                                "I'll help you schedule interview meetings step by step.\n" +
                                "Let's get started by finding the best time slots for your interview.";

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(greetingMessage), cancellationToken);
            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> SchedulingTypeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var choices = new List<Choice>
            {
                new Choice { Value = "AI-Driven", Synonyms = new List<string> { "ai", "intelligent", "smart" } },
                new Choice { Value = "Basic", Synonyms = new List<string> { "basic", "simple", "standard" } }
            };

            var promptMessage = "🤖 **Choose your scheduling approach:**\n\n" +
                              "• **AI-Driven**: Uses Microsoft Graph's intelligent algorithms for optimal scheduling\n" +
                              "• **Basic**: Simple availability checking based on calendar conflicts\n\n" +
                              "Which approach would you prefer?";

            return await stepContext.PromptAsync(ChoicePrompt, new PromptOptions
            {
                Prompt = MessageFactory.Text(promptMessage),
                Choices = choices,
                Style = ListStyle.HeroCard
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> CollectAttendeesStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Store the selected scheduling type
            stepContext.Values["schedulingType"] = ((FoundChoice)stepContext.Result).Value;

            var promptMessage = "📧 **Please provide attendee email addresses**\n\n" +
                              "Enter the email addresses of all attendees (including yourself) separated by commas.\n\n" +
                              "**Example:** john@company.com, jane@company.com, candidate@email.com";

            return await stepContext.PromptAsync(TextPrompt, new PromptOptions
            {
                Prompt = MessageFactory.Text(promptMessage),
                RetryPrompt = MessageFactory.Text("❌ Please enter valid email addresses separated by commas. Example: john@company.com, jane@company.com")
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> CollectDurationStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Store the attendee emails
            stepContext.Values["attendeeEmails"] = stepContext.Result.ToString();

            var promptMessage = "⏱️ **How long should the interview be?**\n\n" +
                              "Please enter the duration in minutes.\n\n" +
                              "**Common durations:**\n" +
                              "• 30 minutes (initial screening)\n" +
                              "• 60 minutes (standard interview)\n" +
                              "• 90 minutes (technical/panel interview)\n" +
                              "• 120 minutes (extended interview)\n\n" +
                              "Enter duration in minutes:";

            return await stepContext.PromptAsync("durationPrompt", new PromptOptions
            {
                Prompt = MessageFactory.Text(promptMessage),
                RetryPrompt = MessageFactory.Text("❌ Please enter a valid duration between 15 and 480 minutes (15 minutes to 8 hours).")
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> FindAvailableSlotsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Store the duration
            if (stepContext.Result != null && int.TryParse(stepContext.Result.ToString(), out int parsedDuration))
            {
                stepContext.Values["duration"] = parsedDuration;
            }
            else
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("❌ Invalid duration provided."), 
                    cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }

            var schedulingType = stepContext.Values["schedulingType"]?.ToString() ?? "Basic";
            var attendeeEmailsStr = stepContext.Values["attendeeEmails"]?.ToString() ?? "";
            var duration = (int)stepContext.Values["duration"];

            // Parse attendee emails
            var attendeeEmails = attendeeEmailsStr.Split(',')
                .Select(email => email.Trim())
                .Where(email => !string.IsNullOrEmpty(email))
                .ToList();

            await stepContext.Context.SendActivityAsync(
                MessageFactory.Text($"🔍 **Searching for available time slots using {schedulingType} scheduling...**\n\nThis may take a moment..."), 
                cancellationToken);

            try
            {
                if (schedulingType == "AI-Driven")
                {
                    // Use Graph scheduling service
                    var graphRequest = CreateGraphSchedulingRequest(attendeeEmails, duration);
                    var graphResponse = await _graphSchedulingService.FindOptimalMeetingTimesAsync(graphRequest, stepContext.Context.Activity.From.Id);
                    
                    stepContext.Values["schedulingResponse"] = graphResponse;
                    stepContext.Values["responseType"] = "graph";
                }
                else
                {
                    // Use basic scheduling service
                    var availabilityRequest = CreateAvailabilityRequest(attendeeEmails, duration);
                    var schedulingResponse = await _schedulingService.FindAvailableTimeSlotsAsync(availabilityRequest, stepContext.Context.Activity.From.Id);
                    
                    stepContext.Values["schedulingResponse"] = schedulingResponse;
                    stepContext.Values["responseType"] = "basic";
                }
            }
            catch (Exception ex)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text($"❌ Error during scheduling: {ex.Message}"), 
                    cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }

            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> PresentSlotsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var responseType = stepContext.Values["responseType"]?.ToString() ?? "basic";
            var schedulingType = stepContext.Values["schedulingType"]?.ToString() ?? "Basic";

            if (responseType == "graph")
            {
                var graphResponse = (GraphSchedulingResponse)stepContext.Values["schedulingResponse"];
                
                if (graphResponse?.IsSuccess == true && graphResponse.HasSuggestions)
                {
                    var choices = new List<Choice>();
                    var messageText = $"✅ **Found {graphResponse.MeetingTimeSuggestions.Count} optimal time slots using AI scheduling!**\n\n";
                    
                    for (int i = 0; i < graphResponse.MeetingTimeSuggestions.Count; i++)
                    {
                        var suggestion = graphResponse.MeetingTimeSuggestions[i];
                        if (suggestion.MeetingTimeSlot?.Start?.DateTime != null && suggestion.MeetingTimeSlot?.End?.DateTime != null)
                        {
                            var startTime = DateTime.Parse(suggestion.MeetingTimeSlot.Start.DateTime);
                            var endTime = DateTime.Parse(suggestion.MeetingTimeSlot.End.DateTime);
                            
                            var choiceText = $"{startTime:MMM dd, yyyy} at {startTime:HH:mm}-{endTime:HH:mm} (Confidence: {suggestion.Confidence * 100:F0}%)";
                            choices.Add(new Choice { Value = (i + 1).ToString() });
                            
                            messageText += $"**{i + 1}.** {choiceText}\n   💡 {suggestion.SuggestionReason}\n\n";
                        }
                    }

                    if (choices.Count > 0)
                    {
                        messageText += "Please select the time slot that works best for you:";
                        choices.Add(new Choice { Value = "cancel" });

                        return await stepContext.PromptAsync("slotSelectionPrompt", new PromptOptions
                        {
                            Prompt = MessageFactory.Text(messageText),
                            Choices = choices,
                            Style = ListStyle.List
                        }, cancellationToken);
                    }
                }
                
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text($"❌ No available slots found: {graphResponse?.Message ?? "Unknown error"}"), 
                    cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            else
            {
                var schedulingResponse = (SchedulingResponse)stepContext.Values["schedulingResponse"];
                
                if (schedulingResponse?.IsSuccess == true && schedulingResponse.HasAvailableSlots)
                {
                    var choices = new List<Choice>();
                    var messageText = $"✅ **Found {schedulingResponse.AvailableSlots.Count} available time slots!**\n\n";
                    
                    for (int i = 0; i < Math.Min(schedulingResponse.AvailableSlots.Count, 10); i++)
                    {
                        var slot = schedulingResponse.AvailableSlots[i];
                        var choiceText = $"{slot.StartTime:MMM dd, yyyy} at {slot.StartTime:HH:mm}-{slot.EndTime:HH:mm}";
                        choices.Add(new Choice { Value = (i + 1).ToString() });
                        
                        messageText += $"**{i + 1}.** {choiceText}\n";
                    }

                    if (schedulingResponse.AvailableSlots.Count > 10)
                    {
                        messageText += $"\n... and {schedulingResponse.AvailableSlots.Count - 10} more slots available.\n";
                    }

                    messageText += "\nPlease select the time slot that works best for you:";
                    choices.Add(new Choice { Value = "cancel" });

                    return await stepContext.PromptAsync("slotSelectionPrompt", new PromptOptions
                    {
                        Prompt = MessageFactory.Text(messageText),
                        Choices = choices,
                        Style = ListStyle.List
                    }, cancellationToken);
                }
                else
                {
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text($"❌ No available slots found: {schedulingResponse?.Message ?? "Unknown error"}"), 
                        cancellationToken);
                    return await stepContext.EndDialogAsync(null, cancellationToken);
                }
            }
        }

        private async Task<DialogTurnResult> ConfirmSelectionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var selectedChoice = ((FoundChoice)stepContext.Result).Value;
            
            if (selectedChoice == "cancel")
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("❌ Scheduling cancelled. You can start over anytime by saying 'schedule interview'."), 
                    cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }

            stepContext.Values["selectedSlot"] = selectedChoice;
            
            var responseType = stepContext.Values["responseType"]?.ToString() ?? "basic";
            var slotIndex = int.Parse(selectedChoice) - 1;
            
            string confirmationMessage = "";
            
            if (responseType == "graph")
            {
                var graphResponse = (GraphSchedulingResponse)stepContext.Values["schedulingResponse"];
                if (graphResponse?.MeetingTimeSuggestions != null && slotIndex >= 0 && slotIndex < graphResponse.MeetingTimeSuggestions.Count)
                {
                    var selectedSuggestion = graphResponse.MeetingTimeSuggestions[slotIndex];
                    if (selectedSuggestion.MeetingTimeSlot?.Start?.DateTime != null && selectedSuggestion.MeetingTimeSlot?.End?.DateTime != null)
                    {
                        var startTime = DateTime.Parse(selectedSuggestion.MeetingTimeSlot.Start.DateTime);
                        var endTime = DateTime.Parse(selectedSuggestion.MeetingTimeSlot.End.DateTime);
                        
                        confirmationMessage = $"📅 **Confirm your selection:**\n\n" +
                                            $"**Date & Time:** {startTime:dddd, MMMM dd, yyyy} at {startTime:HH:mm}-{endTime:HH:mm}\n" +
                                            $"**Confidence:** {selectedSuggestion.Confidence * 100:F0}%\n" +
                                            $"**Reason:** {selectedSuggestion.SuggestionReason}\n" +
                                            $"**Attendees:** {string.Join(", ", stepContext.Values["attendeeEmails"]?.ToString()?.Split(',')?.Select(e => e.Trim()) ?? new string[0])}\n\n" +
                                            "Would you like to book this meeting?";
                    }
                }
            }
            else
            {
                var schedulingResponse = (SchedulingResponse)stepContext.Values["schedulingResponse"];
                if (schedulingResponse?.AvailableSlots != null && slotIndex >= 0 && slotIndex < schedulingResponse.AvailableSlots.Count)
                {
                    var selectedSlot = schedulingResponse.AvailableSlots[slotIndex];
                    
                    confirmationMessage = $"📅 **Confirm your selection:**\n\n" +
                                        $"**Date & Time:** {selectedSlot.StartTime:dddd, MMMM dd, yyyy} at {selectedSlot.StartTime:HH:mm}-{selectedSlot.EndTime:HH:mm}\n" +
                                        $"**Duration:** {selectedSlot.DurationMinutes} minutes\n" +
                                        $"**Attendees:** {string.Join(", ", stepContext.Values["attendeeEmails"]?.ToString()?.Split(',')?.Select(e => e.Trim()) ?? new string[0])}\n\n" +
                                        "Would you like to book this meeting?";
                }
            }

            if (string.IsNullOrEmpty(confirmationMessage))
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("❌ Error retrieving selected slot details."), 
                    cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }

            return await stepContext.PromptAsync(ConfirmPrompt, new PromptOptions
            {
                Prompt = MessageFactory.Text(confirmationMessage)
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var confirmed = (bool)stepContext.Result;
            
            if (!confirmed)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("❌ Meeting booking cancelled. You can start over anytime by saying 'schedule interview'."), 
                    cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }

            var responseType = stepContext.Values["responseType"]?.ToString() ?? "basic";
            var selectedSlot = stepContext.Values["selectedSlot"]?.ToString() ?? "1";
            var slotIndex = int.Parse(selectedSlot) - 1;

            await stepContext.Context.SendActivityAsync(
                MessageFactory.Text("📅 **Booking your meeting...** This may take a moment."), 
                cancellationToken);

            try
            {
                if (responseType == "graph")
                {
                    var graphResponse = (GraphSchedulingResponse)stepContext.Values["schedulingResponse"];
                    if (graphResponse?.MeetingTimeSuggestions != null && slotIndex >= 0 && slotIndex < graphResponse.MeetingTimeSuggestions.Count)
                    {
                        var selectedSuggestion = graphResponse.MeetingTimeSuggestions[slotIndex];
                        
                        var bookingRequest = new BookingRequest
                        {
                            SelectedSuggestion = selectedSuggestion,
                            AttendeeEmails = stepContext.Values["attendeeEmails"]?.ToString()?.Split(',')?.Select(e => e.Trim())?.ToList() ?? new List<string>(),
                            MeetingTitle = "Interview Meeting",
                            MeetingDescription = "Interview meeting scheduled via conversational bot"
                        };

                        var bookingResponse = await _graphSchedulingService.BookMeetingAsync(bookingRequest, stepContext.Context.Activity.From.Id);
                        
                        if (bookingResponse.IsSuccess)
                        {
                            if (selectedSuggestion.MeetingTimeSlot?.Start?.DateTime != null && selectedSuggestion.MeetingTimeSlot?.End?.DateTime != null)
                            {
                                var startTime = DateTime.Parse(selectedSuggestion.MeetingTimeSlot.Start.DateTime);
                                var endTime = DateTime.Parse(selectedSuggestion.MeetingTimeSlot.End.DateTime);
                                
                                var successMessage = $"✅ **Meeting booked successfully!**\n\n" +
                                                   $"**Meeting Details:**\n" +
                                                   $"• **Title:** {bookingRequest.MeetingTitle}\n" +
                                                   $"• **Date:** {startTime:dddd, MMMM dd, yyyy}\n" +
                                                   $"• **Time:** {startTime:HH:mm} - {endTime:HH:mm}\n" +
                                                   $"• **Attendees:** {string.Join(", ", bookingRequest.AttendeeEmails)}\n" +
                                                   $"• **Event ID:** {bookingResponse.EventId}\n\n" +
                                                   $"📧 **Calendar invites have been sent to all attendees.**\n" +
                                                   $"🔗 **Teams meeting link will be included in the calendar invite.**";

                                await stepContext.Context.SendActivityAsync(MessageFactory.Text(successMessage), cancellationToken);
                            }
                        }
                        else
                        {
                            await stepContext.Context.SendActivityAsync(
                                MessageFactory.Text($"❌ Failed to book meeting: {bookingResponse.Message}"), 
                                cancellationToken);
                        }
                    }
                }
                else
                {
                    // For basic scheduling, we'll just show confirmation since actual booking would require similar implementation
                    var schedulingResponse = (SchedulingResponse)stepContext.Values["schedulingResponse"];
                    if (schedulingResponse?.AvailableSlots != null && slotIndex >= 0 && slotIndex < schedulingResponse.AvailableSlots.Count)
                    {
                        var selectedTimeSlot = schedulingResponse.AvailableSlots[slotIndex];
                        
                        var successMessage = $"✅ **Meeting slot confirmed!**\n\n" +
                                           $"**Meeting Details:**\n" +
                                           $"• **Title:** Interview Meeting\n" +
                                           $"• **Date:** {selectedTimeSlot.StartTime:dddd, MMMM dd, yyyy}\n" +
                                           $"• **Time:** {selectedTimeSlot.StartTime:HH:mm} - {selectedTimeSlot.EndTime:HH:mm}\n" +
                                           $"• **Duration:** {selectedTimeSlot.DurationMinutes} minutes\n" +
                                           $"• **Attendees:** {string.Join(", ", stepContext.Values["attendeeEmails"]?.ToString()?.Split(',')?.Select(e => e.Trim()) ?? new string[0])}\n\n" +
                                           $"📧 **Next steps: Calendar invites would be sent to all attendees.**\n" +
                                           $"🔗 **Teams meeting link would be included in the calendar invite.**\n\n" +
                                           $"*Note: This is a demonstration. In production, the meeting would be created in your calendar.*";

                        await stepContext.Context.SendActivityAsync(MessageFactory.Text(successMessage), cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text($"❌ Error booking meeting: {ex.Message}"), 
                    cancellationToken);
            }

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }

        private GraphSchedulingRequest CreateGraphSchedulingRequest(List<string> attendeeEmails, int duration)
        {
            var request = new GraphSchedulingRequest
            {
                AttendeeEmails = attendeeEmails,
                StartDate = DateTime.Now.AddHours(1),
                EndDate = DateTime.Now.AddDays(_configuration.GetValue<int>("Scheduling:SearchDays", 14)),
                DurationMinutes = duration,
                WorkingHoursStart = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:StartTime"] ?? "09:00"),
                WorkingHoursEnd = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:EndTime"] ?? "17:00"),
                MaxSuggestions = 10
            };

            var workingDaysConfig = _configuration.GetSection("Scheduling:WorkingHours:WorkingDays").Get<string[]>();
            if (workingDaysConfig != null)
            {
                request.WorkingDays = workingDaysConfig
                    .Select(day => Enum.Parse<DayOfWeek>(day))
                    .ToList();
            }

            return request;
        }

        private AvailabilityRequest CreateAvailabilityRequest(List<string> attendeeEmails, int duration)
        {
            var request = new AvailabilityRequest
            {
                AttendeeEmails = attendeeEmails,
                StartDate = DateTime.Now.AddHours(1),
                EndDate = DateTime.Now.AddDays(_configuration.GetValue<int>("Scheduling:SearchDays", 14)),
                DurationMinutes = duration,
                WorkingHoursStart = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:StartTime"] ?? "09:00"),
                WorkingHoursEnd = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:EndTime"] ?? "17:00")
            };

            var workingDaysConfig = _configuration.GetSection("Scheduling:WorkingHours:WorkingDays").Get<string[]>();
            if (workingDaysConfig != null)
            {
                request.WorkingDays = workingDaysConfig
                    .Select(day => Enum.Parse<DayOfWeek>(day))
                    .ToList();
            }

            return request;
        }

        private Task<bool> ValidateEmailsAsync(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var value = promptContext.Recognized.Value;
            
            if (string.IsNullOrEmpty(value))
            {
                return Task.FromResult(false);
            }

            var emails = value.Split(',').Select(email => email.Trim()).ToList();
            var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            
            return Task.FromResult(emails.All(email => emailRegex.IsMatch(email)) && emails.Count >= 2);
        }

        private Task<bool> ValidateDurationAsync(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var value = promptContext.Recognized.Value;
            
            if (int.TryParse(value, out int duration))
            {
                return Task.FromResult(duration >= 15 && duration <= 480); // 15 minutes to 8 hours
            }
            
            return Task.FromResult(false);
        }
    }
}