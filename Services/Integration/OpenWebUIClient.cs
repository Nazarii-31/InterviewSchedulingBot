using System.Text;
using System.Text.Json;
using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Services.Integration
{
    public interface IOpenWebUIClient
    {
        Task<OpenWebUIResponse> ProcessQueryAsync(string query, OpenWebUIRequestType requestType, CancellationToken cancellationToken = default);
        Task<string> GenerateResponseAsync(string prompt, object context, CancellationToken cancellationToken = default);
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
        
        public async Task<OpenWebUIResponse> ProcessQueryAsync(string query, OpenWebUIRequestType requestType, CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Processing query with Open WebUI: {Query}, Type: {Type}", query, requestType);
                
                var maxTokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 500);
                var temperature = _configuration.GetValue<double>("OpenWebUI:Temperature", 0.7);
                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                
                var request = new OpenWebUIRequest
                {
                    Query = query,
                    Type = requestType,
                    MaxTokens = maxTokens,
                    Temperature = temperature,
                    Timeout = timeoutMs
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");
                
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
                
                var response = await _httpClient.PostAsync("process", content, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var result = JsonSerializer.Deserialize<OpenWebUIResponse>(responseContent, 
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    
                    if (result != null)
                    {
                        result.ProcessingTime = stopwatch.Elapsed.TotalSeconds;
                        return result;
                    }
                }
                else
                {
                    _logger.LogWarning("Open WebUI API returned error: {StatusCode}", response.StatusCode);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Query processing was cancelled by user");
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Query processing timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing query with Open WebUI API");
            }
            
            var fallbackResponse = CreateFallbackResponse(query, requestType);
            fallbackResponse.ProcessingTime = stopwatch.Elapsed.TotalSeconds;
            return fallbackResponse;
        }
        
        public async Task<string> GenerateResponseAsync(string prompt, object context, CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Generating response with Open WebUI");
                
                var maxTokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 500);
                var temperature = _configuration.GetValue<double>("OpenWebUI:Temperature", 0.7);
                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                
                var request = new
                {
                    Prompt = prompt,
                    Context = context,
                    MaxTokens = maxTokens,
                    Temperature = temperature
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");
                
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
                
                var response = await _httpClient.PostAsync("generate", content, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    using var document = JsonDocument.Parse(responseContent);
                    
                    if (document.RootElement.TryGetProperty("text", out var textElement))
                    {
                        var result = textElement.GetString();
                        if (!string.IsNullOrEmpty(result))
                        {
                            _logger.LogInformation("Generated response in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                            return result;
                        }
                    }
                }
                
                _logger.LogWarning("Open WebUI API generate returned error: {StatusCode}", response.StatusCode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Response generation was cancelled by user");
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Response generation timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response with Open WebUI API");
            }
            
            return CreateFallbackTextResponse(context);
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
                Message = "Parsed using fallback logic",
                GeneratedText = "Fallback parsing completed",
                TokensUsed = 0
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
                Message = "Conflict analysis completed using fallback logic",
                GeneratedText = "Conflict analysis fallback completed",
                TokensUsed = 0
            };
        }

        private OpenWebUIResponse CreateResponseGenerationFallback()
        {
            return new OpenWebUIResponse
            {
                Success = true,
                Message = "Response generated using fallback logic",
                GeneratedText = "Fallback response generated",
                TokensUsed = 0
            };
        }

        private string CreateFallbackTextResponse(object context)
        {
            return "I found some scheduling options for you. Let me know if you'd like to see more details or try different criteria.";
        }
    }
}