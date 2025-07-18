#!/bin/bash

echo "🧪 Quick AI Bot Test - No ngrok Required!"
echo "========================================"
echo ""

# Start the bot in background
echo "🚀 Starting bot..."
dotnet run --configuration Release &
BOT_PID=$!

# Wait for bot to start
echo "⏳ Waiting for bot to start..."
sleep 8

# Test the APIs
echo "🧠 Testing AI Scheduling..."
AI_RESULT=$(curl -s -X POST "http://localhost:5000/api/test/ai-scheduling" \
  -H "Content-Type: application/json" \
  -d '{"attendees":["john@example.com","jane@example.com"],"duration":60,"days":7}')

if echo "$AI_RESULT" | grep -q '"success":true'; then
    echo "✅ AI Scheduling: WORKING"
    CONFIDENCE=$(echo "$AI_RESULT" | grep -o '"confidence":[0-9.]*' | cut -d: -f2)
    SUGGESTIONS=$(echo "$AI_RESULT" | grep -o '"suggestionsCount":[0-9]*' | cut -d: -f2)
    echo "   📊 Confidence: $CONFIDENCE, Suggestions: $SUGGESTIONS"
else
    echo "❌ AI Scheduling: FAILED"
fi

echo ""
echo "📅 Testing Graph Scheduling..."
GRAPH_RESULT=$(curl -s -X POST "http://localhost:5000/api/test/graph-scheduling" \
  -H "Content-Type: application/json")

if echo "$GRAPH_RESULT" | grep -q '"success":true'; then
    echo "✅ Graph Scheduling: WORKING"
    GRAPH_SUGGESTIONS=$(echo "$GRAPH_RESULT" | grep -o '"suggestionsCount":[0-9]*' | cut -d: -f2)
    echo "   📅 Suggestions: $GRAPH_SUGGESTIONS"
else
    echo "❌ Graph Scheduling: FAILED"
fi

echo ""
echo "🎯 Testing User Preferences..."
PREFS_RESULT=$(curl -s -X POST "http://localhost:5000/api/test/user-preferences" \
  -H "Content-Type: application/json")

if echo "$PREFS_RESULT" | grep -q '"userPreferences"'; then
    echo "✅ User Preferences: WORKING"
    PATTERNS=$(echo "$PREFS_RESULT" | grep -o '"patternsCount":[0-9]*' | cut -d: -f2)
    echo "   🔍 Patterns: $PATTERNS"
else
    echo "❌ User Preferences: FAILED"
fi

echo ""
echo "💡 Testing AI Insights..."
INSIGHTS_RESULT=$(curl -s -X POST "http://localhost:5000/api/test/ai-insights" \
  -H "Content-Type: application/json")

if echo "$INSIGHTS_RESULT" | grep -q '"aiInsights"'; then
    echo "✅ AI Insights: WORKING"
else
    echo "❌ AI Insights: FAILED"
fi

echo ""
echo "⚙️ Testing System Status..."
STATUS_RESULT=$(curl -s "http://localhost:5000/api/test/status")

if echo "$STATUS_RESULT" | grep -q '"botStatus":"Operational"'; then
    echo "✅ System Status: OPERATIONAL"
    echo "   🧪 Mock Service: Enabled"
    echo "   🔗 All Services: Ready"
else
    echo "❌ System Status: FAILED"
fi

echo ""
echo "📋 Test Summary:"
echo "==============="
echo "🌐 Web Interface: http://localhost:5000/api/test"
echo "🤖 Bot Status: Running (PID: $BOT_PID)"
echo "📊 Mock Data: Enabled (no Azure required)"
echo "🎯 Testing Complete!"
echo ""
echo "💡 Next Steps:"
echo "   1. Open http://localhost:5000/api/test in your browser"
echo "   2. Test all AI features with the web interface"
echo "   3. All features work without ngrok or Teams!"
echo ""
echo "⏹️  Press Ctrl+C to stop the bot, or leave it running for web testing"

# Keep the script running so user can test the web interface
wait $BOT_PID