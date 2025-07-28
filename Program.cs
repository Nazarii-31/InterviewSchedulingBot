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

// Clean Architecture imports
using MediatR;
using InterviewBot.Domain.Interfaces;
using InterviewBot.Persistence;
using InterviewBot.Persistence.Repositories;
using InterviewBot.Infrastructure.Calendar;
using InterviewBot.Infrastructure.Scheduling;
using InterviewBot.Infrastructure.Telemetry;
using InterviewBot.Bot.State;
using InterviewBot.Bot;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddNewtonsoftJson();

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

// Register Domain Services
builder.Services.AddScoped<ICalendarService, GraphCalendarService>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<ISchedulingService, SchedulingService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();

// Register Infrastructure Services
builder.Services.AddScoped<IGraphClientFactory, GraphClientFactory>();
builder.Services.AddScoped<OptimalSlotFinder>();

// Register Bot State Accessors
builder.Services.AddSingleton<BotStateAccessors>();

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

// Log architectural information
var architectureLogger = app.Services.GetRequiredService<ILogger<Program>>();
architectureLogger.LogInformation("🏗️  Interview Scheduling Bot - Layered Architecture");
architectureLogger.LogInformation("📋 Integration Layer: Teams, Calendar, External AI services");
architectureLogger.LogInformation("💼 Business Layer: Pure scheduling logic and business rules");
architectureLogger.LogInformation("🌐 API Layer: RESTful endpoints with Swagger documentation");
architectureLogger.LogInformation("📚 Swagger Documentation available at: /swagger");

app.Run();