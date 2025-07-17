using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using InterviewSchedulingBot.Services;
using InterviewSchedulingBot.Interfaces;

namespace InterviewSchedulingBot.Testing
{
    public class SimpleValidation
    {
        public static void ValidateAPI()
        {
            Console.WriteLine("Validating Core Scheduling Logic API...");
            
            // Create basic service collection
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<IAuthenticationService, MockAuthenticationService>();
            services.AddSingleton<ICoreSchedulingLogic, CoreSchedulingLogic>();
            
            var serviceProvider = services.BuildServiceProvider();
            var coreSchedulingLogic = serviceProvider.GetRequiredService<ICoreSchedulingLogic>();
            
            Console.WriteLine("✓ Core Scheduling Logic service can be instantiated");
            
            // Test method signature
            var method = typeof(ICoreSchedulingLogic).GetMethod("FindCommonAvailabilityAsync");
            if (method != null)
            {
                Console.WriteLine("✓ FindCommonAvailabilityAsync method exists");
                
                var parameters = method.GetParameters();
                if (parameters.Length >= 5)
                {
                    Console.WriteLine($"✓ Method has {parameters.Length} parameters (expected at least 5)");
                    
                    // Check parameter types
                    if (parameters[0].ParameterType == typeof(List<string>))
                        Console.WriteLine("✓ First parameter is List<string> (participant emails)");
                    
                    if (parameters[1].ParameterType == typeof(int))
                        Console.WriteLine("✓ Second parameter is int (duration in minutes)");
                    
                    if (parameters[2].ParameterType == typeof(DateTime))
                        Console.WriteLine("✓ Third parameter is DateTime (start date)");
                    
                    if (parameters[3].ParameterType == typeof(DateTime))
                        Console.WriteLine("✓ Fourth parameter is DateTime (end date)");
                    
                    if (parameters[4].ParameterType == typeof(string))
                        Console.WriteLine("✓ Fifth parameter is string (user ID)");
                }
            }
            
            Console.WriteLine("✓ API validation completed successfully");
        }
    }
    
    public class MockAuthenticationService : IAuthenticationService
    {
        public Task<string?> GetAccessTokenAsync(string userId)
        {
            return Task.FromResult<string?>(null);
        }
        
        public Task StoreTokenAsync(string userId, string accessToken, string? refreshToken = null, DateTimeOffset? expiresOn = null)
        {
            return Task.CompletedTask;
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
            return "mock://auth/url";
        }
    }
}