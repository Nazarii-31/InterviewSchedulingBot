# Teams API Mock Testing Documentation

This document describes the comprehensive mock testing system implemented for the Interview Scheduling Bot to simulate Microsoft Teams API responses without requiring server deployment.

## 🎯 Purpose

The mock testing system allows developers to:
- **Test Teams API integration** without deploying to a server
- **Validate bot responses** to realistic Teams API data
- **Debug scheduling logic** with controlled data scenarios
- **Develop offline** without requiring Teams authentication

## 🏗️ Architecture

### Mock Service Structure

```
Services/Mock/
├── MockTeamsIntegrationService.cs    # Mock implementation of ITeamsIntegrationService
└── MockDataProvider.cs              # Generates realistic test data

Tests/
├── TeamsIntegrationMockTest.cs       # Comprehensive integration tests
├── LocalTestRunner.cs               # Test orchestration
└── HybridAISchedulingTest.cs        # AI service tests
```

## 📊 Mock Data Coverage

### 1. Teams User Information
Simulates realistic Teams user context:
```csharp
TeamsUserInfo {
    Id = "29:1q2w3e4r5t6y7u8i9o0p",
    Name = "John Doe",
    Email = "john.doe@contoso.com",
    TenantId = "12345678-1234-1234-1234-123456789012",
    TeamId = "19:abcd1234efgh5678ijkl@thread.tacv2",
    ChannelId = "19:wxyz9876mnop5432qrst@thread.tacv2"
}
```

### 2. Authentication Results
Simulates both successful and failed authentication scenarios:
- **80% success rate** for realistic testing
- Mock access tokens for authenticated users
- Login URLs for unauthenticated users

### 3. Calendar Availability Data
Generates realistic busy time patterns:
- **Weekday focus**: No meetings on weekends
- **Working hours**: Meetings between 9 AM - 4 PM
- **Random distribution**: 0-3 meetings per day
- **Meeting durations**: 30 minutes to 2 hours
- **Status variety**: 90% "Busy", 10% "Tentative"

### 4. Working Hours Configuration
Provides diverse working hour patterns:
- **Pacific Standard Time**: 9:00 AM - 5:00 PM
- **Eastern Standard Time**: 8:00 AM - 4:00 PM
- **GMT Standard Time**: 8:30 AM - 5:30 PM
- **Weekdays only**: Monday through Friday

### 5. User Presence Information
Realistic presence states:
- **Availability**: Available, Busy, DoNotDisturb, Away, BeRightBack
- **Activity**: Available, InACall, InAMeeting, Busy, Away
- **Timestamps**: Recent activity within the last hour

### 6. People Search Results
Organization directory simulation:
- **5 diverse employees** across different departments
- **Engineering, Product, Design, HR** representation
- **Realistic job titles** and email addresses
- **Search filtering** by name, title, department, or email

## 🧪 Test Scenarios

### Comprehensive Test Suite

The `TeamsIntegrationMockTest` class provides 8 comprehensive test scenarios:

1. **User Information Retrieval** - Tests Teams context extraction
2. **Authentication Handling** - Tests OAuth flow simulation
3. **Calendar Availability Check** - Tests availability data processing
4. **Working Hours Retrieval** - Tests configuration data handling
5. **User Presence Check** - Tests real-time status information
6. **People Search** - Tests organization directory access
7. **Messaging Capabilities** - Tests message and card sending
8. **End-to-End Scheduling** - Tests complete workflow integration

### Test Validation

Each test includes comprehensive validation:
```csharp
// Example validation for calendar availability
private static void ValidateCalendarAvailability(
    Dictionary<string, List<BusyTimeSlot>> availability, 
    List<string> userEmails)
{
    if (availability.Count != userEmails.Count)
        throw new Exception("Availability data count doesn't match user email count");
    
    foreach (var userEmail in userEmails)
    {
        if (!availability.ContainsKey(userEmail))
            throw new Exception($"Missing availability data for {userEmail}");
    }
}
```

## 🚀 Running Mock Tests

### Command Line Execution
```bash
cd /path/to/InterviewSchedulingBot
dotnet run
```

### Programmatic Execution
```csharp
// Run comprehensive Teams integration tests
await TeamsIntegrationMockTest.RunComprehensiveTeamsIntegrationTest();

// Run AI scheduling tests
await HybridAISchedulingTest.RunHybridAISchedulingTest();

// Run all tests together
await LocalTestRunner.RunLocalAITestAsync();
```

### Expected Output
```
🤖 Starting Comprehensive Teams Integration Mock Test...

=== 🔍 Testing Teams API Integration Components ===

🔍 Test 1: User Information Retrieval
   Testing Teams user context extraction...
   ✅ User ID: 29:1q2w3e4r5t6y7u8i9o0p
   ✅ Name: John Doe
   ✅ Email: john.doe@contoso.com
   ✅ User information retrieval test PASSED

🔐 Test 2: Authentication Handling
   Testing Teams authentication flow...
   ✅ Is Authenticated: True
   ✅ Access Token: mock-acces...
   ✅ Authentication handling test PASSED

📅 Test 3: Calendar Availability Check
   Testing calendar availability retrieval...
   ✅ Retrieved availability for 3 users
   📧 interviewer1@contoso.com: 5 busy slots
   ✅ Calendar availability check test PASSED

...and so on for all 8 tests
```

## 🎯 Key Benefits

### Development Efficiency
- **Offline development** - No Teams authentication required
- **Fast iteration** - Immediate feedback without API delays
- **Controlled testing** - Predictable data patterns for debugging

### Quality Assurance
- **Comprehensive coverage** - Tests all Teams API endpoints
- **Realistic scenarios** - Mock data mirrors production patterns
- **Error simulation** - Tests both success and failure cases

### Production Readiness
- **API compatibility** - Mock responses match actual Teams API structure
- **Performance baseline** - Network delay simulation for realistic testing
- **Integration validation** - Ensures bot handles all data types correctly

## 🔧 Customization

### Adding New Mock Scenarios
To add new mock data patterns:

1. **Extend MockDataProvider**:
```csharp
public Dictionary<string, CustomData> GetMockCustomData()
{
    // Your custom mock data logic here
}
```

2. **Update MockTeamsIntegrationService**:
```csharp
public async Task<CustomData> GetCustomDataAsync(ITurnContext turnContext)
{
    await Task.Delay(100); // Simulate network delay
    return _mockDataProvider.GetMockCustomData();
}
```

3. **Add Test Validation**:
```csharp
private static void ValidateCustomData(CustomData data)
{
    // Your validation logic here
}
```

### Adjusting Mock Behavior
Modify `MockDataProvider` constants:
```csharp
// Change authentication success rate
return _random.NextDouble() < 0.9; // 90% success rate

// Adjust meeting frequency
var numberOfMeetings = _random.Next(1, 5); // 1-4 meetings per day

// Modify working hours patterns
var workingHourPatterns = new[] { /* your patterns */ };
```

## 📋 Mock Data vs. Real API

### Advantages of Mock Data
- ✅ **Consistent testing** - Same data every run
- ✅ **Fast execution** - No network delays
- ✅ **Offline development** - No authentication required
- ✅ **Error simulation** - Controlled failure scenarios

### Limitations
- ⚠️ **Static patterns** - Less variety than real user data
- ⚠️ **No real authentication** - Can't test OAuth edge cases
- ⚠️ **Simplified responses** - May miss API nuances

### Transition to Production
When ready for production testing:
1. Replace `MockTeamsIntegrationService` with `TeamsIntegrationService`
2. Configure Teams app registration and authentication
3. Deploy to server with public endpoint
4. Test with real Teams environments

## 🔍 Troubleshooting

### Common Issues

**Test Failures**
- Check validation logic in test methods
- Verify mock data structure matches interface contracts
- Ensure all required properties are populated

**Missing Mock Data**
- Add new scenarios to `MockDataProvider`
- Update test coverage for new data types
- Validate new mock responses

**Performance Issues**
- Adjust `Task.Delay()` values in mock methods
- Optimize mock data generation algorithms
- Consider caching for frequently accessed data

## 📚 Related Documentation

- **[TEAMS_API_ANALYSIS.md](./TEAMS_API_ANALYSIS.md)** - Comprehensive Teams API endpoint analysis
- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - Overall bot architecture documentation
- **[README.md](./README.md)** - Project setup and deployment guide

## 🎉 Conclusion

The mock testing system provides a comprehensive foundation for developing and testing Teams API integration without requiring server deployment. It enables rapid development cycles, thorough testing coverage, and confidence in production readiness.

Use this system to validate your bot's ability to handle real Teams API responses before moving to production testing environments.