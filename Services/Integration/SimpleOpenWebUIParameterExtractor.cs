using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace InterviewSchedulingBot.Services.Integration
{
    /// <summary>
    /// Simple OpenWebUI Parameter Extractor that sends requests to https://openwebui.ai.godeltech.com/api/
    /// Uses mistral:7b model with straightforward extraction and parsing
    /// </summary>
    public interface ISimpleOpenWebUIParameterExtractor
    {
        Task<ParameterExtractionResult> ExtractParametersAsync(string message);
    }

    public class SimpleOpenWebUIParameterExtractor : ISimpleOpenWebUIParameterExtractor
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SimpleOpenWebUIParameterExtractor> _logger;
        private const string BaseUrl = "https://openwebui.ai.godeltech.com/api/";
        private const string Model = "mistral:7b";
        
        private const string SystemPrompt = @"You are a parameter extractor for scheduling. Extract ONLY these parameters in JSON: 
{ 
  ""isSlotRequest"": true|false, 
  ""duration"": 60, 
  ""timeFrame"": { 
    ""type"": ""specific_day|this_week|next_week"", 
    ""date"": ""YYYY-MM-DD"", 
    ""timeOfDay"": ""morning|afternoon|evening|all_day"" 
  }, 
  ""participants"": [""email@example.com""] 
}";

        public SimpleOpenWebUIParameterExtractor(
            HttpClient httpClient,
            ILogger<SimpleOpenWebUIParameterExtractor> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(BaseUrl);
        }

        public async Task<ParameterExtractionResult> ExtractParametersAsync(string message)
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
                
                if (response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    return ParseOpenWebUIResponse(responseText);
                }
                
                _logger.LogWarning("OpenWebUI API error: {StatusCode}", response.StatusCode);
                return CreateFallbackResult(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenWebUI API");
                return CreateFallbackResult(message);
            }
        }

        private ParameterExtractionResult ParseOpenWebUIResponse(string responseText)
        {
            try
            {
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseText);
                
                if (responseObj.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
                    {
                        var extractedJson = content.GetString();
                        if (!string.IsNullOrEmpty(extractedJson))
                        {
                            return JsonSerializer.Deserialize<ParameterExtractionResult>(extractedJson) 
                                ?? CreateFallbackResult("Invalid JSON");
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse OpenWebUI response");
            }

            return CreateFallbackResult("Parse error");
        }

        private ParameterExtractionResult CreateFallbackResult(string message)
        {
            var lowerMessage = message.ToLowerInvariant();
            
            // Simple pattern matching for fallback
            var isSlotRequest = lowerMessage.Contains("find") || lowerMessage.Contains("slot") || 
                               lowerMessage.Contains("schedule") || lowerMessage.Contains("available");

            if (!isSlotRequest)
            {
                return new ParameterExtractionResult { IsSlotRequest = false };
            }

            // Extract basic parameters using pattern matching
            var duration = lowerMessage switch
            {
                var msg when msg.Contains("30") => 30,
                var msg when msg.Contains("90") => 90,
                var msg when msg.Contains("2 hour") => 120,
                _ => 60
            };

            var timeOfDay = lowerMessage switch
            {
                var msg when msg.Contains("morning") => "morning",
                var msg when msg.Contains("afternoon") => "afternoon",  
                var msg when msg.Contains("evening") => "evening",
                _ => "all_day"
            };

            var frameType = lowerMessage switch
            {
                var msg when msg.Contains("tomorrow") => "specific_day",
                var msg when msg.Contains("this week") => "this_week",
                var msg when msg.Contains("next week") => "next_week",
                _ => "specific_day"
            };

            var date = frameType switch
            {
                "specific_day" when lowerMessage.Contains("tomorrow") => DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"),
                "this_week" => DateTime.Today.ToString("yyyy-MM-dd"),
                "next_week" => DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"),
                _ => DateTime.Today.ToString("yyyy-MM-dd")
            };

            return new ParameterExtractionResult
            {
                IsSlotRequest = true,
                Duration = duration,
                TimeFrame = new TimeFrameData
                {
                    Type = frameType,
                    Date = date,
                    TimeOfDay = timeOfDay
                },
                Participants = new List<string>()
            };
        }
    }

    // Simple data models
    public class ParameterExtractionResult
    {
        public bool IsSlotRequest { get; set; }
        public int Duration { get; set; }
        public TimeFrameData? TimeFrame { get; set; }
        public List<string> Participants { get; set; } = new();
    }

    public class TimeFrameData
    {
        public string Type { get; set; } = "";
        public string Date { get; set; } = "";
        public string TimeOfDay { get; set; } = "";
    }
}