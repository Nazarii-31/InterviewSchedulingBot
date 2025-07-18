using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Interfaces
{
    /// <summary>
    /// Interface for AI-driven scheduling service that learns from historical patterns
    /// and user preferences to provide intelligent meeting time recommendations
    /// </summary>
    public interface IAISchedulingService
    {
        /// <summary>
        /// Finds optimal meeting times using AI algorithms that analyze historical data,
        /// user preferences, and attendee patterns
        /// </summary>
        /// <param name="request">AI scheduling request with parameters</param>
        /// <returns>AI-driven scheduling response with predictions and insights</returns>
        Task<AISchedulingResponse> FindOptimalMeetingTimesAsync(AISchedulingRequest request);

        /// <summary>
        /// Learns from user scheduling behavior to improve future recommendations
        /// </summary>
        /// <param name="historyEntry">Historical scheduling data entry</param>
        /// <returns>Task representing the learning operation</returns>
        Task LearnFromSchedulingBehaviorAsync(SchedulingHistoryEntry historyEntry);

        /// <summary>
        /// Gets user preferences learned from historical behavior
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>User preferences or null if not found</returns>
        Task<UserPreferences?> GetUserPreferencesAsync(string userId);

        /// <summary>
        /// Updates user preferences based on feedback and behavior
        /// </summary>
        /// <param name="preferences">Updated user preferences</param>
        /// <returns>Task representing the update operation</returns>
        Task UpdateUserPreferencesAsync(UserPreferences preferences);

        /// <summary>
        /// Analyzes scheduling patterns for a specific user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="lookbackDays">Number of days to look back for pattern analysis</param>
        /// <returns>List of identified scheduling patterns</returns>
        Task<List<SchedulingPattern>> AnalyzeSchedulingPatternsAsync(string userId, int lookbackDays = 90);

        /// <summary>
        /// Predicts the likelihood of a meeting being rescheduled
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="attendeeEmails">List of attendee emails</param>
        /// <param name="proposedTime">Proposed meeting time</param>
        /// <param name="durationMinutes">Meeting duration in minutes</param>
        /// <returns>Rescheduling probability (0-1 scale)</returns>
        Task<double> PredictReschedulingProbabilityAsync(
            string userId, 
            List<string> attendeeEmails, 
            DateTime proposedTime, 
            int durationMinutes);

        /// <summary>
        /// Provides feedback on a scheduled meeting to improve future recommendations
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="meetingId">Meeting ID</param>
        /// <param name="satisfactionScore">User satisfaction score (0-1 scale)</param>
        /// <param name="feedback">Optional feedback text</param>
        /// <returns>Task representing the feedback operation</returns>
        Task ProvideFeedbackAsync(string userId, string meetingId, double satisfactionScore, string? feedback = null);

        /// <summary>
        /// Adapts scheduling recommendations based on real-time calendar changes
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="calendarChanges">List of calendar changes</param>
        /// <returns>Task representing the adaptation operation</returns>
        Task AdaptToCalendarChangesAsync(string userId, List<string> calendarChanges);

        /// <summary>
        /// Gets AI insights and recommendations for scheduling improvement
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Dictionary of insights and recommendations</returns>
        Task<Dictionary<string, object>> GetAIInsightsAsync(string userId);
    }

    /// <summary>
    /// Interface for historical data repository used by AI scheduling service
    /// </summary>
    public interface ISchedulingHistoryRepository
    {
        /// <summary>
        /// Stores a scheduling history entry
        /// </summary>
        /// <param name="entry">Scheduling history entry</param>
        /// <returns>Task representing the storage operation</returns>
        Task StoreSchedulingHistoryAsync(SchedulingHistoryEntry entry);

        /// <summary>
        /// Retrieves scheduling history for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="lookbackDays">Number of days to look back</param>
        /// <returns>List of scheduling history entries</returns>
        Task<List<SchedulingHistoryEntry>> GetSchedulingHistoryAsync(string userId, int lookbackDays = 90);

        /// <summary>
        /// Retrieves scheduling history for multiple users
        /// </summary>
        /// <param name="userIds">List of user IDs</param>
        /// <param name="lookbackDays">Number of days to look back</param>
        /// <returns>Dictionary of user ID to scheduling history entries</returns>
        Task<Dictionary<string, List<SchedulingHistoryEntry>>> GetSchedulingHistoryAsync(List<string> userIds, int lookbackDays = 90);

        /// <summary>
        /// Stores user preferences
        /// </summary>
        /// <param name="preferences">User preferences</param>
        /// <returns>Task representing the storage operation</returns>
        Task StoreUserPreferencesAsync(UserPreferences preferences);

        /// <summary>
        /// Retrieves user preferences
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>User preferences or null if not found</returns>
        Task<UserPreferences?> GetUserPreferencesAsync(string userId);

        /// <summary>
        /// Stores scheduling patterns
        /// </summary>
        /// <param name="patterns">List of scheduling patterns</param>
        /// <returns>Task representing the storage operation</returns>
        Task StoreSchedulingPatternsAsync(List<SchedulingPattern> patterns);

        /// <summary>
        /// Retrieves scheduling patterns for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of scheduling patterns</returns>
        Task<List<SchedulingPattern>> GetSchedulingPatternsAsync(string userId);
    }

    /// <summary>
    /// Interface for machine learning model used in scheduling prediction
    /// </summary>
    public interface ISchedulingMLModel
    {
        /// <summary>
        /// Predicts the optimal time slots based on historical data and user preferences
        /// </summary>
        /// <param name="features">Feature vector for prediction</param>
        /// <returns>List of time slot predictions</returns>
        Task<List<TimeSlotPrediction>> PredictOptimalTimeSlotsAsync(Dictionary<string, double> features);

        /// <summary>
        /// Trains the model with new data
        /// </summary>
        /// <param name="trainingData">Training data</param>
        /// <returns>Task representing the training operation</returns>
        Task TrainModelAsync(List<SchedulingHistoryEntry> trainingData);

        /// <summary>
        /// Evaluates the model performance
        /// </summary>
        /// <param name="testData">Test data</param>
        /// <returns>Model performance metrics</returns>
        Task<Dictionary<string, double>> EvaluateModelAsync(List<SchedulingHistoryEntry> testData);

        /// <summary>
        /// Gets the current model version and metadata
        /// </summary>
        /// <returns>Model information</returns>
        Task<Dictionary<string, object>> GetModelInfoAsync();
    }
}