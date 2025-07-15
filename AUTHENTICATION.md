# Authentication Flow Implementation

This document describes the authentication flow implementation for the Interview Scheduling Bot.

## Overview

The bot now supports user authentication via OAuth 2.0 using Microsoft Authentication Library (MSAL.NET) to securely access Microsoft Graph APIs on behalf of the user.

## Key Components

### 1. AuthenticationService (`Services/AuthenticationService.cs`)
- **Purpose**: Manages OAuth 2.0 flow and secure token storage
- **Features**:
  - Generates authorization URLs for user sign-in
  - Stores and manages access tokens in memory
  - Automatically refreshes tokens when possible
  - Provides token cleanup functionality

### 2. Enhanced TeamsActivityHandler (`Bots/TeamsActivityHandler.cs`)
- **Purpose**: Handles user interactions and authentication flow within Microsoft Teams
- **Features**:
  - Checks user authentication status before processing commands
  - Presents sign-in cards when users are not authenticated
  - Handles OAuth callback events
  - Provides logout functionality

### 3. Updated GraphCalendarService (`Services/GraphCalendarService.cs`)
- **Purpose**: Interfaces with Microsoft Graph API using user credentials
- **Features**:
  - Uses delegated permissions (user context) instead of app-only permissions
  - Creates calendar events in the authenticated user's calendar
  - Maintains backward compatibility with app-only methods

## Authentication Flow

1. **User Interaction**: User sends a message to the bot
2. **Authentication Check**: Bot checks if user has valid access token
3. **Sign-in Process**: If not authenticated, bot presents sign-in card
4. **OAuth Redirect**: User clicks sign-in and is redirected to Microsoft OAuth
5. **Token Storage**: Upon successful authentication, access token is stored
6. **API Access**: Bot uses stored token to make Graph API calls on user's behalf

## Configuration Requirements

Add the following to `appsettings.json`:

```json
{
  "Authentication": {
    "ClientId": "your-azure-app-client-id",
    "ClientSecret": "your-azure-app-client-secret",
    "TenantId": "your-azure-tenant-id",
    "Authority": "https://login.microsoftonline.com/{tenant}",
    "RedirectUri": "https://token.botframework.com/.auth/web/redirect",
    "Scopes": [
      "https://graph.microsoft.com/Calendars.ReadWrite",
      "https://graph.microsoft.com/User.Read",
      "openid",
      "profile",
      "offline_access"
    ],
    "OAuthPrompt": {
      "ConnectionName": "GraphConnection",
      "Text": "Please sign in to access your calendar",
      "Title": "Sign In",
      "Timeout": 300000
    }
  }
}
```

## Security Features

- **In-Memory Token Storage**: Tokens are stored in memory for development (production should use secure storage)
- **Token Expiration**: Automatically checks token expiration with 5-minute buffer
- **Token Refresh**: Attempts to refresh expired tokens using MSAL cache
- **Clean Logout**: Provides ability to clear stored tokens

## Commands

- `schedule` or `interview`: Start interview scheduling (requires authentication)
- `help`: Show available commands and authentication status
- `logout` or `signout`: Clear stored authentication tokens

## Testing

The implementation includes basic token storage/retrieval functionality that can be tested locally. Network-dependent operations (OAuth redirects) require proper Azure AD configuration.

## Next Steps for Production

1. **Secure Token Storage**: Replace in-memory storage with encrypted database or Azure Key Vault
2. **Error Handling**: Add comprehensive error handling for authentication failures
3. **Token Refresh**: Implement more robust token refresh logic
4. **Logging**: Add detailed logging for authentication events
5. **Configuration Validation**: Add startup validation for required configuration values