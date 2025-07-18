#!/bin/bash

echo "🖥️  Starting Interview Scheduling Bot - Console Testing Application"
echo "=================================================================="
echo ""

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET is not installed. Please install .NET 8.0 or later."
    exit 1
fi

# Build the project first
echo "🔨 Building the project..."
dotnet build --configuration Release

if [ $? -ne 0 ]; then
    echo "❌ Build failed. Please check the error messages above."
    exit 1
fi

echo "✅ Build successful!"
echo ""

# Compile and run the console test application
echo "🚀 Starting console testing application..."
echo ""
echo "🧪 Available Tests:"
echo "   1. 🧠 AI Scheduling"
echo "   2. 📅 Graph Scheduling"
echo "   3. 🎯 User Preferences"
echo "   4. 📊 AI Insights"
echo "   5. 🔍 Basic Scheduling"
echo "   6. ⚙️ System Status"
echo "   7. 🚀 Full AI Demo"
echo ""
echo "💡 No external dependencies required - all tests run with mock data!"
echo ""

# Create a temporary project for the console app
TEMP_DIR="/tmp/bot-console-test"
mkdir -p "$TEMP_DIR"

# Copy the console test application
cp Testing/ConsoleTestApp.cs "$TEMP_DIR/Program.cs"

# Create a project file for the console app
cat > "$TEMP_DIR/ConsoleTestApp.csproj" << EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
  </ItemGroup>
  
  <ItemGroup>
    <ProjectReference Include="../InterviewSchedulingBot.csproj" />
  </ItemGroup>
  
  <ItemGroup>
    <None Include="../appsettings.json" CopyToOutputDirectory="PreserveNewest" />
    <None Include="../appsettings.local.json" CopyToOutputDirectory="PreserveNewest" Condition="Exists('../appsettings.local.json')" />
  </ItemGroup>
</Project>
EOF

# Run the console application
cd "$TEMP_DIR"
dotnet run

# Clean up
cd - > /dev/null
rm -rf "$TEMP_DIR"

echo ""
echo "👋 Console testing session ended."