using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using InterviewSchedulingBot.Bots;
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Interfaces;
using InterviewSchedulingBot.Interfaces.Integration;
using InterviewSchedulingBot.Interfaces.Business;
using InterviewSchedulingBot.Services.Integration;
using InterviewSchedulingBot.Services.Business;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddNewtonsoftJson();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Interview Scheduling Bot API", 
        Version = "v1",
        Description = "RESTful API for interview scheduling operations with clear separation between business and integration layers",
        Contact = new OpenApiContact
        {
            Name = "Interview Scheduling Bot",
            Email = "support@interviewbot.com"
        }
    });
    
    // Include XML comments for better documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Create the Bot Framework Authentication to be used with the Bot Adapter.
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Create the Bot Adapter with error handling enabled.
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, InterviewSchedulingBot.AdapterWithErrorHandler>();

// Create the storage we'll be using for user and conversation state.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Create the user state (used for dialog state).
builder.Services.AddSingleton<UserState>();

// Create the conversation state (used for dialog state).
builder.Services.AddSingleton<ConversationState>();

// Register configuration validation service
builder.Services.AddSingleton<ConfigurationValidationService>();

// Register authentication service
builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();

// Register the Graph Calendar Service
builder.Services.AddSingleton<IGraphCalendarService, GraphCalendarService>();

// Register the Core Scheduling Logic
builder.Services.AddSingleton<ICoreSchedulingLogic, CoreSchedulingLogic>();

// === INTEGRATION LAYER SERVICES ===
// Register Teams integration service (includes calendar access through Teams API)
// Use mock service if configured for testing, otherwise use real service
var useMockTeamsService = builder.Configuration.GetValue<bool>("TeamsIntegration:UseMockService", false);
if (useMockTeamsService)
{
    builder.Services.AddSingleton<ITeamsIntegrationService, InterviewSchedulingBot.Services.Mock.MockTeamsIntegrationService>();
    Console.WriteLine("✓ Using MockTeamsIntegrationService for testing (no Teams deployment required)");
}
else
{
    builder.Services.AddSingleton<ITeamsIntegrationService, TeamsIntegrationService>();
    Console.WriteLine("✓ Using TeamsIntegrationService (requires Teams deployment)");
}

// === BUSINESS LAYER SERVICES ===
// Register pure business logic service
builder.Services.AddSingleton<ISchedulingBusinessService, SchedulingBusinessService>();

// Register the AI Scheduling Services (Hybrid Approach) - Unified Service
builder.Services.AddSingleton<ISchedulingHistoryRepository, InMemorySchedulingHistoryRepository>();
builder.Services.AddSingleton<ISchedulingMLModel, SchedulingMLModel>();
var hybridSchedulingService = new ServiceDescriptor(typeof(HybridAISchedulingService), typeof(HybridAISchedulingService), ServiceLifetime.Singleton);
builder.Services.Add(hybridSchedulingService);

// Register the same instance for both interfaces (AI and Basic scheduling)
builder.Services.AddSingleton<IAISchedulingService>(provider => provider.GetRequiredService<HybridAISchedulingService>());
builder.Services.AddSingleton<ISchedulingService>(provider => provider.GetRequiredService<HybridAISchedulingService>());

// Register the Graph Scheduling Service (AI-driven scheduling)
// Use mock service if configured, otherwise use real service
var useMockGraphService = builder.Configuration.GetValue<bool>("GraphScheduling:UseMockService", false);
if (useMockGraphService)
{
    builder.Services.AddSingleton<IGraphSchedulingService, MockGraphSchedulingService>();
    Console.WriteLine("✓ Using MockGraphSchedulingService for development (no Azure credentials required)");
}
else
{
    builder.Services.AddSingleton<IGraphSchedulingService, GraphSchedulingService>();
    Console.WriteLine("✓ Using GraphSchedulingService (requires Azure credentials)");
}

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
else
{
    // Enable Swagger in development
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Interview Scheduling Bot API v1");
        c.RoutePrefix = "swagger"; // Make Swagger available at /swagger
        c.DocumentTitle = "Interview Scheduling Bot API Documentation";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// Log architectural information
var architectureLogger = app.Services.GetRequiredService<ILogger<Program>>();
architectureLogger.LogInformation("🏗️  Interview Scheduling Bot - Layered Architecture");
architectureLogger.LogInformation("📋 Integration Layer: Teams, Calendar, External AI services");
architectureLogger.LogInformation("💼 Business Layer: Pure scheduling logic and business rules");
architectureLogger.LogInformation("🌐 API Layer: RESTful endpoints with Swagger documentation");
architectureLogger.LogInformation("📚 Swagger Documentation available at: /swagger");

app.Run();