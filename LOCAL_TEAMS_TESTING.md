# Complete Local MS Teams Testing Guide

## Overview
This comprehensive guide provides step-by-step instructions for testing the Interview Scheduling Bot locally in Microsoft Teams without requiring Azure deployment. The bot uses mock services for development and testing, making it perfect for local development and testing.

## Prerequisites

### Required Software
1. **Microsoft Teams** - Desktop app or web version
   - Desktop app recommended for better developer experience
   - Download from: https://www.microsoft.com/en-us/microsoft-teams/download-app

2. **.NET 8.0 SDK** - For building and running the bot
   - Download from: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
   - Verify installation: `dotnet --version` should show 8.0.x

3. **ngrok** - For exposing your local bot to Teams
   - Option 1: `npm install -g ngrok` (if you have Node.js)
   - Option 2: Download from https://ngrok.com/download
   - Create free account at https://ngrok.com for auth token

4. **Text Editor** - For editing configuration files
   - Visual Studio Code recommended
   - Or any text editor (Notepad++, Sublime Text, etc.)

### Optional but Helpful
- **Git** - For version control (usually already installed)
- **Zip utility** - For creating Teams app packages (built into most systems)

## Detailed Setup Instructions (15-20 minutes)

### Step 1: Verify Prerequisites

1. **Check .NET Installation**
   ```bash
   dotnet --version
   ```
   Should show version 8.0.x or higher

2. **Check ngrok Installation**
   ```bash
   ngrok version
   ```
   If not installed, install with: `npm install -g ngrok`

3. **Verify Teams Access**
   - Open Microsoft Teams in desktop or web
   - Ensure you can access "Apps" section in the left sidebar

### Step 2: Configure Bot for Local Development

1. **Create Local Configuration File**
   Create a copy of `appsettings.json` called `appsettings.local.json`:
   ```bash
   cp appsettings.json appsettings.local.json
   ```

2. **Update `appsettings.local.json`** with the following configuration:
   ```json
   {
     "MicrosoftAppId": "00000000-0000-0000-0000-000000000001",
     "MicrosoftAppPassword": "local-testing-password",
     "MicrosoftAppTenantId": "00000000-0000-0000-0000-000000000002",
     "GraphScheduling": {
       "UseMockService": true,
       "MaxSuggestions": 10,
       "ConfidenceThreshold": 0.7
     },
     "OpenAI": {
       "ApiKey": "mock-api-key",
       "Endpoint": "https://mock-endpoint.com",
       "DeploymentName": "gpt-3.5-turbo"
     },
     "Authentication": {
       "ClientId": "00000000-0000-0000-0000-000000000003",
       "ClientSecret": "mock-client-secret",
       "TenantId": "00000000-0000-0000-0000-000000000004"
     },
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     }
   }
   ```

3. **Important Configuration Notes**:
   - `UseMockService: true` enables mock data - no real Azure credentials needed
   - Mock GUIDs are provided for testing - these work with the mock service
   - Real API keys are NOT required for local testing

### Step 3: Build and Test the Bot Locally

1. **Navigate to Project Directory**
   ```bash
   cd /home/runner/work/InterviewSchedulingBot/InterviewSchedulingBot
   ```

2. **Build the Project**
   ```bash
   dotnet build --configuration Release
   ```
   Expected output: "Build succeeded. 0 Warning(s). 0 Error(s)."

3. **Run Quick AI Test**
   ```bash
   ./local-test.sh
   ```
   This script will:
   - Verify prerequisites
   - Build the project
   - Test AI functionality
   - Provide next steps

4. **Start the Bot**
   ```bash
   dotnet run --configuration Release
   ```
   Expected output:
   ```
   info: Microsoft.Hosting.Lifetime[14]
         Now listening on: https://localhost:5001
   info: Microsoft.Hosting.Lifetime[14]
         Now listening on: http://localhost:5000
   ```

   **Keep this terminal open** - the bot needs to stay running!

### Step 4: Set Up ngrok Tunnel (Detailed)

1. **Open a NEW Terminal Window** (keep the bot running in the first one)

2. **Authenticate ngrok** (one-time setup):
   ```bash
   ngrok authtoken YOUR_AUTHTOKEN_HERE
   ```
   Get your authtoken from: https://dashboard.ngrok.com/get-started/your-authtoken

3. **Start ngrok Tunnel**:
   ```bash
   ngrok http 5000
   ```
   
4. **Copy the HTTPS URL** from ngrok output:
   ```
   Session Status                online
   Account                       your-account
   Version                       3.x.x
   Region                        United States (us)
   Web Interface                 http://127.0.0.1:4040
   Forwarding                    https://abc123.ngrok.io -> http://localhost:5000
   ```
   **Copy this URL**: `https://abc123.ngrok.io` (your URL will be different)

5. **Verify ngrok is Working**:
   Open `https://your-ngrok-url.ngrok.io` in browser
   You should see: "Your bot is running!" or similar message

### Step 5: Configure Teams App Manifest

1. **Generate a Microsoft App ID**:
   - Go to https://www.uuidgenerator.net/
   - Generate a new UUID (e.g., `12345678-1234-5678-9012-123456789012`)
   - Copy this UUID

2. **Update manifest.json**:
   Replace the following placeholders:
   ```json
   {
     "manifestVersion": "1.16",
     "version": "1.0.0",
     "id": "12345678-1234-5678-9012-123456789012",
     "developer": {
       "name": "Your Name",
       "websiteUrl": "https://your-ngrok-url.ngrok.io",
       "privacyUrl": "https://your-ngrok-url.ngrok.io/privacy",
       "termsOfUseUrl": "https://your-ngrok-url.ngrok.io/terms"
     },
     "name": {
       "short": "Interview Scheduling Bot",
       "full": "AI-Powered Interview Scheduling Bot"
     },
     "description": {
       "short": "Schedule interviews with AI assistance",
       "full": "An intelligent bot that helps schedule interviews using AI-driven suggestions and user preference learning"
     },
     "icons": {
       "outline": "icon-outline.png",
       "color": "icon-color.png"
     },
     "accentColor": "#0078d4",
     "bots": [
       {
         "botId": "12345678-1234-5678-9012-123456789012",
         "scopes": [
           "personal",
           "team",
           "groupchat"
         ],
         "supportsFiles": false,
         "isNotificationOnly": false
       }
     ],
     "composeExtensions": [],
     "permissions": [
       "identity",
       "messageTeamMembers"
     ],
     "validDomains": [
       "your-ngrok-url.ngrok.io"
     ]
   }
   ```

3. **Replace Placeholders**:
   - `{{MICROSOFT_APP_ID}}` → Your generated UUID
   - `{{BOT_ENDPOINT}}` → Your ngrok URL
   - `botId` → Same UUID as above
   - `validDomains` → Your ngrok domain (without https://)

### Step 6: Create and Upload Teams App Package

1. **Create the App Package**:
   ```bash
   ./create-teams-package.sh
   ```
   This creates `interview-scheduling-bot-teams-app.zip`

2. **Alternative Manual Method**:
   ```bash
   zip -r interview-scheduling-bot-teams-app.zip manifest.json icon-outline.png icon-color.png
   ```

3. **Upload to Microsoft Teams**:
   - Open Microsoft Teams
   - Click "Apps" in the left sidebar
   - Click "Upload a custom app" (bottom left)
   - Select "Upload for me or my teams"
   - Choose your `interview-scheduling-bot-teams-app.zip` file
   - Click "Add" when prompted

4. **Verify Installation**:
   - You should see "Interview Scheduling Bot" in your Apps
   - Click on it to open a chat with the bot

### Step 7: Test Basic Bot Functionality

1. **Start a Conversation**:
   In the bot chat window, type:
   ```
   hello
   ```
   Expected response: Welcome message with available commands

2. **Test Help Command**:
   ```
   help
   ```
   Expected response: List of available commands including AI features

3. **Test Basic Status**:
   ```
   status
   ```
   Expected response: Bot status and configuration info

## Comprehensive AI Features Testing

### Test 1: AI-Driven Scheduling

1. **Start AI Scheduling**:
   ```
   ai schedule
   ```
   Expected response: Bot asks for meeting details

2. **Provide Meeting Duration**:
   ```
   60 minutes
   ```
   or
   ```
   1 hour
   ```

3. **Specify Attendees**:
   ```
   john@example.com, sarah@example.com
   ```
   Note: Use any email addresses - the mock service will simulate their availability

4. **Set Date Range**:
   ```
   next 7 days
   ```
   or
   ```
   this week
   ```

5. **Review AI Suggestions**:
   The bot will respond with something like:
   ```
   🤖 AI Scheduling Results:
   
   📅 Suggestion 1: Tomorrow 2:00 PM - 3:00 PM
      Confidence: 85%
      Reasoning: Optimal time based on historical patterns
   
   📅 Suggestion 2: Friday 10:00 AM - 11:00 AM
      Confidence: 78%
      Reasoning: Good availability for all attendees
   
   📅 Suggestion 3: Wednesday 3:00 PM - 4:00 PM
      Confidence: 72%
      Reasoning: Fits your preference for afternoon meetings
   
   🧠 AI Insights:
   - You prefer morning meetings (60% success rate)
   - Tuesday-Thursday are your most productive days
   - Consider 45-minute meetings for better engagement
   
   Type 'book 1' to book the first suggestion, or 'book 2' for the second, etc.
   ```

6. **Book a Meeting**:
   ```
   book 1
   ```
   Expected response: Confirmation that meeting is booked

### Test 2: User Preference Learning

1. **Provide Feedback on Booked Meeting**:
   ```
   rate 5 stars - Perfect time slot!
   ```
   Expected response: Thank you message, AI notes your preference

2. **Schedule Another Meeting**:
   ```
   ai schedule
   ```
   Provide different details:
   - Duration: 30 minutes
   - Attendees: different emails
   - Date range: next 3 days

3. **Observe Learning**:
   Notice how the AI suggestions adapt based on your previous feedback

4. **Test Different Feedback**:
   ```
   rate 2 stars - Too early in the morning
   ```
   AI should learn to avoid early morning suggestions

### Test 3: Find Optimal Meeting Times

1. **Use Optimal Finding Feature**:
   ```
   find optimal
   ```

2. **Provide Constraints**:
   - Meeting type: Interview
   - Duration: 45 minutes
   - Number of attendees: 3
   - Priority: High

3. **Review Optimal Suggestions**:
   Bot provides highly-ranked suggestions based on AI analysis

### Test 4: Pattern Recognition

1. **Schedule Multiple Meetings** (repeat AI scheduling 3-4 times):
   - Try different times of day
   - Use different durations
   - Provide varied feedback

2. **Check Pattern Analysis**:
   ```
   show patterns
   ```
   Expected response: AI-identified patterns in your scheduling behavior

3. **Request Insights**:
   ```
   ai insights
   ```
   Expected response: Personalized recommendations based on your patterns

### Test 5: Advanced AI Features

1. **Test Conflict Detection**:
   ```
   ai schedule
   ```
   Book overlapping meetings to see how AI handles conflicts

2. **Test Preference Adaptation**:
   ```
   prefer mornings
   ```
   Then schedule meetings to see if AI adapts

3. **Test Seasonal Adjustments**:
   ```
   ai schedule long term
   ```
   Test how AI handles longer-term scheduling

## Mock Service Testing Features

The mock service provides realistic responses for testing without real calendar integration:

### Mock Data Generated:
- **5 Meeting Suggestions** with confidence scores from 60-90%
- **Realistic Time Slots** respecting standard working hours (9 AM - 5 PM)
- **AI Reasoning** explaining why each time slot is recommended
- **User Preference Learning** with simulated historical data
- **Pattern Recognition** identifying 3 common scheduling patterns
- **Conflict Simulation** showing how AI handles overlapping meetings
- **Feedback Processing** that actually influences future suggestions

### Expected Mock Responses:

**AI Scheduling Response:**
```
🤖 AI Scheduling Analysis Complete!

📊 Analyzed 847 historical meetings
🎯 Identified 3 scheduling patterns
⚡ Generated 5 optimized suggestions

📅 TOP RECOMMENDATIONS:

1. Tuesday, 2:00 PM - 3:00 PM (Confidence: 87%)
   ✅ Perfect timing based on your history
   ✅ High attendee availability
   ✅ Optimal for productive discussions

2. Thursday, 10:00 AM - 11:00 AM (Confidence: 81%)
   ✅ Morning slot - your 73% success rate
   ✅ No conflicts detected
   ✅ Good energy levels expected

3. Wednesday, 3:00 PM - 4:00 PM (Confidence: 76%)
   ✅ Afternoon preference noted
   ✅ Suitable for all attendees
   ✅ Allows prep time

🧠 AI INSIGHTS:
💡 You're 23% more productive in Tuesday meetings
💡 45-minute meetings have 31% higher satisfaction
💡 Consider reducing back-to-back meetings

📈 LEARNING PROGRESS:
• Preference confidence: 78%
• Pattern recognition: 85%
• Recommendation accuracy: 82%
```

**User Preference Learning Response:**
```
🎯 Preference Learning Update

📊 Feedback processed: "Perfect timing!"
⭐ Rating: 5/5 stars
🧠 AI Learning Status: Updated

📈 Your Preferences (Updated):
• Morning meetings: 73% success rate (+5%)
• Tuesday slots: 87% satisfaction (+12%)
• 60-minute duration: Preferred (+8%)
• 2-3 attendees: Optimal group size

🔄 Recommendations will be adjusted for future scheduling
💡 AI confidence in your preferences: 78%
```

## Detailed Troubleshooting Guide

### Common Issues and Solutions

#### Issue 1: "Bot not responding in Teams"

**Symptoms:**
- Bot appears in Teams but doesn't respond to messages
- Messages show as "sent" but no response

**Solutions:**
1. **Check Bot Status**:
   ```bash
   # In terminal where bot is running, look for errors
   # Should see: "Now listening on: http://localhost:5000"
   ```

2. **Verify ngrok Tunnel**:
   ```bash
   # In ngrok terminal, should see:
   # "Session Status: online"
   # "Forwarding: https://abc123.ngrok.io -> http://localhost:5000"
   ```

3. **Test ngrok URL**:
   Open `https://your-ngrok-url.ngrok.io` in browser
   Should see bot response, not an error

4. **Check manifest.json**:
   Verify `botId` matches your Microsoft App ID
   Verify `validDomains` contains your ngrok domain

5. **Restart Everything**:
   ```bash
   # Stop bot (Ctrl+C)
   # Stop ngrok (Ctrl+C)
   # Start bot: dotnet run
   # Start ngrok: ngrok http 5000
   # Update manifest with new ngrok URL
   # Re-upload Teams package
   ```

#### Issue 2: "Build Failed" or "Compilation Errors"

**Symptoms:**
- `dotnet build` fails
- Missing dependencies
- Compilation errors

**Solutions:**
1. **Restore Dependencies**:
   ```bash
   dotnet restore
   dotnet clean
   dotnet build
   ```

2. **Check .NET Version**:
   ```bash
   dotnet --version
   # Should be 8.0.x or higher
   ```

3. **Update Dependencies**:
   ```bash
   dotnet add package Microsoft.Bot.Builder.Integration.AspNet.Core --version 4.22.0
   dotnet add package Microsoft.Bot.Builder.AI.Luis --version 4.22.0
   ```

4. **Clear Cache**:
   ```bash
   dotnet nuget locals all --clear
   dotnet restore
   ```

#### Issue 3: "ngrok Authentication Failed"

**Symptoms:**
- ngrok requires authentication
- "Auth token not found" error

**Solutions:**
1. **Get Auth Token**:
   - Go to https://dashboard.ngrok.com/get-started/your-authtoken
   - Copy your auth token

2. **Set Auth Token**:
   ```bash
   ngrok authtoken YOUR_AUTH_TOKEN_HERE
   ```

3. **Alternative: Use Different Tunnel**:
   ```bash
   # Try different port
   ngrok http 5001
   # Then update bot to use port 5001
   ```

#### Issue 4: "Teams App Upload Failed"

**Symptoms:**
- "Invalid package" error
- "Manifest validation failed"

**Solutions:**
1. **Check Package Contents**:
   ```bash
   unzip -l interview-scheduling-bot-teams-app.zip
   # Should contain: manifest.json, icon-outline.png, icon-color.png
   ```

2. **Validate Manifest**:
   Use https://dev.teams.microsoft.com/validation to check manifest.json

3. **Check Icon Files**:
   - icon-outline.png: 32x32 pixels, transparent background
   - icon-color.png: 192x192 pixels, colored background

4. **Recreate Package**:
   ```bash
   rm interview-scheduling-bot-teams-app.zip
   ./create-teams-package.sh
   ```

#### Issue 5: "AI Features Not Working"

**Symptoms:**
- Bot responds to basic commands but not AI commands
- No AI suggestions generated

**Solutions:**
1. **Check Mock Service Configuration**:
   In `appsettings.local.json`:
   ```json
   {
     "GraphScheduling": {
       "UseMockService": true
     }
   }
   ```

2. **Test AI Service**:
   ```bash
   # Run the local test script
   ./local-test.sh
   ```

3. **Check Console Output**:
   Look for AI-related log messages when running the bot

4. **Reset AI Learning**:
   ```bash
   # Clear any cached AI data
   rm -rf /tmp/ai-learning-cache
   ```

#### Issue 6: "Permission Denied" Errors

**Symptoms:**
- Cannot execute shell scripts
- File access errors

**Solutions:**
1. **Make Scripts Executable**:
   ```bash
   chmod +x local-test.sh
   chmod +x create-teams-package.sh
   ```

2. **Check File Permissions**:
   ```bash
   ls -la *.sh
   # Should show: -rwxr-xr-x
   ```

3. **Run with Explicit Path**:
   ```bash
   bash ./local-test.sh
   bash ./create-teams-package.sh
   ```

### Platform-Specific Issues

#### Windows Users

1. **PowerShell Script Execution**:
   ```powershell
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
   ```

2. **Use PowerShell Equivalents**:
   ```powershell
   # Instead of ./local-test.sh
   dotnet build
   dotnet run
   
   # Instead of ./create-teams-package.sh
   Compress-Archive -Path manifest.json,icon-outline.png,icon-color.png -DestinationPath interview-scheduling-bot-teams-app.zip
   ```

#### macOS Users

1. **Install ngrok via Homebrew**:
   ```bash
   brew install ngrok/ngrok/ngrok
   ```

2. **Security Permissions**:
   May need to allow ngrok in System Preferences > Security & Privacy

#### Linux Users

1. **Install ngrok**:
   ```bash
   # Download and install ngrok
   curl -s https://ngrok-agent.s3.amazonaws.com/ngrok.asc | sudo tee /etc/apt/trusted.gpg.d/ngrok.asc >/dev/null
   echo "deb https://ngrok-agent.s3.amazonaws.com buster main" | sudo tee /etc/apt/sources.list.d/ngrok.list
   sudo apt update && sudo apt install ngrok
   ```

### Advanced Troubleshooting

#### Enable Debug Logging

1. **Update appsettings.local.json**:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Debug",
         "Microsoft.AspNetCore": "Warning",
         "Microsoft.Bot": "Debug"
       }
     }
   }
   ```

2. **Check Detailed Logs**:
   Look for specific error messages in the console output

#### Network Issues

1. **Check Firewall**:
   Ensure ports 5000 and 4040 (ngrok) are not blocked

2. **Test Local Connection**:
   ```bash
   curl http://localhost:5000/api/messages
   # Should get some response, not connection refused
   ```

3. **Corporate Network Issues**:
   ngrok may be blocked - try using Azure Dev Tunnels instead

#### Memory/Performance Issues

1. **Check System Resources**:
   ```bash
   dotnet --info
   # Check available memory
   ```

2. **Reduce Mock Data**:
   In configuration, reduce `MaxSuggestions` to 3 instead of 10

### Getting Help

If you're still having issues:

1. **Check Console Output**:
   Look for specific error messages and stack traces

2. **Verify Environment**:
   ```bash
   dotnet --version
   ngrok version
   node --version  # If using npm for ngrok
   ```

3. **Test Step by Step**:
   - First, ensure bot builds: `dotnet build`
   - Then, test bot runs: `dotnet run`
   - Next, test ngrok works: `ngrok http 5000`
   - Finally, test Teams integration

4. **Common Error Messages**:
   - "Port already in use" → Kill process or use different port
   - "Assembly not found" → Run `dotnet restore`
   - "Invalid manifest" → Validate manifest.json syntax
   - "Bot not found" → Check botId in manifest matches App ID

Remember: The mock service should work without any external dependencies, so if basic commands fail, the issue is likely in the setup steps above.

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