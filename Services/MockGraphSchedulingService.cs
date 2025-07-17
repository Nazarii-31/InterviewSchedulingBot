using Microsoft.Graph.Models;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces;
using LocalMeetingTimeSuggestion = InterviewSchedulingBot.Models.MeetingTimeSuggestion;

namespace InterviewSchedulingBot.Services
{
    /// <summary>
    /// Mock implementation of IGraphSchedulingService for development and testing
    /// when Azure credentials are not available. Returns predefined fake meeting time suggestions.
    /// </summary>
    public class MockGraphSchedulingService : IGraphSchedulingService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MockGraphSchedulingService> _logger;

        public MockGraphSchedulingService(IConfiguration configuration, ILogger<MockGraphSchedulingService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<GraphSchedulingResponse> FindOptimalMeetingTimesAsync(
            GraphSchedulingRequest request, 
            string userId)
        {
            _logger.LogInformation("MockGraphSchedulingService: Finding optimal meeting times for user {UserId}", userId);

            // Simulate some processing delay
            await Task.Delay(1000);

            try
            {
                if (!request.IsValid())
                {
                    return GraphSchedulingResponse.CreateFailure(
                        "Invalid scheduling request parameters", 
                        request);
                }

                // Generate fake meeting time suggestions
                var meetingTimeSuggestions = GenerateMockMeetingTimeSuggestions(request);

                if (meetingTimeSuggestions.Count == 0)
                {
                    return GraphSchedulingResponse.CreateFailure(
                        "No suitable meeting times found for the specified criteria (mock data)", 
                        request);
                }

                _logger.LogInformation("MockGraphSchedulingService: Generated {Count} fake meeting suggestions", meetingTimeSuggestions.Count);

                return GraphSchedulingResponse.CreateSuccess(
                    meetingTimeSuggestions, 
                    request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MockGraphSchedulingService: Error generating mock meeting times");
                return GraphSchedulingResponse.CreateFailure(
                    $"Error finding optimal meeting times (mock): {ex.Message}", 
                    request);
            }
        }

        public async Task<BookingResponse> BookMeetingAsync(BookingRequest request, string userId)
        {
            _logger.LogInformation("MockGraphSchedulingService: Booking meeting for user {UserId}", userId);

            // Simulate some processing delay
            await Task.Delay(500);

            try
            {
                if (!request.IsValid())
                {
                    return BookingResponse.CreateFailure(
                        "Invalid booking request parameters", 
                        request);
                }

                // Simulate the booking process more realistically
                _logger.LogInformation("MockGraphSchedulingService: Simulating calendar event creation");
                _logger.LogInformation("MockGraphSchedulingService: Adding attendees: {Attendees}", string.Join(", ", request.AttendeeEmails));
                _logger.LogInformation("MockGraphSchedulingService: Setting up Teams meeting");
                _logger.LogInformation("MockGraphSchedulingService: Sending calendar invitations");

                // Generate a fake event ID that looks realistic
                var fakeEventId = $"AAMkADUwNjQ4ZjE3LTkzYzYtNDNjZi1iZGY5LTc1MmM5NzQxMzAzNgBGAAAAAACx{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";

                _logger.LogInformation("MockGraphSchedulingService: Generated fake event ID {EventId}", fakeEventId);
                _logger.LogInformation("MockGraphSchedulingService: Mock booking completed successfully");

                return BookingResponse.CreateSuccess(fakeEventId, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MockGraphSchedulingService: Error booking mock meeting");
                return BookingResponse.CreateFailure(
                    $"Error booking meeting (mock): {ex.Message}", 
                    request);
            }
        }

        private List<LocalMeetingTimeSuggestion> GenerateMockMeetingTimeSuggestions(GraphSchedulingRequest request)
        {
            var suggestions = new List<LocalMeetingTimeSuggestion>();
            var random = new Random();

            var currentTime = request.StartDate;
            var endTime = request.EndDate;
            var maxSuggestions = Math.Min(request.MaxSuggestions, 10);

            var suggestionCount = 0;
            var attempts = 0;
            var maxAttempts = 100; // Prevent infinite loops

            while (suggestionCount < maxSuggestions && attempts < maxAttempts && currentTime.AddMinutes(request.DurationMinutes) <= endTime)
            {
                attempts++;

                // Skip to next working day if not in working days
                if (!request.WorkingDays.Contains(currentTime.DayOfWeek))
                {
                    currentTime = currentTime.AddDays(1).Date.Add(request.WorkingHoursStart);
                    continue;
                }

                // Skip if outside working hours
                if (currentTime.TimeOfDay < request.WorkingHoursStart || 
                    currentTime.TimeOfDay.Add(TimeSpan.FromMinutes(request.DurationMinutes)) > request.WorkingHoursEnd)
                {
                    // Move to next working day
                    currentTime = currentTime.AddDays(1).Date.Add(request.WorkingHoursStart);
                    continue;
                }

                // Generate a potential meeting time
                var potentialStart = currentTime;
                var potentialEnd = potentialStart.AddMinutes(request.DurationMinutes);

                // Randomly decide if this slot is "available" (80% chance)
                if (random.NextDouble() < 0.8)
                {
                    var confidence = GenerateConfidenceScore(potentialStart, request);
                    var suggestionReason = GenerateSuggestionReason(potentialStart, confidence);

                    suggestions.Add(new LocalMeetingTimeSuggestion
                    {
                        MeetingTimeSlot = new MeetingTimeSlot
                        {
                            Start = new DateTimeTimeZone
                            {
                                DateTime = potentialStart.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                                TimeZone = request.TimeZone
                            },
                            End = new DateTimeTimeZone
                            {
                                DateTime = potentialEnd.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                                TimeZone = request.TimeZone
                            }
                        },
                        Confidence = confidence,
                        SuggestionReason = suggestionReason
                    });

                    suggestionCount++;
                }

                // Move to next interval
                currentTime = currentTime.AddMinutes(request.IntervalMinutes);
            }

            return suggestions;
        }

        private double GenerateConfidenceScore(DateTime meetingTime, GraphSchedulingRequest request)
        {
            var random = new Random();
            
            // Base confidence
            var confidence = 0.7;

            // Higher confidence for mid-morning and mid-afternoon slots
            var timeOfDay = meetingTime.TimeOfDay;
            if (timeOfDay >= TimeSpan.FromHours(10) && timeOfDay <= TimeSpan.FromHours(11))
            {
                confidence += 0.2; // 10-11 AM is prime time
            }
            else if (timeOfDay >= TimeSpan.FromHours(14) && timeOfDay <= TimeSpan.FromHours(15))
            {
                confidence += 0.15; // 2-3 PM is good
            }

            // Higher confidence for Tuesday-Thursday
            if (meetingTime.DayOfWeek >= DayOfWeek.Tuesday && meetingTime.DayOfWeek <= DayOfWeek.Thursday)
            {
                confidence += 0.1;
            }

            // Lower confidence for Monday mornings and Friday afternoons
            if (meetingTime.DayOfWeek == DayOfWeek.Monday && timeOfDay < TimeSpan.FromHours(10))
            {
                confidence -= 0.1;
            }
            else if (meetingTime.DayOfWeek == DayOfWeek.Friday && timeOfDay > TimeSpan.FromHours(15))
            {
                confidence -= 0.1;
            }

            // Add some randomness
            confidence += (random.NextDouble() - 0.5) * 0.1;

            // Ensure confidence is within valid range
            return Math.Max(0.1, Math.Min(1.0, confidence));
        }

        private string GenerateSuggestionReason(DateTime meetingTime, double confidence)
        {
            var timeOfDay = meetingTime.TimeOfDay;
            var dayOfWeek = meetingTime.DayOfWeek;

            if (confidence >= 0.9)
            {
                return "Optimal time slot with excellent attendee availability";
            }
            else if (confidence >= 0.8)
            {
                return "High-confidence slot during peak productivity hours";
            }
            else if (confidence >= 0.7)
            {
                return "Good meeting time with minimal scheduling conflicts";
            }
            else if (confidence >= 0.6)
            {
                return "Available slot with moderate attendee preferences";
            }
            else
            {
                return "Available time slot with some scheduling considerations";
            }
        }
    }
}