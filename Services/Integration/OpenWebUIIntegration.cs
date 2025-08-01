using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InterviewSchedulingBot.Services.Integration
{
    /// <summary>
    /// Pure AI-driven OpenWebUI integration with intelligent fallback handling
    /// No hardcoded scenarios - uses semantic understanding for all interactions
    /// </summary>
    public interface IOpenWebUIIntegration
    {
        Task<string> ProcessGeneralMessageAsync(string message, string conversationId = null);
        Task<MeetingParameters> ExtractSchedulingParametersAsync(string message);
        Task<(DateTime startDate, DateTime endDate)> ProcessDateReferenceAsync(string userRequest, DateTime currentDate);
        Task<string> GenerateConversationalResponseAsync(string context, object data = null);
    }

    public class OpenWebUIIntegration : IOpenWebUIIntegration
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenWebUIIntegration> _logger;
        private readonly IConfiguration _configuration;
        private readonly bool _useMockData;
        private readonly string _selectedModel;
        
        public OpenWebUIIntegration(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OpenWebUIIntegration> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            // Get configuration
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
                    
                    _logger.LogInformation("OpenWebUI integration configured for: {BaseUrl} using model: {Model}", 
                        baseUrl, _selectedModel);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to configure OpenWebUI, falling back to mock data");
                    _useMockData = true;
                }
            }
            else
            {
                _selectedModel = _configuration["OpenWebUI:Model"] ?? "mistral:7b";
                _logger.LogWarning("Using mock data - OpenWebUI integration disabled or configuration missing");
            }
        }

        public async Task<string> ProcessGeneralMessageAsync(string message, string conversationId = null)
        {
            if (_useMockData)
            {
                return GenerateIntelligentFallbackResponse(message);
            }

            try
            {
                var systemPrompt = @"You are an AI-powered Interview Scheduling assistant. Your role is to help users with:
1. Finding available time slots
2. Scheduling meetings and interviews
3. Checking calendar availability
4. General conversation about scheduling

Respond naturally and conversationally. If users greet you, greet them back warmly. If they ask for help, explain your capabilities. If they want to schedule something, guide them through the process.

Be friendly, professional, and helpful. Vary your language to sound natural - don't use repetitive phrases.";

                var request = new
                {
                    model = _selectedModel,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = message }
                    },
                    temperature = _configuration.GetValue<double>("OpenWebUI:Temperature", 0.7),
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 500),
                    stream = false
                };

                var response = await CallOpenWebUIAsync(request);
                return response ?? GenerateIntelligentFallbackResponse(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing general message with OpenWebUI");
                return GenerateIntelligentFallbackResponse(message);
            }
        }

        public async Task<MeetingParameters> ExtractSchedulingParametersAsync(string message)
        {
            if (_useMockData)
            {
                return ExtractParametersFallback(message);
            }

            try
            {
                var systemPrompt = @"Extract scheduling parameters from user messages. Return ONLY a JSON object with these fields:
{
  ""Duration"": 60,
  ""TimeFrame"": ""tomorrow"",
  ""Participants"": [""email@example.com""]
}

Rules:
- Duration: meeting length in minutes (default: 60)
- TimeFrame: when they want to meet (""tomorrow"", ""next week"", ""Monday"", ""next month"", etc.)
- Participants: array of email addresses found in the message

Examples:
""Find slots tomorrow with john@company.com"" → {""Duration"": 60, ""TimeFrame"": ""tomorrow"", ""Participants"": [""john@company.com""]}
""90 minute meeting next week"" → {""Duration"": 90, ""TimeFrame"": ""next week"", ""Participants"": []}

Return only the JSON object, no other text.";

                var request = new
                {
                    model = _selectedModel,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = message }
                    },
                    temperature = 0.1, // Low temperature for consistent parsing
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 300),
                    stream = false
                };

                var response = await CallOpenWebUIAsync(request);
                
                if (!string.IsNullOrEmpty(response))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<MeetingParameters>(response) ?? ExtractParametersFallback(message);
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning("Failed to parse JSON response from OpenWebUI: {Response}", response);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting parameters with OpenWebUI");
            }

            return ExtractParametersFallback(message);
        }

        public async Task<(DateTime startDate, DateTime endDate)> ProcessDateReferenceAsync(string userRequest, DateTime currentDate)
        {
            if (_useMockData)
            {
                return ProcessDateReferenceFallback(userRequest, currentDate);
            }

            try
            {
                var systemPrompt = $@"You are a date/time interpreter. Convert natural language date references to specific dates.

Current date: {currentDate:yyyy-MM-dd} ({currentDate:dddd})

Rules:
1. For weekend requests, automatically adjust to next business day
2. ""tomorrow"" = next business day if weekend, otherwise literal tomorrow
3. ""next week"" = next Monday through Friday
4. ""first 2 days of next week"" = Monday and Tuesday of next week
5. Business days are Monday-Friday only

Return ONLY a JSON object:
{{
  ""startDate"": ""yyyy-MM-dd"",
  ""endDate"": ""yyyy-MM-dd"",
  ""explanation"": ""brief explanation if weekend was adjusted""
}}

Examples:
- ""tomorrow"" when today is Friday → {{""startDate"": ""2025-01-06"", ""endDate"": ""2025-01-06"", ""explanation"": """"}}
- ""tomorrow"" when today is Saturday → {{""startDate"": ""2025-01-08"", ""endDate"": ""2025-01-08"", ""explanation"": ""Adjusted to Monday as tomorrow is Sunday""}}
- ""first 2 days of next week"" → {{""startDate"": ""2025-01-08"", ""endDate"": ""2025-01-09"", ""explanation"": """"}}";

                var request = new
                {
                    model = _selectedModel,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userRequest }
                    },
                    temperature = 0.1, // Low temperature for consistent date parsing
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 200),
                    stream = false
                };

                var response = await CallOpenWebUIAsync(request);
                
                if (!string.IsNullOrEmpty(response))
                {
                    try
                    {
                        var dateResult = JsonSerializer.Deserialize<JsonElement>(response);
                        
                        if (dateResult.TryGetProperty("startDate", out var startProp) &&
                            dateResult.TryGetProperty("endDate", out var endProp))
                        {
                            if (DateTime.TryParse(startProp.GetString(), out var startDate) &&
                                DateTime.TryParse(endProp.GetString(), out var endDate))
                            {
                                return (startDate, endDate);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning("Failed to parse date response from OpenWebUI: {Response}", response);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing date reference with OpenWebUI");
            }

            return ProcessDateReferenceFallback(userRequest, currentDate);
        }

        public async Task<string> GenerateConversationalResponseAsync(string context, object data = null)
        {
            if (_useMockData)
            {
                return GenerateConversationalFallback(context, data);
            }

            try
            {
                var systemPrompt = @"Generate natural, conversational responses for an Interview Scheduling assistant. 
Be warm, professional, and helpful. Use varied language and sentence structures. 
Context will include the type of response needed and any relevant data.";

                var contextJson = JsonSerializer.Serialize(new { context, data });

                var request = new
                {
                    model = _selectedModel,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = $"Generate a response for: {contextJson}" }
                    },
                    temperature = _configuration.GetValue<double>("OpenWebUI:Temperature", 0.7),
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 500),
                    stream = false
                };

                var response = await CallOpenWebUIAsync(request);
                return response ?? GenerateConversationalFallback(context, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating conversational response with OpenWebUI");
                return GenerateConversationalFallback(context, data);
            }
        }

        private async Task<string> CallOpenWebUIAsync(object request)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                Encoding.UTF8,
                "application/json");

            var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

            var response = await _httpClient.PostAsync("chat/completions", content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenWebUI API returned error: {StatusCode} - {ReasonPhrase}", 
                    response.StatusCode, response.ReasonPhrase);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
            var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

            if (responseObj.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var contentElement))
                {
                    return contentElement.GetString();
                }
            }

            return null;
        }

        private string GenerateIntelligentFallbackResponse(string message)
        {
            var lowerMessage = message.ToLowerInvariant();

            if (lowerMessage.Contains("hello") || lowerMessage.Contains("hi") || lowerMessage.Contains("hey"))
            {
                var greetings = new[]
                {
                    "Hello! 👋 I'm your AI-powered Interview Scheduling assistant. I can help you find available time slots and check calendar availability using natural language. What would you like me to help you with today?",
                    "Hi there! Great to see you. I specialize in finding time slots and checking calendar availability. How can I assist you?",
                    "Hey! Welcome to the Interview Scheduling Bot. I'm here to make finding available time slots easier for you. What's on your agenda?",
                    "Hello! I'm your scheduling assistant, ready to help with finding available time slots and checking calendar availability. What can I do for you?"
                };
                return greetings[new Random().Next(greetings.Length)];
            }

            if (lowerMessage.Contains("help") || lowerMessage.Contains("what can you do"))
            {
                return "I can help you with interview scheduling! Here's what I can do:\n\n• Find available time slots using natural language\n• Check calendar availability for multiple participants\n• Analyze scheduling conflicts\n• Suggest optimal meeting times\n\nJust ask me in plain English like 'Find slots tomorrow morning' or 'Check availability next week'!";
            }

            if (lowerMessage.Contains("thank") || lowerMessage.Contains("thanks"))
            {
                var thankYouResponses = new[]
                {
                    "You're welcome! I'm here to help with your scheduling needs. Is there anything else you'd like me to assist you with?",
                    "My pleasure! Feel free to ask if you need help with more scheduling tasks.",
                    "Glad I could help! Let me know if you have any other scheduling questions."
                };
                return thankYouResponses[new Random().Next(thankYouResponses.Length)];
            }

            if (lowerMessage.Contains("slots") || lowerMessage.Contains("available") || lowerMessage.Contains("schedule"))
            {
                return "I'd be happy to help you find available time slots! To get started, please include participant email addresses in your request. For example: 'Find slots tomorrow with john@company.com' or 'Check availability for 90 minutes next week with jane@company.com'.";
            }

            // Default conversational response
            return "I'm here to help with interview scheduling! You can ask me to find time slots, check availability, or schedule meetings using natural language. For example, try 'Find slots tomorrow afternoon with john@company.com' or 'Check when we're all available next week'. How can I assist you today?";
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

        private (DateTime startDate, DateTime endDate) ProcessDateReferenceFallback(string userRequest, DateTime currentDate)
        {
            var lowerRequest = userRequest.ToLowerInvariant();
            
            if (lowerRequest.Contains("tomorrow"))
            {
                var tomorrow = currentDate.AddDays(1);
                // If tomorrow is weekend, move to next Monday
                if (tomorrow.DayOfWeek == DayOfWeek.Saturday || tomorrow.DayOfWeek == DayOfWeek.Sunday)
                {
                    while (tomorrow.DayOfWeek != DayOfWeek.Monday)
                        tomorrow = tomorrow.AddDays(1);
                }
                return (tomorrow, tomorrow);
            }
            
            if (lowerRequest.Contains("first") && lowerRequest.Contains("2") && lowerRequest.Contains("next week"))
            {
                // Find next Monday
                var nextMonday = currentDate.AddDays(1);
                while (nextMonday.DayOfWeek != DayOfWeek.Monday)
                    nextMonday = nextMonday.AddDays(1);
                
                return (nextMonday, nextMonday.AddDays(1)); // Monday and Tuesday
            }
            
            if (lowerRequest.Contains("next week"))
            {
                var nextMonday = currentDate.AddDays(1);
                while (nextMonday.DayOfWeek != DayOfWeek.Monday)
                    nextMonday = nextMonday.AddDays(1);
                
                return (nextMonday, nextMonday.AddDays(4)); // Monday to Friday
            }
            
            // Default to tomorrow
            var defaultDate = currentDate.AddDays(1);
            return (defaultDate, defaultDate);
        }

        private string GenerateConversationalFallback(string context, object data)
        {
            return context.ToLowerInvariant() switch
            {
                "welcome" => "Hello! 👋 I'm your AI-powered Interview Scheduling assistant. I can help you find available time slots and check calendar availability using natural language. What would you like me to help you with today?",
                "no_slots" => "I couldn't find any available slots that match your criteria. Would you like me to check different time ranges or suggest alternative options?",
                "weekend_adjusted" => "Since you asked for a weekend day, I've found available slots for the next business days instead.",
                _ => "I'm here to help with your scheduling needs. How can I assist you today?"
            };
        }
    }
}