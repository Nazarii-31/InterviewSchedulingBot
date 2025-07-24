using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Interfaces;
using InterviewSchedulingBot.Dialogs;
using System.Text.Json;
using System.Text;
using Microsoft.Bot.Connector.Authentication;

namespace InterviewSchedulingBot.Bots
{
    public class InterviewBot : TeamsActivityHandler
    {
        private readonly IGraphCalendarService _calendarService;
        private readonly IAuthenticationService _authService;
        private readonly ISchedulingService _schedulingService;
        private readonly IGraphSchedulingService _graphSchedulingService;
        private readonly IAISchedulingService _aiSchedulingService;
        private readonly IConfiguration _configuration;
        private readonly Dialog _dialog;
        private readonly BotState _conversationState;
        private readonly BotState _userState;

        public InterviewBot(
            IGraphCalendarService calendarService, 
            IAuthenticationService authService, 
            ISchedulingService schedulingService,
            IGraphSchedulingService graphSchedulingService,
            IAISchedulingService aiSchedulingService,
            IConfiguration configuration,
            ConversationState conversationState,
            UserState userState)
        {
            _calendarService = calendarService;
            _authService = authService;
            _schedulingService = schedulingService;
            _graphSchedulingService = graphSchedulingService;
            _aiSchedulingService = aiSchedulingService;
            _configuration = configuration;
            _conversationState = conversationState;
            _userState = userState;
            _dialog = new InterviewSchedulingDialog(schedulingService, graphSchedulingService, configuration);
        }

        protected override async Task OnMembersAddedAsync(
            IList<ChannelAccount> membersAdded,
            ITurnContext<IConversationUpdateActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome! I'm your AI-powered Interview Scheduling Assistant. I specialize in scanning participant calendars to find perfect meeting times for everyone.\n\n" +
                             "🔍 **What I do:**\n" +
                             "• Scan all participant calendars comprehensively\n" +
                             "• Find optimal time slots for all or majority of attendees\n" +
                             "• Provide detailed suggestions with AI-driven reasoning\n" +
                             "• Analyze availability patterns and preferences\n\n" +
                             "To get started with calendar scanning, I'll need you to sign in to access calendar data. Send me a message to begin!";
            
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText), cancellationToken);
                }
            }
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occurred during the turn.
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            var isAuthenticated = await _authService.IsUserAuthenticatedAsync(userId);

            if (!isAuthenticated)
            {
                await HandleAuthenticationAsync(turnContext, cancellationToken);
                return;
            }

            var userMessage = turnContext.Activity.Text?.Trim();
            
            if (string.IsNullOrEmpty(userMessage))
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("Please provide a valid message."), 
                    cancellationToken);
                return;
            }

            // Check if user wants to start the conversational dialog
            if (userMessage.ToLower().Contains("schedule interview") || 
                userMessage.ToLower().Contains("start interview") ||
                userMessage.ToLower().Contains("new interview") ||
                userMessage.ToLower().Contains("conversational"))
            {
                await RunDialogAsync(turnContext, cancellationToken);
                return;
            }

            // Handle other commands (keep existing functionality)
            if (userMessage.ToLower().Contains("schedule") || userMessage.ToLower().Contains("interview"))
            {
                await HandleScheduleRequestAsync(turnContext, cancellationToken);
            }
            else if (userMessage.ToLower().Contains("ai") && userMessage.ToLower().Contains("schedule"))
            {
                await HandleAIScheduleRequestAsync(turnContext, cancellationToken);
            }
            else if (userMessage.ToLower().Contains("smart") && userMessage.ToLower().Contains("schedule"))
            {
                await HandleSmartScheduleRequestAsync(turnContext, cancellationToken);
            }
            else if (userMessage.ToLower().Contains("find") && userMessage.ToLower().Contains("slots"))
            {
                await HandleFindSlotsRequestAsync(turnContext, cancellationToken);
            }
            else if (userMessage.ToLower().Contains("find") && userMessage.ToLower().Contains("optimal"))
            {
                await HandleFindOptimalTimesRequestAsync(turnContext, cancellationToken);
            }
            else if (userMessage.ToLower().Contains("ai") && userMessage.ToLower().Contains("demo"))
            {
                await HandleAIDemoRequestAsync(turnContext, cancellationToken);
            }
            else if (userMessage.ToLower().Contains("insights"))
            {
                await HandleInsightsRequestAsync(turnContext, cancellationToken);
            }
            else if (userMessage.ToLower().Contains("help"))
            {
                await HandleHelpCommandAsync(turnContext, cancellationToken);
            }
            else if (userMessage.ToLower().Contains("logout") || userMessage.ToLower().Contains("signout"))
            {
                await HandleLogoutAsync(turnContext, cancellationToken);
            }
            else
            {
                // Try to continue any existing dialog first
                await RunDialogAsync(turnContext, cancellationToken);
            }
        }

        private async Task RunDialogAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            // Create a dialog context
            var dialogSet = new DialogSet(_conversationState.CreateProperty<DialogState>(nameof(DialogState)));
            dialogSet.Add(_dialog);

            var dialogContext = await dialogSet.CreateContextAsync(turnContext, cancellationToken);
            var results = await dialogContext.ContinueDialogAsync(cancellationToken);
            
            // If no dialog is running, start the main dialog
            if (results.Status == DialogTurnStatus.Empty)
            {
                await dialogContext.BeginDialogAsync(_dialog.Id, null, cancellationToken);
            }
        }

        private async Task HandleAuthenticationAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            try
            {
                var userId = turnContext.Activity.From.Id;
                var conversationId = turnContext.Activity.Conversation.Id;
                
                // Create sign-in card
                var signInCard = new SigninCard
                {
                    Text = _configuration["Authentication:OAuthPrompt:Text"] ?? "Please sign in to access your calendar",
                    Buttons = new[]
                    {
                        new CardAction
                        {
                            Title = _configuration["Authentication:OAuthPrompt:Title"] ?? "Sign In",
                            Type = ActionTypes.Signin,
                            Value = _authService.GetAuthorizationUrl(userId, conversationId)
                        }
                    }
                };

                var attachment = new Attachment
                {
                    ContentType = SigninCard.ContentType,
                    Content = signInCard
                };

                var reply = MessageFactory.Attachment(attachment);
                reply.Text = "You need to sign in before I can help you with calendar operations.";

                await turnContext.SendActivityAsync(reply, cancellationToken);
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"Sorry, I encountered an error setting up authentication. Please try again later. Error: {ex.Message}"), 
                    cancellationToken);
            }
        }

        private async Task HandleScheduleRequestAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            var accessToken = await _authService.GetAccessTokenAsync(userId);

            if (string.IsNullOrEmpty(accessToken))
            {
                await HandleAuthenticationAsync(turnContext, cancellationToken);
                return;
            }

            var response = "Great! You're authenticated and ready for comprehensive calendar analysis.\n\n" +
                          "I offer advanced calendar scanning approaches:\n\n" +
                          "🔍 **AI-Powered Calendar Analysis** (Recommended)\n" +
                          "- Type 'find optimal' to scan all participant calendars intelligently\n" +
                          "- Uses Microsoft Graph's advanced algorithms for deep calendar analysis\n" +
                          "- Finds perfect time slots for all or majority of participants\n" +
                          "- Provides detailed reasoning for each suggestion\n\n" +
                          "📊 **Basic Calendar Scanning**\n" +
                          "- Type 'find slots' for simple availability checking\n" +
                          "- Basic conflict detection across participant calendars\n\n" +
                          "For comprehensive calendar analysis, I'll need:\n" +
                          "- All participant email addresses\n" +
                          "- Desired meeting duration (in minutes)\n" +
                          "- Date range to scan\n\n" +
                          "🎯 **Result**: Detailed optimal time suggestions with AI-driven confidence scoring and reasoning.\n\n" +
                          "Try typing 'find optimal' to experience advanced calendar scanning!";

            await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
        }

        private async Task HandleAIScheduleRequestAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            var accessToken = await _authService.GetAccessTokenAsync(userId);

            if (string.IsNullOrEmpty(accessToken))
            {
                await HandleAuthenticationAsync(turnContext, cancellationToken);
                return;
            }

            var response = "🤖 **AI-Driven Interview Scheduling**\n\n" +
                          "I'll use Microsoft Graph's intelligent scheduling to find optimal meeting times.\n\n" +
                          "**How to use:**\n" +
                          "Provide attendee emails and preferences in this format:\n" +
                          "`attendee1@company.com, attendee2@company.com | duration:60 | days:7`\n\n" +
                          "**Example:**\n" +
                          "`john@company.com, jane@company.com | duration:90 | days:14`\n\n" +
                          "**Parameters:**\n" +
                          "- **Attendees:** Comma-separated email addresses\n" +
                          "- **Duration:** Meeting duration in minutes (default: 60)\n" +
                          "- **Days:** Number of days to search ahead (default: 14)\n\n" +
                          "Or type `optimal demo` to see a demonstration.\n\n" +
                          "✨ **AI Features:**\n" +
                          "- Smart conflict detection\n" +
                          "- Respects working hours and time zones\n" +
                          "- Confidence scoring for suggestions\n" +
                          "- Optimized for productivity";

            await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
        }

        private async Task HandleFindOptimalTimesRequestAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            var accessToken = await _authService.GetAccessTokenAsync(userId);

            if (string.IsNullOrEmpty(accessToken))
            {
                await HandleAuthenticationAsync(turnContext, cancellationToken);
                return;
            }

            var userMessage = turnContext.Activity.Text?.ToLower() ?? "";
            
            if (userMessage.Contains("demo") || userMessage.Contains("example"))
            {
                await HandleOptimalTimesDemo(turnContext, cancellationToken);
                return;
            }

            // Check if the user provided scheduling parameters
            if (userMessage.Contains("@") && userMessage.Contains("|"))
            {
                await ProcessOptimalTimesRequest(turnContext, cancellationToken);
                return;
            }

            // Show instructions
            await HandleAIScheduleRequestAsync(turnContext, cancellationToken);
        }

        private async Task HandleOptimalTimesDemo(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;

            try
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("🔍 **AI-Driven Scheduling Demo**\n\nSearching for optimal meeting times using Microsoft Graph AI... This may take a moment."), 
                    cancellationToken);

                // Create a sample GraphSchedulingRequest
                var defaultDuration = _configuration.GetValue<int>("Scheduling:DefaultDurationMinutes", 60);
                var searchDays = _configuration.GetValue<int>("Scheduling:SearchDays", 14);

                var graphSchedulingRequest = new GraphSchedulingRequest
                {
                    AttendeeEmails = new List<string> { "demo@example.com", "test@example.com" },
                    StartDate = DateTime.Now.AddHours(1), // Start from next hour
                    EndDate = DateTime.Now.AddDays(searchDays),
                    DurationMinutes = defaultDuration,
                    WorkingHoursStart = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:StartTime"] ?? "09:00"),
                    WorkingHoursEnd = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:EndTime"] ?? "17:00"),
                    MaxSuggestions = 5
                };

                // Convert working days from config
                var workingDaysConfig = _configuration.GetSection("Scheduling:WorkingHours:WorkingDays").Get<string[]>();
                if (workingDaysConfig != null)
                {
                    graphSchedulingRequest.WorkingDays = workingDaysConfig
                        .Select(day => Enum.Parse<DayOfWeek>(day))
                        .ToList();
                }

                var graphSchedulingResponse = await _graphSchedulingService.FindOptimalMeetingTimesAsync(graphSchedulingRequest, userId);

                if (graphSchedulingResponse.IsSuccess && graphSchedulingResponse.HasSuggestions)
                {
                    var responseText = $"✅ **AI Calendar Analysis Complete - {graphSchedulingResponse.MeetingTimeSuggestions.Count} Perfect Slots Found!**\n\n" +
                                     $"**📊 Calendar Scanning Results:**\n" +
                                     $"- Duration: {graphSchedulingRequest.DurationMinutes} minutes\n" +
                                     $"- Participants Analyzed: {string.Join(", ", graphSchedulingRequest.AttendeeEmails)}\n" +
                                     $"- Date Range Scanned: {graphSchedulingRequest.StartDate:yyyy-MM-dd} to {graphSchedulingRequest.EndDate:yyyy-MM-dd}\n" +
                                     $"- Calendars Checked: {graphSchedulingRequest.AttendeeEmails.Count} participant calendars\n\n" +
                                     $"**🎯 Optimal Time Suggestions (Perfect for All/Majority):**\n{graphSchedulingResponse.FormattedSuggestionsWithoutBooking}\n\n" +
                                     $"💡 **Advanced Calendar Intelligence:**\n" +
                                     $"- Deep calendar conflict analysis across all participants\n" +
                                     $"- Productivity optimization based on time patterns\n" +
                                     $"- Working hours and time zone consideration\n" +
                                     $"- AI-driven confidence scoring with detailed reasoning\n" +
                                     $"- Majority availability prioritization\n\n" +
                                     $"📋 **Recommendation**: Use the highest-confidence suggestions for maximum participant availability.\n\n" +
                                     $"*This demonstration shows advanced calendar scanning with Microsoft Graph's intelligent algorithms.*";

                    await turnContext.SendActivityAsync(MessageFactory.Text(responseText), cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text($"❌ AI Scheduling failed: {graphSchedulingResponse.Message}"), 
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"❌ Error during AI scheduling: {ex.Message}"), 
                    cancellationToken);
            }
        }

        private async Task ProcessOptimalTimesRequest(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            var userMessage = turnContext.Activity.Text ?? "";

            try
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("🔍 **Scanning all participant calendars for optimal time slots...**"), 
                    cancellationToken);

                // Parse the user input
                var request = ParseSchedulingRequest(userMessage);

                if (request == null)
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text("❌ Invalid format. Please use: `email1@company.com, email2@company.com | duration:60 | days:7`"), 
                        cancellationToken);
                    return;
                }

                var graphSchedulingResponse = await _graphSchedulingService.FindOptimalMeetingTimesAsync(request, userId);

                if (graphSchedulingResponse.IsSuccess && graphSchedulingResponse.HasSuggestions)
                {
                    var responseText = $"✅ **Calendar Analysis Complete - {graphSchedulingResponse.MeetingTimeSuggestions.Count} Perfect Time Slots Found!**\n\n" +
                                     $"**📊 Participant Calendar Analysis:**\n" +
                                     $"- Total calendars scanned: {request.AttendeeEmails.Count}\n" +
                                     $"- Participants: {string.Join(", ", request.AttendeeEmails)}\n" +
                                     $"- Meeting duration: {request.DurationMinutes} minutes\n" +
                                     $"- Search period: {request.StartDate:MMM dd} - {request.EndDate:MMM dd}\n\n" +
                                     $"**🎯 Optimal Time Suggestions (Perfect for All/Majority):**\n{graphSchedulingResponse.FormattedSuggestionsWithoutBooking}\n\n" +
                                     $"💡 **Advanced AI Analysis:**\n" +
                                     $"- Deep calendar conflict detection across all participants\n" +
                                     $"- Productivity optimization using Microsoft Graph algorithms\n" +
                                     $"- Intelligent confidence scoring with detailed reasoning\n" +
                                     $"- Majority availability prioritization for best outcomes\n\n" +
                                     $"📋 **Next Steps**: Review the suggestions above and coordinate with participants to finalize the meeting time.";

                    await turnContext.SendActivityAsync(MessageFactory.Text(responseText), cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text($"❌ Calendar Analysis failed: {graphSchedulingResponse.Message}"), 
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"❌ Error during calendar analysis: {ex.Message}"), 
                    cancellationToken);
            }
        }

        private GraphSchedulingRequest? ParseSchedulingRequest(string input)
        {
            try
            {
                var parts = input.Split('|');
                if (parts.Length < 1) return null;

                var attendeePart = parts[0].Trim();
                var attendeeEmails = attendeePart.Split(',').Select(e => e.Trim()).Where(e => e.Contains("@")).ToList();

                if (attendeeEmails.Count == 0) return null;

                var request = new GraphSchedulingRequest
                {
                    AttendeeEmails = attendeeEmails,
                    StartDate = DateTime.Now.AddHours(1),
                    EndDate = DateTime.Now.AddDays(14),
                    DurationMinutes = 60,
                    WorkingHoursStart = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:StartTime"] ?? "09:00"),
                    WorkingHoursEnd = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:EndTime"] ?? "17:00"),
                    MaxSuggestions = 10
                };

                // Parse additional parameters
                for (int i = 1; i < parts.Length; i++)
                {
                    var param = parts[i].Trim();
                    if (param.StartsWith("duration:"))
                    {
                        if (int.TryParse(param.Substring(9), out int duration))
                        {
                            request.DurationMinutes = duration;
                        }
                    }
                    else if (param.StartsWith("days:"))
                    {
                        if (int.TryParse(param.Substring(5), out int days))
                        {
                            request.EndDate = DateTime.Now.AddDays(days);
                        }
                    }
                }

                // Convert working days from config
                var workingDaysConfig = _configuration.GetSection("Scheduling:WorkingHours:WorkingDays").Get<string[]>();
                if (workingDaysConfig != null)
                {
                    request.WorkingDays = workingDaysConfig
                        .Select(day => Enum.Parse<DayOfWeek>(day))
                        .ToList();
                }

                return request;
            }
            catch
            {
                return null;
            }
        }

        private async Task HandleFindSlotsRequestAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            var accessToken = await _authService.GetAccessTokenAsync(userId);

            if (string.IsNullOrEmpty(accessToken))
            {
                await HandleAuthenticationAsync(turnContext, cancellationToken);
                return;
            }

            // For demo purposes, let's use some example values
            // In a real implementation, you would collect this information through a conversation flow
            var userMessage = turnContext.Activity.Text?.ToLower() ?? "";
            
            if (userMessage.Contains("example") || userMessage.Contains("demo"))
            {
                await HandleExampleSchedulingAsync(turnContext, cancellationToken);
                return;
            }

            var response = "📅 **Interview Scheduling Assistant**\n\n" +
                          "To find available time slots, please provide the following information:\n\n" +
                          "**Format:** attendee1@company.com, attendee2@company.com | duration:60 | days:7\n\n" +
                          "**Example:** \n" +
                          "`john@company.com, jane@company.com | duration:90 | days:14`\n\n" +
                          "Or type `example demo` to see a demonstration with mock data.\n\n" +
                          "**Parameters:**\n" +
                          "- **Attendees:** Comma-separated email addresses\n" +
                          "- **Duration:** Meeting duration in minutes (default: 60)\n" +
                          "- **Days:** Number of days to search ahead (default: 14)\n\n" +
                          "I'll search for available slots during business hours (9 AM - 5 PM, Monday-Friday).";

            await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
        }

        private async Task HandleExampleSchedulingAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;

            try
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("🔍 Searching for available time slots... This may take a moment."), 
                    cancellationToken);

                // Create a sample availability request
                var defaultDuration = _configuration.GetValue<int>("Scheduling:DefaultDurationMinutes", 60);
                var searchDays = _configuration.GetValue<int>("Scheduling:SearchDays", 14);

                var availabilityRequest = new AvailabilityRequest
                {
                    AttendeeEmails = new List<string> { "demo@example.com", "test@example.com" },
                    StartDate = DateTime.Now.AddHours(1), // Start from next hour
                    EndDate = DateTime.Now.AddDays(searchDays),
                    DurationMinutes = defaultDuration,
                    WorkingHoursStart = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:StartTime"] ?? "09:00"),
                    WorkingHoursEnd = TimeSpan.Parse(_configuration["Scheduling:WorkingHours:EndTime"] ?? "17:00")
                };

                // Convert working days from config
                var workingDaysConfig = _configuration.GetSection("Scheduling:WorkingHours:WorkingDays").Get<string[]>();
                if (workingDaysConfig != null)
                {
                    availabilityRequest.WorkingDays = workingDaysConfig
                        .Select(day => Enum.Parse<DayOfWeek>(day))
                        .ToList();
                }

                var schedulingResponse = await _schedulingService.FindAvailableTimeSlotsAsync(availabilityRequest, userId);

                if (schedulingResponse.IsSuccess && schedulingResponse.HasAvailableSlots)
                {
                    var responseText = $"✅ **Found {schedulingResponse.AvailableSlots.Count} available time slots!**\n\n" +
                                     $"**Search criteria:**\n" +
                                     $"- Duration: {schedulingResponse.RequestedDurationMinutes} minutes\n" +
                                     $"- Attendees: {string.Join(", ", schedulingResponse.AttendeeEmails)}\n" +
                                     $"- Date range: {schedulingResponse.SearchStartDate:yyyy-MM-dd} to {schedulingResponse.SearchEndDate:yyyy-MM-dd}\n\n" +
                                     $"**Available slots:**\n{schedulingResponse.FormattedSlotsText}\n\n" +
                                     $"💡 *This was a demonstration with mock data. In a real scenario, I would check actual calendar availability.*";

                    await turnContext.SendActivityAsync(MessageFactory.Text(responseText), cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text($"❌ No available slots found: {schedulingResponse.Message}"), 
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"❌ Error during scheduling: {ex.Message}"), 
                    cancellationToken);
            }
        }

        private async Task HandleSmartScheduleRequestAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            var userMessage = turnContext.Activity.Text ?? "";

            try
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("🤖 **Smart AI Scheduling** - Analyzing your preferences and calendar patterns..."), 
                    cancellationToken);

                // Parse smart scheduling request
                var aiRequest = ParseSmartSchedulingRequest(userMessage, userId);
                if (aiRequest == null)
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text("❌ Invalid format. Please use: `john@company.com, jane@company.com | smart | duration:60 | days:7`"), 
                        cancellationToken);
                    return;
                }

                // Use AI scheduling service
                var aiResponse = await _aiSchedulingService.FindOptimalMeetingTimesAsync(aiRequest);

                if (aiResponse.IsSuccess && aiResponse.HasPredictions)
                {
                    var responseText = $"✅ **AI Found {aiResponse.PredictedTimeSlots.Count} Smart Scheduling Options!**\n\n" +
                                     $"**🧠 AI Analysis:**\n" +
                                     $"- Overall Confidence: {aiResponse.OverallConfidence * 100:F0}%\n" +
                                     $"- Processing Time: {aiResponse.ProcessingTimeMs}ms\n" +
                                     $"- Algorithm: {aiResponse.AlgorithmVersion}\n\n" +
                                     $"**🎯 Smart Predictions:**\n" +
                                     FormatAIPredictions(aiResponse.PredictedTimeSlots) + "\n\n" +
                                     $"**💡 AI Recommendations:**\n" +
                                     string.Join("\n", aiResponse.Recommendations.Select(r => $"• {r}")) + "\n\n" +
                                     $"**📅 To book**: Reply with 'book [number]' (e.g., 'book 1')";

                    await turnContext.SendActivityAsync(MessageFactory.Text(responseText), cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text($"❌ AI Scheduling failed: {aiResponse.Message}"), 
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"❌ Error in smart scheduling: {ex.Message}"), 
                    cancellationToken);
            }
        }

        private async Task HandleAIDemoRequestAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;

            try
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("🔬 **AI Scheduling Demo** - Showcasing advanced machine learning capabilities..."), 
                    cancellationToken);

                // Create demo AI request
                var aiRequest = new AISchedulingRequest
                {
                    UserId = userId,
                    AttendeeEmails = new List<string> { "demo@company.com", "ai-test@company.com" },
                    StartDate = DateTime.Now.AddHours(1),
                    EndDate = DateTime.Now.AddDays(7),
                    DurationMinutes = 60,
                    UseLearningAlgorithm = true,
                    UseHistoricalData = true,
                    UseUserPreferences = true,
                    UseAttendeePatterns = true,
                    OptimizeForProductivity = true,
                    MaxSuggestions = 5
                };

                var aiResponse = await _aiSchedulingService.FindOptimalMeetingTimesAsync(aiRequest);

                if (aiResponse.IsSuccess && aiResponse.HasPredictions)
                {
                    var insights = await _aiSchedulingService.GetAIInsightsAsync(userId);
                    
                    var responseText = $"✅ **AI Demo Results - Advanced Scheduling Intelligence**\n\n" +
                                     $"**🤖 Machine Learning Analysis:**\n" +
                                     $"- Confidence: {aiResponse.OverallConfidence * 100:F0}%\n" +
                                     $"- Processing Time: {aiResponse.ProcessingTimeMs}ms\n" +
                                     $"- Historical Data Points: {insights.GetValueOrDefault("HistoricalDataPoints", 0)}\n" +
                                     $"- Identified Patterns: {insights.GetValueOrDefault("IdentifiedPatterns", 0)}\n" +
                                     $"- Model Accuracy: {(double)insights.GetValueOrDefault("ModelAccuracy", 0.85) * 100:F0}%\n\n" +
                                     $"**🎯 AI-Optimized Time Slots:**\n" +
                                     FormatAIPredictions(aiResponse.PredictedTimeSlots) + "\n\n" +
                                     $"**🧠 AI Insights:**\n" +
                                     $"- Prediction Strength: {insights.GetValueOrDefault("PredictionStrength", "Medium")}\n" +
                                     $"- User Preference Alignment: {(double)insights.GetValueOrDefault("UserPreferenceAlignment", 0.7) * 100:F0}%\n" +
                                     $"- Recommended Time: {insights.GetValueOrDefault("RecommendedTimeSlot", "Not available")}\n\n" +
                                     $"**📈 Smart Recommendations:**\n" +
                                     string.Join("\n", aiResponse.Recommendations.Select(r => $"• {r}")) + "\n\n" +
                                     $"**🔬 This demo showcases:**\n" +
                                     $"• Machine learning pattern recognition\n" +
                                     $"• Historical data analysis\n" +
                                     $"• Predictive scheduling optimization\n" +
                                     $"• User preference learning\n" +
                                     $"• Dynamic calendar adaptation\n\n" +
                                     $"*Try 'smart schedule' for production-ready AI scheduling!*";

                    await turnContext.SendActivityAsync(MessageFactory.Text(responseText), cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text($"❌ AI Demo failed: {aiResponse.Message}"), 
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"❌ Error in AI demo: {ex.Message}"), 
                    cancellationToken);
            }
        }

        private async Task HandleInsightsRequestAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;

            try
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("📊 **Analyzing your scheduling patterns and preferences...**"), 
                    cancellationToken);

                var insights = await _aiSchedulingService.GetAIInsightsAsync(userId);
                var userPreferences = await _aiSchedulingService.GetUserPreferencesAsync(userId);
                var patterns = await _aiSchedulingService.AnalyzeSchedulingPatternsAsync(userId);

                var responseText = $"📈 **Your AI Scheduling Insights**\n\n" +
                                 $"**🎯 Personal Preferences:**\n" +
                                 $"- Total Scheduled Meetings: {userPreferences?.TotalScheduledMeetings ?? 0}\n" +
                                 $"- Average Rescheduling Rate: {(userPreferences?.AverageReschedulingRate ?? 0.3) * 100:F0}%\n" +
                                 $"- Preferred Duration: {userPreferences?.PreferredDurationMinutes ?? 60} minutes\n" +
                                 $"- Optimal Time Window: {userPreferences?.OptimalStartTime:hh\\:mm} - {userPreferences?.OptimalEndTime:hh\\:mm}\n\n" +
                                 $"**📊 Historical Analysis:**\n" +
                                 $"- Data Points Analyzed: {insights.GetValueOrDefault("HistoricalDataPoints", 0)}\n" +
                                 $"- Identified Patterns: {insights.GetValueOrDefault("IdentifiedPatterns", 0)}\n" +
                                 $"- Success Rate: {(double)insights.GetValueOrDefault("HistoricalSuccessIndicator", 0.7) * 100:F0}%\n\n" +
                                 $"**🎯 Optimal Meeting Times:**\n" +
                                 FormatOptimalTimes(insights.GetValueOrDefault("OptimalTimeSlots", new List<string>())) + "\n\n" +
                                 $"**🧠 AI Recommendations:**\n" +
                                 FormatInsightRecommendations(insights.GetValueOrDefault("Recommendations", new List<string>())) + "\n\n" +
                                 $"**🔮 Predictive Insights:**\n" +
                                 $"- Model Accuracy: {(double)insights.GetValueOrDefault("ModelAccuracy", 0.85) * 100:F0}%\n" +
                                 $"- Learning Progress: {(patterns.Count > 5 ? "Advanced" : patterns.Count > 2 ? "Intermediate" : "Basic")}\n\n" +
                                 $"*The AI learns from your scheduling behavior to provide increasingly accurate recommendations.*";

                await turnContext.SendActivityAsync(MessageFactory.Text(responseText), cancellationToken);
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"❌ Error getting insights: {ex.Message}"), 
                    cancellationToken);
            }
        }

        private AISchedulingRequest? ParseSmartSchedulingRequest(string input, string userId)
        {
            try
            {
                var parts = input.Split('|');
                if (parts.Length < 2) return null;

                var attendeeEmails = parts[0].Split(',')
                    .Select(email => email.Trim())
                    .Where(email => email.Contains("@"))
                    .ToList();

                if (attendeeEmails.Count == 0) return null;

                var request = new AISchedulingRequest
                {
                    UserId = userId,
                    AttendeeEmails = attendeeEmails,
                    StartDate = DateTime.Now.AddHours(1),
                    EndDate = DateTime.Now.AddDays(14),
                    DurationMinutes = 60,
                    UseLearningAlgorithm = true,
                    UseHistoricalData = true,
                    UseUserPreferences = true,
                    UseAttendeePatterns = true,
                    OptimizeForProductivity = true
                };

                // Parse additional parameters
                foreach (var part in parts.Skip(1))
                {
                    var trimmed = part.Trim().ToLower();
                    if (trimmed.StartsWith("duration:"))
                    {
                        if (int.TryParse(trimmed.Substring(9), out int duration))
                            request.DurationMinutes = duration;
                    }
                    else if (trimmed.StartsWith("days:"))
                    {
                        if (int.TryParse(trimmed.Substring(5), out int days))
                            request.EndDate = DateTime.Now.AddDays(days);
                    }
                }

                return request;
            }
            catch
            {
                return null;
            }
        }

        private string FormatAIPredictions(List<TimeSlotPrediction> predictions)
        {
            var result = new StringBuilder();
            for (int i = 0; i < predictions.Count; i++)
            {
                var prediction = predictions[i];
                result.AppendLine($"**{i + 1}.** {prediction.StartTime:dddd, MMM dd - HH:mm} - {prediction.EndTime:HH:mm}");
                result.AppendLine($"   • Confidence: {prediction.OverallConfidence * 100:F0}%");
                result.AppendLine($"   • Success Rate: {prediction.PredictedSuccessRate * 100:F0}%");
                result.AppendLine($"   • Reason: {prediction.PredictionReason}");
                if (prediction.IsOptimalSlot)
                    result.AppendLine($"   • ⭐ **Optimal Slot**");
                result.AppendLine();
            }
            return result.ToString();
        }

        private string FormatOptimalTimes(object optimalTimes)
        {
            if (optimalTimes is List<string> timeList && timeList.Count > 0)
            {
                return string.Join("\n", timeList.Select(t => $"• {t}"));
            }
            return "• No specific patterns identified yet";
        }

        private string FormatInsightRecommendations(object recommendations)
        {
            if (recommendations is List<string> recList && recList.Count > 0)
            {
                return string.Join("\n", recList.Select(r => $"• {r}"));
            }
            return "• Continue using the AI scheduler to build more insights";
        }

        private async Task HandleHelpCommandAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            var isAuthenticated = await _authService.IsUserAuthenticatedAsync(userId);

            var helpText = "**Interview Scheduling Bot - AI-Powered Calendar Scanner:**\n\n" +
                          "🔍 **Calendar Analysis & Suggestions:**\n" +
                          "• **find optimal** - Comprehensive calendar scanning for perfect time slots\n" +
                          "• **smart schedule** - Advanced AI analysis with participant learning\n" +
                          "• **ai demo** - Demonstrate intelligent calendar analysis capabilities\n" +
                          "• **insights** - View scheduling patterns and participant preferences\n\n" +
                          "📊 **Calendar Scanning:**\n" +
                          "• **schedule** or **interview** - Learn about calendar analysis options\n" +
                          "• **find slots** - Basic availability checking across calendars\n\n" +
                          "🔧 **General Commands:**\n" +
                          "• **help** - Show this help message\n";

            if (isAuthenticated)
            {
                helpText += "• **logout** or **signout** - Sign out from your account\n\n" +
                           "✅ You are currently signed in and can access all calendar scanning features.\n\n" +
                           "**🌟 Recommended: Try Advanced Calendar Analysis!**\n" +
                           "Type 'schedule interview' for a guided experience that:\n" +
                           "- Greets you personally\n" +
                           "- Collects participant emails\n" +
                           "- Requests meeting duration\n" +
                           "- Scans all calendars comprehensively\n" +
                           "- Provides detailed optimal time suggestions\n\n" +
                           "**🔍 Advanced Calendar Scanning Features:**\n" +
                           "- Deep calendar analysis using Microsoft Graph algorithms\n" +
                           "- Perfect time slot identification for all/majority participants\n" +
                           "- Intelligent conflict detection and resolution\n" +
                           "- AI-driven confidence scoring with detailed explanations\n" +
                           "- Cross-timezone and working hours optimization\n" +
                           "- Detailed reasoning for each suggestion\n\n" +
                           "**📊 Calendar Intelligence:**\n" +
                           "- Scan multiple participant calendars simultaneously\n" +
                           "- Identify optimal availability for everyone\n" +
                           "- Provide comprehensive scheduling recommendations\n\n" +
                           "**🚀 Quick Start:**\n" +
                           "1. Type 'find optimal' to start calendar scanning\n" +
                           "2. Provide participant emails and meeting duration\n" +
                           "3. Review AI-generated optimal time suggestions\n" +
                           "4. Use the detailed recommendations to coordinate with participants\n\n" +
                           GetServiceModeMessage();
            }
            else
            {
                helpText += "\n❌ You need to sign in first to use calendar features.";
            }

            await turnContext.SendActivityAsync(MessageFactory.Text(helpText), cancellationToken);
        }

        private string GetServiceModeMessage()
        {
            var useMockService = _configuration.GetValue<bool>("GraphScheduling:UseMockService", false);
            if (useMockService)
            {
                return "🧪 **Development Mode**: Using mock Graph API service (no Azure credentials required)\n" +
                       "   _This provides realistic fake data for testing purposes_";
            }
            else
            {
                return "🔗 **Production Mode**: Using live Microsoft Graph API";
            }
        }

        private async Task HandleLogoutAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var userId = turnContext.Activity.From.Id;
            await _authService.ClearTokenAsync(userId);

            await turnContext.SendActivityAsync(
                MessageFactory.Text("You have been signed out successfully. Send me a message to sign in again."), 
                cancellationToken);
        }

        protected override async Task OnTeamsSigninVerifyStateAsync(
            ITurnContext<IInvokeActivity> turnContext,
            CancellationToken cancellationToken)
        {
            try
            {
                // Handle the OAuth callback from Teams
                var tokenResponse = turnContext.Activity.Value as TokenResponse;
                
                if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.Token))
                {
                    var userId = turnContext.Activity.From.Id;
                    
                    if (string.IsNullOrEmpty(userId))
                    {
                        await turnContext.SendActivityAsync(
                            MessageFactory.Text("❌ Sign-in failed: Unable to identify user."), 
                            cancellationToken);
                        return;
                    }
                    
                    // Store the token
                    await _authService.StoreTokenAsync(userId, tokenResponse.Token);

                    var response = MessageFactory.Text("✅ Sign-in successful! You can now use the bot to schedule interviews. Type 'help' to see available commands.");
                    
                    await turnContext.SendActivityAsync(response, cancellationToken);
                }
                else
                {
                    var response = MessageFactory.Text("❌ Sign-in failed. Please try again.");
                    
                    await turnContext.SendActivityAsync(response, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"❌ Sign-in process failed: {ex.Message}"), 
                    cancellationToken);
            }
        }

        protected override async Task OnTokenResponseEventAsync(
            ITurnContext<IEventActivity> turnContext,
            CancellationToken cancellationToken)
        {
            try
            {
                // Handle token response from OAuth flow
                var tokenResponse = turnContext.Activity.Value as TokenResponse;
                
                if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.Token))
                {
                    var userId = turnContext.Activity.From.Id;
                    
                    if (string.IsNullOrEmpty(userId))
                    {
                        await turnContext.SendActivityAsync(
                            MessageFactory.Text("❌ Authentication failed: Unable to identify user."), 
                            cancellationToken);
                        return;
                    }
                    
                    // Store the token
                    await _authService.StoreTokenAsync(userId, tokenResponse.Token);

                    await turnContext.SendActivityAsync(
                        MessageFactory.Text("✅ Authentication successful! You can now schedule interviews."), 
                        cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(
                        MessageFactory.Text("❌ Authentication failed: No valid token received."), 
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"❌ Authentication process failed: {ex.Message}"), 
                    cancellationToken);
            }
        }
    }
}