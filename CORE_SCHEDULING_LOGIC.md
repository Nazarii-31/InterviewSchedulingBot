# Core Scheduling Logic Implementation

## Overview

This document describes the Core Scheduling Logic implementation that provides the ability to find common availability based on participant calendars using the Microsoft Graph API.

## Features

- **Email-based Participant Input**: Accepts a list of participant email addresses
- **Duration-based Scheduling**: Specify required meeting duration in minutes
- **Microsoft Graph API Integration**: Uses the Graph API's `getSchedule` endpoint to retrieve free/busy information
- **Working Hours Support**: Configurable working hours and working days
- **Time Zone Awareness**: Supports different time zones
- **Conflict Detection**: Automatically detects and avoids busy time conflicts
- **Optimized Results**: Returns merged and optimized available time slots

## Usage

### Basic Usage

```csharp
// Inject the service
private readonly ICoreSchedulingLogic _coreSchedulingLogic;

public async Task<List<AvailableTimeSlot>> FindMeetingTimes()
{
    var participantEmails = new List<string> 
    { 
        "user1@company.com", 
        "user2@company.com", 
        "user3@company.com" 
    };
    
    var startDate = DateTime.Now.AddDays(1);
    var endDate = startDate.AddDays(7);
    var durationMinutes = 60; // 1 hour meeting
    var userId = "authenticated-user-id";
    
    var availableSlots = await _coreSchedulingLogic.FindCommonAvailabilityAsync(
        participantEmails,
        durationMinutes,
        startDate,
        endDate,
        userId);
    
    return availableSlots;
}
```

### Advanced Usage with Custom Working Hours

```csharp
public async Task<List<AvailableTimeSlot>> FindMeetingTimesCustomHours()
{
    var participantEmails = new List<string> { "user1@company.com", "user2@company.com" };
    var startDate = DateTime.Now.AddDays(1);
    var endDate = startDate.AddDays(7);
    var durationMinutes = 30;
    var userId = "authenticated-user-id";
    
    // Custom working hours: 8 AM to 6 PM
    var workingHoursStart = new TimeSpan(8, 0, 0);
    var workingHoursEnd = new TimeSpan(18, 0, 0);
    
    // Include Saturday in working days
    var workingDays = new List<DayOfWeek>
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday
    };
    
    var timeZone = "Pacific Standard Time";
    
    var availableSlots = await _coreSchedulingLogic.FindCommonAvailabilityAsync(
        participantEmails,
        durationMinutes,
        startDate,
        endDate,
        userId,
        workingHoursStart,
        workingHoursEnd,
        workingDays,
        timeZone);
    
    return availableSlots;
}
```

## API Reference

### ICoreSchedulingLogic Interface

```csharp
public interface ICoreSchedulingLogic
{
    Task<List<AvailableTimeSlot>> FindCommonAvailabilityAsync(
        List<string> participantEmails,
        int durationMinutes,
        DateTime startDate,
        DateTime endDate,
        string userId,
        TimeSpan? workingHoursStart = null,
        TimeSpan? workingHoursEnd = null,
        List<DayOfWeek>? workingDays = null,
        string? timeZone = null);
}
```

### Parameters

- **participantEmails**: List of participant email addresses (required)
- **durationMinutes**: Meeting duration in minutes (15-480 minutes)
- **startDate**: Start date for availability search (required)
- **endDate**: End date for availability search (required)
- **userId**: User ID for authentication (required)
- **workingHoursStart**: Working hours start time (default: 9:00 AM)
- **workingHoursEnd**: Working hours end time (default: 5:00 PM)
- **workingDays**: List of working days (default: Monday-Friday)
- **timeZone**: Time zone identifier (default: system local)

### Return Value

Returns a `List<AvailableTimeSlot>` containing available time slots that work for all participants.

## Implementation Details

### Microsoft Graph API Integration

The implementation uses the Microsoft Graph API's `getSchedule` endpoint to retrieve free/busy information:

- **Endpoint**: `POST /me/calendar/getSchedule`
- **Permissions Required**: `Calendars.Read` (delegated)
- **Data Retrieved**: Free/busy status in 15-minute intervals

### Algorithm

1. **Validation**: Validates input parameters
2. **Authentication**: Retrieves user access token
3. **Free/Busy Retrieval**: Calls Graph API to get participant schedules
4. **Conflict Detection**: Identifies busy time slots for each participant
5. **Availability Calculation**: Finds time slots that don't conflict with any participant
6. **Working Hours Filtering**: Filters results to working hours only
7. **Optimization**: Merges consecutive slots and sorts by start time

### Error Handling

- **Authentication Errors**: Thrown when user is not authenticated
- **Graph API Errors**: Wrapped and re-thrown with descriptive messages
- **Parameter Validation**: Validates all required parameters
- **Graceful Degradation**: Treats participants with inaccessible calendars as "free"

## Service Registration

The service is registered in `Program.cs`:

```csharp
builder.Services.AddSingleton<ICoreSchedulingLogic, CoreSchedulingLogic>();
```

## Dependencies

- `IAuthenticationService`: For user authentication and token management
- `ILogger<CoreSchedulingLogic>`: For logging
- Microsoft Graph SDK packages

## Testing

Basic validation and testing code is included in the `Tests` folder:

```csharp
// Run basic validation
await CoreSchedulingLogicTest.RunBasicTest();
```

## Future Enhancements

- **Recurring Meeting Support**: Support for recurring meeting patterns
- **Room Resource Integration**: Include room/resource availability
- **Preference Scoring**: Weight time slots based on participant preferences
- **Batch Processing**: Process multiple meeting requests efficiently
- **Caching**: Cache free/busy information for better performance