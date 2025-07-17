# Teams Testing Quick Reference Guide

## Essential Commands for Testing

### Basic Bot Commands
```
hello                   # Welcome message
help                    # Show all available commands
status                  # Bot status and configuration
```

### AI Scheduling Commands
```
ai schedule            # Start AI-driven scheduling
find optimal           # Find optimal meeting times
schedule interview     # Schedule an interview
book [number]          # Book a suggested meeting (e.g., "book 1")
```

### User Preference Commands
```
rate [1-5] stars       # Rate a meeting experience
prefer mornings        # Set time preferences
prefer afternoons      # Set time preferences
show patterns          # View your scheduling patterns
ai insights           # Get AI-generated insights
```

### Advanced Commands
```
schedule long term     # Long-term scheduling
reschedule [id]       # Reschedule existing meeting
cancel [id]           # Cancel a meeting
conflicts             # Check for scheduling conflicts
```

## Testing Scenarios

### Scenario 1: Basic AI Scheduling
1. Type: `ai schedule`
2. When prompted, provide:
   - Duration: `60 minutes`
   - Attendees: `john@example.com, sarah@example.com`
   - Date range: `next 7 days`
3. Review suggestions and type: `book 1`
4. Rate the experience: `rate 5 stars - Perfect timing!`

### Scenario 2: User Preference Learning
1. Schedule 3 different meetings with varying times
2. Rate each meeting differently (1-5 stars)
3. Schedule a 4th meeting and observe how AI adapts
4. Check patterns: `show patterns`

### Scenario 3: Optimal Meeting Finding
1. Type: `find optimal`
2. Provide meeting details when prompted
3. Compare results with regular `ai schedule`
4. Notice the higher confidence scores

### Scenario 4: Advanced AI Features
1. Type: `ai insights` to get recommendations
2. Set preferences: `prefer mornings`
3. Schedule again and notice the adaptation
4. Check for conflicts: `conflicts`

## Expected Mock Responses

### AI Scheduling Response
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

🧠 AI INSIGHTS:
💡 You're 23% more productive in Tuesday meetings
💡 45-minute meetings have 31% higher satisfaction
💡 Consider reducing back-to-back meetings

Type 'book 1' to book the first suggestion.
```

### User Preference Learning Response
```
🎯 Preference Learning Update

📊 Feedback processed: "Perfect timing!"
⭐ Rating: 5/5 stars
🧠 AI Learning Status: Updated

📈 Your Preferences (Updated):
• Morning meetings: 73% success rate (+5%)
• Tuesday slots: 87% satisfaction (+12%)
• 60-minute duration: Preferred (+8%)

🔄 Future recommendations will be personalized based on this feedback
```

## Quick Setup Commands

### One-Time Setup
```bash
# Run complete setup
./setup-local-testing.sh

# Or manual setup
dotnet build
./create-teams-package.sh
```

### Start Testing
```bash
# Terminal 1: Start bot
./start-bot.sh

# Terminal 2: Start ngrok
./start-ngrok.sh

# Then upload Teams package and test
```

## Common Issues & Quick Fixes

### Bot Not Responding
```bash
# Check if bot is running
ps aux | grep dotnet

# Restart bot
./start-bot.sh

# Check ngrok tunnel
curl https://your-ngrok-url.ngrok.io
```

### Build Issues
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build --configuration Release
```

### Teams Upload Issues
```bash
# Recreate package
rm interview-scheduling-bot-teams-app.zip
./create-teams-package.sh

# Verify package contents
unzip -l interview-scheduling-bot-teams-app.zip
```

## Performance Validation

### Test Checklist
- [ ] Bot responds to `hello` command
- [ ] `ai schedule` generates 5 suggestions
- [ ] Confidence scores are between 60-90%
- [ ] `book 1` successfully books a meeting
- [ ] Rating feedback is processed
- [ ] `show patterns` displays learned patterns
- [ ] AI insights are generated

### Success Metrics
- Response time: < 2 seconds
- Suggestions generated: 5 per request
- Confidence scores: 60-90% range
- Learning adaptation: Visible after 3+ interactions
- Pattern recognition: 3+ patterns identified

## Troubleshooting Quick Reference

| Issue | Quick Fix |
|-------|-----------|
| Bot not responding | Check ngrok tunnel, restart bot |
| No AI suggestions | Verify `UseMockService: true` in config |
| Build fails | Run `dotnet restore` and `dotnet build` |
| Teams upload fails | Recreate package with correct manifest |
| Permission denied | Run `chmod +x *.sh` |
| Port in use | Kill process or use different port |

## Next Steps After Testing

1. **Validate Core Features**: Ensure all AI commands work
2. **Test User Experience**: Try different conversation flows
3. **Check Error Handling**: Test invalid inputs
4. **Performance Testing**: Test with multiple rapid requests
5. **Ready for Production**: Deploy to Azure when satisfied

## Support Resources

- **Detailed Guide**: LOCAL_TEAMS_TESTING.md
- **Implementation Details**: HYBRID_AI_IMPLEMENTATION.md
- **AI Features**: AI_FEATURES_DOCUMENTATION.md
- **Setup Script**: setup-local-testing.sh

---

**Remember**: This is a mock service environment - all data is simulated for testing purposes. No real calendar integration or Azure services are required for local testing.