# Microsoft Teams Integration

This directory contains the Microsoft Teams App Manifest and related files for deploying the Interview Scheduling Bot to Microsoft Teams.

## Files

- `manifest.json` - Microsoft Teams App Manifest file
- `icon-outline.png` - 32x32 outline icon for Teams (black and white)
- `icon-color.png` - 192x192 color icon for Teams (full color)

## Deployment Steps

### 1. Configure Your Bot

Before deploying to Teams, ensure your bot is properly configured:

1. **Azure Bot Service Registration**:
   - Register your bot in Azure Bot Service
   - Note the Microsoft App ID and App Password
   - Configure the messaging endpoint: `https://your-bot-domain.com/api/messages`

2. **Update Configuration**:
   - Update `appsettings.json` with your Microsoft App ID and App Password
   - Configure the authentication settings for Microsoft Graph API

3. **Update Manifest**:
   - Replace `{{MICROSOFT_APP_ID}}` placeholders in `manifest.json` with your actual Microsoft App ID
   - Optionally update the developer information and URLs

### 2. Create Teams App Package

1. Create a zip file containing:
   - `manifest.json`
   - `icon-outline.png`
   - `icon-color.png`

   ```bash
   zip -r teams-app.zip manifest.json icon-outline.png icon-color.png
   ```

### 3. Deploy to Teams

**Option A: Developer Portal (Recommended)**
1. Go to [Microsoft Teams Developer Portal](https://dev.teams.microsoft.com)
2. Sign in with your Microsoft account
3. Click "Apps" in the left navigation
4. Click "Import app" and upload your `teams-app.zip` file
5. Review and publish your app

**Option B: Teams Admin Center**
1. Go to [Microsoft Teams Admin Center](https://admin.teams.microsoft.com)
2. Navigate to "Teams apps" > "Setup policies"
3. Upload your custom app package
4. Configure app permissions and availability

**Option C: Direct Upload (for testing)**
1. Open Microsoft Teams
2. Go to "Apps" in the left sidebar
3. Click "Upload a custom app" (requires developer mode enabled)
4. Upload your `teams-app.zip` file

### 4. Test Your Bot

1. Once installed, find your bot in the Teams app store
2. Click "Add" to start a conversation
3. Test the bot commands:
   - Type "help" to see available commands
   - Type "schedule" to start scheduling process
   - Type "find optimal" for AI-driven scheduling

## Bot Capabilities

The bot supports the following in Microsoft Teams:

- **Personal conversations**: 1:1 chat with the bot
- **Team conversations**: Bot can be added to team channels
- **Group chats**: Bot can participate in group conversations
- **Authentication**: OAuth 2.0 flow for Microsoft Graph access
- **Calendar integration**: Access to user's calendar for scheduling
- **AI-driven scheduling**: Intelligent meeting time suggestions
- **Teams meeting creation**: Automatic Teams meeting links

## Commands

- `schedule` or `interview` - Start the interview scheduling process
- `ai schedule` or `find optimal` - Use AI-driven intelligent scheduling
- `find slots` - Find available time slots (basic scheduling)
- `book [number]` - Book a meeting from suggestions
- `help` - Show available commands and authentication status
- `logout` or `signout` - Sign out and clear stored credentials

## Permissions

The bot requires the following permissions:

- **Microsoft Graph API**:
  - `Calendars.ReadWrite` - To read and create calendar events
  - `User.Read` - To read user profile information

- **Teams Permissions**:
  - `identity` - To access user identity information
  - `messageTeamMembers` - To send messages to team members

## Security

- OAuth 2.0 authentication with Microsoft Graph
- Secure token storage and management
- Delegated permissions (acts on behalf of authenticated user)
- Automatic token refresh handling

## Support

For issues or questions:
1. Check the bot logs for error messages
2. Verify Azure Bot Service configuration
3. Ensure Microsoft Graph API permissions are granted
4. Review the main README.md for detailed setup instructions

## Icon Customization

The included icons are basic placeholders. For production deployment:
1. Replace `icon-outline.png` with your custom 32x32 black and white outline icon
2. Replace `icon-color.png` with your custom 192x192 full color icon
3. Update the `accentColor` in `manifest.json` to match your brand