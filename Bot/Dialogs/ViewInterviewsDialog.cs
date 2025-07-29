using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using MediatR;
using InterviewBot.Bot.State;
using InterviewBot.Application.Interviews.Queries;

namespace InterviewBot.Bot.Dialogs
{
    public class ViewInterviewsDialog : ComponentDialog
    {
        private readonly BotStateAccessors _accessors;
        private readonly IMediator _mediator;
        private readonly ILogger<ViewInterviewsDialog> _logger;
        
        public ViewInterviewsDialog(
            BotStateAccessors accessors,
            IMediator mediator,
            ILogger<ViewInterviewsDialog> logger) 
            : base(nameof(ViewInterviewsDialog))
        {
            _accessors = accessors;
            _mediator = mediator;
            _logger = logger;
            
            // Add dialog prompts
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            
            // Add waterfall dialog
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                GetUserEmailStepAsync,
                ShowInterviewsStepAsync
            }));
            
            InitialDialogId = nameof(WaterfallDialog);
        }
        
        private async Task<DialogTurnResult> GetUserEmailStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userProfile = await _accessors.UserProfileAccessor.GetAsync(
                stepContext.Context, () => new UserProfile(), cancellationToken);
            
            if (!string.IsNullOrEmpty(userProfile.Email))
            {
                // User email is already known, skip to showing interviews
                return await stepContext.NextAsync(userProfile.Email, cancellationToken);
            }
            
            var promptOptions = new PromptOptions 
            { 
                Prompt = MessageFactory.Text("📧 Please enter your email address to view your interviews:"),
                RetryPrompt = MessageFactory.Text("Please provide a valid email address.")
            };
            
            return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
        }
        
        private async Task<DialogTurnResult> ShowInterviewsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var email = (string)stepContext.Result;
            
            // Update user profile
            var userProfile = await _accessors.UserProfileAccessor.GetAsync(
                stepContext.Context, () => new UserProfile(), cancellationToken);
            userProfile.Email = email;
            userProfile.LastActivity = DateTime.UtcNow;
            
            // Show typing indicator
            await stepContext.Context.SendActivityAsync(
                new Activity { Type = ActivityTypes.Typing }, 
                cancellationToken);
            
            try
            {
                // Query for upcoming interviews
                var query = new GetUpcomingInterviewsQuery
                {
                    ParticipantEmail = email,
                    From = DateTime.Today,
                    To = DateTime.Today.AddDays(30)
                };
                
                var interviews = await _mediator.Send(query, cancellationToken);
                
                if (!interviews.Any())
                {
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text($"📅 No upcoming interviews found for {email}.\n\n" +
                                          "Would you like to schedule a new interview?"), 
                        cancellationToken);
                }
                else
                {
                    var messageText = $"📅 **Upcoming Interviews for {email}**\n\n";
                    
                    foreach (var interview in interviews.Take(10)) // Limit to 10 interviews
                    {
                        var participantList = string.Join(", ", interview.Participants.Select(p => p.Email));
                        var statusIcon = interview.Status switch
                        {
                            "Planned" => "📝",
                            "Scheduled" => "✅",
                            "Completed" => "✔️",
                            "Cancelled" => "❌",
                            _ => "📅"
                        };
                        
                        messageText += $"{statusIcon} **{interview.Title}**\n" +
                                     $"   📅 {interview.StartTime:ddd, MMM dd, yyyy 'at' h:mm tt}\n" +
                                     $"   ⏱️ Duration: {interview.Duration.TotalMinutes} minutes\n" +
                                     $"   👥 Participants: {participantList}\n" +
                                     $"   📊 Status: {interview.Status}\n\n";
                    }
                    
                    if (interviews.Count > 10)
                    {
                        messageText += $"... and {interviews.Count - 10} more interviews.\n\n";
                    }
                    
                    messageText += "Need to schedule a new interview or make changes? Just let me know!";
                    
                    await stepContext.Context.SendActivityAsync(
                        MessageFactory.Text(messageText), 
                        cancellationToken);
                }
                
                _logger.LogInformation("Displayed {InterviewCount} interviews for user {Email}", 
                    interviews.Count, email);
                
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving interviews for user {Email}", email);
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("❌ An error occurred while retrieving your interviews. Please try again later."), 
                    cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }
    }
}