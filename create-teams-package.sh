#!/bin/bash

# Create Teams App Package Script
# This script creates a zip file containing the Teams app manifest and icons

echo "Creating Microsoft Teams App Package..."

# Check if required files exist
if [ ! -f "manifest.json" ]; then
    echo "Error: manifest.json not found!"
    exit 1
fi

if [ ! -f "icon-outline.png" ]; then
    echo "Error: icon-outline.png not found!"
    exit 1
fi

if [ ! -f "icon-color.png" ]; then
    echo "Error: icon-color.png not found!"
    exit 1
fi

# Create the zip file
APP_PACKAGE_NAME="interview-scheduling-bot-teams-app.zip"

# Remove existing package if it exists
if [ -f "$APP_PACKAGE_NAME" ]; then
    rm "$APP_PACKAGE_NAME"
    echo "Removed existing package: $APP_PACKAGE_NAME"
fi

# Create the package
zip -r "$APP_PACKAGE_NAME" manifest.json icon-outline.png icon-color.png

if [ $? -eq 0 ]; then
    echo "✅ Teams app package created successfully: $APP_PACKAGE_NAME"
    echo ""
    echo "Next steps:"
    echo "1. Replace {{MICROSOFT_APP_ID}} in manifest.json with your actual Microsoft App ID"
    echo "2. Upload $APP_PACKAGE_NAME to Microsoft Teams Developer Portal"
    echo "3. Follow the deployment instructions in TEAMS_DEPLOYMENT.md"
else
    echo "❌ Failed to create Teams app package"
    exit 1
fi