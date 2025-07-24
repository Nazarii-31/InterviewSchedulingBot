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

            // Enhanced AI-generated contextual reasons with detailed participant analysis
            if (confidence >= 0.9)
            {
                if (hour == 10 || hour == 11)
                    return $"**OPTIMAL SLOT**: Peak productivity hours ({hour}:00) on {dayOfWeek}. All participants likely available with maximum energy levels. Historical data shows 95% success rate for similar time slots. Ideal for collaborative decision-making and focused discussions.";
                if (hour == 14 || hour == 15)
                    return $"**EXCELLENT CHOICE**: Post-lunch energy peak ({hour}:00) on {dayOfWeek}. Participants refreshed and alert. Strong collaboration potential with minimal external distractions. 92% historical attendance rate for afternoon sessions.";
                return $"**EXCEPTIONAL TIMING**: {meetingTime:dddd, MMM dd at HH:mm}. Maximum success probability based on comprehensive calendar analysis. Perfect balance of availability, productivity, and participant engagement. No major conflicts detected across all calendars.";
            }
            else if (confidence >= 0.8)
            {
                if (dayOfWeek == DayOfWeek.Tuesday || dayOfWeek == DayOfWeek.Wednesday)
                    return $"**HIGHLY RECOMMENDED**: Mid-week scheduling on {dayOfWeek} at {hour}:00. Peak availability window with 87% of participants free. Optimal for strategic discussions and decision-making. Low probability of conflicting priorities.";
                if (hour >= 9 && hour <= 16)
                    return $"**STRONG OPTION**: Core working hours ({hour}:00) with excellent productivity indicators. All participants within standard business hours. Minimal travel/commute conflicts. 84% success rate for similar duration meetings.";
                return $"**HIGH-VALUE SLOT**: {meetingTime:dddd at HH:mm}. Strong attendee compatibility with majority availability. Good balance of participant schedules. Historical data indicates high engagement and completion rates.";
            }
            else if (confidence >= 0.7)
            {
                if (dayOfWeek == DayOfWeek.Monday)
                    return $"**GOOD TIMING**: Monday scheduling at {hour}:00 with start-of-week momentum. Most participants available with fresh perspective. Some potential for delayed starts due to Monday morning catch-up activities.";
                if (dayOfWeek == DayOfWeek.Thursday)
                    return $"**SOLID CHOICE**: Thursday {hour}:00 with strong end-of-week productivity. Good availability across all time zones. Participants motivated to complete weekly objectives. Minor potential for Friday planning conflicts.";
                return $"**BALANCED OPTION**: {meetingTime:dddd, MMM dd at HH:mm}. Well-balanced time slot accommodating majority of participants. Good success probability with standard coordination requirements. Some schedule optimization possible.";
            }
            else if (confidence >= 0.6)
            {
                if (dayOfWeek == DayOfWeek.Friday)
                    return $"**WORKABLE FRIDAY**: Friday {hour}:00 scheduling with moderate availability. Some participants may have early weekend starts. Consider shorter agenda due to end-of-week energy levels. 68% typical attendance rate.";
                if (hour < 9 || hour > 16)
                    return $"**EXTENDED HOURS**: {hour}:00 scheduling outside standard business hours. Accommodates global participants but may challenge local attendees. Consider time zone impacts and energy levels for optimal outcomes.";
                return $"**ACCEPTABLE TIMING**: {meetingTime:dddd at HH:mm}. Workable time slot with some scheduling trade-offs. Majority availability with coordination requirements. May need agenda adjustments for optimal effectiveness.";
            }
            else if (confidence >= 0.5)
            {
                return $"**COORDINATION REQUIRED**: {meetingTime:dddd, MMM dd at HH:mm}. Available time slot requiring careful attendee management. Multiple scheduling considerations needed. Recommend shorter duration or agenda prioritization for success.";
            }
            else
            {
                return $"**CHALLENGING SLOT**: {meetingTime:dddd at HH:mm}. Limited availability requiring significant coordination. Consider rescheduling or reducing participant count. Alternative: Focus on most critical attendees or split into multiple smaller meetings.";
            }
        }
    }
}