# 📊 Mock Data Documentation

## Overview
This document explains all mock data sources used in the AI Interview Scheduling Bot for local testing without Azure dependencies.

## Mock Data Locations

### 1. AI Scheduling Service Mock Data
**File:** `Services/AISchedulingService.cs`
**Method:** `FindOptimalMeetingTimesAsync()`

**Generated Data:**
```json
{
  "suggestions": 5,
  "confidence_range": "0.70-0.85",
  "processing_time": "1500-2500ms",
  "historical_data_points": 847,
  "success_patterns": [
    "Tuesday 10:00-11:00 AM (87% success)",
    "Thursday 2:00-3:00 PM (81% success)", 
    "Wednesday 9:00-10:00 AM (76% success)"
  ]
}
```

**Customizable Parameters:**
- Meeting duration (30/45/60/90 minutes)
- Number of attendees
- Date range (3/7/14 days)
- Working hours preferences

### 2. Microsoft Graph Mock Data
**File:** `Services/MockGraphSchedulingService.cs`
**Method:** `FindOptimalMeetingTimesAsync()`

**Generated Data:**
```json
{
  "meeting_suggestions": [
    {
      "start_time": "2024-01-15T10:00:00",
      "end_time": "2024-01-15T11:00:00",
      "confidence": 0.85,
      "reason": "High availability, optimal time slot"
    }
  ],
  "attendee_availability": "Simulated from predefined patterns",
  "working_hours": "09:00-17:00 Monday-Friday"
}
```

**Mock Features:**
- Simulates Microsoft Graph API responses
- Realistic meeting time suggestions
- Confidence scoring based on availability
- Working hours enforcement

### 3. User Preferences Mock Data
**File:** `Services/InMemorySchedulingHistoryRepository.cs`

**Sample User Data:**
```json
{
  "user_id": "test-user-local",
  "total_meetings": 156,
  "rescheduling_rate": 0.3,
  "preferred_duration": 60,
  "optimal_start_time": "09:00",
  "optimal_end_time": "17:00",
  "successful_meetings": 132,
  "preferred_days": ["Tuesday", "Thursday"],
  "success_rate": 0.85
}
```

**Learning Patterns:**
```json
{
  "patterns": [
    {
      "type": "MorningPreference",
      "frequency": 34,
      "success_rate": 0.78,
      "description": "User prefers morning meetings"
    },
    {
      "type": "ShortMeetings", 
      "frequency": 28,
      "success_rate": 0.83,
      "description": "45-minute meetings more successful"
    }
  ]
}
```

### 4. AI Insights Mock Data
**File:** `Services/HybridAISchedulingService.cs`
**Method:** `GetAIInsightsAsync()`

**Analytics Data:**
```json
{
  "insights": {
    "historical_data_points": 847,
    "identified_patterns": 3,
    "model_accuracy": 0.85,
    "prediction_strength": "Medium",
    "recommendations": [
      "Schedule between 10:00-11:00 AM for highest engagement",
      "Tuesday/Thursday show 23% higher success rates",
      "45-minute meetings have 31% better satisfaction"
    ]
  }
}
```

## Test Scenarios Explained

### Test 1: AI Scheduling Engine
**What it tests:** Core AI functionality replacing hardcoded logic
**Mock data source:** `AISchedulingService.cs` lines 45-120
**Expected results:** 
- 5 intelligent meeting suggestions
- Confidence scores 70-85%
- AI reasoning for each suggestion
- Processing time simulation

### Test 2: Microsoft Graph Integration  
**What it tests:** Enterprise calendar API integration
**Mock data source:** `MockGraphSchedulingService.cs` lines 42-98
**Expected results:**
- Microsoft Graph-style responses
- Availability checking simulation
- Working hours enforcement
- Conflict detection

### Test 3: User Preference Learning
**What it tests:** Machine learning from user behavior
**Mock data source:** `InMemorySchedulingHistoryRepository.cs`
**Expected results:**
- User scheduling history
- Learned preferences and patterns
- Success rate calculations
- Adaptive recommendations

### Test 4: AI Insights & Analytics
**What it tests:** Predictive analytics and pattern recognition
**Mock data source:** `HybridAISchedulingService.cs` lines 85-150
**Expected results:**
- Historical pattern analysis
- Predictive modeling results
- Optimization recommendations
- Success probability metrics

### Test 5: Basic Scheduling (Non-AI)
**What it tests:** Original scheduling without AI enhancements
**Mock data source:** `SchedulingService.cs`
**Expected results:**
- Simple time slot availability
- No confidence scoring
- No learning or patterns
- Basic working hours logic

### Test 6: System Health Check
**What it tests:** Service operational status
**Mock data source:** `appsettings.json` + service status
**Expected results:**
- Service health indicators
- Configuration values
- Testing mode confirmation
- Mock data status

## Customizing Mock Data

### Changing Test Parameters
Edit the test interface inputs:
- **Attendees:** Modify email fields in AI Scheduling test
- **Duration:** Use dropdown (30/45/60/90 minutes)
- **Date Range:** Select 3/7/14 days

### Modifying Mock Responses
Edit these files to change mock data:
- **AI suggestions:** `AISchedulingService.cs` line 85+
- **Graph responses:** `MockGraphSchedulingService.cs` line 55+
- **User patterns:** `InMemorySchedulingHistoryRepository.cs`
- **Insights:** `HybridAISchedulingService.cs` line 100+

### Configuration Settings
**File:** `appsettings.json`
```json
{
  "GraphScheduling": {
    "UseMockService": true,
    "MaxSuggestions": 10,
    "ConfidenceThreshold": 0.7
  },
  "Scheduling": {
    "WorkingHours": {
      "StartTime": "09:00",
      "EndTime": "17:00"
    }
  }
}
```

## Production vs Mock Data

### Mock Mode (Current)
- No Azure credentials required
- Predictable test data
- Fast responses (simulated delays)
- Perfect for development and demos

### Production Mode
- Requires Microsoft Graph API credentials
- Real calendar data
- Actual user behavior learning
- Live scheduling with real attendees

**Switch to production:** Set `"UseMockService": false` in `appsettings.json`