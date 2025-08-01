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
        private const string BaseUrl = "https://openwebui.ai.godeltech.com/api/";
        private const string Model = "mistral:7b";
        
        private const string SystemPrompt = @"You are a meeting parameter extraction assistant. Extract scheduling information from user messages.

EXTRACT THESE PARAMETERS:
1. Duration: Meeting length in minutes (30, 45, 60, 75, 90, 120 etc. - default: 30)
2. TimeFrame: When they want to meet:
   - Specific days: ""Monday"", ""Tuesday"", ""Friday""
   - Relative: ""tomorrow"", ""next week"", ""next Monday""
   - Date ranges: ""this week"", ""next few weeks"", ""next month""
3. Participants: Email addresses mentioned (john@company.com, jane@company.com)

EXAMPLES:
- ""Find free slots for Friday for john.doe@company.com and jane.smith@company.com, I want 75 mins meeting""
  → {""Duration"": 75, ""TimeFrame"": ""Friday"", ""Participants"": [""john.doe@company.com"", ""jane.smith@company.com""]}

- ""I need a slot tomorrow with maria.garcia@company.com""
  → {""Duration"": 30, ""TimeFrame"": ""tomorrow"", ""Participants"": [""maria.garcia@company.com""]}

- ""Schedule interview next week for 90 minutes with alex.wilson@company.com and david.brown@company.com""
  → {""Duration"": 90, ""TimeFrame"": ""next week"", ""Participants"": [""alex.wilson@company.com"", ""david.brown@company.com""]}

Respond with ONLY a JSON object. No explanations or additional text.";

        public CleanOpenWebUIClient(
            HttpClient httpClient,
            ILogger<CleanOpenWebUIClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(BaseUrl);
        }

        public async Task<MeetingParameters> ExtractParametersAsync(string message)
        {
            try
            {
                var request = new
                {
                    model = Model,
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

                var response = await _httpClient.PostAsync("chat/completions", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OpenWebUI API request failed with status {StatusCode}: {ErrorContent}", 
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"OpenWebUI API request failed with status {response.StatusCode}");
                }
                
                var responseText = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    _logger.LogError("OpenWebUI API returned empty response");
                    throw new InvalidOperationException("OpenWebUI API returned empty response");
                }
                
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseText);
                
                if (!responseObj.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                {
                    _logger.LogError("OpenWebUI API response missing choices array");
                    throw new InvalidOperationException("OpenWebUI API response missing choices array");
                }
                
                var firstChoice = choices[0];
                if (!firstChoice.TryGetProperty("message", out var responseMessage))
                {
                    _logger.LogError("OpenWebUI API response missing message in first choice");
                    throw new InvalidOperationException("OpenWebUI API response missing message in first choice");
                }
                
                if (!responseMessage.TryGetProperty("content", out var contentProperty))
                {
                    _logger.LogError("OpenWebUI API response missing content in message");
                    throw new InvalidOperationException("OpenWebUI API response missing content in message");
                }
                
                var extractedJson = contentProperty.GetString();
                
                if (string.IsNullOrWhiteSpace(extractedJson))
                {
                    _logger.LogError("OpenWebUI API returned empty content");
                    throw new InvalidOperationException("OpenWebUI API returned empty content");
                }
                
                return JsonSerializer.Deserialize<MeetingParameters>(extractedJson) 
                    ?? new MeetingParameters();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting parameters, using defaults");
                return new MeetingParameters();
            }
        }
    }

    /// <summary>
    /// Clean data record for meeting parameters
    /// </summary>
    public record MeetingParameters
    {
        public int Duration { get; init; } = 30;
        public string TimeFrame { get; init; } = "";
        public List<string> Participants { get; init; } = new();
    }
}