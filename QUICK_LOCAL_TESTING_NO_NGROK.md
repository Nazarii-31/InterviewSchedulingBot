# 🚀 Quick Local Testing Guide - No ngrok Required!

This guide provides **4 different ways** to test the Interview Scheduling Bot locally without needing ngrok or Teams integration.

## 🎯 Option 1: Web Testing Interface (Recommended)

**Fastest and easiest way to test all AI features in your browser!**

### Quick Start
```bash
# Start the web testing interface
./start-web-testing.sh

# Then open your browser to:
# http://localhost:5000/api/test
```

### Features
- ✅ Complete web-based testing interface
- ✅ Test all AI scheduling features with one click
- ✅ Interactive form inputs for custom testing
- ✅ Real-time results display
- ✅ No external dependencies required
- ✅ Works entirely on localhost

### What You Can Test
1. **🧠 AI Scheduling** - Full machine learning-driven scheduling
2. **📅 Graph Scheduling** - Microsoft Graph-based optimal time finding  
3. **🎯 User Preferences** - AI learning and pattern recognition
4. **📊 AI Insights** - Intelligent recommendations and analysis
5. **🔍 Basic Scheduling** - Standard availability checking
6. **⚙️ System Status** - Configuration and service health

---

## 🎯 Option 2: Console Testing Application

**Interactive command-line testing with detailed output**

### Quick Start
```bash
# Start the console testing application
./start-console-testing.sh

# Follow the interactive menu to test features
```

### Features
- ✅ Interactive menu-driven testing
- ✅ Detailed console output with formatting
- ✅ Custom input for attendees, duration, etc.
- ✅ Full AI demo mode
- ✅ Step-by-step testing guidance
- ✅ No browser required

---

## 🎯 Option 3: Direct API Testing

**Test individual endpoints with curl or any HTTP client**

### Quick Start
```bash
# Start the bot
dotnet run

# Test endpoints directly:
curl -X GET "http://localhost:5000/api/test/status"
curl -X POST "http://localhost:5000/api/test/ai-scheduling" \
  -H "Content-Type: application/json" \
  -d '{"attendees":["john@example.com","jane@example.com"],"duration":60,"days":7}'
```

### Available Endpoints
- `GET /api/test` - Web testing interface
- `GET /api/test/status` - System status
- `POST /api/test/ai-scheduling` - AI scheduling test
- `POST /api/test/graph-scheduling` - Graph scheduling test
- `POST /api/test/user-preferences` - User preferences test
- `POST /api/test/ai-insights` - AI insights test
- `POST /api/test/basic-scheduling` - Basic scheduling test

---

## 🎯 Option 4: Alternative Tunneling Services

**If you want to test with Teams but don't have ngrok**

### Option 4a: Azure Dev Tunnels (Microsoft's ngrok alternative)
```bash
# Install Azure Dev Tunnels
npm install -g @azure/dev-tunnels-cli

# Start tunnel
devtunnel host -p 5000 --allow-anonymous

# Use the provided URL in Teams manifest
```

### Option 4b: Localtunnel
```bash
# Install localtunnel
npm install -g localtunnel

# Start tunnel  
lt --port 5000

# Use the provided URL in Teams manifest
```

### Option 4c: Serveo
```bash
# No installation required
ssh -R 80:localhost:5000 serveo.net

# Use the provided URL in Teams manifest
```

---

## 🧪 Testing Scenarios

### Scenario 1: Quick AI Validation (2 minutes)
1. Run `./start-web-testing.sh`
2. Open http://localhost:5000/api/test
3. Click "🚀 Test AI Scheduling"
4. Click "💡 Test AI Insights"
5. Verify results show AI suggestions with confidence scores

### Scenario 2: Comprehensive Testing (10 minutes)
1. Run `./start-console-testing.sh`
2. Choose option 7 "🚀 Run Full AI Demo"
3. Watch complete end-to-end AI workflow
4. Test individual features with custom inputs

### Scenario 3: API Integration Testing
1. Start bot: `dotnet run`
2. Test status: `curl http://localhost:5000/api/test/status`
3. Test AI scheduling with your own data
4. Integrate results into your own testing framework

---

## 📊 Expected Results

### AI Scheduling Test Results
```json
{
  "Success": true,
  "Confidence": 0.78,
  "ProcessingTime": 245,
  "SuggestionsCount": 5,
  "Suggestions": [
    {
      "StartTime": "2024-01-15 14:00",
      "EndTime": "2024-01-15 15:00", 
      "Confidence": 0.87,
      "Reason": "Optimal timing based on historical patterns",
      "IsOptimal": true
    }
  ]
}
```

### Console Demo Output
```
🧠 Step 1: AI Scheduling Analysis...
✅ AI Analysis Complete: 78.5% confidence, 245ms
📊 Generated 5 AI-optimized suggestions

📅 Step 2: Microsoft Graph Validation...
✅ Graph Analysis Complete: 5 validated suggestions

🎯 Step 3: User Preference Analysis...
✅ Preference Analysis Complete: 3 patterns identified
📈 Learning Status: Intermediate

💡 Step 4: AI Insights Generation...
✅ Insights Generated: 3 patterns, 85.2% accuracy

🎉 Full AI Demo Complete! All features tested successfully.
```

---

## 🔧 Troubleshooting

### If Web Interface Doesn't Load
```bash
# Check if bot is running
curl http://localhost:5000/api/test/status

# Check port availability
netstat -tulpn | grep :5000

# Try different port
dotnet run --urls="http://localhost:5001"
# Then use http://localhost:5001/api/test
```

### If Console App Fails
```bash
# Build manually
dotnet build --configuration Release

# Check dependencies
dotnet restore

# Run with verbose output
dotnet run --configuration Release --verbosity normal
```

### If AI Features Don't Work
1. Verify `appsettings.local.json` has `"UseMockService": true`
2. Check console output for error messages
3. Ensure all services are registered in `Program.cs`

---

## 🎯 Success Criteria

Your testing is successful if you see:
- ✅ AI scheduling generates 5 suggestions with 60-90% confidence
- ✅ Graph scheduling provides validated time slots  
- ✅ User preferences show learning patterns
- ✅ AI insights generate recommendations
- ✅ System status shows all services as "Ready"
- ✅ Processing times under 500ms

---

## 🚀 Next Steps

1. **Validate Core Features**: Test all options above
2. **Custom Scenarios**: Use your own attendee emails and meeting requirements  
3. **Performance Testing**: Run multiple rapid requests
4. **Integration Ready**: Once validated, deploy to Azure for production use

---

## 💡 Pro Tips

- **Web Interface**: Best for quick validation and demos
- **Console App**: Best for detailed testing and development  
- **Direct API**: Best for automated testing and CI/CD
- **Alternative Tunnels**: Use only if you specifically need Teams integration

**All methods test the same AI functionality - choose based on your preference!**

---

## 📞 Support

If any method fails:
1. Check console output for specific error messages
2. Verify .NET 8.0 is installed: `dotnet --version`
3. Ensure project builds: `dotnet build`
4. Review configuration in `appsettings.local.json`

**Remember**: Mock services are enabled by default, so no Azure credentials are required for testing!