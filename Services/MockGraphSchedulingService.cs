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
            // AI-enhanced confidence scoring based on multiple factors
            var baseConfidence = 0.7;
            
            // Factor 1: Time of day optimization
            var timeOfDay = meetingTime.TimeOfDay;
            var timeScore = CalculateOptimalTimeScore(timeOfDay);
            baseConfidence += timeScore * 0.15;
            
            // Factor 2: Day of week preference
            var dayScore = CalculateOptimalDayScore(meetingTime.DayOfWeek);
            baseConfidence += dayScore * 0.10;
            
            // Factor 3: Attendee count impact
            var attendeeCount = request.AttendeeEmails.Count;
            var attendeeScore = Math.Max(0.0, 1.0 - (attendeeCount - 2) * 0.05);
            baseConfidence *= attendeeScore;
            
            // Factor 4: Duration impact
            var durationScore = CalculateDurationScore(request.DurationMinutes);
            baseConfidence += durationScore * 0.05;
            
            // Factor 5: Seasonal adjustment
            var seasonalScore = CalculateSeasonalScore(meetingTime);
            baseConfidence += seasonalScore * 0.03;
            
            // Factor 6: Workload distribution (avoid clustering)
            var workloadScore = CalculateWorkloadScore(meetingTime, request);
            baseConfidence += workloadScore * 0.02;
            
            // Add some controlled randomness to simulate real-world variability
            var random = new Random(meetingTime.GetHashCode());
            baseConfidence += (random.NextDouble() - 0.5) * 0.05;
            
            return Math.Max(0.1, Math.Min(1.0, baseConfidence));
        }

        private double CalculateOptimalTimeScore(TimeSpan timeOfDay)
        {
            var hour = timeOfDay.Hours;
            return hour switch
            {
                10 => 0.25,  // Peak morning productivity
                11 => 0.20,  // Still great morning time
                14 => 0.15,  // Good afternoon start
                15 => 0.10,  // Decent afternoon
                9 => 0.10,   // Early but productive
                13 => 0.05,  // Post-lunch energy dip
                16 => 0.05,  // Late afternoon
                _ => 0.0     // Non-optimal times
            };
        }

        private double CalculateOptimalDayScore(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Tuesday => 0.10,
                DayOfWeek.Wednesday => 0.10,
                DayOfWeek.Thursday => 0.08,
                DayOfWeek.Monday => 0.05,
                DayOfWeek.Friday => 0.03,
                _ => 0.0
            };
        }

        private double CalculateDurationScore(int durationMinutes)
        {
            return durationMinutes switch
            {
                30 => 0.03,      // Quick, efficient
                60 => 0.05,      // Standard, optimal
                90 => 0.02,      // Longer but manageable
                120 => 0.01,     // Quite long
                _ => 0.0         // Other durations
            };
        }

        private double CalculateSeasonalScore(DateTime meetingTime)
        {
            var month = meetingTime.Month;
            return month switch
            {
                3 or 4 or 5 => 0.02,   // Spring - high productivity
                9 or 10 or 11 => 0.02, // Fall - high productivity
                6 or 7 or 8 => -0.01,  // Summer - vacation period
                12 or 1 or 2 => -0.01, // Winter - holiday impact
                _ => 0.0
            };
        }

        private double CalculateWorkloadScore(DateTime meetingTime, GraphSchedulingRequest request)
        {
            // Simulate workload distribution - prefer spreading meetings
            var hour = meetingTime.Hour;
            var random = new Random(meetingTime.Date.GetHashCode());
            
            // Simulate existing meeting density
            var existingMeetingDensity = random.NextDouble() * 0.5;
            
            // Lower score for times with high existing density
            return Math.Max(0.0, 0.02 * (1.0 - existingMeetingDensity));
        }

        private string GenerateSuggestionReason(DateTime meetingTime, double confidence)
        {
            var timeOfDay = meetingTime.TimeOfDay;
            var dayOfWeek = meetingTime.DayOfWeek;
            var hour = timeOfDay.Hours;

            // AI-generated contextual reasons based on confidence score and time factors
            if (confidence >= 0.9)
            {
                if (hour == 10 || hour == 11)
                    return "Peak productivity hours with optimal attendee engagement and minimal conflicts";
                if (hour == 14 || hour == 15)
                    return "Post-lunch energy peak with high collaboration potential";
                return "Exceptional time slot with maximum success probability based on historical data";
            }
            else if (confidence >= 0.8)
            {
                if (dayOfWeek == DayOfWeek.Tuesday || dayOfWeek == DayOfWeek.Wednesday)
                    return "Mid-week optimal scheduling with high attendee availability";
                if (hour >= 9 && hour <= 16)
                    return "Core working hours with strong productivity indicators";
                return "High-value time slot with excellent attendee compatibility";
            }
            else if (confidence >= 0.7)
            {
                if (dayOfWeek == DayOfWeek.Monday)
                    return "Monday scheduling with good start-of-week momentum";
                if (dayOfWeek == DayOfWeek.Thursday)
                    return "Thursday timing with solid end-of-week productivity";
                return "Well-balanced time slot with good success probability";
            }
            else if (confidence >= 0.6)
            {
                if (dayOfWeek == DayOfWeek.Friday)
                    return "Friday scheduling with moderate availability considerations";
                if (hour < 9 || hour > 16)
                    return "Extended hours scheduling with adjusted expectations";
                return "Workable time slot with some scheduling trade-offs";
            }
            else if (confidence >= 0.5)
            {
                return "Available time slot with higher coordination requirements";
            }
            else
            {
                return "Challenging time slot requiring careful attendee management";
            }
        }
    }
}