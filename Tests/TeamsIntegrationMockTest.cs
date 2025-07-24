using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using InterviewSchedulingBot.Services.Mock;
using InterviewSchedulingBot.Interfaces.Integration;
using InterviewSchedulingBot.Services.Integration;
using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Tests
{
    /// <summary>
    /// Comprehensive test suite for Teams API integration using mock data
    /// Tests bot's ability to handle realistic Teams API responses
    /// </summary>
    public static class TeamsIntegrationMockTest
    {
        public static async Task RunComprehensiveTeamsIntegrationTest()
        {
            Console.WriteLine("🤖 Starting Comprehensive Teams Integration Mock Test...\n");

            // Create mock logger
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MockTeamsIntegrationService>.Instance;
            
            // Initialize mock Teams integration service
            var mockTeamsService = new MockTeamsIntegrationService(logger);
            
            // Create mock turn context
            var mockTurnContext = CreateMockTurnContext();

            Console.WriteLine("=== 🔍 Testing Teams API Integration Components ===\n");

            // Test 1: User Information Retrieval
            await TestUserInfoRetrieval(mockTeamsService, mockTurnContext);

            // Test 2: Authentication Handling
            await TestAuthenticationHandling(mockTeamsService, mockTurnContext);

            // Test 3: Calendar Availability Check
            await TestCalendarAvailabilityCheck(mockTeamsService, mockTurnContext);

            // Test 4: Working Hours Retrieval
            await TestWorkingHoursRetrieval(mockTeamsService, mockTurnContext);

            // Test 5: User Presence Check
            await TestUserPresenceCheck(mockTeamsService, mockTurnContext);

            // Test 6: People Search
            await TestPeopleSearch(mockTeamsService, mockTurnContext);

            // Test 7: Message and Card Sending
            await TestMessagingCapabilities(mockTeamsService, mockTurnContext);

            // Test 8: End-to-End Interview Scheduling Scenario
            await TestEndToEndSchedulingScenario(mockTeamsService, mockTurnContext);

            Console.WriteLine("\n🎉 All Teams integration tests completed successfully!");
            Console.WriteLine("\n📋 Test Summary:");
            Console.WriteLine("✅ User information retrieval - PASSED");
            Console.WriteLine("✅ Authentication handling - PASSED");
            Console.WriteLine("✅ Calendar availability checking - PASSED");
            Console.WriteLine("✅ Working hours retrieval - PASSED");
            Console.WriteLine("✅ User presence checking - PASSED");
            Console.WriteLine("✅ People search functionality - PASSED");
            Console.WriteLine("✅ Messaging capabilities - PASSED");
            Console.WriteLine("✅ End-to-end scheduling scenario - PASSED");
            Console.WriteLine("\n🚀 Mock Teams API integration is ready for production testing!");
        }

        private static async Task TestUserInfoRetrieval(ITeamsIntegrationService teamsService, ITurnContext turnContext)
        {
            Console.WriteLine("🔍 Test 1: User Information Retrieval");
            Console.WriteLine("   Testing Teams user context extraction...");

            try
            {
                var userInfo = await teamsService.GetUserInfoAsync(turnContext);
                
                Console.WriteLine($"   ✅ User ID: {userInfo.Id}");
                Console.WriteLine($"   ✅ Name: {userInfo.Name}");
                Console.WriteLine($"   ✅ Email: {userInfo.Email}");
                Console.WriteLine($"   ✅ Tenant ID: {userInfo.TenantId}");
                Console.WriteLine($"   ✅ Team ID: {userInfo.TeamId}");
                Console.WriteLine($"   ✅ Channel ID: {userInfo.ChannelId}");
                
                ValidateUserInfo(userInfo);
                Console.WriteLine("   ✅ User information retrieval test PASSED\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ User information retrieval test FAILED: {ex.Message}\n");
                throw;
            }
        }

        private static async Task TestAuthenticationHandling(ITeamsIntegrationService teamsService, ITurnContext turnContext)
        {
            Console.WriteLine("🔐 Test 2: Authentication Handling");
            Console.WriteLine("   Testing Teams authentication flow...");

            try
            {
                var authResult = await teamsService.HandleAuthenticationAsync(turnContext, "test-user-123");
                
                Console.WriteLine($"   ✅ Is Authenticated: {authResult.IsAuthenticated}");
                
                if (authResult.IsAuthenticated)
                {
                    Console.WriteLine($"   ✅ Access Token: {authResult.AccessToken?[..10]}...");
                }
                else
                {
                    Console.WriteLine($"   ✅ Login URL: {authResult.LoginUrl}");
                }
                
                if (!string.IsNullOrEmpty(authResult.ErrorMessage))
                {
                    Console.WriteLine($"   ⚠️  Error Message: {authResult.ErrorMessage}");
                }
                
                ValidateAuthenticationResult(authResult);
                Console.WriteLine("   ✅ Authentication handling test PASSED\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Authentication handling test FAILED: {ex.Message}\n");
                throw;
            }
        }

        private static async Task TestCalendarAvailabilityCheck(ITeamsIntegrationService teamsService, ITurnContext turnContext)
        {
            Console.WriteLine("📅 Test 3: Calendar Availability Check");
            Console.WriteLine("   Testing calendar availability retrieval...");

            try
            {
                var userEmails = new List<string> 
                { 
                    "interviewer1@contoso.com", 
                    "interviewer2@contoso.com", 
                    "candidate@contoso.com" 
                };
                
                var startTime = DateTime.Now.AddDays(1);
                var endTime = startTime.AddDays(5);
                
                var availability = await teamsService.GetCalendarAvailabilityAsync(turnContext, userEmails, startTime, endTime);
                
                Console.WriteLine($"   ✅ Retrieved availability for {availability.Count} users");
                
                foreach (var userAvailability in availability)
                {
                    Console.WriteLine($"   📧 {userAvailability.Key}: {userAvailability.Value.Count} busy slots");
                    
                    foreach (var busySlot in userAvailability.Value.Take(3)) // Show first 3 for brevity
                    {
                        Console.WriteLine($"      🕒 Busy: {busySlot.Start:yyyy-MM-dd HH:mm} - {busySlot.End:yyyy-MM-dd HH:mm} ({busySlot.Status})");
                    }
                }
                
                ValidateCalendarAvailability(availability, userEmails);
                Console.WriteLine("   ✅ Calendar availability check test PASSED\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Calendar availability check test FAILED: {ex.Message}\n");
                throw;
            }
        }

        private static async Task TestWorkingHoursRetrieval(ITeamsIntegrationService teamsService, ITurnContext turnContext)
        {
            Console.WriteLine("⏰ Test 4: Working Hours Retrieval");
            Console.WriteLine("   Testing working hours configuration...");

            try
            {
                var userEmail = "test.user@contoso.com";
                var workingHours = await teamsService.GetUserWorkingHoursAsync(turnContext, userEmail);
                
                Console.WriteLine($"   ✅ Time Zone: {workingHours.TimeZone}");
                Console.WriteLine($"   ✅ Working Days: {string.Join(", ", workingHours.DaysOfWeek)}");
                Console.WriteLine($"   ✅ Start Time: {workingHours.StartTime}");
                Console.WriteLine($"   ✅ End Time: {workingHours.EndTime}");
                
                ValidateWorkingHours(workingHours);
                Console.WriteLine("   ✅ Working hours retrieval test PASSED\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Working hours retrieval test FAILED: {ex.Message}\n");
                throw;
            }
        }

        private static async Task TestUserPresenceCheck(ITeamsIntegrationService teamsService, ITurnContext turnContext)
        {
            Console.WriteLine("🟢 Test 5: User Presence Check");
            Console.WriteLine("   Testing real-time presence information...");

            try
            {
                var userEmails = new List<string> 
                { 
                    "interviewer1@contoso.com", 
                    "interviewer2@contoso.com", 
                    "candidate@contoso.com" 
                };
                
                var presenceInfo = await teamsService.GetUsersPresenceAsync(turnContext, userEmails);
                
                Console.WriteLine($"   ✅ Retrieved presence for {presenceInfo.Count} users");
                
                foreach (var userPresence in presenceInfo)
                {
                    Console.WriteLine($"   👤 {userPresence.Key}:");
                    Console.WriteLine($"      📶 Availability: {userPresence.Value.Availability}");
                    Console.WriteLine($"      🎯 Activity: {userPresence.Value.Activity}");
                    Console.WriteLine($"      🕒 Last Modified: {userPresence.Value.LastModifiedDateTime}");
                }
                
                ValidateUserPresence(presenceInfo, userEmails);
                Console.WriteLine("   ✅ User presence check test PASSED\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ User presence check test FAILED: {ex.Message}\n");
                throw;
            }
        }

        private static async Task TestPeopleSearch(ITeamsIntegrationService teamsService, ITurnContext turnContext)
        {
            Console.WriteLine("🔍 Test 6: People Search");
            Console.WriteLine("   Testing organization people search...");

            try
            {
                var searchQuery = "Engineering";
                var maxResults = 5;
                
                var searchResults = await teamsService.SearchPeopleAsync(turnContext, searchQuery, maxResults);
                
                Console.WriteLine($"   ✅ Found {searchResults.Count} people matching '{searchQuery}'");
                
                foreach (var person in searchResults)
                {
                    Console.WriteLine($"   👤 {person.DisplayName} ({person.JobTitle})");
                    Console.WriteLine($"      📧 {string.Join(", ", person.EmailAddresses)}");
                    Console.WriteLine($"      🏢 {person.Department}");
                }
                
                ValidatePeopleSearch(searchResults, searchQuery);
                Console.WriteLine("   ✅ People search test PASSED\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ People search test FAILED: {ex.Message}\n");
                throw;
            }
        }

        private static async Task TestMessagingCapabilities(ITeamsIntegrationService teamsService, ITurnContext turnContext)
        {
            Console.WriteLine("💬 Test 7: Messaging Capabilities");
            Console.WriteLine("   Testing message and adaptive card sending...");

            try
            {
                // Test message sending
                var messageResponse = await teamsService.SendMessageAsync(turnContext, 
                    "🤖 This is a test message from the Interview Scheduling Bot!");
                
                Console.WriteLine($"   ✅ Message sent - ID: {messageResponse.Id}");
                
                // Test adaptive card sending (mock attachment)
                var mockAttachment = new Attachment
                {
                    ContentType = "application/vnd.microsoft.card.adaptive",
                    Content = new { type = "AdaptiveCard", version = "1.0", body = new[] { new { type = "TextBlock", text = "Mock Card" } } }
                };
                
                var cardResponse = await teamsService.SendAdaptiveCardAsync(turnContext, mockAttachment);
                
                Console.WriteLine($"   ✅ Adaptive card sent - ID: {cardResponse.Id}");
                
                ValidateMessagingResponse(messageResponse);
                ValidateMessagingResponse(cardResponse);
                Console.WriteLine("   ✅ Messaging capabilities test PASSED\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Messaging capabilities test FAILED: {ex.Message}\n");
                throw;
            }
        }

        private static async Task TestEndToEndSchedulingScenario(ITeamsIntegrationService teamsService, ITurnContext turnContext)
        {
            Console.WriteLine("🎯 Test 8: End-to-End Interview Scheduling Scenario");
            Console.WriteLine("   Testing complete scheduling workflow...");

            try
            {
                // Step 1: Get user info
                var userInfo = await teamsService.GetUserInfoAsync(turnContext);
                Console.WriteLine($"   ✅ Step 1: Got user info for {userInfo.Name}");

                // Step 2: Handle authentication
                var authResult = await teamsService.HandleAuthenticationAsync(turnContext, userInfo.Id);
                Console.WriteLine($"   ✅ Step 2: Authentication handled - Authenticated: {authResult.IsAuthenticated}");

                if (!authResult.IsAuthenticated)
                {
                    Console.WriteLine($"   ⚠️  Would redirect to: {authResult.LoginUrl}");
                    Console.WriteLine("   ✅ End-to-end test PASSED (authentication required scenario)\n");
                    return;
                }

                // Step 3: Search for interview participants
                var participants = await teamsService.SearchPeopleAsync(turnContext, "Senior", 3);
                Console.WriteLine($"   ✅ Step 3: Found {participants.Count} potential participants");

                // Step 4: Get working hours for participants
                var participantEmails = participants.Select(p => p.EmailAddresses.First()).ToList();
                var firstParticipantEmail = participantEmails.FirstOrDefault() ?? "test@contoso.com";
                var workingHours = await teamsService.GetUserWorkingHoursAsync(turnContext, firstParticipantEmail);
                Console.WriteLine($"   ✅ Step 4: Got working hours ({workingHours.StartTime} - {workingHours.EndTime})");

                // Step 5: Check current presence
                var presence = await teamsService.GetUsersPresenceAsync(turnContext, participantEmails);
                Console.WriteLine($"   ✅ Step 5: Checked presence for {presence.Count} participants");

                // Step 6: Get calendar availability
                var startTime = DateTime.Now.AddDays(1);
                var endTime = startTime.AddDays(5);
                var availability = await teamsService.GetCalendarAvailabilityAsync(turnContext, participantEmails, startTime, endTime);
                Console.WriteLine($"   ✅ Step 6: Retrieved calendar availability for {availability.Count} participants");

                // Step 7: Send scheduling results
                var schedulingMessage = $"📅 Found {availability.Values.Sum(slots => slots.Count)} total busy slots across all participants. " +
                                      $"Time range: {startTime:yyyy-MM-dd} to {endTime:yyyy-MM-dd}";
                var messageResponse = await teamsService.SendMessageAsync(turnContext, schedulingMessage);
                Console.WriteLine($"   ✅ Step 7: Sent scheduling results message - ID: {messageResponse.Id}");

                Console.WriteLine("   ✅ End-to-end scheduling scenario test PASSED\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ End-to-end scheduling scenario test FAILED: {ex.Message}\n");
                throw;
            }
        }

        #region Validation Methods

        private static void ValidateUserInfo(TeamsUserInfo userInfo)
        {
            if (string.IsNullOrEmpty(userInfo.Id)) throw new Exception("User ID is required");
            if (string.IsNullOrEmpty(userInfo.Name)) throw new Exception("User name is required");
            if (string.IsNullOrEmpty(userInfo.Email)) throw new Exception("User email is required");
        }

        private static void ValidateAuthenticationResult(AuthenticationResult authResult)
        {
            if (authResult.IsAuthenticated && string.IsNullOrEmpty(authResult.AccessToken))
                throw new Exception("Access token is required when authenticated");
            if (!authResult.IsAuthenticated && string.IsNullOrEmpty(authResult.LoginUrl))
                throw new Exception("Login URL is required when not authenticated");
        }

        private static void ValidateCalendarAvailability(Dictionary<string, List<BusyTimeSlot>> availability, List<string> userEmails)
        {
            if (availability.Count != userEmails.Count)
                throw new Exception("Availability data count doesn't match user email count");
            
            foreach (var userEmail in userEmails)
            {
                if (!availability.ContainsKey(userEmail))
                    throw new Exception($"Missing availability data for {userEmail}");
            }
        }

        private static void ValidateWorkingHours(WorkingHours workingHours)
        {
            if (string.IsNullOrEmpty(workingHours.TimeZone)) throw new Exception("Time zone is required");
            if (workingHours.DaysOfWeek.Count == 0) throw new Exception("Working days are required");
            if (string.IsNullOrEmpty(workingHours.StartTime)) throw new Exception("Start time is required");
            if (string.IsNullOrEmpty(workingHours.EndTime)) throw new Exception("End time is required");
        }

        private static void ValidateUserPresence(Dictionary<string, UserPresence> presence, List<string> userEmails)
        {
            if (presence.Count != userEmails.Count)
                throw new Exception("Presence data count doesn't match user email count");
            
            foreach (var userEmail in userEmails)
            {
                if (!presence.ContainsKey(userEmail))
                    throw new Exception($"Missing presence data for {userEmail}");
            }
        }

        private static void ValidatePeopleSearch(List<PersonInfo> searchResults, string searchQuery)
        {
            // Validation logic - for mock data, just ensure we have results with basic info
            foreach (var person in searchResults)
            {
                if (string.IsNullOrEmpty(person.DisplayName)) throw new Exception("Person display name is required");
                if (person.EmailAddresses.Count == 0) throw new Exception("Person email addresses are required");
            }
        }

        private static void ValidateMessagingResponse(ResourceResponse response)
        {
            if (string.IsNullOrEmpty(response.Id)) throw new Exception("Response ID is required");
        }

        #endregion

        private static ITurnContext CreateMockTurnContext()
        {
            // Create a minimal mock turn context for testing
            // In real scenarios, this would come from the Bot Framework
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                From = new ChannelAccount { Id = "test-user", Name = "Test User" },
                Conversation = new ConversationAccount { Id = "test-conversation" },
                ChannelId = "msteams",
                Text = "Mock message for testing"
            };

            return new TurnContextMock(activity);
        }
    }

    /// <summary>
    /// Simple mock implementation of ITurnContext for testing
    /// </summary>
    public class TurnContextMock : ITurnContext
    {
        public Activity Activity { get; }
        public bool Responded { get; set; }
        public TurnContextStateCollection TurnState { get; } = new TurnContextStateCollection();
        public BotAdapter Adapter { get; } = null!; // Not needed for mock testing

        public TurnContextMock(Activity activity)
        {
            Activity = activity;
        }

        public Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteActivityAsync(ConversationReference conversationReference, string activityId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ITurnContext OnDeleteActivity(DeleteActivityHandler handler)
        {
            return this;
        }

        public ITurnContext OnSendActivities(SendActivitiesHandler handler)
        {
            return this;
        }

        public ITurnContext OnUpdateActivity(UpdateActivityHandler handler)
        {
            return this;
        }

        public Task<ResourceResponse> SendActivityAsync(string textReplyToSend, string? speak = null, string? inputHint = null, CancellationToken cancellationToken = default)
        {
            Responded = true;
            return Task.FromResult(new ResourceResponse { Id = Guid.NewGuid().ToString() });
        }

        public Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            Responded = true;
            return Task.FromResult(new ResourceResponse { Id = Guid.NewGuid().ToString() });
        }

        public Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
        {
            Responded = true;
            return Task.FromResult(activities.Select(a => new ResourceResponse { Id = Guid.NewGuid().ToString() }).ToArray());
        }

        public Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ResourceResponse { Id = Guid.NewGuid().ToString() });
        }
    }
}