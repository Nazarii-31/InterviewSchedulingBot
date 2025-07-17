# Video Tutorial Script for Local Testing

## Tutorial: Testing Interview Scheduling Bot Locally

### Introduction (0:00-0:30)
"Welcome to the local testing tutorial for the AI-powered Interview Scheduling Bot. In this tutorial, we'll walk through setting up and testing the bot locally in Microsoft Teams without requiring Azure deployment."

### Prerequisites Check (0:30-1:30)
"First, let's ensure you have all prerequisites:
1. .NET 8.0 SDK installed
2. Microsoft Teams (desktop or web)
3. ngrok for local tunneling
4. A text editor

Let's verify these are installed by running a few commands..."

**Commands to show:**
```bash
dotnet --version
ngrok version
```

### Complete Setup (1:30-3:00)
"Now let's use our automated setup script to configure everything at once."

**Commands to show:**
```bash
./setup-local-testing.sh
```

"This script will:
- Check all prerequisites
- Create local configuration files
- Generate Microsoft App ID
- Update Teams manifest
- Build the project
- Create helper scripts
- Package the Teams app"

### Manual Configuration (3:00-5:00)
"If you prefer to configure manually, here's what the setup script does:

1. **Create appsettings.local.json** with mock service configuration
2. **Generate Microsoft App ID** using UUID generator
3. **Update manifest.json** with the generated App ID
4. **Build the project** to ensure everything compiles"

**Show file contents:**
```json
{
  "GraphScheduling": {
    "UseMockService": true,
    "MaxSuggestions": 10,
    "ConfidenceThreshold": 0.7
  }
}
```

### Starting the Bot (5:00-6:30)
"Now let's start the bot and ngrok tunnel:

**Terminal 1 - Start the bot:**
```bash
./start-bot.sh
```

**Terminal 2 - Start ngrok:**
```bash
./start-ngrok.sh
```

Copy the ngrok HTTPS URL - we'll need this for Teams."

### Teams Integration (6:30-8:30)
"Now let's integrate with Microsoft Teams:

1. **Update manifest.json** with your ngrok URL
2. **Create Teams package** using our script
3. **Upload to Teams** through the Apps section"

**Show in Teams:**
- Navigate to Apps
- Upload custom app
- Select the zip file
- Click Add

### Testing AI Features (8:30-12:00)
"Let's test the AI features step by step:

**Test 1: Basic AI Scheduling**
```
ai schedule
```

Provide these details:
- Duration: 60 minutes
- Attendees: john@example.com, sarah@example.com
- Date range: next 7 days

**Expected Response:**
The bot will show 5 AI-generated suggestions with confidence scores and reasoning.

**Test 2: Booking a Meeting**
```
book 1
```

**Test 3: Providing Feedback**
```
rate 5 stars - Perfect timing!
```

**Test 4: User Preference Learning**
Schedule multiple meetings and rate them differently to see how the AI adapts."

### Advanced Testing (12:00-14:00)
"Let's test more advanced features:

**Find Optimal Times:**
```
find optimal
```

**Check Patterns:**
```
show patterns
```

**AI Insights:**
```
ai insights
```

**Set Preferences:**
```
prefer mornings
```

Notice how the AI adapts to your preferences in subsequent scheduling requests."

### Mock Service Features (14:00-15:30)
"Our mock service provides realistic testing without external dependencies:

- **5 meeting suggestions** with 60-90% confidence scores
- **Realistic time slots** respecting working hours
- **AI reasoning** explaining each recommendation
- **Pattern recognition** identifying your scheduling habits
- **Preference learning** that adapts to your feedback"

### Troubleshooting (15:30-17:00)
"Common issues and solutions:

**Bot not responding:**
- Check if ngrok tunnel is active
- Verify bot is running in terminal
- Ensure Teams manifest has correct endpoint

**Build failures:**
- Run: dotnet restore
- Run: dotnet clean
- Run: dotnet build

**Teams upload issues:**
- Recreate package: ./create-teams-package.sh
- Verify manifest.json syntax
- Check icon files are present

**AI features not working:**
- Verify UseMockService: true in configuration
- Check console output for errors
- Restart bot if needed"

### Validation Script (17:00-18:00)
"Use our validation script to check everything is working:

```bash
./validate-setup.sh
```

This will check:
- All prerequisites
- Required files
- Configuration validity
- Build status
- AI service functionality
- Teams package integrity"

### Best Practices (18:00-19:00)
"For effective testing:

1. **Start with basic commands** (hello, help, status)
2. **Test AI features systematically** 
3. **Provide varied feedback** to test learning
4. **Try different scenarios** (different times, durations, attendees)
5. **Check error handling** with invalid inputs
6. **Monitor console output** for debugging"

### Next Steps (19:00-20:00)
"After successful local testing:

1. **Validate all AI features** work as expected
2. **Test user experience** with different conversation flows
3. **Check error handling** and edge cases
4. **Performance testing** with multiple requests
5. **Ready for production** deployment to Azure

For detailed instructions, see LOCAL_TEAMS_TESTING.md
For quick reference, use QUICK_TESTING_GUIDE.md"

### Summary (20:00-20:30)
"You've successfully set up and tested the AI-powered Interview Scheduling Bot locally! The bot now features:

- AI-driven scheduling with confidence scoring
- User preference learning
- Pattern recognition
- Intelligent recommendations
- Mock service for easy testing

Happy testing, and thank you for watching!"

---

## Screen Recording Checklist

### Pre-Recording Setup
- [ ] Clean desktop background
- [ ] Close unnecessary applications
- [ ] Prepare multiple terminal windows
- [ ] Have sample emails ready for testing
- [ ] Ensure stable internet connection

### Recording Segments
- [ ] Introduction and overview
- [ ] Prerequisites verification
- [ ] Complete setup process
- [ ] Bot startup and ngrok configuration
- [ ] Teams integration steps
- [ ] AI feature testing
- [ ] Troubleshooting demonstration
- [ ] Validation script usage
- [ ] Summary and next steps

### Post-Recording
- [ ] Edit for clarity and pacing
- [ ] Add captions for accessibility
- [ ] Include timestamps in description
- [ ] Upload to appropriate platform
- [ ] Link from documentation

## Audio Script Notes

### Tone and Pacing
- **Conversational and friendly** tone
- **Clear pronunciation** of technical terms
- **Appropriate pauses** for following along
- **Enthusiasm** for AI features
- **Patience** during longer processes

### Technical Accuracy
- **Accurate command syntax** shown on screen
- **Correct file names** and paths
- **Realistic response times** shown
- **Proper error handling** demonstrated
- **Up-to-date URLs** and references

### Accessibility
- **Describe all visual elements**
- **Read important text** shown on screen
- **Explain button clicks** and navigation
- **Provide alternative approaches** when possible
- **Include keyboard shortcuts** where relevant