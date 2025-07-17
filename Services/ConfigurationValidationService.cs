using Microsoft.Extensions.Options;

namespace InterviewSchedulingBot.Services
{
    /// <summary>
    /// Service to validate configuration settings at startup
    /// </summary>
    public class ConfigurationValidationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationValidationService> _logger;

        public ConfigurationValidationService(IConfiguration configuration, ILogger<ConfigurationValidationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Validates all required authentication configuration settings
        /// </summary>
        /// <returns>True if configuration is valid, false otherwise</returns>
        public bool ValidateAuthenticationConfiguration()
        {
            // Check if mock service is enabled
            var useMockService = _configuration.GetValue<bool>("GraphScheduling:UseMockService", false);
            
            if (useMockService)
            {
                _logger.LogInformation("Using mock service - skipping authentication configuration validation");
                return true;
            }

            var isValid = true;
            var missingSettings = new List<string>();

            // Check Microsoft App settings
            if (string.IsNullOrEmpty(_configuration["MicrosoftAppId"]))
            {
                missingSettings.Add("MicrosoftAppId");
                isValid = false;
            }

            if (string.IsNullOrEmpty(_configuration["MicrosoftAppPassword"]))
            {
                missingSettings.Add("MicrosoftAppPassword");
                isValid = false;
            }

            if (string.IsNullOrEmpty(_configuration["MicrosoftAppTenantId"]))
            {
                missingSettings.Add("MicrosoftAppTenantId");
                isValid = false;
            }

            // Check Authentication settings
            if (string.IsNullOrEmpty(_configuration["Authentication:ClientId"]))
            {
                missingSettings.Add("Authentication:ClientId");
                isValid = false;
            }

            if (string.IsNullOrEmpty(_configuration["Authentication:ClientSecret"]))
            {
                missingSettings.Add("Authentication:ClientSecret");
                isValid = false;
            }

            if (string.IsNullOrEmpty(_configuration["Authentication:TenantId"]))
            {
                missingSettings.Add("Authentication:TenantId");
                isValid = false;
            }

            if (string.IsNullOrEmpty(_configuration["Authentication:Authority"]))
            {
                missingSettings.Add("Authentication:Authority");
                isValid = false;
            }

            if (string.IsNullOrEmpty(_configuration["Authentication:RedirectUri"]))
            {
                missingSettings.Add("Authentication:RedirectUri");
                isValid = false;
            }

            // Check Graph API settings
            if (string.IsNullOrEmpty(_configuration["GraphApi:ClientId"]))
            {
                missingSettings.Add("GraphApi:ClientId");
                isValid = false;
            }

            if (string.IsNullOrEmpty(_configuration["GraphApi:ClientSecret"]))
            {
                missingSettings.Add("GraphApi:ClientSecret");
                isValid = false;
            }

            if (string.IsNullOrEmpty(_configuration["GraphApi:TenantId"]))
            {
                missingSettings.Add("GraphApi:TenantId");
                isValid = false;
            }

            // Check scopes
            var authScopes = _configuration.GetSection("Authentication:Scopes").Get<string[]>();
            if (authScopes == null || authScopes.Length == 0)
            {
                missingSettings.Add("Authentication:Scopes");
                isValid = false;
            }

            var graphScopes = _configuration.GetSection("GraphApi:Scopes").Get<string[]>();
            if (graphScopes == null || graphScopes.Length == 0)
            {
                missingSettings.Add("GraphApi:Scopes");
                isValid = false;
            }

            if (!isValid)
            {
                _logger.LogWarning("Configuration validation failed. Missing required settings: {MissingSettings}. " +
                                  "Please ensure all authentication settings are configured in appsettings.json",
                                  string.Join(", ", missingSettings));
            }
            else
            {
                _logger.LogInformation("Authentication configuration validation passed successfully");
            }

            return isValid;
        }

        /// <summary>
        /// Logs the current configuration state (without secrets)
        /// </summary>
        public void LogConfigurationState()
        {
            var useMockService = _configuration.GetValue<bool>("GraphScheduling:UseMockService", false);
            
            _logger.LogInformation("Current configuration state:");
            _logger.LogInformation("  GraphScheduling:UseMockService: {UseMockService}", useMockService);
            
            if (useMockService)
            {
                _logger.LogInformation("  Mock service enabled - authentication credentials not required");
                return;
            }

            _logger.LogInformation("  MicrosoftAppId: {HasValue}", !string.IsNullOrEmpty(_configuration["MicrosoftAppId"]) ? "✓ Set" : "✗ Missing");
            _logger.LogInformation("  MicrosoftAppPassword: {HasValue}", !string.IsNullOrEmpty(_configuration["MicrosoftAppPassword"]) ? "✓ Set" : "✗ Missing");
            _logger.LogInformation("  MicrosoftAppTenantId: {HasValue}", !string.IsNullOrEmpty(_configuration["MicrosoftAppTenantId"]) ? "✓ Set" : "✗ Missing");
            _logger.LogInformation("  Authentication:ClientId: {HasValue}", !string.IsNullOrEmpty(_configuration["Authentication:ClientId"]) ? "✓ Set" : "✗ Missing");
            _logger.LogInformation("  Authentication:ClientSecret: {HasValue}", !string.IsNullOrEmpty(_configuration["Authentication:ClientSecret"]) ? "✓ Set" : "✗ Missing");
            _logger.LogInformation("  Authentication:TenantId: {HasValue}", !string.IsNullOrEmpty(_configuration["Authentication:TenantId"]) ? "✓ Set" : "✗ Missing");
            _logger.LogInformation("  GraphApi:ClientId: {HasValue}", !string.IsNullOrEmpty(_configuration["GraphApi:ClientId"]) ? "✓ Set" : "✗ Missing");
            _logger.LogInformation("  GraphApi:ClientSecret: {HasValue}", !string.IsNullOrEmpty(_configuration["GraphApi:ClientSecret"]) ? "✓ Set" : "✗ Missing");
            _logger.LogInformation("  GraphApi:TenantId: {HasValue}", !string.IsNullOrEmpty(_configuration["GraphApi:TenantId"]) ? "✓ Set" : "✗ Missing");

            var authScopes = _configuration.GetSection("Authentication:Scopes").Get<string[]>();
            _logger.LogInformation("  Authentication:Scopes: {ScopeCount} scopes configured", authScopes?.Length ?? 0);

            var graphScopes = _configuration.GetSection("GraphApi:Scopes").Get<string[]>();
            _logger.LogInformation("  GraphApi:Scopes: {ScopeCount} scopes configured", graphScopes?.Length ?? 0);
        }
    }
}