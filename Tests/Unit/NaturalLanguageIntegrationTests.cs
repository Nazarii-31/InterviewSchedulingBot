using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using InterviewSchedulingBot.Services.Business;
using InterviewSchedulingBot.Services.Integration;
using Moq;
using System.Net.Http;

namespace InterviewSchedulingBot.Tests.Unit
{
    public class NaturalLanguageIntegrationTests
    {
        [Fact]
        public async Task EndToEndNaturalLanguageTest_ShouldParseAndGenerateResponse()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<OpenWebUIClient>>();
            var mockConfiguration = new Mock<IConfiguration>();
            var httpClient = new HttpClient();
            
            // Setup configuration
            mockConfiguration.Setup(x => x["OpenWebUI:BaseUrl"]).Returns("");
            mockConfiguration.Setup(x => x["OpenWebUI:ApiKey"]).Returns("");
            
            // Create services
            var openWebUIClient = new OpenWebUIClient(httpClient, mockConfiguration.Object, mockLogger.Object);
            var slotQueryParser = new SlotQueryParser(openWebUIClient, new Mock<ILogger<SlotQueryParser>>().Object);
            var responseGenerator = new ConversationalResponseGenerator(openWebUIClient, new Mock<ILogger<ConversationalResponseGenerator>>().Object);

            // Test queries
            var testCases = new[]
            {
                new { Query = "Find slots on Thursday afternoon", ExpectedDay = "Thursday", ExpectedTime = "afternoon" },
                new { Query = "Are there any slots next Monday?", ExpectedDay = "Monday", ExpectedTime = (string?)null },
                new { Query = "Show me morning availability tomorrow", ExpectedDay = (string?)null, ExpectedTime = "morning" },
                new { Query = "Find a 30-minute slot this week", ExpectedDay = (string?)null, ExpectedTime = (string?)null }
            };

            foreach (var testCase in testCases)
            {
                // Act
                var criteria = await slotQueryParser.ParseQueryAsync(testCase.Query);

                // Assert
                Assert.NotNull(criteria);
                Assert.Equal(testCase.ExpectedDay, criteria.SpecificDay);
                
                if (testCase.ExpectedTime == "morning")
                {
                    Assert.NotNull(criteria.TimeOfDay);
                    Assert.Equal(new TimeSpan(9, 0, 0), criteria.TimeOfDay.Start);
                    Assert.Equal(new TimeSpan(12, 0, 0), criteria.TimeOfDay.End);
                }
                else if (testCase.ExpectedTime == "afternoon")
                {
                    Assert.NotNull(criteria.TimeOfDay);
                    Assert.Equal(new TimeSpan(12, 0, 0), criteria.TimeOfDay.Start);
                    Assert.Equal(new TimeSpan(17, 0, 0), criteria.TimeOfDay.End);
                }
            }
        }

        [Fact]
        public async Task ConversationalResponseGeneration_ShouldReturnHumanReadableText()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<OpenWebUIClient>>();
            var mockConfiguration = new Mock<IConfiguration>();
            var httpClient = new HttpClient();
            
            mockConfiguration.Setup(x => x["OpenWebUI:BaseUrl"]).Returns("");
            mockConfiguration.Setup(x => x["OpenWebUI:ApiKey"]).Returns("");
            
            var openWebUIClient = new OpenWebUIClient(httpClient, mockConfiguration.Object, mockLogger.Object);
            var responseGenerator = new ConversationalResponseGenerator(openWebUIClient, new Mock<ILogger<ConversationalResponseGenerator>>().Object);

            // Create test data
            var slots = new List<InterviewBot.Domain.Entities.RankedTimeSlot>
            {
                new InterviewBot.Domain.Entities.RankedTimeSlot
                {
                    StartTime = DateTime.Today.AddDays(1).AddHours(10),
                    EndTime = DateTime.Today.AddDays(1).AddHours(11),
                    Score = 95.5,
                    AvailableParticipants = 3,
                    TotalParticipants = 3
                }
            };

            var criteria = new SlotQueryCriteria
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                DurationMinutes = 60
            };

            // Act
            var response = await responseGenerator.GenerateSlotResponseAsync(slots, criteria);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Length > 50); // Should be a substantial response
            Assert.Contains("scheduling", response.ToLowerInvariant()); // Should mention scheduling
        }

        [Fact]
        public void TimeOfDayRange_ShouldCorrectlyIdentifyContainedTimes()
        {
            // Arrange
            var morningRange = new TimeOfDayRange 
            { 
                Start = new TimeSpan(9, 0, 0), 
                End = new TimeSpan(12, 0, 0) 
            };

            // Act & Assert
            Assert.True(morningRange.Contains(new TimeSpan(10, 30, 0))); // 10:30 AM
            Assert.False(morningRange.Contains(new TimeSpan(14, 0, 0))); // 2:00 PM
            Assert.True(morningRange.Contains(new TimeSpan(9, 0, 0))); // Boundary case: 9:00 AM
            Assert.True(morningRange.Contains(new TimeSpan(12, 0, 0))); // Boundary case: 12:00 PM
        }

        [Fact]
        public async Task SlotQueryCriteria_ToString_ShouldProvideReadableFormat()
        {
            // Arrange
            var criteria = new SlotQueryCriteria
            {
                StartDate = new DateTime(2024, 1, 15),
                EndDate = new DateTime(2024, 1, 16),
                DurationMinutes = 30,
                TimeOfDay = new TimeOfDayRange { Start = new TimeSpan(9, 0, 0), End = new TimeSpan(12, 0, 0) },
                SpecificDay = "Monday",
                ParticipantEmails = new List<string> { "test@example.com" }
            };

            // Act
            var result = criteria.ToString();

            // Assert
            Assert.Contains("2024-01-15", result);
            Assert.Contains("30 minutes", result);
            Assert.Contains("09:00 - 12:00", result);
            Assert.Contains("test@example.com", result);
        }
    }
}