# InterviewSchedulingBot

A Microsoft Teams bot that helps schedule interviews by managing calendar events through Microsoft Graph API with secure user authentication.

## Features

- **User Authentication**: Secure OAuth 2.0 flow using MSAL.NET
- **Calendar Integration**: Create, update, and manage calendar events via Microsoft Graph
- **Teams Integration**: Native Microsoft Teams bot interface
- **Interview Scheduling**: Streamlined process for scheduling interviews with multiple participants
- **Online Meetings**: Automatic Teams meeting creation for scheduled interviews

## Authentication Flow

The bot now supports user authentication, allowing it to act on behalf of the authenticated user:

1. Users sign in via OAuth 2.0 when first interacting with the bot
2. Access tokens are securely stored and managed
3. Calendar operations are performed using delegated permissions
4. Users can sign out to clear stored credentials

## Commands

- `schedule` or `interview` - Start the interview scheduling process (requires authentication)
- `help` - Show available commands and authentication status  
- `logout` or `signout` - Sign out and clear stored credentials

## Setup and Configuration

### Prerequisites

- .NET 8.0 or later
- Azure subscription with bot and app registrations
- Microsoft Teams environment for testing

### Microsoft Teams Deployment

This bot includes a complete Microsoft Teams app manifest for easy deployment:

1. **Quick Start**: Use the provided script to create a Teams app package:
   ```bash
   ./create-teams-package.sh
   ```

2. **Manual Setup**: 
   - Update `manifest.json` with your Microsoft App ID
   - Create a zip file with `manifest.json`, `icon-outline.png`, and `icon-color.png`
   - Upload to Microsoft Teams Developer Portal

3. **Detailed Instructions**: See [TEAMS_DEPLOYMENT.md](TEAMS_DEPLOYMENT.md) for complete deployment steps

The bot supports all Teams contexts:
- **Personal conversations**: 1:1 chat with the bot
- **Team channels**: Add the bot to team channels
- **Group chats**: Bot can participate in group conversations

### Configuration

1. **Azure App Registration**: Create an Azure AD app registration with appropriate permissions
2. **Bot Framework**: Register the bot in Azure Bot Service
3. **Configuration**: Update `appsettings.json` with your credentials:

```json
{
  "MicrosoftAppId": "your-bot-app-id",
  "MicrosoftAppPassword": "your-bot-app-password",
  "MicrosoftAppTenantId": "your-tenant-id",
  "Authentication": {
    "ClientId": "your-azure-app-client-id",
    "ClientSecret": "your-azure-app-client-secret", 
    "TenantId": "your-azure-tenant-id"
  },
  "GraphApi": {
    "ClientId": "your-graph-app-client-id",
    "ClientSecret": "your-graph-app-client-secret",
    "TenantId": "your-azure-tenant-id"
  },
  "GraphScheduling": {
    "UseMockService": false
  }
}
```

### Mock Service for Development

When Azure credentials are not available, you can use the mock Graph API service for development and testing:

1. **Enable Mock Service**: Set `"GraphScheduling:UseMockService": true` in `appsettings.json`
2. **Development Mode**: The bot will use predefined fake meeting time suggestions
3. **Testing**: Perfect for testing the conversational flow without live credentials
4. **Easy Switch**: Change the flag to `false` to use the real Microsoft Graph API

**Mock Service Features:**
- Returns realistic fake meeting time suggestions
- Simulates confidence scoring and suggestion reasons
- Respects working hours and day constraints
- Generates fake event IDs for booking simulation
- Maintains the same interface as the real service

### Required Permissions

The Azure AD app registration needs the following Microsoft Graph permissions:
- `Calendars.ReadWrite` (Delegated)
- `User.Read` (Delegated)

## Running the Bot

```bash
dotnet build
dotnet run
```

The bot will start and listen for incoming requests from Microsoft Teams. During startup, the configuration validation service will check all required settings and log their status.

### Configuration Validation

The bot includes automatic configuration validation at startup. If any required settings are missing, warnings will be logged to help identify configuration issues.

## Architecture

The bot follows a layered architecture with clear separation of concerns:

### Integration Layer
- **TeamsIntegrationService**: Microsoft Teams bot interactions and messaging
- **CalendarIntegrationService**: Calendar operations abstraction (Microsoft Graph API)
- **ExternalAIService**: External AI providers abstraction

### Business Layer
- **SchedulingBusinessService**: Pure business logic for interview scheduling
- **Business Rules**: Validation, conflict analysis, and optimization algorithms
- **Interview Logic**: Type-specific optimizations (Technical, Panel, Final, etc.)

### API Layer
- **SchedulingApiController**: RESTful API with Swagger documentation
- **Clear Interfaces**: Communication between business and integration layers
- **Error Handling**: Comprehensive validation and error responses

For detailed architectural information, see [ARCHITECTURE.md](ARCHITECTURE.md).

## API Documentation

The bot now includes a comprehensive RESTful API with Swagger documentation:

- **Swagger UI**: Available at `/swagger` (development environment)
- **OpenAPI Specification**: Complete API documentation
- **Interactive Testing**: Test API endpoints directly from Swagger UI

### Key API Endpoints
- `POST /api/scheduling/find-optimal-slots` - Find optimal interview time slots
- `POST /api/scheduling/validate` - Validate scheduling requirements  
- `POST /api/scheduling/analyze-conflicts` - Analyze scheduling conflicts

## External AI Integration

The bot uses external AI APIs for intelligent scheduling:
- **No Built-in LLMs**: Uses external providers like Azure OpenAI
- **Natural Language Processing**: Extract requirements from conversational input
- **Intelligent Ranking**: AI-powered meeting time suggestions
- **Pattern Analysis**: Learn from historical scheduling data

## Security

- OAuth 2.0 authentication with MSAL.NET
- Secure token storage (in-memory for development)
- Delegated permissions for user-context operations
- Automatic token refresh handling

## Development

For detailed authentication implementation information, see [AUTHENTICATION.md](AUTHENTICATION.md).

### Build Notes

The project builds successfully with some deprecation warnings related to MSAL.NET account management methods. These warnings are acceptable and do not impact functionality. The deprecated methods are still functional and will be addressed in future MSAL.NET versions.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request