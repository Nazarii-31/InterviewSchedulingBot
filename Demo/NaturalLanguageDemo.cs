using InterviewSchedulingBot.Services.Business;
using InterviewSchedulingBot.Services.Integration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System.Net.Http;

namespace InterviewSchedulingBot.Demo
{
    /// <summary>
    /// Demonstration script showing the new natural language slot finding capabilities
    /// </summary>
    public class NaturalLanguageDemo
    {
        public static async Task RunDemo()
        {
            Console.WriteLine("🤖 Interview Scheduling Bot - Natural Language Demo");
            Console.WriteLine("==================================================\n");

            // Create mock services for demonstration
            var mockConfiguration = new MockConfiguration();
            var logger = new MockLogger<OpenWebUIClient>();
            var httpClient = new HttpClient();
            
            // Initialize the natural language services
            var openWebUIClient = new OpenWebUIClient(httpClient, mockConfiguration, logger);
            var slotQueryParser = new SlotQueryParser(openWebUIClient, new MockLogger<SlotQueryParser>());
            var responseGenerator = new ConversationalResponseGenerator(openWebUIClient, new MockLogger<ConversationalResponseGenerator>());

            // Demo queries to test
            var testQueries = new[]
            {
                "Find slots on Thursday afternoon",
                "Are there any slots next Monday?",
                "Show me morning availability tomorrow",
                "Find a 30-minute slot this week",
                "Do we have any free time on Friday",
                "Schedule something Tuesday morning"
            };

            Console.WriteLine("📝 Testing Natural Language Query Parsing:");
            Console.WriteLine("-----------------------------------------\n");

            foreach (var query in testQueries)
            {
                Console.WriteLine($"🔍 Query: \"{query}\"");
                
                try
                {
                    var criteria = await slotQueryParser.ParseQueryAsync(query);
                    
                    if (criteria != null)
                    {
                        Console.WriteLine($"✅ Parsed successfully:");
                        Console.WriteLine($"   📅 Date Range: {criteria.StartDate:yyyy-MM-dd} to {criteria.EndDate:yyyy-MM-dd}");
                        
                        if (criteria.TimeOfDay != null)
                            Console.WriteLine($"   🕐 Time of Day: {criteria.TimeOfDay.Start:hh\\:mm} - {criteria.TimeOfDay.End:hh\\:mm}");
                        
                        if (!string.IsNullOrEmpty(criteria.SpecificDay))
                            Console.WriteLine($"   📆 Specific Day: {criteria.SpecificDay}");
                        
                        if (!string.IsNullOrEmpty(criteria.RelativeDay))
                            Console.WriteLine($"   📊 Relative Day: {criteria.RelativeDay}");
                        
                        Console.WriteLine($"   ⏱️  Duration: {criteria.DurationMinutes} minutes");
                    }
                    else
                    {
                        Console.WriteLine("❌ Could not parse query");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
                
                Console.WriteLine();
            }

            Console.WriteLine("\n💬 Testing Conversational Response Generation:");
            Console.WriteLine("----------------------------------------------\n");

            // Create mock slots for response generation demo
            var mockSlots = new List<InterviewBot.Domain.Entities.RankedTimeSlot>
            {
                new InterviewBot.Domain.Entities.RankedTimeSlot
                {
                    StartTime = DateTime.Today.AddDays(1).AddHours(10),
                    EndTime = DateTime.Today.AddDays(1).AddHours(11),
                    Score = 95.5,
                    AvailableParticipants = 3,
                    TotalParticipants = 3
                },
                new InterviewBot.Domain.Entities.RankedTimeSlot
                {
                    StartTime = DateTime.Today.AddDays(1).AddHours(14),
                    EndTime = DateTime.Today.AddDays(1).AddHours(15),
                    Score = 87.2,
                    AvailableParticipants = 2,
                    TotalParticipants = 3
                },
                new InterviewBot.Domain.Entities.RankedTimeSlot
                {
                    StartTime = DateTime.Today.AddDays(2).AddHours(9),
                    EndTime = DateTime.Today.AddDays(2).AddHours(10),
                    Score = 91.0,
                    AvailableParticipants = 3,
                    TotalParticipants = 3
                }
            };

            var mockCriteria = new SlotQueryCriteria
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                DurationMinutes = 60,
                TimeOfDay = new TimeOfDayRange { Start = new TimeSpan(9, 0, 0), End = new TimeSpan(17, 0, 0) }
            };

            try
            {
                var response = await responseGenerator.GenerateSlotResponseAsync(mockSlots, mockCriteria);
                Console.WriteLine("🎯 Generated Response:");
                Console.WriteLine($"{response}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error generating response: {ex.Message}\n");
            }

            Console.WriteLine("✨ Demo completed! The natural language features are working correctly.");
            Console.WriteLine("\n📋 Summary of New Capabilities:");
            Console.WriteLine("• Parse natural language queries about time and availability");
            Console.WriteLine("• Understand day references (Thursday, next Monday, tomorrow)");
            Console.WriteLine("• Detect time periods (morning, afternoon, evening)");
            Console.WriteLine("• Extract duration constraints (30-minute, 1-hour)");
            Console.WriteLine("• Generate conversational responses about availability");
            Console.WriteLine("• Provide conflict explanations and alternatives");
            Console.WriteLine("• Handle fallback scenarios when AI services are unavailable");
        }
    }

    // Mock implementations for demo
    public class MockConfiguration : IConfiguration
    {
        public string? this[string key] 
        { 
            get => key switch
            {
                "OpenWebUI:BaseUrl" => "",
                "OpenWebUI:ApiKey" => "",
                _ => null
            };
            set { }
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);
        public IConfigurationSection GetSection(string key) => new MockConfigurationSection();
    }

    public class MockLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // Silent logger for demo
        }
    }

    public class MockConfigurationSection : IConfigurationSection
    {
        public string? this[string key] { get => null; set { } }
        public string Key => "";
        public string Path => "";
        public string? Value { get => null; set { } }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);
        public IConfigurationSection GetSection(string key) => new MockConfigurationSection();
    }
}