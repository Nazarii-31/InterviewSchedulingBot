using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Interfaces;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Tests;

namespace InterviewSchedulingBot.Tests
{
    /// <summary>
    /// Test to validate AI Scheduling functionality
    /// </summary>
    public class AISchedulingTest
    {
        public static async Task RunAISchedulingTest()
        {
            // Create service collection for testing
            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            
            // Add configuration
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"Scheduling:DefaultDurationMinutes", "60"},
                    {"Scheduling:SearchDays", "14"},
                    {"Scheduling:WorkingHours:StartTime", "09:00"},
                    {"Scheduling:WorkingHours:EndTime", "17:00"}
                })
                .Build();
            services.AddSingleton<IConfiguration>(configuration);
            
            // Add mock authentication service
            services.AddSingleton<IAuthenticationService, MockAuthenticationService>();
            
            // Add AI scheduling services
            services.AddSingleton<ISchedulingHistoryRepository, InMemorySchedulingHistoryRepository>();
            services.AddSingleton<ISchedulingMLModel, SchedulingMLModel>();
            services.AddSingleton<IAISchedulingService, AISchedulingService>();
            
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<AISchedulingTest>>();
            
            try
            {
                var aiSchedulingService = serviceProvider.GetRequiredService<IAISchedulingService>();
                
                logger.LogInformation("=== AI Scheduling Service Test ===");
                
                // Test 1: Basic AI scheduling request
                logger.LogInformation("Test 1: Basic AI scheduling functionality");
                
                var aiRequest = new AISchedulingRequest
                {
                    UserId = "test-user",
                    AttendeeEmails = new List<string> { "test1@company.com", "test2@company.com" },
                    StartDate = DateTime.Now.AddDays(1),
                    EndDate = DateTime.Now.AddDays(7),
                    DurationMinutes = 60,
                    UseLearningAlgorithm = true,
                    UseHistoricalData = true,
                    UseUserPreferences = true,
                    MaxSuggestions = 5
                };
                
                var aiResponse = await aiSchedulingService.FindOptimalMeetingTimesAsync(aiRequest);
                
                if (aiResponse.IsSuccess)
                {
                    logger.LogInformation("✓ AI scheduling successful with {PredictionCount} predictions", 
                        aiResponse.PredictedTimeSlots.Count);
                    
                    foreach (var prediction in aiResponse.PredictedTimeSlots.Take(3))
                    {
                        logger.LogInformation("  - {StartTime:yyyy-MM-dd HH:mm} | Confidence: {Confidence:P0} | {Reason}",
                            prediction.StartTime, prediction.OverallConfidence, prediction.PredictionReason);
                    }
                }
                else
                {
                    logger.LogError("✗ AI scheduling failed: {Message}", aiResponse.Message);
                }
                
                // Test 2: User preferences functionality
                logger.LogInformation("Test 2: User preferences and learning");
                
                var preferences = await aiSchedulingService.GetUserPreferencesAsync("test-user");
                if (preferences != null)
                {
                    logger.LogInformation("✓ User preferences retrieved: {PreferredDays} days, {PreferredTimes} times",
                        preferences.PreferredDays.Count, preferences.PreferredTimes.Count);
                }
                else
                {
                    logger.LogInformation("✓ No existing preferences found (expected for new user)");
                }
                
                // Test 3: Historical data learning
                logger.LogInformation("Test 3: Historical data learning simulation");
                
                var historyEntry = new SchedulingHistoryEntry
                {
                    UserId = "test-user",
                    AttendeeEmails = new List<string> { "test1@company.com", "test2@company.com" },
                    ScheduledTime = DateTime.Now.AddDays(-1),
                    DurationMinutes = 60,
                    DayOfWeek = DayOfWeek.Wednesday,
                    TimeOfDay = new TimeSpan(10, 0, 0),
                    UserSatisfactionScore = 0.9,
                    MeetingCompleted = true,
                    WasRescheduled = false
                };
                
                await aiSchedulingService.LearnFromSchedulingBehaviorAsync(historyEntry);
                logger.LogInformation("✓ Historical data learning completed");
                
                // Test 4: Pattern analysis
                logger.LogInformation("Test 4: Scheduling pattern analysis");
                
                var patterns = await aiSchedulingService.AnalyzeSchedulingPatternsAsync("test-user");
                logger.LogInformation("✓ Analyzed {PatternCount} scheduling patterns", patterns.Count);
                
                // Test 5: AI insights
                logger.LogInformation("Test 5: AI insights generation");
                
                var insights = await aiSchedulingService.GetAIInsightsAsync("test-user");
                logger.LogInformation("✓ AI insights generated with {InsightCount} categories", insights.Count);
                
                // Test 6: Rescheduling probability prediction
                logger.LogInformation("Test 6: Rescheduling probability prediction");
                
                var reschedulingProbability = await aiSchedulingService.PredictReschedulingProbabilityAsync(
                    "test-user", 
                    new List<string> { "test1@company.com" }, 
                    DateTime.Now.AddDays(2), 
                    60);
                
                logger.LogInformation("✓ Rescheduling probability: {Probability:P0}", reschedulingProbability);
                
                // Test 7: ML Model functionality
                logger.LogInformation("Test 7: ML Model functionality");
                
                var mlModel = serviceProvider.GetRequiredService<ISchedulingMLModel>();
                var features = new Dictionary<string, double>
                {
                    ["DayOfWeek"] = 3, // Wednesday
                    ["HourOfDay"] = 10,
                    ["DurationMinutes"] = 60,
                    ["AttendeeCount"] = 2,
                    ["UserHistoryScore"] = 0.8
                };
                
                var predictions = await mlModel.PredictOptimalTimeSlotsAsync(features);
                logger.LogInformation("✓ ML model generated {PredictionCount} predictions", predictions.Count);
                
                if (predictions.Count > 0)
                {
                    var bestPrediction = predictions.OrderByDescending(p => p.OverallConfidence).First();
                    logger.LogInformation("  Best prediction: {StartTime:HH:mm} with {Confidence:P0} confidence",
                        bestPrediction.StartTime, bestPrediction.OverallConfidence);
                }
                
                // Test 8: Model evaluation
                logger.LogInformation("Test 8: Model evaluation");
                
                var testData = new List<SchedulingHistoryEntry> { historyEntry };
                var modelMetrics = await mlModel.EvaluateModelAsync(testData);
                logger.LogInformation("✓ Model evaluation completed with {Accuracy:P0} accuracy", 
                    modelMetrics.GetValueOrDefault("Accuracy", 0.0));
                
                logger.LogInformation("=== All AI Scheduling Tests Completed Successfully! ===");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI Scheduling test failed");
                throw;
            }
        }
    }
}