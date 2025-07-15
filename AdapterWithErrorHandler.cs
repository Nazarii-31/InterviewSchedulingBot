using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Logging;

namespace InterviewSchedulingBot
{
    public class AdapterWithErrorHandler : CloudAdapter
    {
        public AdapterWithErrorHandler(
            BotFrameworkAuthentication botFrameworkAuthentication,
            ILogger<IBotFrameworkHttpAdapter> logger)
            : base(botFrameworkAuthentication, logger)
        {
            OnTurnError = async (turnContext, exception) =>
            {
                // Log any leaked exception from the application.
                logger.LogError(exception, "[OnTurnError] unhandled error : {ExceptionMessage}", exception.Message);

                // Send a message to the user
                var errorMessageText = "The bot encountered an error or bug.";
                var errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, Microsoft.Bot.Schema.InputHints.IgnoringInput);
                await turnContext.SendActivityAsync(errorMessage);

                errorMessageText = "To continue to run this bot, please fix the bot source code.";
                errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, Microsoft.Bot.Schema.InputHints.ExpectingInput);
                await turnContext.SendActivityAsync(errorMessage);
            };
        }
    }
}