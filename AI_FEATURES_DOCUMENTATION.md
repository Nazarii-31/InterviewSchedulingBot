# AI Features Documentation

## Overview
The Interview Scheduling Bot now includes AI-driven scheduling capabilities that learn from user behavior and preferences to provide intelligent meeting time recommendations.

## Core AI Components

### 1. AISchedulingService
Main service that orchestrates AI-driven scheduling decisions.

**Key Features:**
- Historical pattern analysis
- User preference learning
- Intelligent time slot prediction
- Behavioral adaptation
- Real-time feedback processing

**Usage:**
```csharp
var aiService = serviceProvider.GetRequiredService<IAISchedulingService>();
var request = new AISchedulingRequest
{
    UserId = "user123",
    AttendeeEmails = ["attendee1@company.com", "attendee2@company.com"],
    StartDate = DateTime.Now.AddDays(1),
    EndDate = DateTime.Now.AddDays(7),
    DurationMinutes = 60,
    UseLearningAlgorithm = true,
    MinimumConfidenceThreshold = 0.7
};

var response = await aiService.FindOptimalMeetingTimesAsync(request);
```

### 2. User Preference Learning System
Automatically learns from user scheduling behavior to improve future recommendations.

**Learning Sources:**
- Meeting completion rates
- Rescheduling patterns
- User satisfaction scores
- Time slot preferences
- Attendee compatibility

**Tracked Preferences:**
- Preferred days of week
- Optimal meeting times
- Morning vs afternoon preference
- Meeting duration preferences
- Attendee compatibility scores

### 3. Scheduling Pattern Analysis
Identifies recurring patterns in user scheduling behavior.

**Pattern Types:**
- **Short-term patterns:** Within 30 days
- **Regular patterns:** 30-90 days
- **Long-term patterns:** Over 90 days

**Analyzed Metrics:**
- Success rate per time slot
- Rescheduling frequency
- User satisfaction scores
- Attendee compatibility
- Meeting completion rates

### 4. ML Model for Time Slot Prediction
Custom machine learning model that predicts optimal meeting times.

**Input Features:**
- Day of week
- Time of day
- Meeting duration
- Attendee count
- Historical success rate
- User preferences
- Seasonal factors
- Workload factors

**Output:**
- Confidence score (0-1)
- Conflict probability
- Success rate prediction
- Reasoning for recommendation

## API Reference

### AISchedulingService Methods

#### FindOptimalMeetingTimesAsync
```csharp
Task<AISchedulingResponse> FindOptimalMeetingTimesAsync(AISchedulingRequest request)
```
Finds optimal meeting times using AI algorithms.

**Parameters:**
- `request`: AI scheduling request with parameters

**Returns:**
- `AISchedulingResponse`: Contains predictions, insights, and recommendations

#### LearnFromSchedulingBehaviorAsync
```csharp
Task LearnFromSchedulingBehaviorAsync(SchedulingHistoryEntry historyEntry)
```
Learns from user scheduling behavior.

**Parameters:**
- `historyEntry`: Historical scheduling data

#### GetUserPreferencesAsync
```csharp
Task<UserPreferences?> GetUserPreferencesAsync(string userId)
```
Retrieves learned user preferences.

#### AnalyzeSchedulingPatternsAsync
```csharp
Task<List<SchedulingPattern>> AnalyzeSchedulingPatternsAsync(string userId, int lookbackDays = 90)
```
Analyzes scheduling patterns for insights.

#### PredictReschedulingProbabilityAsync
```csharp
Task<double> PredictReschedulingProbabilityAsync(string userId, List<string> attendeeEmails, DateTime proposedTime, int durationMinutes)
```
Predicts likelihood of meeting rescheduling.

#### ProvideFeedbackAsync
```csharp
Task ProvideFeedbackAsync(string userId, string meetingId, double satisfactionScore, string? feedback = null)
```
Provides feedback to improve future recommendations.

#### GetAIInsightsAsync
```csharp
Task<Dictionary<string, object>> GetAIInsightsAsync(string userId)
```
Gets AI insights and recommendations.

## Data Models

### AISchedulingRequest
```csharp
public class AISchedulingRequest
{
    public string UserId { get; set; }
    public List<string> AttendeeEmails { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DurationMinutes { get; set; }
    public List<DayOfWeek> PreferredDays { get; set; }
    public TimeSpan? PreferredStartTime { get; set; }
    public bool UseLearningAlgorithm { get; set; }
    public double MinimumConfidenceThreshold { get; set; }
    public int MaxSuggestions { get; set; }
}
```

### AISchedulingResponse
```csharp
public class AISchedulingResponse
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<TimeSlotPrediction> Predictions { get; set; }
    public UserPreferences? UserPreferences { get; set; }
    public List<SchedulingPattern> RelevantPatterns { get; set; }
    public Dictionary<string, double> AttendeeCompatibilityScores { get; set; }
    public Dictionary<string, object> AIInsights { get; set; }
    public List<string> Recommendations { get; set; }
    public long ProcessingTimeMs { get; set; }
}
```

### TimeSlotPrediction
```csharp
public class TimeSlotPrediction
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double OverallConfidence { get; set; }
    public double PredictedSuccessRate { get; set; }
    public double ConflictProbability { get; set; }
    public double UserPreferenceScore { get; set; }
    public double AttendeeCompatibilityScore { get; set; }
    public double HistoricalSuccessScore { get; set; }
    public bool IsOptimalSlot { get; set; }
    public string PredictionReason { get; set; }
    public List<string> ContributingFactors { get; set; }
    public Dictionary<string, double> FeatureScores { get; set; }
}
```

### UserPreferences
```csharp
public class UserPreferences
{
    public string UserId { get; set; }
    public List<DayOfWeek> PreferredDays { get; set; }
    public List<TimeSpan> PreferredTimes { get; set; }
    public Dictionary<DayOfWeek, double> DayPreferenceScores { get; set; }
    public double MorningPreference { get; set; }
    public double AfternoonPreference { get; set; }
    public double EveningPreference { get; set; }
    public TimeSpan OptimalStartTime { get; set; }
    public TimeSpan OptimalEndTime { get; set; }
    public double AverageReschedulingRate { get; set; }
    public int TotalScheduledMeetings { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

## Configuration

### appsettings.json
```json
{
  "AI": {
    "DefaultConfidenceThreshold": 0.7,
    "MaxSuggestions": 10,
    "HistoryLookbackDays": 90,
    "MinimumTrainingData": 10,
    "EnableLearning": true,
    "ModelRetrainingInterval": "24:00:00"
  }
}
```

### Service Registration
```csharp
// Program.cs
builder.Services.AddScoped<IAISchedulingService, AISchedulingService>();
builder.Services.AddScoped<ISchedulingMLModel, SchedulingMLModel>();
builder.Services.AddScoped<ISchedulingHistoryRepository, InMemorySchedulingHistoryRepository>();
```

## Testing

### Unit Tests
```csharp
[Test]
public async Task FindOptimalMeetingTimesAsync_ValidRequest_ReturnsSuccess()
{
    // Arrange
    var aiService = new AISchedulingService(mockRepo, mockModel, mockAuth, mockLogger, mockConfig);
    var request = new AISchedulingRequest
    {
        UserId = "testuser",
        AttendeeEmails = ["test@company.com"],
        StartDate = DateTime.Now.AddDays(1),
        EndDate = DateTime.Now.AddDays(7),
        DurationMinutes = 60
    };

    // Act
    var response = await aiService.FindOptimalMeetingTimesAsync(request);

    // Assert
    Assert.IsTrue(response.IsSuccess);
    Assert.IsNotNull(response.Predictions);
    Assert.Greater(response.Predictions.Count, 0);
}
```

### Integration Tests
```csharp
[Test]
public async Task AIScheduling_EndToEnd_Test()
{
    // Test complete AI scheduling flow
    var request = CreateValidAISchedulingRequest();
    var response = await aiService.FindOptimalMeetingTimesAsync(request);
    
    Assert.IsTrue(response.IsSuccess);
    Assert.IsNotNull(response.UserPreferences);
    Assert.Greater(response.Predictions.Count, 0);
    
    // Verify learning from behavior
    var historyEntry = CreateHistoryEntry(response.Predictions.First());
    await aiService.LearnFromSchedulingBehaviorAsync(historyEntry);
    
    // Verify preferences updated
    var updatedPreferences = await aiService.GetUserPreferencesAsync(request.UserId);
    Assert.IsNotNull(updatedPreferences);
}
```

## Performance Considerations

### Optimization Tips
1. **Batch Operations:** Process multiple users in batches
2. **Caching:** Cache user preferences and patterns
3. **Async Processing:** Use async methods for all operations
4. **Data Pruning:** Regularly clean old historical data
5. **Model Retraining:** Retrain models periodically, not after each interaction

### Monitoring
Monitor these metrics:
- Average response time
- ML model accuracy
- User satisfaction scores
- Rescheduling rates
- System resource usage

## Security Considerations

### Data Protection
- User preferences are stored securely
- Historical data is anonymized when possible
- Access tokens are properly managed
- Data retention policies are enforced

### Privacy
- Users can opt out of learning algorithms
- Personal scheduling data is not shared
- Feedback is optional and anonymous
- Data can be deleted upon request

## Future Enhancements

### Planned Features
1. **Multi-language support** for feedback processing
2. **Advanced ML models** with TensorFlow.NET
3. **Group scheduling optimization**
4. **Calendar integration improvements**
5. **Real-time learning** from calendar events
6. **Predictive analytics** for scheduling trends

### Integration Opportunities
1. **Azure OpenAI** for intelligent recommendations
2. **Microsoft Graph** enhanced scheduling APIs
3. **Power Platform** for workflow automation
4. **Teams** for better meeting integration
5. **Outlook** for calendar sync improvements

## Troubleshooting

### Common Issues
1. **Low confidence scores:** Check historical data quality
2. **Poor predictions:** Verify user preferences are updated
3. **Performance issues:** Review data retention policies
4. **Learning not working:** Check minimum training data threshold

### Debug Configuration
```json
{
  "Logging": {
    "LogLevel": {
      "InterviewSchedulingBot.Services.AISchedulingService": "Debug",
      "InterviewSchedulingBot.Services.SchedulingMLModel": "Debug"
    }
  }
}
```

This AI system provides a foundation for intelligent scheduling that improves over time through user interaction and feedback.