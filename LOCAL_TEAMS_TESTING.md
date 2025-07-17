# Local MS Teams Testing Guide

## Overview
This guide helps you test the Interview Scheduling Bot locally in Microsoft Teams without requiring Azure deployment. The bot uses mock services for development and testing.

## Prerequisites
- .NET 8.0 SDK installed
- Microsoft Teams (desktop or web)
- ngrok or similar tunneling tool for local development
- Text editor for configuration

## Quick Start (5-minute setup)

### 1. Configure for Local Development

Update `appsettings.json` to use mock services:

```json
{
  "MicrosoftAppId": "your-app-id-here",
  "MicrosoftAppPassword": "your-app-password-here",
  "MicrosoftAppTenantId": "your-tenant-id-here",
  "GraphScheduling": {
    "UseMockService": true,
    "MaxSuggestions": 10,
    "ConfidenceThreshold": 0.7
  },
  "OpenAI": {
    "ApiKey": "",
    "Endpoint": "",
    "DeploymentName": "gpt-3.5-turbo"
  },
  "Authentication": {
    "ClientId": "your-azure-app-client-id",
    "ClientSecret": "your-azure-app-client-secret",
    "TenantId": "your-azure-tenant-id"
  }
}
```

**Note**: With `UseMockService: true`, the bot will work with fake data - no real Azure credentials needed for basic testing!

### 2. Set Up Local Tunnel

1. Install ngrok: `npm install -g ngrok` or download from https://ngrok.com
2. Start your bot: `dotnet run`
3. In another terminal, expose port 3978: `ngrok http 3978`
4. Copy the HTTPS URL (e.g., `https://abc123.ngrok.io`)

### 3. Create Teams App Package

1. Update `manifest.json`:
   - Replace `{{MICROSOFT_APP_ID}}` with any GUID (you can generate one online)
   - Update `botId` with the same GUID
   - Set `messagingEndpoint` to `https://your-ngrok-url.ngrok.io/api/messages`

2. Create Teams package:
   ```bash
   ./create-teams-package.sh
   ```
   Or manually zip: `manifest.json`, `icon-outline.png`, `icon-color.png`

### 4. Install in Teams

1. Open Microsoft Teams
2. Go to "Apps" → "Upload a custom app" → "Upload for me or my teams"
3. Upload your `teams-app.zip` file
4. Click "Add" to install

## Testing the AI Features

### Basic Commands
- **`help`** - Show available commands
- **`schedule`** - Start basic scheduling
- **`ai schedule`** - Use AI-driven scheduling
- **`find optimal`** - Find optimal meeting times

### AI Scheduling Test Flow

1. **Start AI Scheduling**:
   ```
   ai schedule
   ```

2. **Provide Meeting Details**:
   - Duration: 60 minutes
   - Attendees: test@example.com, another@example.com
   - Date range: Next 7 days

3. **Review AI Suggestions**:
   - Bot will show 5 AI-generated suggestions
   - Each with confidence scores and reasoning
   - AI insights and recommendations included

4. **Book a Meeting**:
   ```
   book 1
   ```

5. **Provide Feedback**:
   - Rate the suggestion (1-5 stars)
   - AI will learn from your feedback

### User Preference Learning Test

1. **Schedule Multiple Meetings**:
   - Book meetings at different times
   - Rate each meeting experience

2. **Check Learning Progress**:
   - Ask for suggestions again
   - Notice how AI adapts to your preferences

3. **View Patterns**:
   - AI will identify your scheduling patterns
   - Recommendations become more personalized

## Mock Service Features

The mock service provides realistic testing without real calendar integration:

### Mock Data Generated:
- **5 Meeting Suggestions** with varying confidence scores
- **Realistic Time Slots** respecting working hours
- **AI Confidence Scoring** from 0.60 to 0.90
- **Suggestion Reasoning** explaining why each slot is recommended
- **User Preference Learning** with simulated historical data
- **Pattern Analysis** identifying 3 common scheduling patterns
- **AI Insights** with actionable recommendations

### Sample AI Response:
```
🤖 AI Scheduling Results:

📅 Suggestion 1: Tomorrow 2:00 PM - 3:00 PM
   Confidence: 85%
   Reasoning: Optimal time based on your historical preferences

📅 Suggestion 2: Friday 10:00 AM - 11:00 AM  
   Confidence: 78%
   Reasoning: Good availability for all attendees

🧠 AI Insights:
- You prefer morning meetings (60% success rate)
- Tuesday-Thursday are your most productive days
- Consider shorter meetings for better engagement
```

## Advanced Testing

### Test User Preference Learning

1. **Create Test Scenarios**:
   ```csharp
   // Simulate user feedback
   await hybridAIService.ProvideFeedbackAsync(
       userId: "test-user",
       meetingId: "meeting-123",
       satisfactionScore: 0.9,
       feedback: "Perfect time slot!"
   );
   ```

2. **Check Pattern Recognition**:
   - Schedule at different times
   - Provide varying feedback
   - Observe AI adaptation

### Test AI Insights

1. **Generate Insights**:
   ```
   The bot will automatically provide:
   - Scheduling pattern analysis
   - Productivity recommendations
   - Behavioral insights
   ```

2. **Validate Recommendations**:
   - Check if suggestions improve over time
   - Verify personalization accuracy

## Troubleshooting

### Common Issues:

1. **"Bot not responding"**:
   - Check if ngrok tunnel is active
   - Verify bot is running (`dotnet run`)
   - Ensure messaging endpoint is correct

2. **"Authentication failed"**:
   - With mock services, authentication is simulated
   - Check `UseMockService: true` in config

3. **"No meeting suggestions"**:
   - Mock service always returns 5 suggestions
   - Check console logs for errors

### Debug Mode:

Enable detailed logging in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## Real Azure Integration (Optional)

If you want to test with real calendars later:

1. **Set up Azure App Registration**:
   - Create app in Azure Portal
   - Add Microsoft Graph permissions
   - Generate client secret

2. **Update Configuration**:
   ```json
   {
     "GraphScheduling": {
       "UseMockService": false
     },
     "Authentication": {
       "ClientId": "real-azure-app-id",
       "ClientSecret": "real-azure-secret",
       "TenantId": "real-tenant-id"
     }
   }
   ```

3. **Test with Real Calendars**:
   - Users will need to authenticate
   - Real calendar events will be created
   - AI will learn from actual usage

## Success Metrics

Your local testing is successful if:
- ✅ Bot responds to commands in Teams
- ✅ AI scheduling generates 5 suggestions
- ✅ Confidence scores are displayed (60-90%)
- ✅ AI insights are provided
- ✅ User feedback is processed
- ✅ Preferences are learned and adapted

## Next Steps

1. **Test all AI features locally**
2. **Validate user experience**
3. **Deploy to Azure when ready**
4. **Enable real calendar integration**
5. **Add Azure OpenAI for enhanced recommendations**

## Support

For issues during testing:
1. Check console output for error messages
2. Verify ngrok tunnel is active
3. Review bot logs for debugging info
4. Test with different meeting scenarios

The mock service ensures you can test all AI features without external dependencies!