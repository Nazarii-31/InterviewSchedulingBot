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
        private readonly ISchedulingService _schedulingService;
        private readonly IConfiguration _configuration;

        public InterviewBot(IGraphCalendarService calendarService, IAuthenticationService authService, ISchedulingService schedulingService, IConfiguration configuration)
        {
            _calendarService = calendarService;
            _authService = authService;
            _schedulingService = schedulingService;
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
            else if (userMessage.ToLower().Contains("find") && userMessage.ToLower().Contains("slots"))
            {
                await HandleFindSlotsRequestAsync(turnContext, cancellationToken);
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
            try
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
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"Sorry, I encountered an error setting up authentication. Please try again later. Error: {ex.Message}"), 
                    cancellationToken);
            }
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
                          "I can help you in two ways:\n" +
                          "1. **Find available slots** - Type 'find slots' and I'll help you find common availability for multiple attendees\n" +
                          "2. **Create interview directly** - Provide interview details and I'll create the calendar event\n\n" +
                          "For finding slots, I'll need:\n" +
                          "- Attendee email addresses\n" +
                          "- Interview duration\n" +
                          "- Date range to search\n\n" +
                          "Type 'find slots' to start the scheduling assistant!";

            await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
        }

        private async Task HandleFindSlotsRequestAsync(
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

            // For demo purposes, let's use some example values
            // In a real implementation, you would collect this information through a conversation flow
            var userMessage = turnContext.Activity.Text?.ToLower() ?? "";
            
            if (userMessage.Contains("example") || userMessage.Contains("demo"))
            {
                await HandleExampleSchedulingAsync(turnContext, cancellationToken);
                return;
            }

            var response = "📅 **Interview Scheduling Assistant**\n\n" +
                          "To find available time slots, please provide the following information:\n\n" +
                          "**Format:** attendee1@company.com, attendee2@company.com | duration:60 | days:7\n\n" +
                          "**Example:** \n" +
                          "`john@company.com, jane@company.com | duration:90 | days:14`\n\n" +
                          "Or type `example demo` to see a demonstration with mock data.\n\n" +
                          "**Parameters:**\n" +
                          "- **Attendees:** Comma-separated email addresses\n" +
                          "- **Duration:** Meeting duration in minutes (default: 60)\n" +
                          "- **Days:** Number of days to search ahead (default: 14)\n\n" +
                          "I'll search for available slots during business hours (9 AM - 5 PM, Monday-Friday).";

            await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
        }

        private async Task HandleExampleSchedulingAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;

            try
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("🔍 Searching for available time slots... This may take a moment."), 
                    cancellationToken);

                // Create a sample availability request
                var defaultDuration = _configuration.GetValue<int>("Scheduling:DefaultDurationMinutes", 60);
                var searchDays = _configuration.GetValue<int>("Scheduling:SearchDays", 14);

                var availabilityRequest = new AvailabilityRequest
                {
                    AttendeeEmails = new List<string> { "demo@example.com", "test@example.com" },
                    StartDate = DateTime.Now.AddHours(1), // Start from next hour
                    EndDate = DateTime.Now.AddDays(searchDays),
                    DurationMinutes = defaultDuration,
                    WorkingHoursStart = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:StartTime"] ?? "09:00"),
                    WorkingHoursEnd = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:EndTime"] ?? "17:00")
                };

                // Convert working days from config
                var workingDaysConfig = _configuration.GetSection("Scheduling:WorkingHours:WorkingDays").Get<string[]>();
                if (workingDaysConfig != null)
                {
                    availabilityRequest.WorkingDays = workingDaysConfig
                        .Select(day => Enum.Parse<DayOfWeek>(day))
                        .ToList();
                }

                var schedulingResponse = await _schedulingService.FindAvailableTimeSlotsAsync(availabilityRequest, userId);

                if (schedulingResponse.IsSuccess && schedulingResponse.HasAvailableSlots)
                {
                    var responseText = $"✅ **Found {schedulingResponse.AvailableSlots.Count} available time slots!**\n\n" +
                                     $"**Search criteria:**\n" +
                                     $"- Duration: {schedulingResponse.RequestedDurationMinutes} minutes\n" +
                                     $"- Attendees: {string.Join(", ", schedulingResponse.AttendeeEmails)}\n" +
                                     $"- Date range: {schedulingResponse.SearchStartDate:yyyy-MM-dd} to {schedulingResponse.SearchEndDate:yyyy-MM-dd}\n\n" +
                                     $"**Available slots:**\n{schedulingResponse.FormattedSlotsText}\n\n" +
                                     $"💡 *This was a demonstration with mock data. In a real scenario, I would check actual calendar availability.*";

                    await turnContext.SendActivityAsync(MessageFactory.Text(responseText), cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text($"❌ No available slots found: {schedulingResponse.Message}"), 
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"❌ Error during scheduling: {ex.Message}"), 
                    cancellationToken);
            }
        }

        private async Task HandleHelpCommandAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            var isAuthenticated = await _authService.IsUserAuthenticatedAsync(userId);

            var helpText = "**Interview Scheduling Bot Commands:**\n\n" +
                          "• **schedule** or **interview** - Learn about scheduling options\n" +
                          "• **find slots** - Find available time slots for multiple attendees\n" +
                          "• **help** - Show this help message\n";

            if (isAuthenticated)
            {
                helpText += "• **logout** or **signout** - Sign out from your account\n\n" +
                           "✅ You are currently signed in and can access calendar features.\n\n" +
                           "**Scheduling Features:**\n" +
                           "- Find common availability across multiple calendars\n" +
                           "- Respect business hours and working days\n" +
                           "- Create Teams meetings with calendar integration\n" +
                           "- Smart conflict detection and resolution";
            }
            else
            {
                helpText += "\n❌ You need to sign in first to use calendar features.";
            }

            helpText += "\n\n💡 **Quick Start:** Type 'find slots' and then 'example demo' to see the scheduling assistant in action!";

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
            try
            {
                // Handle the OAuth callback from Teams
                var tokenResponse = turnContext.Activity.Value as TokenResponse;
                
                if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.Token))
                {
                    var userId = turnContext.Activity.From.Id;
                    
                    if (string.IsNullOrEmpty(userId))
                    {
                        await turnContext.SendActivityAsync(
                            MessageFactory.Text("❌ Sign-in failed: Unable to identify user."), 
                            cancellationToken);
                        return;
                    }
                    
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
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"❌ Sign-in process failed: {ex.Message}"), 
                    cancellationToken);
            }
        }

        protected override async Task OnTokenResponseEventAsync(
            ITurnContext<IEventActivity> turnContext,
            CancellationToken cancellationToken)
        {
            try
            {
                // Handle token response from OAuth flow
                var tokenResponse = turnContext.Activity.Value as TokenResponse;
                
                if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.Token))
                {
                    var userId = turnContext.Activity.From.Id;
                    
                    if (string.IsNullOrEmpty(userId))
                    {
                        await turnContext.SendActivityAsync(
                            MessageFactory.Text("❌ Authentication failed: Unable to identify user."), 
                            cancellationToken);
                        return;
                    }
                    
                    // Store the token
                    await _authService.StoreTokenAsync(userId, tokenResponse.Token);

                    await turnContext.SendActivityAsync(
                        MessageFactory.Text("✅ Authentication successful! You can now schedule interviews."), 
                        cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text("❌ Authentication failed: No valid token received."), 
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"❌ Authentication process failed: {ex.Message}"), 
                    cancellationToken);
            }
        }
    }
}