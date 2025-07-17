using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using InterviewSchedulingBot.Bots;
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddNewtonsoftJson();

// Create the Bot Framework Authentication to be used with the Bot Adapter.
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Create the Bot Adapter with error handling enabled.
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, InterviewSchedulingBot.AdapterWithErrorHandler>();

// Register configuration validation service
builder.Services.AddSingleton<ConfigurationValidationService>();

// Register authentication service
builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();

// Register the Graph Calendar Service
builder.Services.AddSingleton<IGraphCalendarService, GraphCalendarService>();

// Register the Scheduling Service
builder.Services.AddSingleton<ISchedulingService, SchedulingService>();

// Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
builder.Services.AddTransient<IBot, InterviewBot>();

var app = builder.Build();

// Validate configuration at startup
var configValidator = app.Services.GetRequiredService<ConfigurationValidationService>();
configValidator.LogConfigurationState();

if (!configValidator.ValidateAuthenticationConfiguration())
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("Authentication configuration is incomplete. The bot will start but authentication features may not work properly.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.Run();