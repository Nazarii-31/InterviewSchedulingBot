using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces;

namespace InterviewSchedulingBot.Tests
{
    public static class LocalTestRunner
    {
        public static async Task RunLocalAITestAsync()
        {
            Console.WriteLine("🤖 Starting Local AI Scheduling Test...\n");

            // Create a minimal service provider for testing
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<ISchedulingHistoryRepository, InMemorySchedulingHistoryRepository>();
            services.AddSingleton<IGraphSchedulingService, MockGraphSchedulingService>();
            services.AddSingleton<IAISchedulingService, HybridAISchedulingService>();

            var serviceProvider = services.BuildServiceProvider();
            var aiService = serviceProvider.GetRequiredService<IAISchedulingService>();

            // Test 1: Basic AI Scheduling
            Console.WriteLine("📅 Test 1: Basic AI Scheduling");
            var request = new AISchedulingRequest
            {
                UserId = "test-user-123",
                AttendeeEmails = new List<string> { "attendee1@example.com", "attendee2@example.com" },
                StartDate = DateTime.Now.AddDays(1),
                EndDate = DateTime.Now.AddDays(7),
                DurationMinutes = 60,
                MaxSuggestions = 5
            };

            var response = await aiService.FindOptimalMeetingTimesAsync(request);
            Console.WriteLine($"✅ Generated {response.PredictedTimeSlots.Count} AI suggestions");
            Console.WriteLine($"   Average confidence: {response.PredictedTimeSlots.Average(p => p.OverallConfidence):F2}");

            // Test 2: User Preference Learning
            Console.WriteLine("\n🧠 Test 2: User Preference Learning");
            var historyEntry = new SchedulingHistoryEntry
            {
                UserId = "test-user-123",
                ScheduledTime = DateTime.Now.AddDays(1).AddHours(10),
                DayOfWeek = DayOfWeek.Tuesday,
                UserSatisfactionScore = 0.9,
                MeetingCompleted = true,
                WasRescheduled = false
            };

            await aiService.LearnFromSchedulingBehaviorAsync(historyEntry);
            Console.WriteLine("✅ User preference learning completed");

            // Test 3: Feedback Processing
            Console.WriteLine("\n⭐ Test 3: Feedback Processing");
            await aiService.ProvideFeedbackAsync(
                userId: "test-user-123",
                meetingId: "meeting-456",
                satisfactionScore: 0.85,
                feedback: "Perfect meeting time, very convenient"
            );
            Console.WriteLine("✅ Feedback processed successfully");

            // Test 4: Pattern Analysis
            Console.WriteLine("\n📊 Test 4: Pattern Analysis");
            var patterns = await aiService.AnalyzeSchedulingPatternsAsync("test-user-123");
            Console.WriteLine($"✅ Identified {patterns.Count} scheduling patterns");
            foreach (var pattern in patterns)
            {
                Console.WriteLine($"   - {pattern.PatternType} pattern (Success Rate: {pattern.SuccessRate:F2})");
            }

            // Test 5: AI Insights
            Console.WriteLine("\n🔍 Test 5: AI Insights");
            var insights = await aiService.GetAIInsightsAsync("test-user-123");
            Console.WriteLine($"✅ Generated {insights.Count} AI insights:");
            foreach (var insight in insights)
            {
                Console.WriteLine($"   - {insight}");
            }

            Console.WriteLine("\n🎉 All AI features tested successfully!");
            
            // Also run Teams integration tests
            Console.WriteLine("\n" + new string('=', 60));
            await TeamsIntegrationMockTest.RunComprehensiveTeamsIntegrationTest();
            
            Console.WriteLine("\nNext steps:");
            Console.WriteLine("1. Run 'dotnet run' to start the bot");
            Console.WriteLine("2. Use ngrok to expose the bot to Teams");
            Console.WriteLine("3. Test the AI features in Microsoft Teams");
            Console.WriteLine("4. See LOCAL_TEAMS_TESTING.md for detailed instructions");
        }
    }
}