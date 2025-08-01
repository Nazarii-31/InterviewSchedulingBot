using Xunit;
using Microsoft.Extensions.Logging;
using InterviewBot.Services;
using InterviewBot.Models;
using Moq;

namespace InterviewBot.Tests.Unit
{
    public class EnhancedSlotConsistencyTests
    {
        [Fact]
        public void SlotRecommendationService_ShouldGenerateConsistentSlots()
        {
            // Arrange
            var service = new SlotRecommendationService();
            var startDate = new DateTime(2025, 8, 2); // Saturday (tomorrow)
            var endDate = startDate.AddHours(8);
            var emails = new List<string> { "jane.smith@company.com", "alex.wilson@company.com" };
            var duration = 66;
            
            // Act - Generate slots multiple times
            var slots1 = service.GenerateConsistentTimeSlots(startDate, endDate, duration, emails);
            var slots2 = service.GenerateConsistentTimeSlots(startDate, endDate, duration, emails);
            var slots3 = service.GenerateConsistentTimeSlots(startDate, endDate, duration, emails);
            
            // Assert - Results should be identical
            Assert.Equal(slots1.Count, slots2.Count);
            Assert.Equal(slots1.Count, slots3.Count);
            
            for (int i = 0; i < slots1.Count; i++)
            {
                Assert.Equal(slots1[i].StartTime, slots2[i].StartTime);
                Assert.Equal(slots1[i].StartTime, slots3[i].StartTime);
                Assert.Equal(slots1[i].Score, slots2[i].Score);
                Assert.Equal(slots1[i].Score, slots3[i].Score);
            }
        }

        [Fact]
        public void SlotRecommendationService_ShouldAlignToQuarterHours()
        {
            // Arrange
            var service = new SlotRecommendationService();
            var startDate = new DateTime(2025, 8, 4); // Monday
            var endDate = startDate.AddHours(8);
            var emails = new List<string> { "test@company.com" };
            var duration = 30;
            
            // Act
            var slots = service.GenerateConsistentTimeSlots(startDate, endDate, duration, emails);
            
            // Assert - All slots should start at quarter-hour intervals
            foreach (var slot in slots)
            {
                Assert.True(slot.StartTime.Minute % 15 == 0, 
                    $"Slot starting at {slot.StartTime:HH:mm} is not quarter-hour aligned");
            }
        }

        [Fact]
        public void EnhancedTimeSlot_ShouldFormatCorrectly()
        {
            // Arrange
            var slot = new EnhancedTimeSlot
            {
                StartTime = new DateTime(2025, 8, 4, 9, 15, 0), // Monday 09:15
                EndTime = new DateTime(2025, 8, 4, 10, 21, 0)   // Monday 10:21
            };
            
            // Act
            var timeRange = slot.GetFormattedTimeRange();
            var dateWithDay = slot.GetFormattedDateWithDay();
            
            // Assert
            Assert.Equal("09:15 - 10:21", timeRange);
            Assert.Equal("Monday [04.08.2025]", dateWithDay);
        }

        [Fact]
        public void SlotResponseFormatter_ShouldFormatSingleDayResponse()
        {
            // Arrange
            var formatter = new SlotResponseFormatter();
            var startDate = new DateTime(2025, 8, 4, 9, 0, 0); // Monday 09:00
            var endDate = new DateTime(2025, 8, 4, 17, 0, 0);  // Monday 17:00
            var duration = 66;
            
            var slots = new List<EnhancedTimeSlot>
            {
                new EnhancedTimeSlot
                {
                    StartTime = new DateTime(2025, 8, 4, 9, 15, 0),
                    EndTime = new DateTime(2025, 8, 4, 10, 21, 0),
                    Reason = "(All participants available) ⭐ RECOMMENDED"
                }
            };
            
            // Act
            var response = formatter.FormatTimeSlotResponse(slots, startDate, endDate, duration);
            
            // Assert
            Assert.Contains("Here are the available 66-minute time slots for Monday [04.08.2025]:", response);
            Assert.Contains("09:15 - 10:21 (All participants available) ⭐ RECOMMENDED", response);
            Assert.Contains("Please let me know which time slot works best for you.", response);
        }

        [Fact]
        public void DateFormattingService_ShouldFormatConsistently()
        {
            // Arrange
            var testDate = new DateTime(2025, 8, 4, 14, 30, 0); // Monday 14:30
            
            // Act
            var formattedWithDay = DateFormattingService.FormatDateWithDay(testDate);
            var timeRange = DateFormattingService.FormatTimeRange(testDate, testDate.AddMinutes(66));
            
            // Assert
            Assert.Equal("Monday [04.08.2025]", formattedWithDay);
            Assert.Equal("14:30 - 15:36", timeRange);
        }
    }
}
