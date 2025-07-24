# Microsoft Teams API Analysis for Interview Scheduling Bot

This document provides a comprehensive analysis of Microsoft Teams API endpoints that are helpful for implementing an interview scheduling bot focused on finding common availability using calendar data.

## 🔗 Core API Categories

### 1. Bot Framework APIs (Teams-specific)

#### **Teams Activity Handler**
- **Purpose**: Handle incoming messages, events, and user interactions
- **Key Methods**:
  - `OnTeamsMessagingExtensionQueryAsync()` - Handle search queries
  - `OnTeamsTaskModuleFetchAsync()` - Display task modules/dialogs
  - `OnTeamsTaskModuleSubmitAsync()` - Handle form submissions
  - `OnTeamsMembersAddedAsync()` - Welcome new team members

#### **Teams Context APIs**
```csharp
// Get Teams-specific context information
TeamsChannelData channelData = turnContext.Activity.GetChannelData<TeamsChannelData>();
string tenantId = channelData.Tenant.Id;
string teamId = channelData.Team?.Id;
string channelId = channelData.Channel?.Id;
```

### 2. Microsoft Graph API (via Teams Authentication)

#### **Calendar & Availability APIs**

##### **Get Free/Busy Information**
- **Endpoint**: `POST /me/calendar/getSchedule`
- **Purpose**: Get availability information for multiple users
- **Use Case**: Find common free time slots for interview scheduling
```http
POST https://graph.microsoft.com/v1.0/me/calendar/getSchedule
{
  "schedules": ["user1@contoso.com", "user2@contoso.com"],
  "startTime": {
    "dateTime": "2024-01-15T09:00:00.0000000",
    "timeZone": "Pacific Standard Time"
  },
  "endTime": {
    "dateTime": "2024-01-15T18:00:00.0000000", 
    "timeZone": "Pacific Standard Time"
  },
  "availabilityViewInterval": 60
}
```

##### **Get Calendar Events**
- **Endpoint**: `GET /me/calendar/events`
- **Purpose**: Retrieve detailed calendar events for availability analysis
- **Parameters**:
  - `$filter`: Filter by date range
  - `$select`: Choose specific properties
  - `$top`: Limit number of results

##### **Get Working Hours**
- **Endpoint**: `GET /me/mailboxSettings/workingHours`
- **Purpose**: Get user's configured working hours and time zone
- **Use Case**: Respect user preferences for interview scheduling

#### **User & Organization APIs**

##### **Get User Profile**
- **Endpoint**: `GET /me`
- **Purpose**: Get detailed user information including email, timezone, manager
- **Properties**: `id`, `mail`, `displayName`, `mailboxSettings`, `manager`

##### **Get Team Members**
- **Endpoint**: `GET /teams/{team-id}/members`
- **Purpose**: Get all members of a team for group scheduling
- **Use Case**: Schedule interviews with multiple team members

##### **Get User's Manager**
- **Endpoint**: `GET /me/manager`
- **Purpose**: Get manager information for approval workflows

##### **Find People**
- **Endpoint**: `GET /me/people`
- **Purpose**: Search for people in organization
- **Parameters**: `$search`, `$filter`, `$top`

#### **Presence & Status APIs**

##### **Get User Presence**
- **Endpoint**: `GET /me/presence`
- **Purpose**: Get real-time availability status
- **Statuses**: Available, Busy, DoNotDisturb, Away, Offline
- **Use Case**: Consider current availability when suggesting times

##### **Get Multiple Users' Presence**
- **Endpoint**: `POST /communications/getPresencesByUserId`
- **Purpose**: Batch get presence for multiple users
- **Use Case**: Check team availability before scheduling

### 3. Teams-Specific Bot APIs

#### **Messaging Extensions**
- **Purpose**: Enable rich search and action capabilities
- **Use Cases**:
  - Search for available time slots
  - Quick schedule actions
  - Display availability cards

#### **Task Modules**
- **Purpose**: Display modal dialogs for complex interactions
- **Use Cases**:
  - Date/time picker interfaces
  - Multi-step scheduling wizards
  - Confirmation dialogs

#### **Adaptive Cards**
- **Purpose**: Rich, interactive card-based UI
- **Use Cases**:
  - Display available time slots
  - Show scheduling conflicts
  - Interactive time selection

### 4. Teams App Manifest Permissions

#### **Required Permissions**
```json
{
  "webApplicationInfo": {
    "id": "your-app-id",
    "resource": "https://graph.microsoft.com"
  },
  "permissions": [
    "https://graph.microsoft.com/Calendars.Read",
    "https://graph.microsoft.com/Calendars.ReadWrite", 
    "https://graph.microsoft.com/User.Read",
    "https://graph.microsoft.com/People.Read",
    "https://graph.microsoft.com/Presence.Read"
  ]
}
```

## 🎯 Key Endpoints for Interview Scheduling

### Priority 1: Essential for Core Functionality

1. **`POST /me/calendar/getSchedule`** - Core availability checking
2. **`GET /me/mailboxSettings/workingHours`** - Working hours configuration
3. **`GET /me/presence`** - Real-time availability status
4. **Teams Bot Context APIs** - User identification and context

### Priority 2: Enhanced Functionality

5. **`GET /me/calendar/events`** - Detailed calendar analysis
6. **`GET /teams/{team-id}/members`** - Team member discovery
7. **`POST /communications/getPresencesByUserId`** - Batch presence checking
8. **`GET /me/people`** - Organization people search

### Priority 3: Advanced Features

9. **`GET /me/manager`** - Approval workflow support
10. **Teams Messaging Extensions** - Rich search interface
11. **Teams Task Modules** - Advanced scheduling UI
12. **Adaptive Cards** - Interactive scheduling interface

## 🔧 Implementation Considerations

### Authentication Flow
- Use Teams SSO (Single Sign-On) for seamless authentication
- Leverage existing Teams token for Graph API calls
- Handle token refresh gracefully

### Error Handling
- Handle Graph API throttling (429 responses)
- Graceful fallback when permissions are missing
- User-friendly error messages for authentication issues

### Performance Optimization
- Batch API calls when possible
- Cache frequently accessed data (working hours, user info)
- Use appropriate time ranges for calendar queries

### Privacy & Compliance
- Respect user privacy settings
- Handle sensitive calendar information appropriately
- Comply with organizational data policies

## 📚 Resources

- [Teams Bot Framework Documentation](https://docs.microsoft.com/en-us/microsoftteams/platform/bots/what-are-bots)
- [Microsoft Graph Calendar API](https://docs.microsoft.com/en-us/graph/api/resources/calendar)
- [Teams App Manifest Schema](https://docs.microsoft.com/en-us/microsoftteams/platform/resources/schema/manifest-schema)
- [Graph API Permissions Reference](https://docs.microsoft.com/en-us/graph/permissions-reference)