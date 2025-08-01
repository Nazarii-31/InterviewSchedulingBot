using Xunit;
using Microsoft.Extensions.Logging;
using InterviewSchedulingBot.Services.Business;
using InterviewBot.Domain.Entities;
using Moq;

namespace InterviewSchedulingBot.Tests.Unit
{
    public class EnhancedResponseSystemTests
    {
        [Fact]
        public void SlotRecommendationService_ShouldRankAndExplainSlots()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<SlotRecommendationService>>();
            var service = new SlotRecommendationService(mockLogger.Object);
            
            var testSlots = new List<RankedTimeSlot>
            {
                new RankedTimeSlot
                {
                    StartTime = new DateTime(2025, 8, 4, 9, 0, 0), // Monday 9:00 AM
                    EndTime = new DateTime(2025, 8, 4, 9, 30, 0),
                    Score = 80,
                    AvailableParticipants = 3,
                    TotalParticipants = 3,
                    AvailableParticipantEmails = new List<string> { "user1@test.com", "user2@test.com", "user3@test.com" }
                },
                new RankedTimeSlot
                {
                    StartTime = new DateTime(2025, 8, 4, 16, 30, 0), // Monday 4:30 PM
                    EndTime = new DateTime(2025, 8, 4, 17, 0, 0),
                    Score = 60,
                    AvailableParticipants = 2,
                    TotalParticipants = 3,
                    AvailableParticipantEmails = new List<string> { "user1@test.com", "user2@test.com" }
                }
            };
            
            var criteria = new SlotQueryCriteria
            {
                StartDate = new DateTime(2025, 8, 4),
                EndDate = new DateTime(2025, 8, 4),
                DurationMinutes = 30,
                ParticipantEmails = new List<string> { "user1@test.com", "user2@test.com", "user3@test.com" },
                RelativeDay = "tomorrow"
            };

            // Act
            var enhancedSlots = service.RankAndExplainSlots(testSlots, criteria);

            // Assert
            Assert.Equal(2, enhancedSlots.Count);
            
            // First slot should be the recommended one (morning, full availability)
            var bestSlot = enhancedSlots.First();
            Assert.True(bestSlot.IsRecommended);
            Assert.Contains("All participants available", bestSlot.Explanation);
            Assert.Contains("optimal", bestSlot.RecommendationReason.ToLower());
            Assert.True(bestSlot.Score > testSlots.First().Score); // Enhanced score should be higher
            
            // Second slot should not be recommended
            var secondSlot = enhancedSlots.Last();
            Assert.False(secondSlot.IsRecommended);
            Assert.Contains("2/3 participants available", secondSlot.Explanation);
        }

        [Fact]
        public void ResponseFormatter_ShouldFormatWithRecommendations()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ResponseFormatter>>();
            var formatter = new ResponseFormatter(mockLogger.Object);
            
            var enhancedSlots = new List<EnhancedRankedTimeSlot>
            {
                new EnhancedRankedTimeSlot
                {
                    StartTime = new DateTime(2025, 8, 4, 9, 0, 0), // Monday 9:00 AM
                    EndTime = new DateTime(2025, 8, 4, 9, 30, 0),
                    Score = 95,
                    AvailableParticipants = 3,
                    TotalParticipants = 3,
                    Explanation = "All participants available, Best for productivity",
                    IsRecommended = true,
                    RecommendationReason = "Optimal time for all participants based on highest overall availability and optimal meeting hours"
                },
                new EnhancedRankedTimeSlot
                {
                    StartTime = new DateTime(2025, 8, 4, 10, 15, 0), // Monday 10:15 AM
                    EndTime = new DateTime(2025, 8, 4, 10, 45, 0),
                    Score = 85,
                    AvailableParticipants = 3,
                    TotalParticipants = 3,
                    Explanation = "All participants available",
                    IsRecommended = false
                }
            };
            
            var criteria = new SlotQueryCriteria
            {
                StartDate = new DateTime(2025, 8, 4),
                EndDate = new DateTime(2025, 8, 4),
                DurationMinutes = 30,
                ParticipantEmails = new List<string> { "user1@test.com", "user2@test.com", "user3@test.com" },
                RelativeDay = "tomorrow"
            };

            // Act
            var result = formatter.FormatSlotResponse(enhancedSlots, criteria);

            // Assert
            Assert.Contains("Here are the available 30-minute time slots for Monday [04.08.2025]:", result);
            Assert.Contains("Monday [04.08.2025]", result);
            Assert.Contains("09:00 - 09:30", result);
            Assert.Contains("10:15 - 10:45", result);
            Assert.Contains("⭐ RECOMMENDED", result);
            Assert.Contains("(All participants available, Best for productivity)", result);
            Assert.Contains("Please let me know which time slot works best for you.", result);
            
            // The recommended slot should have the RECOMMENDED marker
            var lines = result.Split('\n');
            var recommendedLine = lines.FirstOrDefault(l => l.Contains("09:00 - 09:30"));
            Assert.NotNull(recommendedLine);
            Assert.Contains("⭐ RECOMMENDED", recommendedLine);
        }

        [Fact]
        public void DateFormattingExtensions_ShouldFormatConsistently()
        {
            // Arrange
            var testDate = new DateTime(2025, 8, 4, 9, 15, 0); // Monday, August 4, 2025, 9:15 AM

            // Act & Assert
            Assert.Equal("Monday [04.08.2025]", testDate.ToSchedulingDayFormat());
            Assert.Equal("04.08.2025", testDate.ToSchedulingDateFormat());
            Assert.Equal("09:15", testDate.ToSchedulingTimeFormat());
            
            var endTime = testDate.AddMinutes(30);
            Assert.Equal("09:15 - 09:45", testDate.ToSchedulingTimeRangeFormat(endTime));
        }
    }
}
