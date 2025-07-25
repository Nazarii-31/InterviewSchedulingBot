using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces.Business;
using InterviewSchedulingBot.Interfaces.Integration;

namespace InterviewSchedulingBot.Dialogs
{
    public class InterviewSchedulingDialog : ComponentDialog
    {
        private readonly ISchedulingBusinessService _schedulingBusinessService;
        private readonly ITeamsIntegrationService _teamsIntegrationService;
        private readonly IConfiguration _configuration;

        public InterviewSchedulingDialog(
            ISchedulingBusinessService schedulingBusinessService,
            ITeamsIntegrationService teamsIntegrationService,
            IConfiguration configuration) 
            : base(nameof(InterviewSchedulingDialog))
        {
            _schedulingBusinessService = schedulingBusinessService;
            _teamsIntegrationService = teamsIntegrationService;
            _configuration = configuration;

            // Simple dialog for demonstration
            AddDialog(new WaterfallDialog("mainWaterfallDialog", new WaterfallStep[]
            {
                GreetingStepAsync,
                FinalStepAsync
            }));

            AddDialog(new TextPrompt("textPrompt"));
            
            InitialDialogId = "mainWaterfallDialog";
        }

        private async Task<DialogTurnResult> GreetingStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var welcomeMessage = "🤖 **Interview Scheduling Dialog**\n\n" +
                "This is the dialog interface for the Interview Scheduling Bot. " +
                "For the best experience, please use the web interface or API endpoints.\n\n" +
                "Would you like to learn more about the available options?";

            return await stepContext.PromptAsync("textPrompt", 
                new PromptOptions { Prompt = MessageFactory.Text(welcomeMessage) }, 
                cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var finalMessage = "✅ **Thank you for using Interview Scheduling Bot!**\n\n" +
                "**Available Options:**\n" +
                "- 🌐 Web UI: Interactive scheduling interface\n" +
                "- 🔗 API: RESTful endpoints for integration\n" +
                "- 📖 Docs: Visit `/swagger` for documentation\n\n" +
                "The bot uses a modern layered architecture with business logic, integration services, and API layers.";

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(finalMessage), cancellationToken);
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}