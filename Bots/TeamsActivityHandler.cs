using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Services;
using System.Text.Json;

namespace InterviewSchedulingBot.Bots
{
    public class InterviewBot : TeamsActivityHandler
    {
        private readonly GraphCalendarService _calendarService;

        public InterviewBot(GraphCalendarService calendarService)
        {
            _calendarService = calendarService;
        }

        protected override async Task OnMembersAddedAsync(
            IList<ChannelAccount> membersAdded,
            ITurnContext<IConversationUpdateActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome! I'm your Interview Scheduling Bot. I can help you schedule interviews by managing calendar events. Send me a message to get started!";
            
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText), cancellationToken);
                }
            }
        }

        protected override async Task OnMessageActivityAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userMessage = turnContext.Activity.Text?.Trim();
            
            if (string.IsNullOrEmpty(userMessage))
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("Please provide a valid message."), 
                    cancellationToken);
                return;
            }

            // Handle different commands
            if (userMessage.ToLower().Contains("schedule") || userMessage.ToLower().Contains("interview"))
            {
                await HandleScheduleRequestAsync(turnContext, cancellationToken);
            }
            else if (userMessage.ToLower().Contains("help"))
            {
                await HandleHelpCommandAsync(turnContext, cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"I received your message: '{userMessage}'. Type 'help' to see available commands."), 
                    cancellationToken);
            }
        }

        private async Task HandleScheduleRequestAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var response = "To schedule an interview, I'll need the following information:\n" +
                          "- Interviewer email\n" +
                          "- Candidate email\n" +
                          "- Date and time\n" +
                          "- Duration\n" +
                          "- Interview title\n\n" +
                          "Please provide these details and I'll help you create the calendar event.";

            await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
        }

        private async Task HandleHelpCommandAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var helpText = "**Interview Scheduling Bot Commands:**\n\n" +
                          "• **schedule** or **interview** - Start the interview scheduling process\n" +
                          "• **help** - Show this help message\n\n" +
                          "I can help you create calendar events for interviews using Microsoft Graph integration.";

            await turnContext.SendActivityAsync(MessageFactory.Text(helpText), cancellationToken);
        }

        protected override async Task OnTeamsSigninVerifyStateAsync(
            ITurnContext<IInvokeActivity> turnContext,
            CancellationToken cancellationToken)
        {
            // Handle Teams sign-in verification
            await turnContext.SendActivityAsync(
                MessageFactory.Text("Sign-in successful! You can now use the bot to schedule interviews."), 
                cancellationToken);
        }
    }
}