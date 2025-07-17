#!/bin/bash

# Validate Local Testing Environment
# This script checks that everything is set up correctly for local testing

echo "🔍 Validating Local Testing Environment"
echo "======================================="
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

ISSUES=0

# Function to check and report status
check_item() {
    local item="$1"
    local command="$2"
    local expected="$3"
    
    echo -n "Checking $item... "
    
    if eval "$command" &> /dev/null; then
        echo -e "${GREEN}✅ OK${NC}"
        if [ ! -z "$expected" ]; then
            echo "   $(eval "$command" 2>&1)"
        fi
    else
        echo -e "${RED}❌ FAILED${NC}"
        ISSUES=$((ISSUES + 1))
        if [ ! -z "$expected" ]; then
            echo "   Expected: $expected"
        fi
    fi
}

# Check file existence
check_file() {
    local file="$1"
    local description="$2"
    
    echo -n "Checking $description... "
    
    if [ -f "$file" ]; then
        echo -e "${GREEN}✅ EXISTS${NC}"
    else
        echo -e "${RED}❌ MISSING${NC}"
        ISSUES=$((ISSUES + 1))
    fi
}

echo "1. Prerequisites"
echo "================"
check_item ".NET SDK" "dotnet --version" "8.0.x"
check_item "ngrok" "ngrok version" "any version"
check_item "zip utility" "zip -v" "any version"
echo ""

echo "2. Required Files"
echo "================="
check_file "appsettings.json" "Main configuration"
check_file "appsettings.local.json" "Local configuration"
check_file "manifest.json" "Teams manifest"
check_file "icon-outline.png" "Teams outline icon"
check_file "icon-color.png" "Teams color icon"
check_file "InterviewSchedulingBot.csproj" "Project file"
echo ""

echo "3. Helper Scripts"
echo "================"
check_file "setup-local-testing.sh" "Complete setup script"
check_file "local-test.sh" "Basic test script"
check_file "create-teams-package.sh" "Package creation script"
echo ""

echo "4. Configuration Validation"
echo "=========================="

# Check if appsettings.local.json has correct structure
if [ -f "appsettings.local.json" ]; then
    echo -n "Checking mock service configuration... "
    if grep -q '"UseMockService": true' appsettings.local.json; then
        echo -e "${GREEN}✅ ENABLED${NC}"
    else
        echo -e "${RED}❌ DISABLED${NC}"
        ISSUES=$((ISSUES + 1))
    fi
    
    echo -n "Checking Microsoft App ID... "
    if grep -q '"MicrosoftAppId"' appsettings.local.json; then
        echo -e "${GREEN}✅ CONFIGURED${NC}"
    else
        echo -e "${RED}❌ MISSING${NC}"
        ISSUES=$((ISSUES + 1))
    fi
fi

# Check manifest.json structure
if [ -f "manifest.json" ]; then
    echo -n "Checking Teams manifest structure... "
    if grep -q '"manifestVersion"' manifest.json && grep -q '"botId"' manifest.json; then
        echo -e "${GREEN}✅ VALID${NC}"
    else
        echo -e "${RED}❌ INVALID${NC}"
        ISSUES=$((ISSUES + 1))
    fi
fi

echo ""

echo "5. Build Validation"
echo "=================="
echo "Building project..."
if dotnet build --configuration Release --verbosity quiet; then
    echo -e "${GREEN}✅ BUILD SUCCESS${NC}"
else
    echo -e "${RED}❌ BUILD FAILED${NC}"
    ISSUES=$((ISSUES + 1))
fi
echo ""

echo "6. AI Service Test"
echo "=================="
echo "Testing AI service functionality..."

# Start bot briefly to test AI service
timeout 10s dotnet run --no-build --configuration Release --verbosity quiet > /dev/null 2>&1 &
BOT_PID=$!
sleep 3

if ps -p $BOT_PID > /dev/null 2>&1; then
    echo -e "${GREEN}✅ AI SERVICE RESPONSIVE${NC}"
    kill $BOT_PID 2>/dev/null
else
    echo -e "${RED}❌ AI SERVICE FAILED${NC}"
    ISSUES=$((ISSUES + 1))
fi

echo ""

echo "7. Teams Package Validation"
echo "=========================="
if [ -f "interview-scheduling-bot-teams-app.zip" ]; then
    echo -n "Checking Teams package contents... "
    if unzip -l interview-scheduling-bot-teams-app.zip | grep -q "manifest.json" && \
       unzip -l interview-scheduling-bot-teams-app.zip | grep -q "icon-outline.png" && \
       unzip -l interview-scheduling-bot-teams-app.zip | grep -q "icon-color.png"; then
        echo -e "${GREEN}✅ COMPLETE${NC}"
    else
        echo -e "${RED}❌ INCOMPLETE${NC}"
        ISSUES=$((ISSUES + 1))
    fi
else
    echo -e "${YELLOW}⚠️  Teams package not found. Run: ./create-teams-package.sh${NC}"
fi

echo ""

# Final summary
echo "======================================="
if [ $ISSUES -eq 0 ]; then
    echo -e "${GREEN}🎉 ALL CHECKS PASSED!${NC}"
    echo ""
    echo "Your environment is ready for local testing:"
    echo "1. Run: ./start-bot.sh"
    echo "2. Run: ./start-ngrok.sh (in another terminal)"
    echo "3. Update manifest.json with ngrok URL"
    echo "4. Upload Teams package to Microsoft Teams"
    echo "5. Test AI features with the bot"
    echo ""
    echo "For detailed instructions, see: LOCAL_TEAMS_TESTING.md"
else
    echo -e "${RED}❌ $ISSUES ISSUES FOUND${NC}"
    echo ""
    echo "Please fix the issues above before testing."
    echo "For help, see: LOCAL_TEAMS_TESTING.md"
    echo ""
    echo "Quick fixes:"
    echo "- Run: ./setup-local-testing.sh to fix configuration"
    echo "- Run: dotnet restore to fix build issues"
    echo "- Run: ./create-teams-package.sh to create package"
fi

echo ""
echo "For support, check the troubleshooting section in LOCAL_TEAMS_TESTING.md"