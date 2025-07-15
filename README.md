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
  }
}
```

### Required Permissions

The Azure AD app registration needs the following Microsoft Graph permissions:
- `Calendars.ReadWrite` (Delegated)
- `User.Read` (Delegated)

## Running the Bot

```bash
dotnet run
```

The bot will start and listen for incoming requests from Microsoft Teams.

## Architecture

- **TeamsActivityHandler**: Manages bot interactions and authentication flow
- **AuthenticationService**: Handles OAuth 2.0 flow and token management
- **GraphCalendarService**: Interfaces with Microsoft Graph API for calendar operations
- **SchedulingRequest**: Data model for interview scheduling requests

## Security

- OAuth 2.0 authentication with MSAL.NET
- Secure token storage (in-memory for development)
- Delegated permissions for user-context operations
- Automatic token refresh handling

## Development

For detailed authentication implementation information, see [AUTHENTICATION.md](AUTHENTICATION.md).

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request