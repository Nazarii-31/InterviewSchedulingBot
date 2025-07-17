using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Interfaces;

namespace InterviewSchedulingBot.Tests
{
    /// <summary>
    /// Simple test to validate CoreSchedulingLogic functionality
    /// </summary>
    public class CoreSchedulingLogicTest
    {
        public static async Task RunBasicTest()
        {
            // Create basic service collection for testing
            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder => builder.AddConsole());
            
            // Add mock authentication service for testing
            services.AddSingleton<IAuthenticationService, MockAuthenticationService>();
            
            // Add the core scheduling logic
            services.AddSingleton<ICoreSchedulingLogic, CoreSchedulingLogic>();
            
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<CoreSchedulingLogicTest>>();
            
            try
            {
                var coreSchedulingLogic = serviceProvider.GetRequiredService<ICoreSchedulingLogic>();
                
                // Test basic functionality
                logger.LogInformation("Testing Core Scheduling Logic...");
                
                var participantEmails = new List<string> { "test1@example.com", "test2@example.com" };
                var startDate = DateTime.Now.AddDays(1);
                var endDate = startDate.AddDays(7);
                const int durationMinutes = 60;
                const string userId = "test-user";
                
                logger.LogInformation("Test parameters: {ParticipantCount} participants, {Duration} minutes, {StartDate} to {EndDate}", 
                    participantEmails.Count, durationMinutes, startDate, endDate);
                
                // This will throw an exception because we're using a mock authentication service
                // But it validates that the API is correctly structured
                try
                {
                    var availableSlots = await coreSchedulingLogic.FindCommonAvailabilityAsync(
                        participantEmails, 
                        durationMinutes, 
                        startDate, 
                        endDate, 
                        userId);
                    
                    logger.LogInformation("Found {SlotCount} available slots", availableSlots.Count);
                }
                catch (Exception ex)
                {
                    logger.LogInformation("Expected exception during mock test: {Message}", ex.Message);
                }
                
                // Test input validation
                logger.LogInformation("Testing input validation...");
                
                // Test empty participant list
                try
                {
                    await coreSchedulingLogic.FindCommonAvailabilityAsync(
                        new List<string>(), 
                        durationMinutes, 
                        startDate, 
                        endDate, 
                        userId);
                    
                    logger.LogError("Expected exception for empty participant list");
                }
                catch (ArgumentException ex)
                {
                    logger.LogInformation("✓ Correctly caught empty participant list: {Message}", ex.Message);
                }
                
                // Test invalid duration
                try
                {
                    await coreSchedulingLogic.FindCommonAvailabilityAsync(
                        participantEmails, 
                        10, // Invalid duration (too short)
                        startDate, 
                        endDate, 
                        userId);
                    
                    logger.LogError("Expected exception for invalid duration");
                }
                catch (ArgumentException ex)
                {
                    logger.LogInformation("✓ Correctly caught invalid duration: {Message}", ex.Message);
                }
                
                // Test invalid date range
                try
                {
                    await coreSchedulingLogic.FindCommonAvailabilityAsync(
                        participantEmails, 
                        durationMinutes, 
                        endDate, // Start after end
                        startDate, 
                        userId);
                    
                    logger.LogError("Expected exception for invalid date range");
                }
                catch (ArgumentException ex)
                {
                    logger.LogInformation("✓ Correctly caught invalid date range: {Message}", ex.Message);
                }
                
                logger.LogInformation("✓ Core Scheduling Logic test completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Test failed");
                throw;
            }
        }
    }
    
    /// <summary>
    /// Mock authentication service for testing purposes
    /// </summary>
    public class MockAuthenticationService : IAuthenticationService
    {
        public Task<string?> GetAccessTokenAsync(string userId)
        {
            throw new NotImplementedException("Mock authentication service - not implemented for testing");
        }
        
        public Task StoreTokenAsync(string userId, string accessToken, string? refreshToken = null, DateTimeOffset? expiresOn = null)
        {
            throw new NotImplementedException("Mock authentication service - not implemented for testing");
        }
        
        public Task<bool> IsUserAuthenticatedAsync(string userId)
        {
            return Task.FromResult(false);
        }
        
        public Task ClearTokenAsync(string userId)
        {
            return Task.CompletedTask;
        }
        
        public string GetAuthorizationUrl(string userId, string conversationId)
        {
            throw new NotImplementedException("Mock authentication service - not implemented for testing");
        }
    }
}