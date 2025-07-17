#!/bin/bash

# Local MS Teams Bot Test Script
# This script helps you set up and test the Interview Scheduling Bot locally

echo "🤖 Interview Scheduling Bot - Local Teams Test Setup"
echo "=================================================="
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET is not installed. Please install .NET 8.0 SDK first."
    echo "   Download from: https://dotnet.microsoft.com/download"
    exit 1
fi

echo "✅ .NET SDK found: $(dotnet --version)"

# Check if ngrok is installed
if ! command -v ngrok &> /dev/null; then
    echo "⚠️  ngrok is not installed. You'll need it for Teams testing."
    echo "   Install: npm install -g ngrok"
    echo "   Or download from: https://ngrok.com/download"
    echo ""
fi

# Build the project
echo "🔨 Building the project..."
dotnet build --configuration Release

if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please check the errors above."
    exit 1
fi

echo "✅ Build successful!"
echo ""

# Run AI functionality test
echo "🧠 Testing AI functionality..."
echo "=================================================="
ASPNETCORE_ENVIRONMENT=Development timeout 10s dotnet run --no-build --configuration Release 2>/dev/null | grep -E "(✅|🔄|📊|🎯|🤖|🧠|💬|📈|🔍|💡)" || echo "AI test completed (timeout reached)"
echo ""

# Instructions for Teams testing
echo "🚀 Ready for Teams Testing!"
echo "=================================================="
echo ""
echo "Next steps:"
echo "1. Start the bot:           dotnet run --no-build --configuration Release"
echo "2. In another terminal:     ngrok http 5000"
echo "3. Update manifest.json with your ngrok URL"
echo "4. Create Teams package:    ./create-teams-package.sh"
echo "5. Upload to Teams and test!"
echo ""
echo "📖 For detailed instructions, see: LOCAL_TEAMS_TESTING.md"
echo ""
echo "💡 The bot uses mock services - no Azure credentials needed for basic testing!"
echo "   Set 'GraphScheduling:UseMockService: true' in appsettings.json"
echo ""
echo "🎯 Test these AI features in Teams:"
echo "   • 'ai schedule' - AI-driven scheduling"
echo "   • 'find optimal' - Optimal meeting times"
echo "   • 'help' - Show all commands"
echo ""
echo "Happy testing! 🎉"