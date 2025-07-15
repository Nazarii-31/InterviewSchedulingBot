using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Interfaces;
using System.Text.Json;
using Microsoft.Bot.Connector.Authentication;

namespace InterviewSchedulingBot.Bots
{
    public class InterviewBot : TeamsActivityHandler
    {
        private readonly IGraphCalendarService _calendarService;
        private readonly IAuthenticationService _authService;
        private readonly IConfiguration _configuration;

        public InterviewBot(IGraphCalendarService calendarService, IAuthenticationService authService, IConfiguration configuration)
        {
            _calendarService = calendarService;
            _authService = authService;
            _configuration = configuration;
        }

        protected override async Task OnMembersAddedAsync(
            IList<ChannelAccount> membersAdded,
            ITurnContext<IConversationUpdateActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome! I'm your Interview Scheduling Bot. I can help you schedule interviews by managing calendar events.\n\n" +
                             "To get started, I'll need you to sign in to access your calendar. Send me a message to begin!";
            
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
            var userId = turnContext.Activity.From.Id;
            var isAuthenticated = await _authService.IsUserAuthenticatedAsync(userId);

            if (!isAuthenticated)
            {
                await HandleAuthenticationAsync(turnContext, cancellationToken);
                return;
            }

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
            else if (userMessage.ToLower().Contains("logout") || userMessage.ToLower().Contains("signout"))
            {
                await HandleLogoutAsync(turnContext, cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"I received your message: '{userMessage}'. Type 'help' to see available commands."), 
                    cancellationToken);
            }
        }

        private async Task HandleAuthenticationAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            var conversationId = turnContext.Activity.Conversation.Id;
            
            // Create sign-in card
            var signInCard = new SigninCard
            {
                Text = _configuration["Authentication:OAuthPrompt:Text"] ?? "Please sign in to access your calendar",
                Buttons = new[]
                {
                    new CardAction
                    {
                        Title = _configuration["Authentication:OAuthPrompt:Title"] ?? "Sign In",
                        Type = ActionTypes.Signin,
                        Value = _authService.GetAuthorizationUrl(userId, conversationId)
                    }
                }
            };

            var attachment = new Attachment
            {
                ContentType = SigninCard.ContentType,
                Content = signInCard
            };

            var reply = MessageFactory.Attachment(attachment);
            reply.Text = "You need to sign in before I can help you with calendar operations.";

            await turnContext.SendActivityAsync(reply, cancellationToken);
        }

        private async Task HandleScheduleRequestAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            var accessToken = await _authService.GetAccessTokenAsync(userId);

            if (string.IsNullOrEmpty(accessToken))
            {
                await HandleAuthenticationAsync(turnContext, cancellationToken);
                return;
            }

            var response = "Great! You're authenticated and ready to schedule interviews.\n\n" +
                          "To schedule an interview, I'll need the following information:\n" +
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
            var userId = turnContext.Activity.From.Id;
            var isAuthenticated = await _authService.IsUserAuthenticatedAsync(userId);

            var helpText = "**Interview Scheduling Bot Commands:**\n\n" +
                          "• **schedule** or **interview** - Start the interview scheduling process\n" +
                          "• **help** - Show this help message\n";

            if (isAuthenticated)
            {
                helpText += "• **logout** or **signout** - Sign out from your account\n\n" +
                           "✅ You are currently signed in and can create calendar events.";
            }
            else
            {
                helpText += "\n❌ You need to sign in first to use calendar features.";
            }

            helpText += "\n\nI can help you create calendar events for interviews using Microsoft Graph integration.";

            await turnContext.SendActivityAsync(MessageFactory.Text(helpText), cancellationToken);
        }

        private async Task HandleLogoutAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            await _authService.ClearTokenAsync(userId);

            await turnContext.SendActivityAsync(
                MessageFactory.Text("You have been signed out successfully. Send me a message to sign in again."), 
                cancellationToken);
        }

        protected override async Task OnTeamsSigninVerifyStateAsync(
            ITurnContext<IInvokeActivity> turnContext,
            CancellationToken cancellationToken)
        {
            // Handle the OAuth callback from Teams
            var tokenResponse = turnContext.Activity.Value as TokenResponse;
            
            if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.Token))
            {
                var userId = turnContext.Activity.From.Id;
                
                // Store the token
                await _authService.StoreTokenAsync(userId, tokenResponse.Token);

                var response = MessageFactory.Text("✅ Sign-in successful! You can now use the bot to schedule interviews. Type 'help' to see available commands.");
                
                await turnContext.SendActivityAsync(response, cancellationToken);
            }
            else
            {
                var response = MessageFactory.Text("❌ Sign-in failed. Please try again.");
                
                await turnContext.SendActivityAsync(response, cancellationToken);
            }
        }

        protected override async Task OnTokenResponseEventAsync(
            ITurnContext<IEventActivity> turnContext,
            CancellationToken cancellationToken)
        {
            // Handle token response from OAuth flow
            var tokenResponse = turnContext.Activity.Value as TokenResponse;
            
            if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.Token))
            {
                var userId = turnContext.Activity.From.Id;
                
                // Store the token
                await _authService.StoreTokenAsync(userId, tokenResponse.Token);

                await turnContext.SendActivityAsync(
                    MessageFactory.Text("✅ Authentication successful! You can now schedule interviews."), 
                    cancellationToken);
            }
        }
    }
}