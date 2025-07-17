using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces;
using System.Diagnostics;

namespace InterviewSchedulingBot.Services
{
    /// <summary>
    /// AI-driven scheduling service that uses machine learning to provide intelligent
    /// meeting time recommendations based on historical data and user preferences
    /// </summary>
    public class AISchedulingService : IAISchedulingService
    {
        private readonly ISchedulingHistoryRepository _historyRepository;
        private readonly ISchedulingMLModel _mlModel;
        private readonly IAuthenticationService _authService;
        private readonly ILogger<AISchedulingService> _logger;
        private readonly IConfiguration _configuration;

        public AISchedulingService(
            ISchedulingHistoryRepository historyRepository,
            ISchedulingMLModel mlModel,
            IAuthenticationService authService,
            ILogger<AISchedulingService> logger,
            IConfiguration configuration)
        {
            _historyRepository = historyRepository;
            _mlModel = mlModel;
            _authService = authService;
            _logger = logger;
            _configuration = configuration;
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

                _logger.LogInformation("Finding optimal meeting times for user {UserId} with {AttendeeCount} attendees using AI", 
                    request.UserId, request.AttendeeEmails.Count);

                // Check authentication
                var isAuthenticated = await _authService.IsUserAuthenticatedAsync(request.UserId);
                if (!isAuthenticated)
                {
                    return AISchedulingResponse.CreateFailure("User authentication required", request);
                }

                // Get user preferences and historical data
                var userPreferences = await GetOrCreateUserPreferencesAsync(request.UserId);
                var historicalData = await _historyRepository.GetSchedulingHistoryAsync(request.UserId, 90);
                var patterns = await _historyRepository.GetSchedulingPatternsAsync(request.UserId);

                // Train/update ML model with recent data if enabled
                if (request.UseLearningAlgorithm && historicalData.Count > 0)
                {
                    await _mlModel.TrainModelAsync(historicalData);
                }

                // Generate feature vector for ML prediction
                var features = await GenerateFeatureVectorAsync(request, userPreferences, historicalData, patterns);

                // Get AI predictions
                var mlPredictions = await _mlModel.PredictOptimalTimeSlotsAsync(features);

                // Filter and refine predictions based on request parameters
                var refinedPredictions = await RefineAndFilterPredictionsAsync(
                    mlPredictions, request, userPreferences, patterns);

                // Calculate attendee compatibility scores
                var attendeeCompatibilityScores = await CalculateAttendeeCompatibilityScoresAsync(
                    request.AttendeeEmails, request.UserId);

                // Generate AI insights and recommendations
                var aiInsights = await GenerateAIInsightsAsync(request, userPreferences, historicalData, refinedPredictions);
                var recommendations = GenerateRecommendations(refinedPredictions, userPreferences, patterns);

                stopwatch.Stop();

                var response = AISchedulingResponse.CreateSuccess(refinedPredictions, request, userPreferences);
                response.RelevantPatterns = patterns;
                response.AttendeeCompatibilityScores = attendeeCompatibilityScores;
                response.AIInsights = aiInsights;
                response.Recommendations = recommendations;
                response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("AI scheduling completed in {ElapsedMs}ms with {PredictionCount} predictions", 
                    stopwatch.ElapsedMilliseconds, refinedPredictions.Count);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI scheduling for user {UserId}", request.UserId);
                return AISchedulingResponse.CreateFailure($"AI scheduling error: {ex.Message}", request);
            }
        }

        public async Task LearnFromSchedulingBehaviorAsync(SchedulingHistoryEntry historyEntry)
        {
            try
            {
                _logger.LogInformation("Learning from scheduling behavior for user {UserId}", historyEntry.UserId);

                // Store the history entry
                await _historyRepository.StoreSchedulingHistoryAsync(historyEntry);

                // Update user preferences based on this behavior
                await UpdateUserPreferencesFromBehaviorAsync(historyEntry);

                // Analyze and update patterns
                await UpdateSchedulingPatternsAsync(historyEntry);

                // Retrain the ML model with new data
                var recentHistory = await _historyRepository.GetSchedulingHistoryAsync(historyEntry.UserId, 30);
                if (recentHistory.Count > 10) // Only retrain if we have enough data
                {
                    await _mlModel.TrainModelAsync(recentHistory);
                }

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

                // Group by day of week and time patterns
                var dayTimeGroups = historicalData
                    .GroupBy(e => new { e.DayOfWeek, Hour = e.TimeOfDay.Hours })
                    .Where(g => g.Count() > 1) // Only patterns that occur more than once
                    .OrderByDescending(g => g.Count());

                foreach (var group in dayTimeGroups)
                {
                    var entries = group.ToList();
                    var avgSatisfaction = entries.Average(e => e.UserSatisfactionScore);
                    var successRate = entries.Count(e => e.MeetingCompleted) / (double)entries.Count;
                    var reschedulingRate = entries.Count(e => e.WasRescheduled) / (double)entries.Count;

                    var pattern = new SchedulingPattern
                    {
                        UserId = userId,
                        DayOfWeek = group.Key.DayOfWeek,
                        StartTime = new TimeSpan(group.Key.Hour, 0, 0),
                        EndTime = new TimeSpan(group.Key.Hour + 1, 0, 0),
                        FrequencyCount = entries.Count,
                        SuccessRate = successRate,
                        AverageUserSatisfaction = avgSatisfaction,
                        AverageDurationMinutes = (int)entries.Average(e => e.DurationMinutes),
                        ReschedulingRate = reschedulingRate,
                        LastOccurrence = entries.Max(e => e.ScheduledTime),
                        CommonAttendees = entries.SelectMany(e => e.AttendeeEmails).Distinct().ToList(),
                        PatternType = DeterminePatternType(entries)
                    };

                    patterns.Add(pattern);
                }

                // Store the analyzed patterns
                await _historyRepository.StoreSchedulingPatternsAsync(patterns);

                _logger.LogInformation("Analyzed {PatternCount} scheduling patterns for user {UserId}", 
                    patterns.Count, userId);

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
                    return 0.3; // Default probability if no historical data
                }

                var dayOfWeek = proposedTime.DayOfWeek;
                var hourOfDay = proposedTime.Hour;

                // Find similar historical meetings
                var similarMeetings = historicalData.Where(e =>
                    e.DayOfWeek == dayOfWeek &&
                    Math.Abs(e.TimeOfDay.Hours - hourOfDay) <= 1 &&
                    Math.Abs(e.DurationMinutes - durationMinutes) <= 30 &&
                    e.AttendeeEmails.Any(email => attendeeEmails.Contains(email))).ToList();

                if (similarMeetings.Count == 0)
                {
                    // Use overall rescheduling rate
                    return historicalData.Count(e => e.WasRescheduled) / (double)historicalData.Count;
                }

                // Calculate probability based on similar meetings
                var reschedulingRate = similarMeetings.Count(e => e.WasRescheduled) / (double)similarMeetings.Count;
                
                // Adjust based on attendee history
                var attendeeReschedulingRate = CalculateAttendeeReschedulingRate(historicalData, attendeeEmails);
                
                // Combine the rates with weights
                var combinedRate = (reschedulingRate * 0.7) + (attendeeReschedulingRate * 0.3);
                
                return Math.Max(0.1, Math.Min(0.9, combinedRate));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error predicting rescheduling probability for user {UserId}", userId);
                return 0.3; // Default probability on error
            }
        }

        public async Task ProvideFeedbackAsync(string userId, string meetingId, double satisfactionScore, string? feedback = null)
        {
            try
            {
                _logger.LogInformation("Receiving feedback for meeting {MeetingId} from user {UserId}", meetingId, userId);

                // Find the corresponding history entry and update it
                var historyEntries = await _historyRepository.GetSchedulingHistoryAsync(userId, 30);
                var relevantEntry = historyEntries.FirstOrDefault(e => e.UserId == userId); // Simplified matching

                if (relevantEntry != null)
                {
                    relevantEntry.UserSatisfactionScore = satisfactionScore;
                    await _historyRepository.StoreSchedulingHistoryAsync(relevantEntry);
                }

                // Update user preferences based on feedback
                var preferences = await GetOrCreateUserPreferencesAsync(userId);
                if (relevantEntry != null)
                {
                    await UpdatePreferencesFromFeedbackAsync(preferences, relevantEntry, satisfactionScore);
                }

                _logger.LogInformation("Processed feedback for user {UserId} with satisfaction score {Score}", 
                    userId, satisfactionScore);
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
                
                // Analyze calendar changes to adjust preferences
                foreach (var change in calendarChanges)
                {
                    // Simplified change analysis - in production, this would be more sophisticated
                    if (change.Contains("cancelled") || change.Contains("rescheduled"))
                    {
                        preferences.AverageReschedulingRate = Math.Min(1.0, preferences.AverageReschedulingRate + 0.01);
                    }
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
                var modelInfo = await _mlModel.GetModelInfoAsync();

                var insights = new Dictionary<string, object>
                {
                    ["UserPreferences"] = preferences,
                    ["HistoricalDataPoints"] = historicalData.Count,
                    ["IdentifiedPatterns"] = patterns.Count,
                    ["ModelAccuracy"] = modelInfo.GetValueOrDefault("Accuracy", 0.85),
                    ["OptimalMeetingTimes"] = patterns.Where(p => p.SuccessRate > 0.8).Select(p => $"{p.DayOfWeek} {p.StartTime:hh\\:mm}").ToList(),
                    ["ReschedulingTrends"] = CalculateReschedulingTrends(historicalData),
                    ["AttendeeCompatibility"] = CalculateAttendeeInsights(historicalData),
                    ["ProductivityPatterns"] = AnalyzeProductivityPatterns(historicalData),
                    ["Recommendations"] = GenerateInsightRecommendations(preferences, patterns, historicalData)
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
                    MorningPreference = 0.6,
                    AfternoonPreference = 0.7,
                    EveningPreference = 0.2,
                    DayPreferenceScores = new Dictionary<DayOfWeek, double>
                    {
                        [DayOfWeek.Monday] = 0.7,
                        [DayOfWeek.Tuesday] = 0.9,
                        [DayOfWeek.Wednesday] = 1.0,
                        [DayOfWeek.Thursday] = 0.9,
                        [DayOfWeek.Friday] = 0.6,
                        [DayOfWeek.Saturday] = 0.2,
                        [DayOfWeek.Sunday] = 0.1
                    }
                };
                await _historyRepository.StoreUserPreferencesAsync(preferences);
            }
            return preferences;
        }

        private async Task<Dictionary<string, double>> GenerateFeatureVectorAsync(
            AISchedulingRequest request, UserPreferences preferences, 
            List<SchedulingHistoryEntry> historicalData, List<SchedulingPattern> patterns)
        {
            var features = new Dictionary<string, double>
            {
                ["DurationMinutes"] = request.DurationMinutes,
                ["AttendeeCount"] = request.AttendeeEmails.Count,
                ["UserHistoryScore"] = historicalData.Count > 0 ? historicalData.Average(e => e.UserSatisfactionScore) : 0.7,
                ["UserPreferenceScore"] = CalculateUserPreferenceScore(preferences, request),
                ["SeasonalFactor"] = CalculateSeasonalFactor(request.StartDate),
                ["WorkloadFactor"] = CalculateWorkloadFactor(request.StartDate, request.EndDate),
                ["AttendeeHistoryScore"] = await CalculateAttendeeHistoryScore(request.AttendeeEmails, request.UserId),
                ["TimeZoneCompatibility"] = 0.9, // Simplified for now
                ["PatternMatchScore"] = CalculatePatternMatchScore(patterns, request)
            };

            return features;
        }

        private async Task<List<TimeSlotPrediction>> RefineAndFilterPredictionsAsync(
            List<TimeSlotPrediction> predictions, AISchedulingRequest request, 
            UserPreferences preferences, List<SchedulingPattern> patterns)
        {
            await Task.Delay(50); // Simulate processing

            var refined = predictions
                .Where(p => p.OverallConfidence >= request.MinimumConfidenceThreshold)
                .Where(p => IsWithinRequestedTimeRange(p, request))
                .Where(p => IsWithinPreferredDays(p, request, preferences))
                .OrderByDescending(p => p.OverallConfidence)
                .Take(request.MaxSuggestions)
                .ToList();

            // Apply final adjustments based on user preferences and patterns
            foreach (var prediction in refined)
            {
                ApplyPreferenceAdjustments(prediction, preferences, patterns);
            }

            return refined;
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
                    var reschedulingRate = meetingsWithAttendee.Count(e => e.WasRescheduled) / (double)meetingsWithAttendee.Count;
                    
                    scores[email] = (avgSatisfaction + successRate + (1 - reschedulingRate)) / 3.0;
                }
                else
                {
                    scores[email] = 0.7; // Default score for new attendees
                }
            }

            return scores;
        }

        private async Task<Dictionary<string, object>> GenerateAIInsightsAsync(
            AISchedulingRequest request, UserPreferences preferences, 
            List<SchedulingHistoryEntry> historicalData, List<TimeSlotPrediction> predictions)
        {
            await Task.Delay(25); // Simulate processing

            var insights = new Dictionary<string, object>
            {
                ["OptimalTimeSlots"] = predictions.Where(p => p.IsOptimalSlot).Count(),
                ["AverageConfidence"] = predictions.Any() ? predictions.Average(p => p.OverallConfidence) : 0.0,
                ["HighConfidencePredictions"] = predictions.Count(p => p.OverallConfidence > 0.8),
                ["UserPreferenceAlignment"] = CalculatePreferenceAlignment(predictions, preferences),
                ["HistoricalSuccessIndicator"] = historicalData.Count > 0 ? historicalData.Average(e => e.UserSatisfactionScore) : 0.7,
                ["RecommendedTimeSlot"] = predictions.FirstOrDefault()?.StartTime.ToString("yyyy-MM-dd HH:mm"),
                ["PredictionStrength"] = predictions.Any() ? "High" : "Low",
                ["LearningDataPoints"] = historicalData.Count
            };

            return insights;
        }

        private List<string> GenerateRecommendations(
            List<TimeSlotPrediction> predictions, UserPreferences preferences, List<SchedulingPattern> patterns)
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

            if (patterns.Any(p => p.SuccessRate > 0.8))
            {
                recommendations.Add("Consider your most successful meeting patterns for optimal scheduling");
            }

            if (preferences.AverageReschedulingRate > 0.3)
            {
                recommendations.Add("Consider adding buffer time to reduce rescheduling likelihood");
            }

            if (predictions.Any(p => p.ConflictProbability > 0.5))
            {
                recommendations.Add("Some time slots have higher conflict probability - consider alternatives");
            }

            return recommendations;
        }

        // Additional helper methods for calculations and analysis
        private double CalculateUserPreferenceScore(UserPreferences preferences, AISchedulingRequest request)
        {
            // Simplified preference scoring
            var score = 0.7; // Base score
            
            if (request.PreferredDays.Any() && request.PreferredDays.All(d => preferences.PreferredDays.Contains(d)))
                score += 0.2;
            
            if (request.PreferredStartTime.HasValue && preferences.PreferredTimes.Any(t => Math.Abs((t - request.PreferredStartTime.Value).TotalMinutes) < 60))
                score += 0.1;

            return Math.Min(1.0, score);
        }

        private double CalculateSeasonalFactor(DateTime date)
        {
            // Simple seasonal adjustment - could be more sophisticated
            var month = date.Month;
            return month switch
            {
                12 or 1 or 2 => 0.9, // Winter - slightly lower
                3 or 4 or 5 => 1.0,  // Spring - optimal
                6 or 7 or 8 => 0.95, // Summer - slightly lower due to vacations
                9 or 10 or 11 => 1.0, // Fall - optimal
                _ => 1.0
            };
        }

        private double CalculateWorkloadFactor(DateTime startDate, DateTime endDate)
        {
            // Simplified workload calculation
            var days = (endDate - startDate).Days;
            return days switch
            {
                <= 7 => 0.8,  // Urgent scheduling
                <= 14 => 1.0, // Normal scheduling
                <= 30 => 0.9, // Flexible scheduling
                _ => 0.85     // Long-term scheduling
            };
        }

        private async Task<double> CalculateAttendeeHistoryScore(List<string> attendeeEmails, string userId)
        {
            var historicalData = await _historyRepository.GetSchedulingHistoryAsync(userId, 90);
            
            if (historicalData.Count == 0)
                return 0.7; // Default score

            var totalScore = 0.0;
            var count = 0;

            foreach (var email in attendeeEmails)
            {
                var meetingsWithAttendee = historicalData.Where(e => e.AttendeeEmails.Contains(email)).ToList();
                if (meetingsWithAttendee.Count > 0)
                {
                    var avgSatisfaction = meetingsWithAttendee.Average(e => e.UserSatisfactionScore);
                    totalScore += avgSatisfaction;
                    count++;
                }
            }

            return count > 0 ? totalScore / count : 0.7;
        }

        private double CalculatePatternMatchScore(List<SchedulingPattern> patterns, AISchedulingRequest request)
        {
            if (patterns.Count == 0)
                return 0.5;

            var bestPattern = patterns.OrderByDescending(p => p.SuccessRate).FirstOrDefault();
            if (bestPattern == null)
                return 0.5;

            // Check if request aligns with successful patterns
            return bestPattern.SuccessRate;
        }

        private bool IsWithinRequestedTimeRange(TimeSlotPrediction prediction, AISchedulingRequest request)
        {
            return prediction.StartTime >= request.StartDate && 
                   prediction.EndTime <= request.EndDate;
        }

        private bool IsWithinPreferredDays(TimeSlotPrediction prediction, AISchedulingRequest request, UserPreferences preferences)
        {
            if (request.PreferredDays.Any())
                return request.PreferredDays.Contains(prediction.StartTime.DayOfWeek);
            
            return preferences.PreferredDays.Contains(prediction.StartTime.DayOfWeek);
        }

        private void ApplyPreferenceAdjustments(TimeSlotPrediction prediction, UserPreferences preferences, List<SchedulingPattern> patterns)
        {
            var dayOfWeek = prediction.StartTime.DayOfWeek;
            var timeOfDay = prediction.StartTime.TimeOfDay;

            // Adjust based on day preferences
            if (preferences.DayPreferenceScores.TryGetValue(dayOfWeek, out var dayScore))
            {
                prediction.OverallConfidence = (prediction.OverallConfidence + dayScore) / 2.0;
            }

            // Adjust based on time preferences
            if (timeOfDay >= preferences.OptimalStartTime && timeOfDay <= preferences.OptimalEndTime)
            {
                prediction.OverallConfidence = Math.Min(1.0, prediction.OverallConfidence + 0.1);
            }

            // Adjust based on successful patterns
            var matchingPattern = patterns.FirstOrDefault(p => 
                p.DayOfWeek == dayOfWeek && 
                Math.Abs((p.StartTime - timeOfDay).TotalMinutes) < 60);

            if (matchingPattern != null && matchingPattern.SuccessRate > 0.8)
            {
                prediction.OverallConfidence = Math.Min(1.0, prediction.OverallConfidence + 0.15);
                prediction.IsOptimalSlot = true;
            }
        }

        private async Task UpdateUserPreferencesFromBehaviorAsync(SchedulingHistoryEntry historyEntry)
        {
            var preferences = await GetOrCreateUserPreferencesAsync(historyEntry.UserId);
            
            // Update based on successful meetings
            if (historyEntry.UserSatisfactionScore > 0.7)
            {
                if (!preferences.PreferredDays.Contains(historyEntry.DayOfWeek))
                {
                    preferences.PreferredDays.Add(historyEntry.DayOfWeek);
                }

                if (!preferences.PreferredTimes.Contains(historyEntry.TimeOfDay))
                {
                    preferences.PreferredTimes.Add(historyEntry.TimeOfDay);
                }
            }

            // Update preference scores
            if (preferences.DayPreferenceScores.ContainsKey(historyEntry.DayOfWeek))
            {
                var currentScore = preferences.DayPreferenceScores[historyEntry.DayOfWeek];
                var newScore = (currentScore + historyEntry.UserSatisfactionScore) / 2.0;
                preferences.DayPreferenceScores[historyEntry.DayOfWeek] = newScore;
            }

            preferences.TotalScheduledMeetings++;
            await _historyRepository.StoreUserPreferencesAsync(preferences);
        }

        private async Task UpdateSchedulingPatternsAsync(SchedulingHistoryEntry historyEntry)
        {
            var patterns = await _historyRepository.GetSchedulingPatternsAsync(historyEntry.UserId);
            
            // Find or create matching pattern
            var matchingPattern = patterns.FirstOrDefault(p => 
                p.DayOfWeek == historyEntry.DayOfWeek && 
                Math.Abs((p.StartTime - historyEntry.TimeOfDay).TotalHours) < 1);

            if (matchingPattern != null)
            {
                // Update existing pattern
                matchingPattern.FrequencyCount++;
                matchingPattern.LastOccurrence = historyEntry.ScheduledTime;
                matchingPattern.AverageUserSatisfaction = 
                    (matchingPattern.AverageUserSatisfaction + historyEntry.UserSatisfactionScore) / 2.0;
            }
            else
            {
                // Create new pattern
                patterns.Add(new SchedulingPattern
                {
                    UserId = historyEntry.UserId,
                    DayOfWeek = historyEntry.DayOfWeek,
                    StartTime = historyEntry.TimeOfDay,
                    EndTime = historyEntry.TimeOfDay.Add(TimeSpan.FromMinutes(historyEntry.DurationMinutes)),
                    FrequencyCount = 1,
                    AverageUserSatisfaction = historyEntry.UserSatisfactionScore,
                    LastOccurrence = historyEntry.ScheduledTime,
                    PatternType = "Emerging"
                });
            }

            await _historyRepository.StoreSchedulingPatternsAsync(patterns);
        }

        private string DeterminePatternType(List<SchedulingHistoryEntry> entries)
        {
            var timeSpan = entries.Max(e => e.ScheduledTime) - entries.Min(e => e.ScheduledTime);
            
            if (timeSpan.TotalDays <= 30)
                return "Short-term";
            if (timeSpan.TotalDays <= 90)
                return "Regular";
            
            return "Long-term";
        }

        private double CalculateAttendeeReschedulingRate(List<SchedulingHistoryEntry> historicalData, List<string> attendeeEmails)
        {
            var relevantMeetings = historicalData.Where(e => e.AttendeeEmails.Any(email => attendeeEmails.Contains(email))).ToList();
            
            if (relevantMeetings.Count == 0)
                return 0.3; // Default rate

            return relevantMeetings.Count(e => e.WasRescheduled) / (double)relevantMeetings.Count;
        }

        private async Task UpdatePreferencesFromFeedbackAsync(UserPreferences preferences, SchedulingHistoryEntry entry, double satisfactionScore)
        {
            // Adjust preferences based on feedback
            if (satisfactionScore > 0.8)
            {
                // Positive feedback - reinforce preferences
                if (preferences.DayPreferenceScores.ContainsKey(entry.DayOfWeek))
                {
                    preferences.DayPreferenceScores[entry.DayOfWeek] = 
                        Math.Min(1.0, preferences.DayPreferenceScores[entry.DayOfWeek] + 0.05);
                }
            }
            else if (satisfactionScore < 0.5)
            {
                // Negative feedback - adjust preferences
                if (preferences.DayPreferenceScores.ContainsKey(entry.DayOfWeek))
                {
                    preferences.DayPreferenceScores[entry.DayOfWeek] = 
                        Math.Max(0.1, preferences.DayPreferenceScores[entry.DayOfWeek] - 0.05);
                }
            }

            await _historyRepository.StoreUserPreferencesAsync(preferences);
        }

        private Dictionary<string, object> CalculateReschedulingTrends(List<SchedulingHistoryEntry> historicalData)
        {
            if (historicalData.Count == 0)
                return new Dictionary<string, object>();

            var totalRescheduled = historicalData.Count(e => e.WasRescheduled);
            var reschedulingRate = totalRescheduled / (double)historicalData.Count;

            var dayTrends = historicalData.GroupBy(e => e.DayOfWeek)
                .ToDictionary(g => g.Key.ToString(), g => g.Count(e => e.WasRescheduled) / (double)g.Count());

            return new Dictionary<string, object>
            {
                ["OverallReschedulingRate"] = reschedulingRate,
                ["TotalRescheduled"] = totalRescheduled,
                ["DayTrends"] = dayTrends,
                ["MostProblematicDay"] = dayTrends.OrderByDescending(kv => kv.Value).FirstOrDefault().Key
            };
        }

        private Dictionary<string, object> CalculateAttendeeInsights(List<SchedulingHistoryEntry> historicalData)
        {
            var attendeeData = new Dictionary<string, List<double>>();
            
            foreach (var entry in historicalData)
            {
                foreach (var email in entry.AttendeeEmails)
                {
                    if (!attendeeData.ContainsKey(email))
                        attendeeData[email] = new List<double>();
                    
                    attendeeData[email].Add(entry.UserSatisfactionScore);
                }
            }

            var insights = new Dictionary<string, object>();
            foreach (var kv in attendeeData)
            {
                insights[kv.Key] = new
                {
                    AverageSatisfaction = kv.Value.Average(),
                    MeetingCount = kv.Value.Count,
                    CompatibilityScore = kv.Value.Average() // Simplified
                };
            }

            return insights;
        }

        private Dictionary<string, object> AnalyzeProductivityPatterns(List<SchedulingHistoryEntry> historicalData)
        {
            var hourlyData = historicalData.GroupBy(e => e.TimeOfDay.Hours)
                .ToDictionary(g => g.Key, g => g.Average(e => e.UserSatisfactionScore));

            var dailyData = historicalData.GroupBy(e => e.DayOfWeek)
                .ToDictionary(g => g.Key.ToString(), g => g.Average(e => e.UserSatisfactionScore));

            return new Dictionary<string, object>
            {
                ["HourlyProductivity"] = hourlyData,
                ["DailyProductivity"] = dailyData,
                ["OptimalHour"] = hourlyData.OrderByDescending(kv => kv.Value).FirstOrDefault().Key,
                ["OptimalDay"] = dailyData.OrderByDescending(kv => kv.Value).FirstOrDefault().Key
            };
        }

        private List<string> GenerateInsightRecommendations(
            UserPreferences preferences, List<SchedulingPattern> patterns, List<SchedulingHistoryEntry> historicalData)
        {
            var recommendations = new List<string>();

            if (patterns.Any(p => p.SuccessRate > 0.8))
            {
                var bestPattern = patterns.OrderByDescending(p => p.SuccessRate).First();
                recommendations.Add($"Your most successful meeting pattern is {bestPattern.DayOfWeek} at {bestPattern.StartTime:HH:mm}");
            }

            if (preferences.AverageReschedulingRate > 0.3)
            {
                recommendations.Add("Consider scheduling meetings with more lead time to reduce rescheduling");
            }

            if (historicalData.Any() && historicalData.Average(e => e.UserSatisfactionScore) < 0.7)
            {
                recommendations.Add("Try scheduling meetings during your identified optimal time slots for better outcomes");
            }

            return recommendations;
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
    }
}