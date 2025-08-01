using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Interfaces;
using InterviewSchedulingBot.Interfaces.Integration;
using InterviewSchedulingBot.Interfaces.Business;
using InterviewSchedulingBot.Services.Integration;
using InterviewSchedulingBot.Services.Business;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

// Clean Architecture imports
using MediatR;
using InterviewBot.Domain.Interfaces;
using InterviewBot.Persistence;
using InterviewBot.Persistence.Repositories;
using InterviewBot.Infrastructure.Calendar;
using InterviewBot.Infrastructure.Scheduling;
using InterviewBot.Infrastructure.Caching;
using InterviewBot.Infrastructure.Telemetry;
using InterviewBot.Bot.State;
using InterviewBot.Bot;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddNewtonsoftJson();

// Add Memory Cache for caching
builder.Services.AddMemoryCache();

// === CLEAN ARCHITECTURE SETUP ===

// Add MediatR for CQRS
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Add Entity Framework with SQLite
builder.Services.AddDbContext<InterviewBotDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=InterviewBot.db";
    options.UseSqlite(connectionString);
});

// Register Unit of Work and Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IInterviewRepository, InterviewRepository>();
builder.Services.AddScoped<IParticipantRepository, ParticipantRepository>();
builder.Services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();

// Register Domain Services with caching
builder.Services.AddScoped<ICalendarService, GraphCalendarService>();
builder.Services.AddScoped<AvailabilityService>(); // Base service
builder.Services.AddScoped<IAvailabilityService>(provider =>
{
    var baseService = provider.GetRequiredService<AvailabilityService>();
    var cache = provider.GetRequiredService<IMemoryCache>();
    var logger = provider.GetRequiredService<ILogger<CachedAvailabilityService>>();
    return new CachedAvailabilityService(baseService, cache, logger);
});
builder.Services.AddScoped<ISchedulingService, SchedulingService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();

// Register Infrastructure Services
builder.Services.AddScoped<IGraphClientFactory, GraphClientFactory>();
builder.Services.AddScoped<OptimalSlotFinder>();

// Register Natural Language Processing Services
builder.Services.AddHttpClient<InterviewSchedulingBot.Services.Integration.IOpenWebUIClient, InterviewSchedulingBot.Services.Integration.OpenWebUIClient>();
builder.Services.AddHttpClient<InterviewSchedulingBot.Services.Integration.ICleanOpenWebUIClient, InterviewSchedulingBot.Services.Integration.CleanOpenWebUIClient>();
builder.Services.AddScoped<InterviewSchedulingBot.Services.Business.SlotQueryParser>();
builder.Services.AddScoped<InterviewSchedulingBot.Services.Business.ConversationalResponseGenerator>();
builder.Services.AddScoped<InterviewSchedulingBot.Services.Business.ISlotRecommendationService, InterviewSchedulingBot.Services.Business.SlotRecommendationService>();
builder.Services.AddScoped<InterviewSchedulingBot.Services.Business.IResponseFormatter, InterviewSchedulingBot.Services.Business.ResponseFormatter>();
builder.Services.AddScoped<InterviewSchedulingBot.Services.Business.IAIResponseService, InterviewSchedulingBot.Services.Business.AIResponseService>();

// Register Conversation Store
builder.Services.AddSingleton<InterviewSchedulingBot.Interfaces.IConversationStore, InterviewSchedulingBot.Services.InMemoryConversationStore>();

// Register ConversationStateManager
builder.Services.AddSingleton<InterviewSchedulingBot.Services.ConversationStateManager>();

// Register Bot State Accessors
builder.Services.AddSingleton<BotStateAccessors>();

// Register Clean Services
builder.Services.AddHttpClient<InterviewSchedulingBot.Services.Integration.ISimpleOpenWebUIParameterExtractor, InterviewSchedulingBot.Services.Integration.SimpleOpenWebUIParameterExtractor>();
builder.Services.AddScoped<InterviewSchedulingBot.Services.Business.ICleanMockDataGenerator, InterviewSchedulingBot.Services.Business.CleanMockDataGenerator>();

// Register new deterministic slot generation services
builder.Services.AddSingleton<InterviewBot.Services.DateRangeInterpreter>();
builder.Services.AddSingleton<InterviewBot.Services.DeterministicSlotRecommendationService>();
builder.Services.AddSingleton<InterviewBot.Services.TimeSlotResponseFormatter>();

// === EXISTING SERVICES ===

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

// Register authentication service
builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();

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

// Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
// Use the enhanced bot with Clean Architecture integration
builder.Services.AddTransient<IBot, InterviewSchedulingBotEnhanced>();

var app = builder.Build();

// === CONFIGURATION VERIFICATION ===
// Verify and log API configuration
var configuration = app.Services.GetRequiredService<IConfiguration>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

var apiKey = configuration["OpenWebUI:ApiKey"];
var useMockData = configuration.GetValue<bool>("OpenWebUI:UseMockData", false);

if (string.IsNullOrEmpty(apiKey))
{
    logger.LogWarning("OpenWebUI API key not found - using mock data for responses");
}
else if (useMockData)
{
    logger.LogWarning("OpenWebUI mock data enabled by configuration - API calls will be simulated");
}
else
{
    logger.LogInformation("OpenWebUI configured with API key - will make real API calls");
}

// === DATABASE INITIALIZATION ===
// Ensure database is created and apply migrations
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<InterviewBotDbContext>();
        dbContext.Database.EnsureCreated();
        app.Logger.LogInformation("✓ Database initialized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "❌ Failed to initialize database");
    }
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

// Set chat interface as default route instead of old dashboard
app.MapGet("/", context => {
    context.Response.Redirect("/api/chat");
    return Task.CompletedTask;
});

// Log architectural information
var architectureLogger = app.Services.GetRequiredService<ILogger<Program>>();
architectureLogger.LogInformation("🏗️  Interview Scheduling Bot - Layered Architecture");
architectureLogger.LogInformation("📋 Integration Layer: Teams, Calendar, External AI services");
architectureLogger.LogInformation("💼 Business Layer: Pure scheduling logic and business rules");
architectureLogger.LogInformation("🌐 API Layer: RESTful endpoints with Swagger documentation");
architectureLogger.LogInformation("📚 Swagger Documentation available at: /swagger");

app.Run();