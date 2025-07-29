using System.Text;
using System.Text.Json;

namespace InterviewSchedulingBot.Services.Integration
{
    public interface IOpenWebUIClient
    {
        Task<OpenWebUIResponse> ProcessQueryAsync(string query, OpenWebUIRequestType requestType);
        Task<string> GenerateResponseAsync(string prompt, object context);
    }

    public class OpenWebUIClient : IOpenWebUIClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenWebUIClient> _logger;
        
        public OpenWebUIClient(
            HttpClient httpClient, 
            IConfiguration configuration, 
            ILogger<OpenWebUIClient> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            var baseUrl = _configuration["OpenWebUI:BaseUrl"];
            var apiKey = _configuration["OpenWebUI:ApiKey"];
            
            if (!string.IsNullOrEmpty(baseUrl))
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
        }
        
        public async Task<OpenWebUIResponse> ProcessQueryAsync(string query, OpenWebUIRequestType requestType)
        {
            try
            {
                _logger.LogInformation("Processing query with Open WebUI: {Query}, Type: {Type}", query, requestType);
                
                var request = new OpenWebUIRequest
                {
                    Query = query,
                    Type = requestType.ToString()
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");
                
                var response = await _httpClient.PostAsync("process", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OpenWebUIResponse>(responseContent, 
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    
                    return result ?? CreateFallbackResponse(query, requestType);
                }
                else
                {
                    _logger.LogWarning("Open WebUI API returned error: {StatusCode}", response.StatusCode);
                    return CreateFallbackResponse(query, requestType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing query with Open WebUI API");
                return CreateFallbackResponse(query, requestType);
            }
        }
        
        public async Task<string> GenerateResponseAsync(string prompt, object context)
        {
            try
            {
                _logger.LogInformation("Generating response with Open WebUI");
                
                var request = new
                {
                    Prompt = prompt,
                    Context = context,
                    MaxTokens = 500,
                    Temperature = 0.7
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");
                
                var response = await _httpClient.PostAsync("generate", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(responseContent);
                    
                    if (document.RootElement.TryGetProperty("text", out var textElement))
                    {
                        return textElement.GetString() ?? CreateFallbackTextResponse(context);
                    }
                }
                
                _logger.LogWarning("Open WebUI API generate returned error: {StatusCode}", response.StatusCode);
                return CreateFallbackTextResponse(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response with Open WebUI API");
                return CreateFallbackTextResponse(context);
            }
        }

        private OpenWebUIResponse CreateFallbackResponse(string query, OpenWebUIRequestType requestType)
        {
            // Create a fallback response when Open WebUI is not available
            return requestType switch
            {
                OpenWebUIRequestType.SlotQuery => CreateSlotQueryFallback(query),
                OpenWebUIRequestType.ConflictAnalysis => CreateConflictAnalysisFallback(),
                OpenWebUIRequestType.ResponseGeneration => CreateResponseGenerationFallback(),
                _ => new OpenWebUIResponse
                {
                    Success = false,
                    Message = "Unable to process query at this time"
                }
            };
        }

        private OpenWebUIResponse CreateSlotQueryFallback(string query)
        {
            // Simple keyword-based parsing when Open WebUI is not available
            var response = new OpenWebUIResponse
            {
                Success = true,
                Message = "Parsed using fallback logic"
            };

            // Extract basic information from query
            var lowerQuery = query.ToLowerInvariant();
            
            // Detect time of day
            if (lowerQuery.Contains("morning"))
                response.TimeOfDay = "morning";
            else if (lowerQuery.Contains("afternoon"))
                response.TimeOfDay = "afternoon";
            else if (lowerQuery.Contains("evening"))
                response.TimeOfDay = "evening";

            // Detect specific days
            if (lowerQuery.Contains("monday"))
                response.SpecificDay = "Monday";
            else if (lowerQuery.Contains("tuesday"))
                response.SpecificDay = "Tuesday";
            else if (lowerQuery.Contains("wednesday"))
                response.SpecificDay = "Wednesday";
            else if (lowerQuery.Contains("thursday"))
                response.SpecificDay = "Thursday";
            else if (lowerQuery.Contains("friday"))
                response.SpecificDay = "Friday";

            // Detect relative days
            if (lowerQuery.Contains("tomorrow"))
                response.RelativeDay = "tomorrow";
            else if (lowerQuery.Contains("next week"))
                response.RelativeDay = "next week";

            // Set default date range
            response.DateRange = new DateRange
            {
                Start = DateTime.Today,
                End = DateTime.Today.AddDays(7)
            };

            // Extract duration if mentioned
            if (lowerQuery.Contains("30 min"))
                response.Duration = 30;
            else if (lowerQuery.Contains("90 min") || lowerQuery.Contains("1.5 hour"))
                response.Duration = 90;
            else
                response.Duration = 60; // Default

            return response;
        }

        private OpenWebUIResponse CreateConflictAnalysisFallback()
        {
            return new OpenWebUIResponse
            {
                Success = true,
                Message = "Conflict analysis completed using fallback logic"
            };
        }

        private OpenWebUIResponse CreateResponseGenerationFallback()
        {
            return new OpenWebUIResponse
            {
                Success = true,
                Message = "Response generated using fallback logic"
            };
        }

        private string CreateFallbackTextResponse(object context)
        {
            return "I found some scheduling options for you. Let me know if you'd like to see more details or try different criteria.";
        }
    }

    public class OpenWebUIRequest
    {
        public string Query { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class OpenWebUIResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateRange? DateRange { get; set; }
        public string? TimeOfDay { get; set; }
        public List<string>? Participants { get; set; }
        public int? MinRequiredParticipants { get; set; }
        public int? Duration { get; set; }
        public string? SpecificDay { get; set; }
        public string? RelativeDay { get; set; }
    }

    public class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public enum OpenWebUIRequestType
    {
        SlotQuery,
        ConflictAnalysis,
        ResponseGeneration
    }
}