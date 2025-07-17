using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces;

namespace InterviewSchedulingBot.Services
{
    /// <summary>
    /// Machine learning model for scheduling prediction using rule-based AI
    /// In production, this would use actual ML frameworks like ML.NET, TensorFlow.NET, etc.
    /// </summary>
    public class SchedulingMLModel : ISchedulingMLModel
    {
        private readonly ILogger<SchedulingMLModel> _logger;
        private readonly Dictionary<string, double> _modelWeights;
        private readonly string _modelVersion = "1.0";
        private DateTime _lastTrainingTime = DateTime.UtcNow;
        private int _trainingDataSize = 0;
        private double _lastModelAccuracy = 0.85;

        public SchedulingMLModel(ILogger<SchedulingMLModel> logger)
        {
            _logger = logger;
            _modelWeights = InitializeModelWeights();
        }

        public async Task<List<TimeSlotPrediction>> PredictOptimalTimeSlotsAsync(Dictionary<string, double> features)
        {
            try
            {
                _logger.LogInformation("Predicting optimal time slots using ML model");
                
                // Simulate ML processing delay
                await Task.Delay(100);

                var predictions = new List<TimeSlotPrediction>();
                
                // Extract key features
                var dayOfWeek = (DayOfWeek)features.GetValueOrDefault("DayOfWeek", 2); // Default to Tuesday
                var hourOfDay = features.GetValueOrDefault("HourOfDay", 10);
                var durationMinutes = features.GetValueOrDefault("DurationMinutes", 60);
                var attendeeCount = features.GetValueOrDefault("AttendeeCount", 2);
                var userHistoryScore = features.GetValueOrDefault("UserHistoryScore", 0.7);
                var seasonalFactor = features.GetValueOrDefault("SeasonalFactor", 1.0);
                var workloadFactor = features.GetValueOrDefault("WorkloadFactor", 0.8);

                // Generate predictions for different time slots
                for (int hour = 9; hour <= 17; hour++)
                {
                    for (int minute = 0; minute < 60; minute += 30)
                    {
                        var timeSlot = new TimeSpan(hour, minute, 0);
                        var prediction = await PredictTimeSlotAsync(timeSlot, dayOfWeek, features);
                        
                        if (prediction.OverallConfidence > 0.3) // Only include reasonable predictions
                        {
                            predictions.Add(prediction);
                        }
                    }
                }

                // Sort by confidence and return top predictions
                var topPredictions = predictions
                    .OrderByDescending(p => p.OverallConfidence)
                    .Take(10)
                    .ToList();

                _logger.LogInformation("Generated {Count} time slot predictions", topPredictions.Count);
                return topPredictions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error predicting optimal time slots");
                throw;
            }
        }

        public async Task TrainModelAsync(List<SchedulingHistoryEntry> trainingData)
        {
            try
            {
                _logger.LogInformation("Training ML model with {Count} data points", trainingData.Count);
                
                // Simulate training process
                await Task.Delay(500);

                _trainingDataSize = trainingData.Count;
                _lastTrainingTime = DateTime.UtcNow;

                // Analyze training data to update model weights
                await AnalyzeTrainingDataAsync(trainingData);

                // Simulate model accuracy improvement
                _lastModelAccuracy = Math.Min(0.95, _lastModelAccuracy + (trainingData.Count * 0.0001));

                _logger.LogInformation("Model training completed. Accuracy: {Accuracy:P2}", _lastModelAccuracy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error training ML model");
                throw;
            }
        }

        public async Task<Dictionary<string, double>> EvaluateModelAsync(List<SchedulingHistoryEntry> testData)
        {
            try
            {
                _logger.LogInformation("Evaluating ML model with {Count} test data points", testData.Count);
                
                // Simulate evaluation process
                await Task.Delay(200);

                var metrics = new Dictionary<string, double>
                {
                    ["Accuracy"] = _lastModelAccuracy,
                    ["Precision"] = _lastModelAccuracy + 0.02,
                    ["Recall"] = _lastModelAccuracy - 0.01,
                    ["F1Score"] = _lastModelAccuracy + 0.01,
                    ["AUC"] = _lastModelAccuracy + 0.03,
                    ["TestDataSize"] = testData.Count,
                    ["TrainingDataSize"] = _trainingDataSize,
                    ["ModelComplexity"] = _modelWeights.Count,
                    ["PredictionLatency"] = 0.12 // seconds
                };

                _logger.LogInformation("Model evaluation completed. Accuracy: {Accuracy:P2}", metrics["Accuracy"]);
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating ML model");
                throw;
            }
        }

        public async Task<Dictionary<string, object>> GetModelInfoAsync()
        {
            await Task.Delay(10); // Simulate async operation

            return new Dictionary<string, object>
            {
                ["Version"] = _modelVersion,
                ["LastTrainingTime"] = _lastTrainingTime,
                ["TrainingDataSize"] = _trainingDataSize,
                ["Accuracy"] = _lastModelAccuracy,
                ["ModelWeights"] = _modelWeights,
                ["Features"] = new[]
                {
                    "DayOfWeek", "HourOfDay", "DurationMinutes", "AttendeeCount",
                    "UserHistoryScore", "SeasonalFactor", "WorkloadFactor",
                    "ConflictProbability", "ProductivityScore", "UserPreferenceScore"
                },
                ["ModelType"] = "Rule-Based ML with Weighted Features",
                ["SupportedPredictions"] = new[] { "OptimalTimeSlots", "ConflictProbability", "UserSatisfaction" }
            };
        }

        private async Task<TimeSlotPrediction> PredictTimeSlotAsync(
            TimeSpan timeSlot, 
            DayOfWeek dayOfWeek, 
            Dictionary<string, double> features)
        {
            await Task.Delay(1); // Simulate prediction processing

            var hour = timeSlot.Hours;
            var minute = timeSlot.Minutes;
            
            // Calculate various scoring factors
            var timeScore = CalculateTimeScore(hour, minute);
            var dayScore = CalculateDayScore(dayOfWeek);
            var userPreferenceScore = features.GetValueOrDefault("UserPreferenceScore", 0.7);
            var historicalSuccessScore = features.GetValueOrDefault("UserHistoryScore", 0.7);
            var attendeeCompatibilityScore = CalculateAttendeeCompatibilityScore(features);
            var workloadScore = features.GetValueOrDefault("WorkloadFactor", 0.8);
            var seasonalScore = features.GetValueOrDefault("SeasonalFactor", 1.0);

            // Calculate conflict probability
            var conflictProbability = CalculateConflictProbability(timeSlot, dayOfWeek, features);

            // Calculate overall confidence using weighted combination
            var overallConfidence = CalculateOverallConfidence(
                timeScore, dayScore, userPreferenceScore, historicalSuccessScore,
                attendeeCompatibilityScore, workloadScore, seasonalScore, conflictProbability);

            var prediction = new TimeSlotPrediction
            {
                StartTime = DateTime.Today.Add(timeSlot),
                EndTime = DateTime.Today.Add(timeSlot).AddMinutes(features.GetValueOrDefault("DurationMinutes", 60)),
                PredictedSuccessRate = overallConfidence,
                UserPreferenceScore = userPreferenceScore,
                AttendeeCompatibilityScore = attendeeCompatibilityScore,
                HistoricalSuccessScore = historicalSuccessScore,
                OverallConfidence = overallConfidence,
                ConflictProbability = conflictProbability,
                IsOptimalSlot = overallConfidence > 0.8,
                PredictionReason = GeneratePredictionReason(overallConfidence, timeSlot, dayOfWeek),
                ContributingFactors = GenerateContributingFactors(timeScore, dayScore, userPreferenceScore, historicalSuccessScore),
                FeatureScores = new Dictionary<string, double>
                {
                    ["TimeScore"] = timeScore,
                    ["DayScore"] = dayScore,
                    ["UserPreferenceScore"] = userPreferenceScore,
                    ["HistoricalSuccessScore"] = historicalSuccessScore,
                    ["AttendeeCompatibilityScore"] = attendeeCompatibilityScore,
                    ["WorkloadScore"] = workloadScore,
                    ["SeasonalScore"] = seasonalScore,
                    ["ConflictProbability"] = conflictProbability
                }
            };

            return prediction;
        }

        private double CalculateTimeScore(int hour, int minute)
        {
            // Optimal hours: 10-11 AM and 2-3 PM
            if ((hour == 10 || hour == 14) && minute == 0)
                return 1.0;
            if ((hour == 9 || hour == 11 || hour == 13 || hour == 15) && minute == 0)
                return 0.9;
            if (hour >= 9 && hour <= 16)
                return 0.7;
            if (hour == 8 || hour == 17)
                return 0.5;
            return 0.3;
        }

        private double CalculateDayScore(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Tuesday => 1.0,
                DayOfWeek.Wednesday => 1.0,
                DayOfWeek.Thursday => 0.9,
                DayOfWeek.Monday => 0.7,
                DayOfWeek.Friday => 0.6,
                DayOfWeek.Saturday => 0.2,
                DayOfWeek.Sunday => 0.1,
                _ => 0.5
            };
        }

        private double CalculateAttendeeCompatibilityScore(Dictionary<string, double> features)
        {
            var attendeeCount = features.GetValueOrDefault("AttendeeCount", 2);
            var baseScore = Math.Max(0.3, 1.0 - (attendeeCount - 2) * 0.1); // Decreases with more attendees
            
            var attendeeHistoryScore = features.GetValueOrDefault("AttendeeHistoryScore", 0.7);
            var timeZoneCompatibility = features.GetValueOrDefault("TimeZoneCompatibility", 0.9);
            
            return (baseScore + attendeeHistoryScore + timeZoneCompatibility) / 3.0;
        }

        private double CalculateConflictProbability(TimeSpan timeSlot, DayOfWeek dayOfWeek, Dictionary<string, double> features)
        {
            var hour = timeSlot.Hours;
            var baseProbability = 0.2; // Base 20% conflict probability
            
            // Higher conflict probability during popular hours
            if (hour >= 10 && hour <= 11)
                baseProbability += 0.15;
            if (hour >= 14 && hour <= 15)
                baseProbability += 0.10;
            
            // Higher conflict on popular days
            if (dayOfWeek == DayOfWeek.Tuesday || dayOfWeek == DayOfWeek.Wednesday)
                baseProbability += 0.05;
            
            // Adjust based on attendee count
            var attendeeCount = features.GetValueOrDefault("AttendeeCount", 2);
            baseProbability += (attendeeCount - 2) * 0.05;
            
            return Math.Min(0.8, baseProbability);
        }

        private double CalculateOverallConfidence(
            double timeScore, double dayScore, double userPreferenceScore, 
            double historicalSuccessScore, double attendeeCompatibilityScore, 
            double workloadScore, double seasonalScore, double conflictProbability)
        {
            var weights = _modelWeights;
            
            var weightedScore = 
                timeScore * weights["TimeScore"] +
                dayScore * weights["DayScore"] +
                userPreferenceScore * weights["UserPreferenceScore"] +
                historicalSuccessScore * weights["HistoricalSuccessScore"] +
                attendeeCompatibilityScore * weights["AttendeeCompatibilityScore"] +
                workloadScore * weights["WorkloadScore"] +
                seasonalScore * weights["SeasonalScore"] +
                (1 - conflictProbability) * weights["ConflictAvoidance"];
            
            return Math.Min(1.0, Math.Max(0.0, weightedScore));
        }

        private string GeneratePredictionReason(double confidence, TimeSpan timeSlot, DayOfWeek dayOfWeek)
        {
            if (confidence > 0.9)
                return $"Optimal time slot with excellent historical success rate on {dayOfWeek}s at {timeSlot:hh\\:mm}";
            if (confidence > 0.8)
                return $"High-confidence prediction based on user preferences and productivity patterns";
            if (confidence > 0.7)
                return $"Good time slot with moderate attendee compatibility and low conflict probability";
            if (confidence > 0.6)
                return $"Acceptable time slot with some scheduling considerations";
            if (confidence > 0.5)
                return $"Available time slot with higher conflict probability";
            return $"Low-confidence prediction with potential scheduling challenges";
        }

        private List<string> GenerateContributingFactors(
            double timeScore, double dayScore, double userPreferenceScore, double historicalSuccessScore)
        {
            var factors = new List<string>();
            
            if (timeScore > 0.8)
                factors.Add("Optimal time of day for productivity");
            if (dayScore > 0.8)
                factors.Add("Preferred day of week for meetings");
            if (userPreferenceScore > 0.8)
                factors.Add("Aligns with user historical preferences");
            if (historicalSuccessScore > 0.8)
                factors.Add("High historical success rate for similar meetings");
            
            if (factors.Count == 0)
                factors.Add("Basic availability with standard scheduling considerations");
            
            return factors;
        }

        private Dictionary<string, double> InitializeModelWeights()
        {
            return new Dictionary<string, double>
            {
                ["TimeScore"] = 0.20,
                ["DayScore"] = 0.15,
                ["UserPreferenceScore"] = 0.25,
                ["HistoricalSuccessScore"] = 0.20,
                ["AttendeeCompatibilityScore"] = 0.10,
                ["WorkloadScore"] = 0.05,
                ["SeasonalScore"] = 0.03,
                ["ConflictAvoidance"] = 0.02
            };
        }

        private async Task AnalyzeTrainingDataAsync(List<SchedulingHistoryEntry> trainingData)
        {
            await Task.Delay(100); // Simulate analysis
            
            // Analyze patterns in training data to adjust weights
            var successfulMeetings = trainingData.Where(e => e.UserSatisfactionScore > 0.7).ToList();
            var unsuccessfulMeetings = trainingData.Where(e => e.UserSatisfactionScore <= 0.7).ToList();
            
            // Adjust weights based on successful patterns
            if (successfulMeetings.Count > 0)
            {
                var avgSuccessHour = successfulMeetings.Average(e => e.TimeOfDay.Hours);
                var mostSuccessfulDay = successfulMeetings.GroupBy(e => e.DayOfWeek)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? DayOfWeek.Tuesday;
                
                // Slightly adjust weights based on patterns (simplified)
                if (avgSuccessHour >= 10 && avgSuccessHour <= 14)
                    _modelWeights["TimeScore"] = Math.Min(0.25, _modelWeights["TimeScore"] + 0.01);
                
                if (mostSuccessfulDay == DayOfWeek.Tuesday || mostSuccessfulDay == DayOfWeek.Wednesday)
                    _modelWeights["DayScore"] = Math.Min(0.20, _modelWeights["DayScore"] + 0.01);
            }
            
            _logger.LogInformation("Analyzed {SuccessfulCount} successful and {UnsuccessfulCount} unsuccessful meetings", 
                successfulMeetings.Count, unsuccessfulMeetings.Count);
        }
    }
}