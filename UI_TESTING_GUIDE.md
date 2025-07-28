# UI Testing Guide

## Interview Scheduling Bot - User Interface

This document describes the web-based testing interface for the Interview Scheduling Bot, which allows you to test all bot functionality without requiring Teams deployment.

## 🚀 Quick Start

1. **Start the application:**
   ```bash
   dotnet run
   ```

2. **Open the UI:**
   - Navigate to `http://localhost:5000` in your browser
   - The UI will automatically load with pre-configured test data

3. **Test Features:**
   - Use the three tabs to test different bot capabilities
   - All mock services are enabled by default for offline testing

## 🎯 Features

### 1. Find Optimal Slots Tab
**Purpose:** Test the core scheduling functionality - finding the best interview times

**Features:**
- Input participant emails (one per line)
- Select interview duration (30-120 minutes)
- Choose interview type (General, Technical, Behavioral, Panel)
- Set priority level (Low, Normal, High, Urgent)
- Specify date range for scheduling
- Optional requester ID and department

**Results Display:**
- **Recommended Slots:** Top-ranked time slots with business scores
- **Alternative Slots:** Backup options with confidence ratings
- **Business Insights:** Analytics including availability overview, best time windows, and scheduling tips

### 2. Validate Request Tab
**Purpose:** Test the validation engine for scheduling requests

**Features:**
- Quick validation of scheduling parameters
- Real-time error detection and warnings
- Business rule validation

**Results Display:**
- **Validation Status:** Pass/Fail indicator
- **Errors:** Critical issues that must be fixed
- **Warnings:** Recommendations for improvement
- **Suggestions:** Helpful tips for better scheduling

### 3. Analyze Conflicts Tab
**Purpose:** Test conflict detection capabilities

**Features:**
- Analyze conflicts for a specific proposed time
- Input participant list and meeting duration
- Mock calendar access demonstration

**Results Display:**
- **Conflict Status:** Conflicts detected or clear
- **Impact Analysis:** Severity and impact assessment
- **Affected Participants:** List of users with conflicts
- **Mitigation Suggestions:** Recommendations to resolve conflicts

## 🛠️ Technical Details

### Mock Services
The UI uses mock services for testing without requiring:
- Teams deployment
- Azure credentials
- Microsoft Graph API access
- External authentication

### API Integration
- All tabs call the corresponding REST API endpoints
- Real-time validation and error handling
- Smooth user experience with loading indicators

### Responsive Design
- Works on desktop and mobile devices
- Modern, clean interface
- Accessible with keyboard navigation

## 🔍 Testing Scenarios

### Scenario 1: Basic Scheduling
1. Go to "Find Optimal Slots" tab
2. Use default participant emails
3. Set duration to 60 minutes
4. Choose date range (tomorrow to next week)
5. Click "Find Optimal Slots"
6. Review recommended and alternative slots

### Scenario 2: Validation Testing
1. Go to "Validate Request" tab
2. Try invalid inputs:
   - Empty participant list
   - Negative duration
   - Invalid date ranges
3. Observe validation errors and warnings
4. Fix issues and see successful validation

### Scenario 3: Conflict Analysis
1. Go to "Analyze Conflicts" tab
2. Set a proposed meeting time
3. Add participant emails
4. Run conflict analysis
5. Review impact assessment and suggestions

## 🎨 UI Components

### Navigation
- **Tab Interface:** Three main functional areas
- **Responsive Design:** Works on all screen sizes
- **Visual Feedback:** Loading states and animations

### Forms
- **Smart Defaults:** Pre-filled with realistic test data
- **Validation:** Real-time field validation
- **Date/Time Pickers:** Easy scheduling input

### Results Display
- **Color-Coded Status:** Green for success, red for errors
- **Business Insights:** Rich analytics and recommendations
- **Time Slot Cards:** Visual representation of scheduling options

## 🔗 Related Resources

- **API Documentation:** `/swagger` - Complete API reference
- **Health Check:** `/health` - System status
- **Alternative URLs:** `/ui` or `/test` also load the interface

## 💡 Tips for Testing

1. **Use Realistic Data:** The mock services provide realistic scheduling patterns
2. **Test Edge Cases:** Try unusual durations, date ranges, and participant counts
3. **Check Validation:** Intentionally input invalid data to test error handling
4. **Review Insights:** Pay attention to business insights and recommendations
5. **Mobile Testing:** Test the interface on mobile devices for full coverage

## 🚧 Limitations

- **Calendar Access:** Real calendar data requires Teams bot context
- **Authentication:** Uses mock authentication for testing
- **Meeting Creation:** Focuses on scheduling analysis, not booking
- **Real-time Updates:** Mock data doesn't reflect live calendar changes

The UI provides a comprehensive testing environment for all bot functionality while maintaining the focus on finding common availability for interviews using calendar data.