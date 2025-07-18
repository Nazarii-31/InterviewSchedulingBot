#!/bin/bash

echo "🌐 Starting Interview Scheduling Bot - Web Testing Interface"
echo "==========================================================="
echo ""

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET is not installed. Please install .NET 8.0 or later."
    exit 1
fi

# Build the project
echo "🔨 Building the project..."
dotnet build --configuration Release

if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please check the error messages above."
    exit 1
fi

echo "✅ Build successful!"
echo ""

# Start the web server
echo "🚀 Starting web server..."
echo "📍 Web Testing Interface will be available at:"
echo "   http://localhost:5000/api/test"
echo ""
echo "🧪 This provides a complete web-based testing interface for all AI features:"
echo "   • AI Scheduling Test"
echo "   • Graph Scheduling Test" 
echo "   • User Preferences Test"
echo "   • AI Insights Test"
echo "   • Basic Scheduling Test"
echo "   • System Status Check"
echo ""
echo "💡 No ngrok or Teams required - test everything in your browser!"
echo ""
echo "⏹️  Press Ctrl+C to stop the server"
echo ""

# Start the bot with web interface
dotnet run --configuration Release