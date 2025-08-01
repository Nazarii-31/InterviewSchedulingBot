using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace InterviewSchedulingBot.Services.Integration
{
    /// <summary>
    /// Clean OpenWebUI client for parameter extraction with ZERO conditionals
    /// Sends user messages and receives structured parameters directly
    /// </summary>
    public interface ICleanOpenWebUIClient
    {
        Task<MeetingParameters> ExtractParametersAsync(string message);
    }

    public class CleanOpenWebUIClient : ICleanOpenWebUIClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CleanOpenWebUIClient> _logger;
        private readonly IConfiguration _configuration;
        private readonly bool _useMockData;
        private readonly string _selectedModel;
        
        private const string SystemPrompt = @"You are a meeting parameter extraction assistant. Extract scheduling information from user messages.

EXTRACT THESE PARAMETERS:
1. Duration: Meeting length in minutes (30, 45, 60, 75, 90, 120 etc. - default: 60)
2. TimeFrame: When they want to meet:
   - Specific days: ""Monday"", ""Tuesday"", ""Friday""
   - Relative: ""tomorrow"", ""next week"", ""next Monday""
   - Date ranges: ""this week"", ""next few weeks"", ""next month""
3. Participants: Email addresses mentioned (john@company.com, jane@company.com)

EXAMPLES:
- ""Find free slots for Friday for john.doe@company.com and jane.smith@company.com, I want 75 mins meeting""
  → {""Duration"": 75, ""TimeFrame"": ""Friday"", ""Participants"": [""john.doe@company.com"", ""jane.smith@company.com""]}

- ""I need a slot tomorrow with maria.garcia@company.com""
  → {""Duration"": 60, ""TimeFrame"": ""tomorrow"", ""Participants"": [""maria.garcia@company.com""]}

- ""Schedule interview next week for 90 minutes with alex.wilson@company.com and david.brown@company.com""
  → {""Duration"": 90, ""TimeFrame"": ""next week"", ""Participants"": [""alex.wilson@company.com"", ""david.brown@company.com""]}

Respond with ONLY a JSON object. No explanations or additional text.";

        public CleanOpenWebUIClient(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CleanOpenWebUIClient> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            // Get configuration settings
            var baseUrl = _configuration["OpenWebUI:BaseUrl"];
            _useMockData = _configuration.GetValue<bool>("OpenWebUI:UseMockData", false) ||
                          string.IsNullOrEmpty(baseUrl);
            
            if (!_useMockData && !string.IsNullOrEmpty(baseUrl))
            {
                try
                {
                    _httpClient.BaseAddress = new Uri(baseUrl);
                    
                    // Add API key if available
                    var apiKey = _configuration["OpenWebUI:ApiKey"];
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    }
                    
                    _selectedModel = _configuration["OpenWebUI:Model"] ?? "mistral:7b";
                    
                    _logger.LogInformation("CleanOpenWebUIClient configured for: {BaseUrl} using model: {Model}", 
                        baseUrl, _selectedModel);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to configure CleanOpenWebUIClient, falling back to mock data");
                    _useMockData = true;
                    _selectedModel = _configuration["OpenWebUI:Model"] ?? "mistral:7b";
                }
            }
            else
            {
                _selectedModel = _configuration["OpenWebUI:Model"] ?? "mistral:7b";
                _logger.LogWarning("CleanOpenWebUIClient using mock data - OpenWebUI integration disabled or configuration missing");
            }
        }

        public async Task<MeetingParameters> ExtractParametersAsync(string message)
        {
            // If OpenWebUI is not configured or mock data is enabled, use fallback
            if (_useMockData)
            {
                _logger.LogDebug("Using fallback parameter extraction for message: {Message}", message);
                return ExtractParametersFallback(message);
            }

            try
            {
                var request = new
                {
                    model = _selectedModel,
                    messages = new[]
                    {
                        new { role = "system", content = SystemPrompt },
                        new { role = "user", content = message }
                    },
                    temperature = 0.1,
                    max_tokens = 500,
                    stream = false
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("chat/completions", content, cts.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("OpenWebUI API request failed with status {StatusCode}: {ErrorContent}", 
                        response.StatusCode, errorContent);
                    return ExtractParametersFallback(message);
                }
                
                var responseText = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    _logger.LogWarning("OpenWebUI API returned empty response");
                    return ExtractParametersFallback(message);
                }
                
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseText);
                
                if (!responseObj.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                {
                    _logger.LogWarning("OpenWebUI API response missing choices array");
                    return ExtractParametersFallback(message);
                }
                
                var firstChoice = choices[0];
                if (!firstChoice.TryGetProperty("message", out var responseMessage))
                {
                    _logger.LogWarning("OpenWebUI API response missing message in first choice");
                    return ExtractParametersFallback(message);
                }
                
                if (!responseMessage.TryGetProperty("content", out var contentProperty))
                {
                    _logger.LogWarning("OpenWebUI API response missing content in message");
                    return ExtractParametersFallback(message);
                }
                
                var extractedJson = contentProperty.GetString();
                
                if (string.IsNullOrWhiteSpace(extractedJson))
                {
                    _logger.LogWarning("OpenWebUI API returned empty content");
                    return ExtractParametersFallback(message);
                }
                
                var result = JsonSerializer.Deserialize<MeetingParameters>(extractedJson);
                return result ?? ExtractParametersFallback(message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("OpenWebUI request timed out, using fallback extraction");
                return ExtractParametersFallback(message);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error calling OpenWebUI, using fallback extraction");
                return ExtractParametersFallback(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting parameters, using fallback");
                return ExtractParametersFallback(message);
            }
        }

        private MeetingParameters ExtractParametersFallback(string message)
        {
            var lowerMessage = message.ToLowerInvariant();
            var parameters = new MeetingParameters();

            // Extract duration using simple pattern matching
            if (lowerMessage.Contains("30 min")) parameters = parameters with { Duration = 30 };
            else if (lowerMessage.Contains("90 min")) parameters = parameters with { Duration = 90 };
            else if (lowerMessage.Contains("45 min")) parameters = parameters with { Duration = 45 };
            else if (lowerMessage.Contains("2 hour")) parameters = parameters with { Duration = 120 };

            // Extract time frame
            if (lowerMessage.Contains("tomorrow")) parameters = parameters with { TimeFrame = "tomorrow" };
            else if (lowerMessage.Contains("next week")) parameters = parameters with { TimeFrame = "next week" };
            else if (lowerMessage.Contains("monday")) parameters = parameters with { TimeFrame = "monday" };
            else if (lowerMessage.Contains("tuesday")) parameters = parameters with { TimeFrame = "tuesday" };
            else if (lowerMessage.Contains("friday")) parameters = parameters with { TimeFrame = "friday" };

            // Extract email addresses
            var emailPattern = new System.Text.RegularExpressions.Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
            var matches = emailPattern.Matches(message);
            var emails = matches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).ToList();

            return parameters with { Participants = emails };
        }
    }

    /// <summary>
    /// Clean data record for meeting parameters
    /// </summary>
    public record MeetingParameters
    {
        public int Duration { get; init; } = 60;
        public string TimeFrame { get; init; } = "";
        public List<string> Participants { get; init; } = new();
    }
}