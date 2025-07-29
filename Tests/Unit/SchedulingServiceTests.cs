using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using InterviewBot.Infrastructure.Scheduling;
using InterviewBot.Domain.Entities;
using InterviewBot.Domain.Interfaces;

namespace InterviewBot.Tests.Unit
{
    public class SchedulingServiceTests
    {
        private readonly Mock<IAvailabilityService> _mockAvailabilityService;
        private readonly Mock<ILogger<SchedulingService>> _mockLogger;
        private readonly Mock<ILogger<OptimalSlotFinder>> _mockSlotFinderLogger;
        private readonly SchedulingService _schedulingService;
        private readonly OptimalSlotFinder _slotFinder;

        public SchedulingServiceTests()
        {
            _mockAvailabilityService = new Mock<IAvailabilityService>();
            _mockLogger = new Mock<ILogger<SchedulingService>>();
            _mockSlotFinderLogger = new Mock<ILogger<OptimalSlotFinder>>();
            _slotFinder = new OptimalSlotFinder(_mockSlotFinderLogger.Object);
            _schedulingService = new SchedulingService(_mockAvailabilityService.Object, _slotFinder, _mockLogger.Object);
        }

        [Fact]
        public async Task FindOptimalSlotsAsync_WithAvailableParticipants_ReturnsRankedSlots()
        {
            // Arrange
            var participantIds = new List<string> { "user1", "user2" };
            var startDate = DateTime.Today.AddDays(1);
            var endDate = startDate.AddDays(1);
            var durationMinutes = 60;
            var maxResults = 5;

            var mockAvailability = new Dictionary<string, List<TimeSlot>>
            {
                ["user1"] = new List<TimeSlot>
                {
                    new TimeSlot { StartTime = startDate.AddHours(9), EndTime = startDate.AddHours(12) },
                    new TimeSlot { StartTime = startDate.AddHours(14), EndTime = startDate.AddHours(17) }
                },
                ["user2"] = new List<TimeSlot>
                {
                    new TimeSlot { StartTime = startDate.AddHours(10), EndTime = startDate.AddHours(12) },
                    new TimeSlot { StartTime = startDate.AddHours(14), EndTime = startDate.AddHours(16) }
                }
            };

            _mockAvailabilityService
                .Setup(x => x.GetParticipantAvailabilityAsync(participantIds, startDate, endDate))
                .ReturnsAsync(mockAvailability);

            // Act
            var result = await _schedulingService.FindOptimalSlotsAsync(
                participantIds, startDate, endDate, durationMinutes, maxResults);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.All(result, slot => Assert.True(slot.Score > 0));
            Assert.All(result, slot => Assert.True(slot.AvailableParticipants > 0));
        }

        [Fact]
        public async Task FindOptimalSlotsAsync_WithNoAvailability_ReturnsEmptyList()
        {
            // Arrange
            var participantIds = new List<string> { "user1", "user2" };
            var startDate = DateTime.Today.AddDays(1);
            var endDate = startDate.AddDays(1);
            var durationMinutes = 60;
            var maxResults = 5;

            var emptyAvailability = new Dictionary<string, List<TimeSlot>>
            {
                ["user1"] = new List<TimeSlot>(),
                ["user2"] = new List<TimeSlot>()
            };

            _mockAvailabilityService
                .Setup(x => x.GetParticipantAvailabilityAsync(participantIds, startDate, endDate))
                .ReturnsAsync(emptyAvailability);

            // Act
            var result = await _schedulingService.FindOptimalSlotsAsync(
                participantIds, startDate, endDate, durationMinutes, maxResults);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void OptimalSlotFinder_ScoresSlotsByTimeOfDay()
        {
            // Arrange
            var participantAvailability = new Dictionary<string, List<TimeSlot>>
            {
                ["user1"] = new List<TimeSlot>
                {
                    new TimeSlot { StartTime = DateTime.Today.AddHours(8), EndTime = DateTime.Today.AddHours(18) }
                }
            };

            var requirements = new InterviewRequirements
            {
                DurationMinutes = 60,
                PreferredTimeOfDay = TimeSpan.FromHours(10),
                WorkingHoursStart = TimeSpan.FromHours(9),
                WorkingHoursEnd = TimeSpan.FromHours(17),
                MaxResults = 10
            };

            // Act
            var result = _slotFinder.FindOptimalSlots(participantAvailability, requirements);

            // Assert
            Assert.NotEmpty(result);
            
            // Slots closer to preferred time (10 AM) should have higher scores
            var morningSlots = result.Where(s => s.StartTime.Hour >= 9 && s.StartTime.Hour <= 11).ToList();
            var afternoonSlots = result.Where(s => s.StartTime.Hour >= 15 && s.StartTime.Hour <= 17).ToList();
            
            if (morningSlots.Any() && afternoonSlots.Any())
            {
                Assert.True(morningSlots.Max(s => s.Score) >= afternoonSlots.Max(s => s.Score));
            }
        }

        [Fact]
        public void OptimalSlotFinder_PrefersHigherParticipantAvailability()
        {
            // Arrange
            var participantAvailability = new Dictionary<string, List<TimeSlot>>
            {
                ["user1"] = new List<TimeSlot>
                {
                    new TimeSlot { StartTime = DateTime.Today.AddHours(9), EndTime = DateTime.Today.AddHours(12) }
                },
                ["user2"] = new List<TimeSlot>
                {
                    new TimeSlot { StartTime = DateTime.Today.AddHours(10), EndTime = DateTime.Today.AddHours(12) }
                },
                ["user3"] = new List<TimeSlot>
                {
                    new TimeSlot { StartTime = DateTime.Today.AddHours(11), EndTime = DateTime.Today.AddHours(12) }
                }
            };

            var requirements = new InterviewRequirements
            {
                DurationMinutes = 60,
                MaxResults = 10
            };

            // Act
            var result = _slotFinder.FindOptimalSlots(participantAvailability, requirements);

            // Assert
            Assert.NotEmpty(result);
            
            // Slots with more participants should be scored higher
            var sortedByParticipants = result.OrderByDescending(s => s.AvailableParticipants).ToList();
            for (int i = 0; i < sortedByParticipants.Count - 1; i++)
            {
                if (sortedByParticipants[i].AvailableParticipants > sortedByParticipants[i + 1].AvailableParticipants)
                {
                    Assert.True(sortedByParticipants[i].Score >= sortedByParticipants[i + 1].Score);
                }
            }
        }

        [Theory]
        [InlineData(30, 5)]  // 30-minute meeting should have many options
        [InlineData(60, 3)]  // 60-minute meeting should have fewer options  
        [InlineData(120, 1)] // 120-minute meeting should have very few options
        public void OptimalSlotFinder_RespectsMinimumDuration(int durationMinutes, int expectedMinSlots)
        {
            // Arrange
            var participantAvailability = new Dictionary<string, List<TimeSlot>>
            {
                ["user1"] = new List<TimeSlot>
                {
                    new TimeSlot { StartTime = DateTime.Today.AddHours(9), EndTime = DateTime.Today.AddHours(17) }
                }
            };

            var requirements = new InterviewRequirements
            {
                DurationMinutes = durationMinutes,
                MaxResults = 20
            };

            // Act
            var result = _slotFinder.FindOptimalSlots(participantAvailability, requirements);

            // Assert
            Assert.NotEmpty(result);
            Assert.True(result.Count >= expectedMinSlots);
            Assert.All(result, slot => Assert.True(slot.Duration.TotalMinutes >= durationMinutes));
        }
    }
}