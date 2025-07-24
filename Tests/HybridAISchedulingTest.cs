using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace InterviewSchedulingBot.Tests
{
    public class MockAuthenticationService : IAuthenticationService
    {
        public Task<string?> GetAccessTokenAsync(string userId)
        {
            return Task.FromResult<string?>("mock-token");
        }
        
        public Task StoreTokenAsync(string userId, string accessToken, string? refreshToken = null, DateTimeOffset? expiresOn = null)
        {
            return Task.CompletedTask;
        }
        
        public Task<bool> IsUserAuthenticatedAsync(string userId)
        {
            return Task.FromResult(true);
        }
        
        public Task ClearTokenAsync(string userId)
        {
            return Task.CompletedTask;
        }
        
        public string GetAuthorizationUrl(string userId, string conversationId)
        {
            return "https://mock-auth-url.com";
        }
    }

    public static class HybridAISchedulingTest
    {
        public static async Task RunHybridAISchedulingTest()
        {
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<HybridAISchedulingService>.Instance;
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenAI:ApiKey"] = "",
                    ["OpenAI:DeploymentName"] = "gpt-3.5-turbo"
                })
                .Build();

            // Create mock services
            var mockAuthService = new MockAuthenticationService();
            var mockGraphSchedulingService = new MockGraphSchedulingService();
            var mockGraphCalendarService = new GraphCalendarService(config, mockAuthService);
            var mockHistoryRepository = new InMemorySchedulingHistoryRepository(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemorySchedulingHistoryRepository>.Instance);

            // Create hybrid AI scheduling service
            var hybridService = new HybridAISchedulingService(
                mockGraphSchedulingService,
                mockGraphCalendarService,
                mockHistoryRepository,
                mockAuthService,
                logger,
                config);

            // Test basic scheduling request
            var request = new AISchedulingRequest
            {
                UserId = "test-user",
                AttendeeEmails = new List<string> { "attendee1@test.com", "attendee2@test.com" },
                StartDate = DateTime.Now.AddDays(1),
                EndDate = DateTime.Now.AddDays(7),
                DurationMinutes = 60,
                MaxSuggestions = 5,
                MinimumConfidenceThreshold = 0.6
            };

            Console.WriteLine("🔄 Testing Hybrid AI Scheduling Service...");
            
            var response = await hybridService.FindOptimalMeetingTimesAsync(request);
            
            if (response.IsSuccess)
            {
                Console.WriteLine($"✅ Hybrid AI scheduling successful! Found {response.PredictedTimeSlots.Count} suggestions");
                Console.WriteLine($"📊 Average confidence: {response.PredictedTimeSlots.Average(p => p.OverallConfidence):F2}");
                Console.WriteLine($"🎯 Processing time: {response.ProcessingTimeMs}ms");
                Console.WriteLine($"🤖 Approach: {response.AIInsights?.GetValueOrDefault("ApproachType", "Unknown")}");
                
                // Test user preference learning
                var historyEntry = new SchedulingHistoryEntry
                {
                    UserId = "test-user",
                    AttendeeEmails = new List<string> { "attendee1@test.com" },
                    ScheduledTime = DateTime.Now.AddDays(2),
                    DurationMinutes = 60,
                    DayOfWeek = DateTime.Now.AddDays(2).DayOfWeek,
                    TimeOfDay = new TimeSpan(10, 0, 0),
                    UserSatisfactionScore = 0.85,
                    WasRescheduled = false,
                    MeetingCompleted = true
                };
                
                Console.WriteLine("🧠 Testing user preference learning...");
                await hybridService.LearnFromSchedulingBehaviorAsync(historyEntry);
                
                // Test feedback processing
                Console.WriteLine("💬 Testing feedback processing...");
                await hybridService.ProvideFeedbackAsync("test-user", "meeting-123", 0.9, "Great meeting time!");
                
                // Test pattern analysis
                Console.WriteLine("📈 Testing pattern analysis...");
                var patterns = await hybridService.AnalyzeSchedulingPatternsAsync("test-user");
                Console.WriteLine($"📊 Identified {patterns.Count} scheduling patterns");
                
                // Test AI insights
                Console.WriteLine("🔍 Testing AI insights...");
                var insights = await hybridService.GetAIInsightsAsync("test-user");
                Console.WriteLine($"💡 Generated {insights.Count} AI insights");
                
                Console.WriteLine("✅ All hybrid AI scheduling tests passed!");
            }
            else
            {
                Console.WriteLine($"❌ Hybrid AI scheduling failed: {response.Message}");
            }
        }
    }

    // Mock implementation for testing
    public class MockGraphSchedulingService : IGraphSchedulingService
    {
        public async Task<GraphSchedulingResponse> FindOptimalMeetingTimesAsync(GraphSchedulingRequest request, string userId)
        {
            await Task.Delay(100); // Simulate processing time
            
            var suggestions = new List<MeetingTimeSuggestion>();
            
            // Generate some mock suggestions
            for (int i = 0; i < Math.Min(request.MaxSuggestions, 5); i++)
            {
                var startTime = request.StartDate.AddDays(i).AddHours(10 + i);
                var endTime = startTime.AddMinutes(request.DurationMinutes);
                
                suggestions.Add(new MeetingTimeSuggestion
                {
                    MeetingTimeSlot = new MeetingTimeSlot
                    {
                        Start = new Microsoft.Graph.Models.DateTimeTimeZone
                        {
                            DateTime = startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                            TimeZone = "UTC"
                        },
                        End = new Microsoft.Graph.Models.DateTimeTimeZone
                        {
                            DateTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                            TimeZone = "UTC"
                        }
                    },
                    Confidence = 0.85 - (i * 0.1), // Decreasing confidence
                    SuggestionReason = $"Available time slot {i + 1} from Microsoft Graph (Mock)"
                });
            }
            
            return GraphSchedulingResponse.CreateSuccess(suggestions, request);
        }
    }
}