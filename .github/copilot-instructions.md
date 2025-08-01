# Copilot Instructions for Interview Scheduling Bot

## Project Overview
This is an ASP.NET Core 8.0 interview scheduling bot with Microsoft Teams integration and natural language processing via OpenWebUI. The bot uses Clean Architecture patterns and integrates with Microsoft Graph API for calendar management.

## Architecture & Core Components

### Clean Architecture Layers
- **Domain**: `Domain/` - Core entities (Interview, Participant, TimeSlot)
- **Application**: `Application/` - MediatR commands/queries and DTOs
- **Infrastructure**: `Infrastructure/` - External services (Graph API, Caching)
- **Bot**: `Bot/` - Microsoft Bot Framework components and dialogs
- **Services**: `Services/` - Business logic and AI integration

### Key Services & Integration Points
- **OpenWebUI Integration**: `Services/Integration/OpenWebUIClient.cs` - AI natural language processing
- **Mock Data System**: `Services/Business/CleanMockDataGenerator.cs` - Clean, data-driven calendar simulation
- **Calendar Services**: `Infrastructure/Calendar/GraphCalendarService.cs` - Microsoft Graph integration
- **Availability Engine**: `Infrastructure/Scheduling/AvailabilityService.cs` - Multi-participant conflict detection

## Development Patterns

### AI-First Approach
- **NO hardcoded responses**: All bot responses should go through OpenWebUI client
- **NO conditional logic**: Use `ICleanOpenWebUIClient.ExtractParametersAsync()` for parsing user requests
- **System prompts**: Define AI behavior in `CleanOpenWebUIClient` system prompts, not code conditionals

### Mock Data Strategy
- Use `CleanMockDataGenerator` (new system) instead of `MockCalendarGenerator` (legacy)
- Access via `/api/mock-data/interface` endpoint
- Calendar events include **organizer + participants** to simulate real conflicts

### Bot Message Flow
```
User Message → CleanOpenWebUIClient.ExtractParametersAsync() → Find Slots → Generate AI Response
```

## Critical Implementation Rules

### Calendar Conflict Detection
```csharp
// MUST check BOTH organizer AND participant calendars
var organizerAvailability = await GetOrganizerAvailability(organizerId, startDate, endDate);
var participantAvailability = await GetParticipantAvailability(participants, startDate, endDate);
```

### Natural Language Processing
```csharp
// Use AI extraction, NOT conditional parsing
var parameters = await _cleanOpenWebUIClient.ExtractParametersAsync(userMessage);
// Extract: Duration, TimeFrame, Participants from natural language
```

### Response Generation
```csharp
// NO hardcoded templates - use AI response generation
var response = await _openWebUIClient.GenerateResponseAsync(prompt, context);
```

## Key Files to Understand

### Core Bot Logic
- `Bot/InterviewSchedulingBotEnhanced.cs` - Main bot entry point
- `Services/Integration/CleanOpenWebUIClient.cs` - AI parameter extraction

### API Endpoints
- `/api/chat` - Main chat interface with 2 tabs (Chat + Mock Data)
- `/api/mock-data/*` - Clean mock data management

### Data Models
- `Models/IntegrationModels.cs` - API integration types
- `Services/Integration/CleanOpenWebUIClient.cs` - `MeetingParameters` record

## Development Workflows

### Testing Bot Locally
1. Run project: `dotnet run`
2. Navigate to `https://localhost:5001/api/chat`
3. Use Chat tab for conversation testing
4. Use Mock Data tab to simulate calendar scenarios

### Mock Data Generation
- Reset: POST `/api/mock-data/reset-default`
- Generate: POST `/api/mock-data/generate-random`
- Export: GET `/api/mock-data/export`

### Debugging AI Integration
- Check `OpenWebUI:BaseUrl` in `appsettings.json`
- Monitor logs for OpenWebUI API calls
- Fallback responses indicate API connectivity issues

## Common Pitfalls
- ❌ Adding hardcoded message templates
- ❌ Using old `MockCalendarGenerator` instead of `CleanMockDataGenerator`
- ❌ Forgetting to include organizer in availability checks
- ❌ Using conditional parsing instead of AI extraction
- ✅ Always use AI for natural language understanding
- ✅ Generate calendar conflicts for realistic testing
- ✅ Include participant availability details in responses
