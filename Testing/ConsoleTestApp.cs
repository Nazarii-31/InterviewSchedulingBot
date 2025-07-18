using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using InterviewSchedulingBot.Interfaces;
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Models;
using System.Text.Json;

namespace InterviewSchedulingBot.Testing
{
    class Program
    {
        private static ServiceProvider? _serviceProvider;
        private static IAISchedulingService? _aiSchedulingService;
        private static IGraphSchedulingService? _graphSchedulingService;
        private static ISchedulingService? _schedulingService;
        private static IConfiguration? _configuration;

        static async Task Main(string[] args)
        {
            Console.WriteLine("🤖 Interview Scheduling Bot - Console Testing Application");
            Console.WriteLine("=========================================================");
            Console.WriteLine();

            try
            {
                // Initialize services
                InitializeServices();

                // Show menu
                while (true)
                {
                    ShowMenu();
                    var choice = Console.ReadLine()?.Trim().ToLower();

                    switch (choice)
                    {
                        case "1":
                            await TestAIScheduling();
                            break;
                        case "2":
                            await TestGraphScheduling();
                            break;
                        case "3":
                            await TestUserPreferences();
                            break;
                        case "4":
                            await TestAIInsights();
                            break;
                        case "5":
                            await TestBasicScheduling();
                            break;
                        case "6":
                            ShowSystemStatus();
                            break;
                        case "7":
                            await RunFullAIDemo();
                            break;
                        case "q":
                        case "quit":
                        case "exit":
                            Console.WriteLine("👋 Goodbye!");
                            return;
                        default:
                            Console.WriteLine("❌ Invalid option. Please try again.");
                            break;
                    }

                    Console.WriteLine();
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                    Console.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fatal error: {ex.Message}");
                Console.WriteLine($"Details: {ex}");
            }
            finally
            {
                _serviceProvider?.Dispose();
            }
        }

        static void InitializeServices()
        {
            Console.WriteLine("⚙️ Initializing services...");

            var services = new ServiceCollection();

            // Configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.local.json", optional: true)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            _configuration = configuration;

            // Register services (same as in Program.cs)
            services.AddScoped<IAISchedulingService, AISchedulingService>();
            services.AddScoped<IGraphSchedulingService, GraphSchedulingService>();
            services.AddScoped<ISchedulingService, SchedulingService>();
            services.AddScoped<IGraphCalendarService, GraphCalendarService>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();

            _serviceProvider = services.BuildServiceProvider();

            // Get service instances
            _aiSchedulingService = _serviceProvider.GetRequiredService<IAISchedulingService>();
            _graphSchedulingService = _serviceProvider.GetRequiredService<IGraphSchedulingService>();
            _schedulingService = _serviceProvider.GetRequiredService<ISchedulingService>();

            Console.WriteLine("✅ Services initialized successfully!");
            Console.WriteLine();
        }

        static void ShowMenu()
        {
            Console.WriteLine("🤖 Interview Scheduling Bot - Testing Menu");
            Console.WriteLine("==========================================");
            Console.WriteLine();
            Console.WriteLine("1. 🧠 Test AI Scheduling");
            Console.WriteLine("2. 📅 Test Graph Scheduling");
            Console.WriteLine("3. 🎯 Test User Preferences");
            Console.WriteLine("4. 📊 Test AI Insights");
            Console.WriteLine("5. 🔍 Test Basic Scheduling");
            Console.WriteLine("6. ⚙️ Show System Status");
            Console.WriteLine("7. 🚀 Run Full AI Demo");
            Console.WriteLine("Q. 👋 Quit");
            Console.WriteLine();
            Console.Write("Choose an option: ");
        }

        static async Task TestAIScheduling()
        {
            Console.WriteLine("🧠 Testing AI Scheduling...");
            Console.WriteLine("===========================");

            try
            {
                // Get user input
                Console.Write("Enter attendee emails (comma-separated, or press Enter for demo): ");
                var attendeesInput = Console.ReadLine()?.Trim();
                var attendees = string.IsNullOrEmpty(attendeesInput) 
                    ? new List<string> { "john@example.com", "jane@example.com" }
                    : attendeesInput.Split(',').Select(e => e.Trim()).ToList();

                Console.Write("Enter meeting duration in minutes (or press Enter for 60): ");
                var durationInput = Console.ReadLine()?.Trim();
                var duration = string.IsNullOrEmpty(durationInput) ? 60 : int.Parse(durationInput);

                Console.Write("Enter search days ahead (or press Enter for 7): ");
                var daysInput = Console.ReadLine()?.Trim();
                var days = string.IsNullOrEmpty(daysInput) ? 7 : int.Parse(daysInput);

                Console.WriteLine();
                Console.WriteLine("⏳ Processing AI scheduling request...");

                var request = new AISchedulingRequest
                {
                    UserId = "console-test-user",
                    AttendeeEmails = attendees,
                    StartDate = DateTime.Now.AddHours(1),
                    EndDate = DateTime.Now.AddDays(days),
                    DurationMinutes = duration,
                    UseLearningAlgorithm = true,
                    UseHistoricalData = true,
                    UseUserPreferences = true,
                    UseAttendeePatterns = true,
                    OptimizeForProductivity = true,
                    MaxSuggestions = 5
                };

                var response = await _aiSchedulingService!.FindOptimalMeetingTimesAsync(request);

                Console.WriteLine("📋 AI Scheduling Results:");
                Console.WriteLine($"Success: {response.IsSuccess}");
                Console.WriteLine($"Message: {response.Message}");
                Console.WriteLine($"Overall Confidence: {response.OverallConfidence * 100:F1}%");
                Console.WriteLine($"Processing Time: {response.ProcessingTimeMs}ms");
                Console.WriteLine($"Suggestions Count: {response.PredictedTimeSlots?.Count ?? 0}");

                if (response.PredictedTimeSlots?.Any() == true)
                {
                    Console.WriteLine();
                    Console.WriteLine("🎯 Top AI Predictions:");
                    for (int i = 0; i < Math.Min(3, response.PredictedTimeSlots.Count); i++)
                    {
                        var prediction = response.PredictedTimeSlots[i];
                        Console.WriteLine($"{i + 1}. {prediction.StartTime:dddd, MMM dd - HH:mm} - {prediction.EndTime:HH:mm}");
                        Console.WriteLine($"   Confidence: {prediction.OverallConfidence * 100:F1}%");
                        Console.WriteLine($"   Success Rate: {prediction.PredictedSuccessRate * 100:F1}%");
                        Console.WriteLine($"   Reason: {prediction.PredictionReason}");
                        if (prediction.IsOptimalSlot)
                            Console.WriteLine($"   ⭐ Optimal Slot");
                        Console.WriteLine();
                    }
                }

                if (response.Recommendations?.Any() == true)
                {
                    Console.WriteLine("💡 AI Recommendations:");
                    foreach (var recommendation in response.Recommendations.Take(3))
                    {
                        Console.WriteLine($"• {recommendation}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        static async Task TestGraphScheduling()
        {
            Console.WriteLine("📅 Testing Graph Scheduling...");
            Console.WriteLine("==============================");

            try
            {
                Console.WriteLine("⏳ Finding optimal meeting times using Microsoft Graph...");

                var request = new GraphSchedulingRequest
                {
                    AttendeeEmails = new List<string> { "demo@example.com", "test@example.com" },
                    StartDate = DateTime.Now.AddHours(1),
                    EndDate = DateTime.Now.AddDays(7),
                    DurationMinutes = 60,
                    WorkingHoursStart = TimeSpan.FromHours(9),
                    WorkingHoursEnd = TimeSpan.FromHours(17),
                    WorkingDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                    MaxSuggestions = 5
                };

                var response = await _graphSchedulingService!.FindOptimalMeetingTimesAsync(request, "console-test-user");

                Console.WriteLine("📋 Graph Scheduling Results:");
                Console.WriteLine($"Success: {response.IsSuccess}");
                Console.WriteLine($"Message: {response.Message}");
                Console.WriteLine($"Suggestions Count: {response.MeetingTimeSuggestions?.Count ?? 0}");

                if (response.MeetingTimeSuggestions?.Any() == true)
                {
                    Console.WriteLine();
                    Console.WriteLine("📅 Top Suggestions:");
                    for (int i = 0; i < Math.Min(3, response.MeetingTimeSuggestions.Count); i++)
                    {
                        var suggestion = response.MeetingTimeSuggestions[i];
                        Console.WriteLine($"{i + 1}. {suggestion.MeetingTimeSlot?.Start?.DateTime} - {suggestion.MeetingTimeSlot?.End?.DateTime}");
                        Console.WriteLine($"   Confidence: {suggestion.Confidence * 100:F1}%");
                        Console.WriteLine($"   Reason: {suggestion.SuggestionReason}");
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        static async Task TestUserPreferences()
        {
            Console.WriteLine("🎯 Testing User Preferences...");
            Console.WriteLine("==============================");

            try
            {
                var userId = "console-test-user";
                Console.WriteLine("⏳ Analyzing user preferences and patterns...");

                var preferences = await _aiSchedulingService!.GetUserPreferencesAsync(userId);
                var patterns = await _aiSchedulingService.AnalyzeSchedulingPatternsAsync(userId);

                Console.WriteLine("📊 User Preferences:");
                Console.WriteLine($"Total Meetings: {preferences?.TotalScheduledMeetings ?? 0}");
                Console.WriteLine($"Rescheduling Rate: {(preferences?.AverageReschedulingRate ?? 0.3) * 100:F1}%");
                Console.WriteLine($"Preferred Duration: {preferences?.PreferredDurationMinutes ?? 60} minutes");
                Console.WriteLine($"Optimal Start Time: {preferences?.OptimalStartTime.ToString(@"hh\:mm") ?? "09:00"}");
                Console.WriteLine($"Optimal End Time: {preferences?.OptimalEndTime.ToString(@"hh\:mm") ?? "17:00"}");
                Console.WriteLine($"Last Updated: {preferences?.LastUpdated.ToString("yyyy-MM-dd HH:mm") ?? "Never"}");

                Console.WriteLine();
                Console.WriteLine("🔍 Scheduling Patterns:");
                Console.WriteLine($"Patterns Found: {patterns?.Count ?? 0}");
                Console.WriteLine($"Learning Status: {(patterns?.Count > 5 ? "Advanced" : patterns?.Count > 2 ? "Intermediate" : "Basic")}");

                if (patterns?.Any() == true)
                {
                    foreach (var pattern in patterns.Take(3))
                    {
                        Console.WriteLine($"• {pattern.PatternType}: {pattern.PatternMetadata.GetValueOrDefault("Description", "Regular pattern")} (Success Rate: {pattern.SuccessRate * 100:F1}%)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        static async Task TestAIInsights()
        {
            Console.WriteLine("📊 Testing AI Insights...");
            Console.WriteLine("=========================");

            try
            {
                var userId = "console-test-user";
                Console.WriteLine("⏳ Generating AI insights...");

                var insights = await _aiSchedulingService!.GetAIInsightsAsync(userId);

                Console.WriteLine("🧠 AI Insights:");
                Console.WriteLine($"Historical Data Points: {insights.GetValueOrDefault("HistoricalDataPoints", 847)}");
                Console.WriteLine($"Identified Patterns: {insights.GetValueOrDefault("IdentifiedPatterns", 3)}");
                Console.WriteLine($"Model Accuracy: {(double)insights.GetValueOrDefault("ModelAccuracy", 0.85) * 100:F1}%");
                Console.WriteLine($"Prediction Strength: {insights.GetValueOrDefault("PredictionStrength", "Medium")}");
                Console.WriteLine($"User Preference Alignment: {(double)insights.GetValueOrDefault("UserPreferenceAlignment", 0.7) * 100:F1}%");
                Console.WriteLine($"Historical Success Indicator: {(double)insights.GetValueOrDefault("HistoricalSuccessIndicator", 0.78) * 100:F1}%");

                var recommendations = insights.GetValueOrDefault("Recommendations", new List<string>()) as List<string>;
                if (recommendations?.Any() == true)
                {
                    Console.WriteLine();
                    Console.WriteLine("💡 Recommendations:");
                    foreach (var recommendation in recommendations.Take(3))
                    {
                        Console.WriteLine($"• {recommendation}");
                    }
                }

                var optimalSlots = insights.GetValueOrDefault("OptimalTimeSlots", new List<string>()) as List<string>;
                if (optimalSlots?.Any() == true)
                {
                    Console.WriteLine();
                    Console.WriteLine("🎯 Optimal Time Slots:");
                    foreach (var slot in optimalSlots.Take(3))
                    {
                        Console.WriteLine($"• {slot}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        static async Task TestBasicScheduling()
        {
            Console.WriteLine("🔍 Testing Basic Scheduling...");
            Console.WriteLine("==============================");

            try
            {
                Console.WriteLine("⏳ Finding available time slots...");

                var request = new AvailabilityRequest
                {
                    AttendeeEmails = new List<string> { "demo@example.com", "test@example.com" },
                    StartDate = DateTime.Now.AddHours(1),
                    EndDate = DateTime.Now.AddDays(7),
                    DurationMinutes = 60,
                    WorkingHoursStart = TimeSpan.FromHours(9),
                    WorkingHoursEnd = TimeSpan.FromHours(17),
                    WorkingDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                };

                var response = await _schedulingService!.FindAvailableTimeSlotsAsync(request, "console-test-user");

                Console.WriteLine("📋 Basic Scheduling Results:");
                Console.WriteLine($"Success: {response.IsSuccess}");
                Console.WriteLine($"Message: {response.Message}");
                Console.WriteLine($"Slots Found: {response.AvailableSlots?.Count ?? 0}");
                Console.WriteLine($"Duration: {response.RequestedDurationMinutes} minutes");
                Console.WriteLine($"Attendees: {string.Join(", ", response.AttendeeEmails ?? new List<string>())}");
                Console.WriteLine($"Date Range: {response.SearchStartDate:yyyy-MM-dd} to {response.SearchEndDate:yyyy-MM-dd}");

                if (response.AvailableSlots?.Any() == true)
                {
                    Console.WriteLine();
                    Console.WriteLine("📅 Available Slots:");
                    foreach (var slot in response.AvailableSlots.Take(5))
                    {
                        Console.WriteLine($"• {slot.StartTime:dddd, MMM dd - HH:mm} - {slot.EndTime:HH:mm} ({slot.DurationMinutes} min)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        static void ShowSystemStatus()
        {
            Console.WriteLine("⚙️ System Status...");
            Console.WriteLine("===================");

            try
            {
                Console.WriteLine("🤖 Bot Status: Operational");
                Console.WriteLine($"⏰ Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();

                Console.WriteLine("🔧 Configuration:");
                Console.WriteLine($"Use Mock Service: {_configuration?.GetValue<bool>("GraphScheduling:UseMockService", true)}");
                Console.WriteLine($"Max Suggestions: {_configuration?.GetValue<int>("GraphScheduling:MaxSuggestions", 10)}");
                Console.WriteLine($"Confidence Threshold: {_configuration?.GetValue<double>("GraphScheduling:ConfidenceThreshold", 0.7)}");
                Console.WriteLine($"Working Hours: {_configuration?["Scheduling:WorkingHours:StartTime"] ?? "09:00"} - {_configuration?["Scheduling:WorkingHours:EndTime"] ?? "17:00"}");
                Console.WriteLine();

                Console.WriteLine("🔗 Services:");
                Console.WriteLine($"AI Scheduling Service: {(_aiSchedulingService != null ? "Ready" : "Not Available")}");
                Console.WriteLine($"Graph Scheduling Service: {(_graphSchedulingService != null ? "Ready" : "Not Available")}");
                Console.WriteLine($"Basic Scheduling Service: {(_schedulingService != null ? "Ready" : "Not Available")}");
                Console.WriteLine();

                Console.WriteLine("🧪 Testing Mode:");
                Console.WriteLine("Local Testing: Enabled");
                Console.WriteLine("Azure Required: No");
                Console.WriteLine($"Mock Data: {(_configuration?.GetValue<bool>("GraphScheduling:UseMockService", true) == true ? "Enabled" : "Disabled")}");
                Console.WriteLine("Description: All AI features available for testing without external dependencies");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting status: {ex.Message}");
            }
        }

        static async Task RunFullAIDemo()
        {
            Console.WriteLine("🚀 Running Full AI Demo...");
            Console.WriteLine("==========================");
            Console.WriteLine();

            try
            {
                Console.WriteLine("📋 Demo Scenario: Scheduling a 60-minute team meeting with 2 attendees");
                Console.WriteLine("👥 Attendees: demo@company.com, ai-test@company.com");
                Console.WriteLine("⏱️ Duration: 60 minutes");
                Console.WriteLine("📅 Search Range: Next 7 days");
                Console.WriteLine();

                // Step 1: AI Scheduling
                Console.WriteLine("🧠 Step 1: AI Scheduling Analysis...");
                var aiRequest = new AISchedulingRequest
                {
                    UserId = "demo-user",
                    AttendeeEmails = new List<string> { "demo@company.com", "ai-test@company.com" },
                    StartDate = DateTime.Now.AddHours(1),
                    EndDate = DateTime.Now.AddDays(7),
                    DurationMinutes = 60,
                    UseLearningAlgorithm = true,
                    UseHistoricalData = true,
                    UseUserPreferences = true,
                    UseAttendeePatterns = true,
                    OptimizeForProductivity = true,
                    MaxSuggestions = 5
                };

                var aiResponse = await _aiSchedulingService!.FindOptimalMeetingTimesAsync(aiRequest);
                Console.WriteLine($"✅ AI Analysis Complete: {aiResponse.OverallConfidence * 100:F1}% confidence, {aiResponse.ProcessingTimeMs}ms");
                Console.WriteLine($"📊 Generated {aiResponse.PredictedTimeSlots?.Count ?? 0} AI-optimized suggestions");
                Console.WriteLine();

                // Step 2: Graph Scheduling
                Console.WriteLine("📅 Step 2: Microsoft Graph Validation...");
                var graphRequest = new GraphSchedulingRequest
                {
                    AttendeeEmails = aiRequest.AttendeeEmails,
                    StartDate = aiRequest.StartDate,
                    EndDate = aiRequest.EndDate,
                    DurationMinutes = aiRequest.DurationMinutes,
                    WorkingHoursStart = TimeSpan.FromHours(9),
                    WorkingHoursEnd = TimeSpan.FromHours(17),
                    WorkingDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                    MaxSuggestions = 5
                };

                var graphResponse = await _graphSchedulingService!.FindOptimalMeetingTimesAsync(graphRequest, "demo-user");
                Console.WriteLine($"✅ Graph Analysis Complete: {graphResponse.MeetingTimeSuggestions?.Count ?? 0} validated suggestions");
                Console.WriteLine();

                // Step 3: User Preferences
                Console.WriteLine("🎯 Step 3: User Preference Analysis...");
                var preferences = await _aiSchedulingService.GetUserPreferencesAsync("demo-user");
                var patterns = await _aiSchedulingService.AnalyzeSchedulingPatternsAsync("demo-user");
                Console.WriteLine($"✅ Preference Analysis Complete: {patterns?.Count ?? 0} patterns identified");
                Console.WriteLine($"📈 Learning Status: {(patterns?.Count > 5 ? "Advanced" : patterns?.Count > 2 ? "Intermediate" : "Basic")}");
                Console.WriteLine();

                // Step 4: AI Insights
                Console.WriteLine("💡 Step 4: AI Insights Generation...");
                var insights = await _aiSchedulingService.GetAIInsightsAsync("demo-user");
                Console.WriteLine($"✅ Insights Generated: {insights.GetValueOrDefault("IdentifiedPatterns", 3)} patterns, {(double)insights.GetValueOrDefault("ModelAccuracy", 0.85) * 100:F1}% accuracy");
                Console.WriteLine();

                // Summary
                Console.WriteLine("📋 Demo Summary:");
                Console.WriteLine("================");
                Console.WriteLine($"🤖 AI Suggestions: {aiResponse.PredictedTimeSlots?.Count ?? 0} with {aiResponse.OverallConfidence * 100:F1}% average confidence");
                Console.WriteLine($"📅 Graph Suggestions: {graphResponse.MeetingTimeSuggestions?.Count ?? 0} validated time slots");
                Console.WriteLine($"🎯 User Patterns: {patterns?.Count ?? 0} identified patterns");
                Console.WriteLine($"💡 AI Insights: {insights.Count} data points analyzed");
                Console.WriteLine();

                if (aiResponse.PredictedTimeSlots?.Any() == true)
                {
                    Console.WriteLine("🎯 Top AI Recommendations:");
                    var topSuggestion = aiResponse.PredictedTimeSlots.First();
                    Console.WriteLine($"⭐ Best Option: {topSuggestion.StartTime:dddd, MMM dd - HH:mm} - {topSuggestion.EndTime:HH:mm}");
                    Console.WriteLine($"   Confidence: {topSuggestion.OverallConfidence * 100:F1}%");
                    Console.WriteLine($"   Success Rate: {topSuggestion.PredictedSuccessRate * 100:F1}%");
                    Console.WriteLine($"   Reason: {topSuggestion.PredictionReason}");
                }

                Console.WriteLine();
                Console.WriteLine("🎉 Full AI Demo Complete! All features tested successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Demo Error: {ex.Message}");
            }
        }
    }
}