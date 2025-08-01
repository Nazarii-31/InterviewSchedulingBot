using Xunit;
using InterviewBot.Services;
using InterviewBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace InterviewBot.Tests.Unit
{
    public class DeterministicSlotRecommendationServiceTests
    {
        [Fact]
        public void GenerateConsistentTimeSlots_SameInputs_ReturnsSameResults()
        {
            // Arrange
            var service = new DeterministicSlotRecommendationService();
            var startDate = new DateTime(2025, 1, 6, 9, 0, 0); // Monday
            var endDate = new DateTime(2025, 1, 6, 17, 0, 0);
            var duration = 60;
            var participants = new List<string> { "jane.smith@company.com", "alex.wilson@company.com" };

            // Act
            var slots1 = service.GenerateConsistentTimeSlots(startDate, endDate, duration, participants);
            var slots2 = service.GenerateConsistentTimeSlots(startDate, endDate, duration, participants);
            var slots3 = service.GenerateConsistentTimeSlots(startDate, endDate, duration, participants);

            // Assert
            Assert.NotEmpty(slots1);
            Assert.Equal(slots1.Count, slots2.Count);
            Assert.Equal(slots1.Count, slots3.Count);

            // Check that the slots are identical
            for (int i = 0; i < slots1.Count; i++)
            {
                Assert.Equal(slots1[i].StartTime, slots2[i].StartTime);
                Assert.Equal(slots1[i].EndTime, slots2[i].EndTime);
                Assert.Equal(slots1[i].Score, slots2[i].Score);
                Assert.Equal(slots1[i].Reason, slots2[i].Reason);
                Assert.Equal(slots1[i].IsRecommended, slots2[i].IsRecommended);
                
                Assert.Equal(slots1[i].StartTime, slots3[i].StartTime);
                Assert.Equal(slots1[i].EndTime, slots3[i].EndTime);
                Assert.Equal(slots1[i].Score, slots3[i].Score);
                Assert.Equal(slots1[i].Reason, slots3[i].Reason);
                Assert.Equal(slots1[i].IsRecommended, slots3[i].IsRecommended);
            }
        }

        [Fact]
        public void GenerateConsistentTimeSlots_QuarterHourAlignment_AllSlotsAligned()
        {
            // Arrange
            var service = new DeterministicSlotRecommendationService();
            var startDate = new DateTime(2025, 1, 6, 9, 0, 0);
            var endDate = new DateTime(2025, 1, 6, 17, 0, 0);
            var duration = 60;
            var participants = new List<string> { "test@example.com" };

            // Act
            var slots = service.GenerateConsistentTimeSlots(startDate, endDate, duration, participants);

            // Assert
            foreach (var slot in slots)
            {
                Assert.True(slot.StartTime.Minute % 15 == 0, 
                    $"Slot start time {slot.StartTime} is not aligned to 15-minute intervals");
            }
        }

        [Fact]
        public void GenerateConsistentTimeSlots_HasRecommendedSlot_OnlyOneRecommended()
        {
            // Arrange
            var service = new DeterministicSlotRecommendationService();
            var startDate = new DateTime(2025, 1, 6, 9, 0, 0);
            var endDate = new DateTime(2025, 1, 6, 17, 0, 0);
            var duration = 60;
            var participants = new List<string> { "test@example.com" };

            // Act
            var slots = service.GenerateConsistentTimeSlots(startDate, endDate, duration, participants);

            // Assert
            var recommendedSlots = slots.Where(s => s.IsRecommended).ToList();
            Assert.True(recommendedSlots.Count <= 1, "More than one slot is marked as recommended");
            
            if (recommendedSlots.Any())
            {
                Assert.Contains("⭐ RECOMMENDED", recommendedSlots[0].Reason);
                
                // The recommended slot should be the first one (highest score)
                Assert.Equal(slots[0].StartTime, recommendedSlots[0].StartTime);
            }
        }
    }
}