using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using InterviewSchedulingBot.Services.Business;
using InterviewSchedulingBot.Services.Integration;
using Moq;
using System.Net.Http;

namespace InterviewSchedulingBot.Tests.Unit
{
    public class SlotQueryParserTests
    {
        private readonly Mock<ILogger<SlotQueryParser>> _mockLogger;
        private readonly Mock<IOpenWebUIClient> _mockOpenWebUIClient;
        private readonly SlotQueryParser _slotQueryParser;

        public SlotQueryParserTests()
        {
            _mockLogger = new Mock<ILogger<SlotQueryParser>>();
            _mockOpenWebUIClient = new Mock<IOpenWebUIClient>();
            _slotQueryParser = new SlotQueryParser(_mockOpenWebUIClient.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ParseQueryAsync_WithThursdayAfternoonQuery_ShouldReturnCorrectCriteria()
        {
            // Arrange
            var query = "Find slots on Thursday afternoon";
            var mockResponse = new OpenWebUIResponse
            {
                Success = true,
                TimeOfDay = "afternoon",
                SpecificDay = "Thursday",
                Duration = 60,
                DateRange = new DateRange
                {
                    Start = DateTime.Today,
                    End = DateTime.Today.AddDays(7)
                }
            };

            _mockOpenWebUIClient
                .Setup(x => x.ProcessQueryAsync(query, OpenWebUIRequestType.SlotQuery))
                .ReturnsAsync(mockResponse);

            // Act
            var result = await _slotQueryParser.ParseQueryAsync(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Thursday", result.SpecificDay);
            Assert.NotNull(result.TimeOfDay);
            Assert.Equal(new TimeSpan(12, 0, 0), result.TimeOfDay.Start);
            Assert.Equal(new TimeSpan(17, 0, 0), result.TimeOfDay.End);
            Assert.Equal(60, result.DurationMinutes);
        }

        [Fact]
        public async Task ParseQueryAsync_WithMorningQuery_ShouldReturnMorningTimeRange()
        {
            // Arrange
            var query = "Show me morning availability tomorrow";
            var mockResponse = new OpenWebUIResponse
            {
                Success = true,
                TimeOfDay = "morning",
                RelativeDay = "tomorrow",
                Duration = 60
            };

            _mockOpenWebUIClient
                .Setup(x => x.ProcessQueryAsync(query, OpenWebUIRequestType.SlotQuery))
                .ReturnsAsync(mockResponse);

            // Act
            var result = await _slotQueryParser.ParseQueryAsync(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("tomorrow", result.RelativeDay);
            Assert.NotNull(result.TimeOfDay);
            Assert.Equal(new TimeSpan(9, 0, 0), result.TimeOfDay.Start);
            Assert.Equal(new TimeSpan(12, 0, 0), result.TimeOfDay.End);
        }

        [Fact]
        public async Task ParseQueryAsync_WithFailedResponse_ShouldReturnNull()
        {
            // Arrange
            var query = "Invalid query";
            var mockResponse = new OpenWebUIResponse
            {
                Success = false,
                Message = "Could not parse query"
            };

            _mockOpenWebUIClient
                .Setup(x => x.ProcessQueryAsync(query, OpenWebUIRequestType.SlotQuery))
                .ReturnsAsync(mockResponse);

            // Act
            var result = await _slotQueryParser.ParseQueryAsync(query);

            // Assert
            Assert.Null(result);
        }
    }

    public class OpenWebUIClientTests
    {
        private readonly Mock<ILogger<OpenWebUIClient>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly HttpClient _httpClient;
        private readonly OpenWebUIClient _openWebUIClient;

        public OpenWebUIClientTests()
        {
            _mockLogger = new Mock<ILogger<OpenWebUIClient>>();
            _mockConfiguration = new Mock<IConfiguration>();
            _httpClient = new HttpClient();
            
            // Setup configuration mocks
            _mockConfiguration.Setup(x => x["OpenWebUI:BaseUrl"]).Returns("https://test-api.com");
            _mockConfiguration.Setup(x => x["OpenWebUI:ApiKey"]).Returns("test-key");
            
            _openWebUIClient = new OpenWebUIClient(_httpClient, _mockConfiguration.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ProcessQueryAsync_WithSlotQuery_ShouldReturnFallbackResponse()
        {
            // Arrange
            var query = "Find slots on Monday morning";

            // Act
            var result = await _openWebUIClient.ProcessQueryAsync(query, OpenWebUIRequestType.SlotQuery);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("morning", result.TimeOfDay);
            Assert.Equal("Monday", result.SpecificDay);
            Assert.Equal(60, result.Duration);
        }

        [Fact]
        public async Task ProcessQueryAsync_WithAfternoonQuery_ShouldDetectAfternoon()
        {
            // Arrange
            var query = "Are there any slots Thursday afternoon?";

            // Act
            var result = await _openWebUIClient.ProcessQueryAsync(query, OpenWebUIRequestType.SlotQuery);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("afternoon", result.TimeOfDay);
            Assert.Equal("Thursday", result.SpecificDay);
        }

        [Fact]
        public async Task GenerateResponseAsync_ShouldReturnFallbackText()
        {
            // Arrange
            var prompt = "Generate response about available slots";
            var context = new { SlotCount = 5, Day = "Thursday" };

            // Act
            var result = await _openWebUIClient.GenerateResponseAsync(prompt, context);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("scheduling options", result);
        }
    }
}