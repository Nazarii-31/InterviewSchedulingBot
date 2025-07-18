using Azure.AI.OpenAI;
using Azure;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces;
using System.Text.Json;
using System.Diagnostics;

namespace InterviewSchedulingBot.Services
{
    /// <summary>
    /// Hybrid AI scheduling service that combines Microsoft Graph's native scheduling capabilities
    /// with Azure OpenAI for intelligent recommendations and simplified user preference learning.
    /// This is the unified scheduling service that replaces both basic and AI scheduling variants.
    /// </summary>
    public class HybridAISchedulingService : IAISchedulingService, ISchedulingService
    {
        private readonly IGraphSchedulingService _graphSchedulingService;
        private readonly IGraphCalendarService _calendarService;
        private readonly ISchedulingHistoryRepository _historyRepository;
        private readonly IAuthenticationService _authService;
        private readonly ILogger<HybridAISchedulingService> _logger;
        private readonly IConfiguration _configuration;
        private readonly OpenAIClient? _openAIClient;

        public HybridAISchedulingService(
            IGraphSchedulingService graphSchedulingService,
            IGraphCalendarService calendarService,
            ISchedulingHistoryRepository historyRepository,
            IAuthenticationService authService,
            ILogger<HybridAISchedulingService> logger,
            IConfiguration configuration)
        {
            _graphSchedulingService = graphSchedulingService;
            _calendarService = calendarService;
            _historyRepository = historyRepository;
            _authService = authService;
            _logger = logger;
            _configuration = configuration;

            // Initialize OpenAI client if configured
            var openAIKey = _configuration["OpenAI:ApiKey"];
            var openAIEndpoint = _configuration["OpenAI:Endpoint"];
            
            if (!string.IsNullOrEmpty(openAIKey))
            {
                if (!string.IsNullOrEmpty(openAIEndpoint))
                {
                    // Azure OpenAI
                    _openAIClient = new OpenAIClient(new Uri(openAIEndpoint), new AzureKeyCredential(openAIKey));
                }
                else
                {
                    // Standard OpenAI
                    _openAIClient = new OpenAIClient(openAIKey);
                }
            }
        }

        public async Task<AISchedulingResponse> FindOptimalMeetingTimesAsync(AISchedulingRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                if (!request.IsValid())
                {
                    return AISchedulingResponse.CreateFailure("Invalid scheduling request parameters", request);
                }

                _logger.LogInformation("Finding optimal meeting times using hybrid approach for user {UserId}", request.UserId);

                // Step 1: Use Microsoft Graph for core scheduling
                var graphRequest = ConvertToGraphRequest(request);
                var graphResponse = await _graphSchedulingService.FindOptimalMeetingTimesAsync(graphRequest, request.UserId);

                if (!graphResponse.IsSuccess || graphResponse.MeetingTimeSuggestions.Count == 0)
                {
                    return AISchedulingResponse.CreateFailure(
                        graphResponse.Message ?? "No meeting times found by Microsoft Graph", 
                        request);
                }

                // Step 2: Get user preferences for enhancement
                var userPreferences = await GetOrCreateUserPreferencesAsync(request.UserId);
                var historicalData = await _historyRepository.GetSchedulingHistoryAsync(request.UserId, 90);

                // Step 3: Convert Graph suggestions to AI predictions with user preference enhancement
                var aiPredictions = await EnhanceGraphSuggestionsWithUserPreferencesAsync(
                    graphResponse.MeetingTimeSuggestions, 
                    userPreferences, 
                    historicalData,
                    request);

                // Step 4: Generate AI insights and recommendations using OpenAI if available
                var aiInsights = await GenerateAIInsightsAsync(request, userPreferences, historicalData, aiPredictions);
                var recommendations = await GenerateIntelligentRecommendationsAsync(aiPredictions, userPreferences, historicalData);

                // Step 5: Calculate attendee compatibility scores
                var attendeeCompatibilityScores = await CalculateAttendeeCompatibilityScoresAsync(
                    request.AttendeeEmails, request.UserId);

                stopwatch.Stop();

                var response = AISchedulingResponse.CreateSuccess(aiPredictions, request, userPreferences);
                response.AttendeeCompatibilityScores = attendeeCompatibilityScores;
                response.AIInsights = aiInsights;
                response.Recommendations = recommendations;
                response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Hybrid AI scheduling completed in {ElapsedMs}ms with {PredictionCount} predictions", 
                    stopwatch.ElapsedMilliseconds, aiPredictions.Count);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in hybrid AI scheduling for user {UserId}", request.UserId);
                return AISchedulingResponse.CreateFailure($"Hybrid AI scheduling error: {ex.Message}", request);
            }
        }

        public async Task LearnFromSchedulingBehaviorAsync(SchedulingHistoryEntry historyEntry)
        {
            try
            {
                _logger.LogInformation("Learning from scheduling behavior for user {UserId}", historyEntry.UserId);

                // Store the history entry
                await _historyRepository.StoreSchedulingHistoryAsync(historyEntry);

                // Update user preferences based on behavior (simplified learning)
                await UpdateUserPreferencesFromBehaviorAsync(historyEntry);

                _logger.LogInformation("Successfully learned from scheduling behavior for user {UserId}", historyEntry.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error learning from scheduling behavior for user {UserId}", historyEntry.UserId);
                throw;
            }
        }

        public async Task<UserPreferences?> GetUserPreferencesAsync(string userId)
        {
            return await _historyRepository.GetUserPreferencesAsync(userId);
        }

        public async Task UpdateUserPreferencesAsync(UserPreferences preferences)
        {
            await _historyRepository.StoreUserPreferencesAsync(preferences);
        }

        public async Task<List<SchedulingPattern>> AnalyzeSchedulingPatternsAsync(string userId, int lookbackDays = 90)
        {
            try
            {
                _logger.LogInformation("Analyzing scheduling patterns for user {UserId}", userId);

                var historicalData = await _historyRepository.GetSchedulingHistoryAsync(userId, lookbackDays);
                if (historicalData.Count == 0)
                {
                    return new List<SchedulingPattern>();
                }

                var patterns = new List<SchedulingPattern>();

                // Simplified pattern analysis focusing on most successful patterns
                var successfulMeetings = historicalData.Where(e => e.UserSatisfactionScore > 0.7).ToList();
                
                var dayTimeGroups = successfulMeetings
                    .GroupBy(e => new { e.DayOfWeek, Hour = e.TimeOfDay.Hours })
                    .Where(g => g.Count() > 1)
                    .OrderByDescending(g => g.Count());

                foreach (var group in dayTimeGroups)
                {
                    var entries = group.ToList();
                    var avgSatisfaction = entries.Average(e => e.UserSatisfactionScore);
                    var successRate = entries.Count(e => e.MeetingCompleted) / (double)entries.Count;

                    patterns.Add(new SchedulingPattern
                    {
                        UserId = userId,
                        DayOfWeek = group.Key.DayOfWeek,
                        StartTime = new TimeSpan(group.Key.Hour, 0, 0),
                        EndTime = new TimeSpan(group.Key.Hour + 1, 0, 0),
                        FrequencyCount = entries.Count,
                        SuccessRate = successRate,
                        AverageUserSatisfaction = avgSatisfaction,
                        AverageDurationMinutes = (int)entries.Average(e => e.DurationMinutes),
                        LastOccurrence = entries.Max(e => e.ScheduledTime),
                        PatternType = "Successful"
                    });
                }

                await _historyRepository.StoreSchedulingPatternsAsync(patterns);

                return patterns;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing scheduling patterns for user {UserId}", userId);
                throw;
            }
        }

        public async Task<double> PredictReschedulingProbabilityAsync(
            string userId, List<string> attendeeEmails, DateTime proposedTime, int durationMinutes)
        {
            try
            {
                var historicalData = await _historyRepository.GetSchedulingHistoryAsync(userId, 90);
                if (historicalData.Count == 0)
                {
                    return 0.25; // Default probability if no historical data
                }

                // Simple rescheduling probability based on historical data
                var similarMeetings = historicalData.Where(e =>
                    e.DayOfWeek == proposedTime.DayOfWeek &&
                    Math.Abs(e.TimeOfDay.Hours - proposedTime.Hour) <= 1).ToList();

                if (similarMeetings.Count == 0)
                {
                    return historicalData.Count(e => e.WasRescheduled) / (double)historicalData.Count;
                }

                return similarMeetings.Count(e => e.WasRescheduled) / (double)similarMeetings.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error predicting rescheduling probability for user {UserId}", userId);
                return 0.25; // Default probability on error
            }
        }

        public async Task ProvideFeedbackAsync(string userId, string meetingId, double satisfactionScore, string? feedback = null)
        {
            try
            {
                _logger.LogInformation("Receiving feedback for meeting {MeetingId} from user {UserId}", meetingId, userId);

                // Update user preferences based on feedback
                var preferences = await GetOrCreateUserPreferencesAsync(userId);
                await UpdatePreferencesFromFeedbackAsync(preferences, satisfactionScore, feedback);

                // If OpenAI is available, analyze feedback for insights
                if (_openAIClient != null && !string.IsNullOrEmpty(feedback))
                {
                    await AnalyzeFeedbackWithAIAsync(userId, feedback, satisfactionScore);
                }

                _logger.LogInformation("Processed feedback for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing feedback for user {UserId}", userId);
                throw;
            }
        }

        public async Task AdaptToCalendarChangesAsync(string userId, List<string> calendarChanges)
        {
            try
            {
                _logger.LogInformation("Adapting to calendar changes for user {UserId}", userId);

                var preferences = await GetOrCreateUserPreferencesAsync(userId);
                
                // Simple adaptation based on calendar changes
                var rescheduledCount = calendarChanges.Count(c => c.Contains("rescheduled", StringComparison.OrdinalIgnoreCase));
                var cancelledCount = calendarChanges.Count(c => c.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
                
                if (rescheduledCount > 0 || cancelledCount > 0)
                {
                    preferences.AverageReschedulingRate = Math.Min(1.0, preferences.AverageReschedulingRate + 0.05);
                }

                await _historyRepository.StoreUserPreferencesAsync(preferences);
                
                _logger.LogInformation("Adapted to {ChangeCount} calendar changes for user {UserId}", 
                    calendarChanges.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adapting to calendar changes for user {UserId}", userId);
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetAIInsightsAsync(string userId)
        {
            try
            {
                var preferences = await GetUserPreferencesAsync(userId);
                var historicalData = await _historyRepository.GetSchedulingHistoryAsync(userId, 90);
                var patterns = await _historyRepository.GetSchedulingPatternsAsync(userId);

                var insights = new Dictionary<string, object>
                {
                    ["ApproachType"] = "Hybrid (Microsoft Graph + AI)",
                    ["UserPreferences"] = preferences,
                    ["HistoricalDataPoints"] = historicalData.Count,
                    ["IdentifiedPatterns"] = patterns.Count,
                    ["OptimalMeetingTimes"] = patterns.Where(p => p.SuccessRate > 0.8).Select(p => $"{p.DayOfWeek} {p.StartTime:hh\\:mm}").ToList(),
                    ["AverageUserSatisfaction"] = historicalData.Any() ? historicalData.Average(h => h.UserSatisfactionScore) : 0.0,
                    ["SuccessfulMeetingRate"] = historicalData.Any() ? historicalData.Count(h => h.MeetingCompleted) / (double)historicalData.Count : 0.0
                };

                return insights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI insights for user {UserId}", userId);
                throw;
            }
        }

        // Private helper methods

        private GraphSchedulingRequest ConvertToGraphRequest(AISchedulingRequest request)
        {
            return new GraphSchedulingRequest
            {
                AttendeeEmails = request.AttendeeEmails,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                DurationMinutes = request.DurationMinutes,
                MaxSuggestions = request.MaxSuggestions,
                TimeZone = request.TimeZone ?? "UTC",
                IntervalMinutes = 30,
                WorkingDays = request.PreferredDays.Any() ? request.PreferredDays : new List<DayOfWeek>
                {
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
                },
                WorkingHoursStart = request.PreferredStartTime ?? new TimeSpan(9, 0, 0),
                WorkingHoursEnd = request.PreferredEndTime ?? new TimeSpan(17, 0, 0)
            };
        }

        private async Task<List<TimeSlotPrediction>> EnhanceGraphSuggestionsWithUserPreferencesAsync(
            List<MeetingTimeSuggestion> graphSuggestions, 
            UserPreferences userPreferences, 
            List<SchedulingHistoryEntry> historicalData,
            AISchedulingRequest request)
        {
            var predictions = new List<TimeSlotPrediction>();

            foreach (var suggestion in graphSuggestions)
            {
                if (suggestion.MeetingTimeSlot?.Start?.DateTime == null || 
                    suggestion.MeetingTimeSlot?.End?.DateTime == null)
                    continue;

                var startTime = DateTime.Parse(suggestion.MeetingTimeSlot.Start.DateTime);
                var endTime = DateTime.Parse(suggestion.MeetingTimeSlot.End.DateTime);

                var prediction = new TimeSlotPrediction
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    OverallConfidence = suggestion.Confidence,
                    UserPreferenceScore = CalculateUserPreferenceScore(userPreferences, startTime),
                    HistoricalSuccessScore = CalculateHistoricalSuccessRate(historicalData, startTime),
                    ConflictProbability = 1.0 - suggestion.Confidence,
                    IsOptimalSlot = suggestion.Confidence > 0.8,
                    PredictionReason = suggestion.SuggestionReason ?? "Available time slot from Microsoft Graph"
                };

                // Adjust confidence based on user preferences
                prediction.OverallConfidence = (prediction.OverallConfidence + prediction.UserPreferenceScore) / 2.0;

                predictions.Add(prediction);
            }

            return predictions.OrderByDescending(p => p.OverallConfidence).ToList();
        }

        private double CalculateUserPreferenceScore(UserPreferences preferences, DateTime startTime)
        {
            var score = 0.7; // Base score
            
            if (preferences.DayPreferenceScores.TryGetValue(startTime.DayOfWeek, out var dayScore))
            {
                score = (score + dayScore) / 2.0;
            }

            var timeOfDay = startTime.TimeOfDay;
            if (timeOfDay >= preferences.OptimalStartTime && timeOfDay <= preferences.OptimalEndTime)
            {
                score = Math.Min(1.0, score + 0.15);
            }

            return score;
        }

        private double CalculateHistoricalSuccessRate(List<SchedulingHistoryEntry> historicalData, DateTime startTime)
        {
            if (historicalData.Count == 0)
                return 0.7;

            var similarMeetings = historicalData.Where(e =>
                e.DayOfWeek == startTime.DayOfWeek &&
                Math.Abs(e.TimeOfDay.Hours - startTime.Hour) <= 1).ToList();

            if (similarMeetings.Count == 0)
                return historicalData.Average(e => e.UserSatisfactionScore);

            return similarMeetings.Average(e => e.UserSatisfactionScore);
        }

        private async Task<Dictionary<string, object>> GenerateAIInsightsAsync(
            AISchedulingRequest request, 
            UserPreferences preferences, 
            List<SchedulingHistoryEntry> historicalData,
            List<TimeSlotPrediction> predictions)
        {
            var insights = new Dictionary<string, object>
            {
                ["ApproachType"] = "Hybrid AI (Microsoft Graph + User Preferences)",
                ["PredictionCount"] = predictions.Count,
                ["AverageConfidence"] = predictions.Any() ? predictions.Average(p => p.OverallConfidence) : 0.0,
                ["HighConfidencePredictions"] = predictions.Count(p => p.OverallConfidence > 0.8),
                ["UserPreferenceAlignment"] = CalculatePreferenceAlignment(predictions, preferences),
                ["HistoricalDataPoints"] = historicalData.Count,
                ["RecommendedTimeSlot"] = predictions.FirstOrDefault()?.StartTime.ToString("yyyy-MM-dd HH:mm")
            };

            return insights;
        }

        private async Task<List<string>> GenerateIntelligentRecommendationsAsync(
            List<TimeSlotPrediction> predictions, 
            UserPreferences preferences, 
            List<SchedulingHistoryEntry> historicalData)
        {
            var recommendations = new List<string>();

            if (predictions.Count == 0)
            {
                recommendations.Add("No optimal time slots found. Consider expanding your search criteria.");
                return recommendations;
            }

            var bestPrediction = predictions.First();
            if (bestPrediction.OverallConfidence > 0.9)
            {
                recommendations.Add($"Highly recommended: {bestPrediction.StartTime:HH:mm} on {bestPrediction.StartTime.DayOfWeek}");
            }

            if (preferences.AverageReschedulingRate > 0.3)
            {
                recommendations.Add("Consider adding buffer time to reduce rescheduling likelihood");
            }

            // Use OpenAI for intelligent recommendations if available
            if (_openAIClient != null)
            {
                var aiRecommendations = await GenerateOpenAIRecommendationsAsync(predictions, preferences, historicalData);
                recommendations.AddRange(aiRecommendations);
            }

            return recommendations;
        }

        private async Task<List<string>> GenerateOpenAIRecommendationsAsync(
            List<TimeSlotPrediction> predictions, 
            UserPreferences preferences, 
            List<SchedulingHistoryEntry> historicalData)
        {
            try
            {
                var context = new
                {
                    predictions = predictions.Take(3).Select(p => new
                    {
                        time = p.StartTime.ToString("yyyy-MM-dd HH:mm"),
                        confidence = p.OverallConfidence,
                        dayOfWeek = p.StartTime.DayOfWeek.ToString()
                    }),
                    userPreferences = new
                    {
                        averageReschedulingRate = preferences.AverageReschedulingRate,
                        preferredDays = preferences.PreferredDays.Select(d => d.ToString()),
                        totalMeetings = preferences.TotalScheduledMeetings
                    },
                    historicalMetrics = new
                    {
                        totalMeetings = historicalData.Count,
                        averageSatisfaction = historicalData.Any() ? historicalData.Average(h => h.UserSatisfactionScore) : 0.0,
                        successRate = historicalData.Any() ? historicalData.Count(h => h.MeetingCompleted) / (double)historicalData.Count : 0.0
                    }
                };

                var prompt = $@"Based on the following scheduling data, provide 2-3 brief, actionable recommendations for optimal meeting scheduling:

{JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true })}

Focus on practical advice for improving meeting success rates and user satisfaction. Keep recommendations concise and specific.";

                var response = await _openAIClient.GetCompletionsAsync(new CompletionsOptions
                {
                    DeploymentName = _configuration["OpenAI:DeploymentName"] ?? "gpt-3.5-turbo",
                    Prompts = { prompt },
                    MaxTokens = 200,
                    Temperature = 0.7f
                });

                var recommendations = response.Value.Choices.FirstOrDefault()?.Text
                    ?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r.Trim().TrimStart('-', '*', ' '))
                    .ToList() ?? new List<string>();

                return recommendations;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating OpenAI recommendations, falling back to default");
                return new List<string>();
            }
        }

        private async Task<UserPreferences> GetOrCreateUserPreferencesAsync(string userId)
        {
            var preferences = await _historyRepository.GetUserPreferencesAsync(userId);
            if (preferences == null)
            {
                preferences = new UserPreferences
                {
                    UserId = userId,
                    PreferredDays = new List<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday },
                    PreferredTimes = new List<TimeSpan> { new TimeSpan(10, 0, 0), new TimeSpan(14, 0, 0) },
                    OptimalStartTime = new TimeSpan(9, 0, 0),
                    OptimalEndTime = new TimeSpan(17, 0, 0),
                    DayPreferenceScores = new Dictionary<DayOfWeek, double>
                    {
                        [DayOfWeek.Monday] = 0.7,
                        [DayOfWeek.Tuesday] = 0.9,
                        [DayOfWeek.Wednesday] = 1.0,
                        [DayOfWeek.Thursday] = 0.9,
                        [DayOfWeek.Friday] = 0.6,
                        [DayOfWeek.Saturday] = 0.2,
                        [DayOfWeek.Sunday] = 0.1
                    },
                    AverageReschedulingRate = 0.25
                };
                await _historyRepository.StoreUserPreferencesAsync(preferences);
            }
            return preferences;
        }

        private async Task UpdateUserPreferencesFromBehaviorAsync(SchedulingHistoryEntry historyEntry)
        {
            var preferences = await GetOrCreateUserPreferencesAsync(historyEntry.UserId);
            
            // Update based on successful meetings
            if (historyEntry.UserSatisfactionScore > 0.7)
            {
                // Increase preference for successful day/time combinations
                if (preferences.DayPreferenceScores.ContainsKey(historyEntry.DayOfWeek))
                {
                    var currentScore = preferences.DayPreferenceScores[historyEntry.DayOfWeek];
                    preferences.DayPreferenceScores[historyEntry.DayOfWeek] = 
                        Math.Min(1.0, currentScore + 0.02);
                }
            }
            else if (historyEntry.UserSatisfactionScore < 0.5)
            {
                // Decrease preference for unsuccessful day/time combinations
                if (preferences.DayPreferenceScores.ContainsKey(historyEntry.DayOfWeek))
                {
                    var currentScore = preferences.DayPreferenceScores[historyEntry.DayOfWeek];
                    preferences.DayPreferenceScores[historyEntry.DayOfWeek] = 
                        Math.Max(0.1, currentScore - 0.02);
                }
            }

            preferences.TotalScheduledMeetings++;
            await _historyRepository.StoreUserPreferencesAsync(preferences);
        }

        private async Task<Dictionary<string, double>> CalculateAttendeeCompatibilityScoresAsync(
            List<string> attendeeEmails, string userId)
        {
            var scores = new Dictionary<string, double>();
            var historicalData = await _historyRepository.GetSchedulingHistoryAsync(userId, 90);

            foreach (var email in attendeeEmails)
            {
                var meetingsWithAttendee = historicalData.Where(e => e.AttendeeEmails.Contains(email)).ToList();
                if (meetingsWithAttendee.Count > 0)
                {
                    var avgSatisfaction = meetingsWithAttendee.Average(e => e.UserSatisfactionScore);
                    var successRate = meetingsWithAttendee.Count(e => e.MeetingCompleted) / (double)meetingsWithAttendee.Count;
                    
                    scores[email] = (avgSatisfaction + successRate) / 2.0;
                }
                else
                {
                    scores[email] = 0.7; // Default score for new attendees
                }
            }

            return scores;
        }

        private async Task UpdatePreferencesFromFeedbackAsync(UserPreferences preferences, double satisfactionScore, string? feedback)
        {
            // Simple preference adjustment based on feedback
            if (satisfactionScore > 0.8)
            {
                // Positive feedback - no specific adjustments needed for now
                _logger.LogInformation("Positive feedback received for user {UserId}", preferences.UserId);
            }
            else if (satisfactionScore < 0.5)
            {
                // Negative feedback - slightly increase rescheduling rate expectation
                preferences.AverageReschedulingRate = Math.Min(1.0, preferences.AverageReschedulingRate + 0.01);
            }

            await _historyRepository.StoreUserPreferencesAsync(preferences);
        }

        private async Task AnalyzeFeedbackWithAIAsync(string userId, string feedback, double satisfactionScore)
        {
            try
            {
                var prompt = $@"Analyze this meeting feedback and provide insights on what to improve:

Satisfaction Score: {satisfactionScore:F2}/5.0
Feedback: {feedback}

Provide brief insights on what went well or what could be improved for future scheduling.";

                var response = await _openAIClient!.GetCompletionsAsync(new CompletionsOptions
                {
                    DeploymentName = _configuration["OpenAI:DeploymentName"] ?? "gpt-3.5-turbo",
                    Prompts = { prompt },
                    MaxTokens = 150,
                    Temperature = 0.3f
                });

                var insights = response.Value.Choices.FirstOrDefault()?.Text?.Trim();
                if (!string.IsNullOrEmpty(insights))
                {
                    _logger.LogInformation("AI feedback analysis for user {UserId}: {Insights}", userId, insights);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing feedback with AI for user {UserId}", userId);
            }
        }

        private double CalculatePreferenceAlignment(List<TimeSlotPrediction> predictions, UserPreferences preferences)
        {
            if (predictions.Count == 0)
                return 0.0;

            var alignmentScores = predictions.Select(p =>
            {
                var dayScore = preferences.DayPreferenceScores.GetValueOrDefault(p.StartTime.DayOfWeek, 0.5);
                var timeScore = IsWithinOptimalTime(p.StartTime.TimeOfDay, preferences) ? 1.0 : 0.5;
                return (dayScore + timeScore) / 2.0;
            });

            return alignmentScores.Average();
        }

        private bool IsWithinOptimalTime(TimeSpan timeOfDay, UserPreferences preferences)
        {
            return timeOfDay >= preferences.OptimalStartTime && timeOfDay <= preferences.OptimalEndTime;
        }

        #region ISchedulingService Implementation (Basic Scheduling Methods)

        /// <summary>
        /// Finds available time slots using basic scheduling logic (non-AI)
        /// This method provides compatibility with the original ISchedulingService interface
        /// </summary>
        public async Task<SchedulingResponse> FindAvailableTimeSlotsAsync(AvailabilityRequest request, string userId)
        {
            try
            {
                if (!request.IsValid())
                {
                    return SchedulingResponse.CreateFailure("Invalid availability request parameters", request);
                }

                _logger.LogInformation("Finding available time slots for user {UserId} with {AttendeeCount} attendees using basic scheduling", 
                    userId, request.AttendeeEmails.Count);

                // Get busy times for all attendees
                var attendeeBusyTimes = await _calendarService.GetFreeBusyInformationAsync(
                    request.AttendeeEmails, 
                    request.StartDate, 
                    request.EndDate, 
                    userId);

                // Find common availability
                var availableSlots = GetCommonAvailability(attendeeBusyTimes, request);

                if (availableSlots.Count == 0)
                {
                    return SchedulingResponse.CreateFailure(
                        "No common availability found for the specified attendees and time period", 
                        request);
                }

                return SchedulingResponse.CreateSuccess(availableSlots, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding available time slots for user {UserId}", userId);
                return SchedulingResponse.CreateFailure($"Error finding available time slots: {ex.Message}", request);
            }
        }

        public List<AvailableTimeSlot> GetCommonAvailability(Dictionary<string, List<AvailableTimeSlot>> attendeeBusyTimes, AvailabilityRequest request)
        {
            // Generate all possible working hour slots within the date range
            var workingHourSlots = GenerateWorkingHourSlots(request.StartDate, request.EndDate, request);

            // Find slots that don't conflict with any attendee's busy times
            var availableSlots = new List<AvailableTimeSlot>();

            foreach (var workingSlot in workingHourSlots)
            {
                if (workingSlot.CanAccommodate(request.DurationMinutes))
                {
                    // Check if this slot conflicts with any attendee's busy time
                    bool isAvailable = true;
                    
                    foreach (var attendeeEmail in request.AttendeeEmails)
                    {
                        if (attendeeBusyTimes.ContainsKey(attendeeEmail))
                        {
                            var busyTimes = attendeeBusyTimes[attendeeEmail];
                            
                            // Check if the required duration slot conflicts with any busy time
                            var requiredSlot = new AvailableTimeSlot(workingSlot.StartTime, 
                                workingSlot.StartTime.AddMinutes(request.DurationMinutes));
                            
                            if (busyTimes.Any(busyTime => busyTime.OverlapsWith(requiredSlot)))
                            {
                                isAvailable = false;
                                break;
                            }
                        }
                    }

                    if (isAvailable)
                    {
                        // Create a slot with the exact duration needed
                        var availableSlot = new AvailableTimeSlot(workingSlot.StartTime, 
                            workingSlot.StartTime.AddMinutes(request.DurationMinutes));
                        availableSlots.Add(availableSlot);
                    }
                }
            }

            // Remove overlapping slots and limit results
            var optimizedSlots = OptimizeTimeSlots(availableSlots, request.DurationMinutes);
            
            // Sort by start time and take top 20 results
            return optimizedSlots.OrderBy(slot => slot.StartTime).Take(20).ToList();
        }

        public List<AvailableTimeSlot> FilterByWorkingHours(List<AvailableTimeSlot> timeSlots, AvailabilityRequest request)
        {
            return timeSlots.Where(slot =>
            {
                var startTime = slot.StartTime.TimeOfDay;
                var endTime = slot.EndTime.TimeOfDay;
                
                return request.WorkingDays.Contains(slot.StartTime.DayOfWeek) &&
                       startTime >= request.WorkingHoursStart &&
                       endTime <= request.WorkingHoursEnd;
            }).ToList();
        }

        public List<AvailableTimeSlot> GenerateWorkingHourSlots(DateTime startDate, DateTime endDate, AvailabilityRequest request)
        {
            var workingSlots = new List<AvailableTimeSlot>();
            var currentDate = startDate.Date;

            while (currentDate <= endDate.Date)
            {
                // Check if current date is a working day
                if (request.WorkingDays.Contains(currentDate.DayOfWeek))
                {
                    var workingStart = currentDate.Add(request.WorkingHoursStart);
                    var workingEnd = currentDate.Add(request.WorkingHoursEnd);

                    // Adjust for the actual start/end dates
                    if (workingStart < startDate)
                        workingStart = startDate;
                    if (workingEnd > endDate)
                        workingEnd = endDate;

                    // Generate 30-minute slots within working hours
                    var slotStart = workingStart;
                    while (slotStart.AddMinutes(30) <= workingEnd)
                    {
                        var slotEnd = slotStart.AddMinutes(30);
                        workingSlots.Add(new AvailableTimeSlot(slotStart, slotEnd));
                        slotStart = slotStart.AddMinutes(30);
                    }
                }

                currentDate = currentDate.AddDays(1);
            }

            return workingSlots;
        }

        private List<AvailableTimeSlot> OptimizeTimeSlots(List<AvailableTimeSlot> availableSlots, int durationMinutes)
        {
            var optimized = new List<AvailableTimeSlot>();
            var sorted = availableSlots.OrderBy(slot => slot.StartTime).ToList();

            foreach (var slot in sorted)
            {
                // Skip if this slot overlaps with an already selected slot
                bool overlaps = optimized.Any(existing => slot.OverlapsWith(existing));
                
                if (!overlaps)
                {
                    optimized.Add(slot);
                }
            }

            return optimized;
        }

        #endregion
    }
}