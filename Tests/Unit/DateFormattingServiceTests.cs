using Xunit;
using InterviewBot.Services;
using System;

namespace InterviewBot.Tests.Unit
{
    public class DateFormattingServiceTests
    {
        [Fact]
        public void FormatDateWithDay_ValidDate_ReturnsCorrectFormat()
        {
            // Arrange
            var date = new DateTime(2025, 8, 4); // Monday, August 4, 2025

            // Act
            var result = DateFormattingService.FormatDateWithDay(date);

            // Assert
            Assert.Equal("Monday [04.08.2025]", result);
        }

        [Fact]
        public void FormatTimeRange_ValidTimes_ReturnsCorrectFormat()
        {
            // Arrange
            var start = new DateTime(2025, 1, 6, 9, 30, 0);
            var end = new DateTime(2025, 1, 6, 10, 30, 0);

            // Act
            var result = DateFormattingService.FormatTimeRange(start, end);

            // Assert
            Assert.Equal("09:30 - 10:30", result);
        }

        [Fact]
        public void FormatDateRange_ValidDates_ReturnsCorrectFormat()
        {
            // Arrange
            var start = new DateTime(2025, 1, 6);
            var end = new DateTime(2025, 1, 10);

            // Act
            var result = DateFormattingService.FormatDateRange(start, end);

            // Assert
            Assert.Equal("[06.01.2025 - 10.01.2025]", result);
        }

        [Fact]
        public void GetNextBusinessDay_Friday_ReturnsMonday()
        {
            // Arrange
            var friday = new DateTime(2025, 1, 3); // Friday

            // Act
            var result = DateFormattingService.GetNextBusinessDay(friday);

            // Assert
            Assert.Equal(DayOfWeek.Monday, result.DayOfWeek);
            Assert.Equal(new DateTime(2025, 1, 6), result.Date); // Monday
        }

        [Fact]
        public void GetNextBusinessDay_Saturday_ReturnsMonday()
        {
            // Arrange
            var saturday = new DateTime(2025, 1, 4); // Saturday

            // Act
            var result = DateFormattingService.GetNextBusinessDay(saturday);

            // Assert
            Assert.Equal(DayOfWeek.Monday, result.DayOfWeek);
            Assert.Equal(new DateTime(2025, 1, 6), result.Date); // Monday
        }

        [Fact]
        public void GetNextBusinessDay_Tuesday_ReturnsWednesday()
        {
            // Arrange
            var tuesday = new DateTime(2025, 1, 7); // Tuesday

            // Act
            var result = DateFormattingService.GetNextBusinessDay(tuesday);

            // Assert
            Assert.Equal(DayOfWeek.Wednesday, result.DayOfWeek);
            Assert.Equal(new DateTime(2025, 1, 8), result.Date); // Wednesday
        }

        [Fact]
        public void GetRelativeDateDescription_Today_ReturnsToday()
        {
            // Arrange
            var today = DateTime.Now.Date;

            // Act
            var result = DateFormattingService.GetRelativeDateDescription(today, today);

            // Assert
            Assert.Equal("today", result);
        }

        [Fact]
        public void GetRelativeDateDescription_Tomorrow_ReturnsTomorrow()
        {
            // Arrange
            var today = DateTime.Now.Date;
            var tomorrow = today.AddDays(1);

            // Act
            var result = DateFormattingService.GetRelativeDateDescription(tomorrow, today);

            // Assert
            Assert.Equal("tomorrow", result);
        }

        [Fact]
        public void GetRelativeDateDescription_ThreeDaysAway_ReturnsRelativeDescription()
        {
            // Arrange
            var today = new DateTime(2025, 1, 6); // Monday
            var target = new DateTime(2025, 1, 9); // Thursday

            // Act
            var result = DateFormattingService.GetRelativeDateDescription(target, today);

            // Assert
            Assert.Equal("in 3 days on Thursday [09.01.2025]", result);
        }
    }
}