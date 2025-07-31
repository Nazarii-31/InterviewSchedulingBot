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
        
        private const string SystemPrompt = @"You are a parameter extraction service. Extract ONLY the following from messages:
1. Duration: The meeting length in minutes (default: 60 if not specified)
2. TimeFrame: When the meeting should occur (specific day, date range, etc.)
3. Participants: Email addresses of attendees

Respond with ONLY a JSON object containing these parameters. No explanations.";

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
                var responseText = await response.Content.ReadAsStringAsync();
                
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseText);
                var choices = responseObj.GetProperty("choices");
                var firstChoice = choices[0];
                var responseMessage = firstChoice.GetProperty("message");
                var extractedJson = responseMessage.GetProperty("content").GetString();
                
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
        public int Duration { get; init; } = 60;
        public string TimeFrame { get; init; } = "";
        public List<string> Participants { get; init; } = new();
    }
}