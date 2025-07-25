using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using InterviewSchedulingBot.Interfaces.Business;
using InterviewSchedulingBot.Interfaces.Integration;
using InterviewSchedulingBot.Interfaces;

namespace InterviewSchedulingBot.Bots
{
    public class InterviewBot : TeamsActivityHandler
    {
        private readonly IAuthenticationService _authService;
        private readonly ISchedulingBusinessService _schedulingBusinessService;
        private readonly ITeamsIntegrationService _teamsIntegrationService;
        private readonly IConfiguration _configuration;
        private readonly BotState _conversationState;
        private readonly BotState _userState;

        public InterviewBot(
            IAuthenticationService authService, 
            ISchedulingBusinessService schedulingBusinessService,
            ITeamsIntegrationService teamsIntegrationService,
            IConfiguration configuration,
            ConversationState conversationState,
            UserState userState)
        {
            _authService = authService;
            _schedulingBusinessService = schedulingBusinessService;
            _teamsIntegrationService = teamsIntegrationService;
            _configuration = configuration;
            _conversationState = conversationState;
            _userState = userState;
        }

        protected override async Task OnMembersAddedAsync(
            IList<ChannelAccount> membersAdded,
            ITurnContext<IConversationUpdateActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var welcomeText = "🤖 **Welcome to Interview Scheduling Bot!**\n\n" +
                "I can help you find optimal times for interviews using our layered architecture with:\n\n" +
                "✅ **Business Layer** - Pure scheduling logic\n" +
                "✅ **Integration Layer** - Teams and calendar integration\n" +
                "✅ **API Layer** - RESTful endpoints\n\n" +
                "**Get Started:**\n" +
                "- Use the web UI for interactive scheduling\n" +
                "- Call `/api/scheduling/find-optimal-slots` for programmatic access\n" +
                "- Visit `/swagger` for complete API documentation\n\n" +
                "Type 'help' for more options!";

            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText), cancellationToken);
                }
            }
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var text = turnContext.Activity.Text?.Trim().ToLowerInvariant() ?? "";

            switch (text)
            {
                case "help":
                    await HandleHelpAsync(turnContext, cancellationToken);
                    break;
                case "schedule":
                    await HandleScheduleAsync(turnContext, cancellationToken);
                    break;
                case "api":
                    await HandleApiInfoAsync(turnContext, cancellationToken);
                    break;
                default:
                    await HandleDefaultAsync(turnContext, cancellationToken);
                    break;
            }

            // Save conversation state
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        private async Task HandleHelpAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var helpText = "📚 **Interview Scheduling Bot - Help**\n\n" +
                "**Available Commands:**\n" +
                "- `schedule` - Get scheduling information\n" +
                "- `api` - Learn about API endpoints\n" +
                "- `help` - Show this help message\n\n" +
                "**Features:**\n" +
                "✅ Find optimal interview time slots\n" +
                "✅ Check participant availability\n" +
                "✅ Analyze scheduling conflicts\n" +
                "✅ Business rule validation\n\n" +
                "**Powered by:**\n" +
                "- Layered architecture design\n" +
                "- Microsoft Teams integration\n" +
                "- RESTful API with Swagger docs";

            await turnContext.SendActivityAsync(MessageFactory.Text(helpText), cancellationToken);
        }

        private async Task HandleScheduleAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var scheduleText = "📅 **Interview Scheduling**\n\n" +
                "To schedule an interview, please use one of these options:\n\n" +
                "**🌐 Web Interface** (Recommended)\n" +
                "- Visit the bot's home page for interactive scheduling\n" +
                "- Real-time mock data editing\n" +
                "- Visual time slot selection\n\n" +
                "**🔗 API Integration**\n" +
                "- `POST /api/scheduling/find-optimal-slots`\n" +
                "- `POST /api/scheduling/validate`\n" +
                "- `POST /api/scheduling/analyze-conflicts`\n\n" +
                "**📖 Documentation**\n" +
                "- Visit `/swagger` for interactive API docs\n" +
                "- Complete request/response examples\n" +
                "- Try out endpoints directly";

            await turnContext.SendActivityAsync(MessageFactory.Text(scheduleText), cancellationToken);
        }

        private async Task HandleApiInfoAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var apiText = "🔧 **API Information**\n\n" +
                "**Base URL:** Your bot's hosted URL\n\n" +
                "**Main Endpoints:**\n" +
                "- `POST /api/scheduling/find-optimal-slots`\n" +
                "  Find the best available time slots\n\n" +
                "- `POST /api/scheduling/validate`\n" +
                "  Validate scheduling requirements\n\n" +
                "- `POST /api/scheduling/analyze-conflicts`\n" +
                "  Analyze potential scheduling conflicts\n\n" +
                "**Authentication:** Bearer token required\n" +
                "**Format:** JSON requests and responses\n" +
                "**Documentation:** Available at `/swagger`\n\n" +
                "**Sample Request:**\n" +
                "```json\n" +
                "{\n" +
                "  \"participantEmails\": [\"user1@company.com\"],\n" +
                "  \"durationMinutes\": 60,\n" +
                "  \"earliestDate\": \"2025-01-28T09:00:00Z\",\n" +
                "  \"latestDate\": \"2025-02-04T17:00:00Z\"\n" +
                "}\n" +
                "```";

            await turnContext.SendActivityAsync(MessageFactory.Text(apiText), cancellationToken);
        }

        private async Task HandleDefaultAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var defaultText = "🤖 **I'm here to help with interview scheduling!**\n\n" +
                "I didn't understand that command. Here are some things you can try:\n\n" +
                "- Type `help` for available commands\n" +
                "- Type `schedule` for scheduling options\n" +
                "- Type `api` for API information\n\n" +
                "**Quick Access:**\n" +
                "- Web UI: Visit the bot's home page\n" +
                "- API Docs: Visit `/swagger`\n" +
                "- Live Demo: Use the interactive web interface";

            await turnContext.SendActivityAsync(MessageFactory.Text(defaultText), cancellationToken);
        }
    }
}