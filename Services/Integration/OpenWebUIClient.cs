using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Services.Integration
{
    public interface IOpenWebUIClient
    {
        Task<OpenWebUIResponse> ProcessQueryAsync(string query, OpenWebUIRequestType requestType, CancellationToken cancellationToken = default);
        Task<string> GenerateResponseAsync(string prompt, object context, CancellationToken cancellationToken = default);
        Task<string> GetDirectResponseAsync(string message, string conversationId, List<MessageHistoryItem> history);
        Task<string> GetResponseAsync(string query, string conversationId = null);
        Task<List<TimeSlot>> FindSlotsAsync(SlotRequest request);
        Task<string> GenerateSlotSuggestionsWithConflicts(List<TimeSlot> availableSlots, List<string> participants, Dictionary<string, List<ConflictDetail>> conflicts);
        Task<string> GenerateSlotResponseAsync(List<TimeSlot> slots, InterviewSchedulingBot.Services.Business.SlotQueryCriteria criteria);
        Task<string> GetConversationalResponseAsync(string message, string conversationId, List<MessageHistoryItem> history, ConversationOptions options);
        Task<string> GetClarificationRequestAsync(string message, string context, List<MessageHistoryItem> history);
        Task<string> GenerateNoSlotsResponseAsync(InterviewSchedulingBot.Services.Business.SlotQueryCriteria criteria, List<MessageHistoryItem> history);
        
        // New methods for intent recognition and slot finding
        Task<IntentResponse> RecognizeIntentAsync(string message, List<MessageHistoryItem> history);
        Task<SlotParameters> ExtractSlotParametersAsync(string message, List<MessageHistoryItem> history);
        Task<string> FormatAvailableSlotsAsync(List<TimeSlot> slots, SlotParameters parameters);
        Task<string> GenerateClarificationAsync(SlotParameters parameters, List<MessageHistoryItem> history);
        Task<string> GenerateNoSlotsResponseAsync(SlotParameters parameters, List<MessageHistoryItem> history);
        
        // Specialized parameter extraction assistant
        Task<ParameterExtractionResponse> ExtractParametersAsAssistantAsync(string message);
    }

    public class OpenWebUIClient : IOpenWebUIClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenWebUIClient> _logger;
        private readonly bool _useMockData;
        private readonly string _selectedModel;
        
        public OpenWebUIClient(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OpenWebUIClient> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            // Configure for your specific self-hosted instance
            var baseUrl = configuration["OpenWebUI:BaseUrl"];
            
            // Only use mock data if explicitly configured or if base URL is missing/empty
            _useMockData = configuration.GetValue<bool>("OpenWebUI:UseMockData", false) ||
                          string.IsNullOrEmpty(baseUrl);
            
            if (!_useMockData && !string.IsNullOrEmpty(baseUrl))
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
                
                // Add API key if your instance requires it
                var apiKey = configuration["OpenWebUI:ApiKey"];
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                }
                
                // Configure model - use mistral:7b as recommended
                _selectedModel = configuration["OpenWebUI:Model"] ?? "mistral:7b";
                
                _logger.LogInformation("OpenWebUI client configured for self-hosted instance at: {BaseUrl} using model: {Model}", 
                    baseUrl, _selectedModel);
            }
            else
            {
                // Set default model for mock data scenarios
                _selectedModel = configuration["OpenWebUI:Model"] ?? "mistral:7b";
                _logger.LogWarning("Using mock data - OpenWebUI integration disabled or configuration missing");
            }
        }

        public async Task<string> GetResponseAsync(string query, string conversationId = null)
        {
            try
            {
                if (_useMockData)
                {
                    _logger.LogWarning("Using mock data - OpenWebUI integration disabled or configuration missing");
                    return GenerateMockResponse(query);
                }
                
                // Log that we're making a real API call
                _logger.LogInformation("Making API call to OpenWebUI with query: {Query} using model: {Model}", query, _selectedModel);
                
                var request = new
                {
                    model = _selectedModel,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "You are an AI interview scheduling assistant. Provide helpful, concise responses."
                        },
                        new
                        {
                            role = "user", 
                            content = query
                        }
                    },
                    temperature = _configuration.GetValue<double>("OpenWebUI:Temperature", 0.7),
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 1000),
                    stream = false
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");
                
                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
                    
                var response = await _httpClient.PostAsync("chat/completions", content, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    if (responseObj.TryGetProperty("choices", out var choicesElement) && 
                        choicesElement.GetArrayLength() > 0)
                    {
                        var firstChoice = choicesElement[0];
                        if (firstChoice.TryGetProperty("message", out var messageElement) &&
                            messageElement.TryGetProperty("content", out var contentElement))
                        {
                            var result = contentElement.GetString();
                            if (!string.IsNullOrEmpty(result))
                            {
                                _logger.LogInformation("Successfully received response from OpenWebUI");
                                return result;
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("OpenWebUI API returned error: {StatusCode} - {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                    {
                        _logger.LogError("405 Method Not Allowed - Check if the OpenWebUI endpoint and HTTP method are correct");
                    }
                }
                
                return GenerateMockResponse(query);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling OpenWebUI - falling back to mock data");
                return GenerateMockResponse(query);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "OpenWebUI request timed out - falling back to mock data");
                return GenerateMockResponse(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling OpenWebUI - falling back to mock data");
                return GenerateMockResponse(query);
            }
        }

        public async Task<List<TimeSlot>> FindSlotsAsync(SlotRequest request)
        {
            if (_useMockData)
            {
                return GenerateRandomMockSlots(request);
            }
            
            // Make actual API call to find slots
            // ...implementation...
            return new List<TimeSlot>();
        }

        public async Task<string> GetDirectResponseAsync(string message, string conversationId, List<MessageHistoryItem> history)
        {
            // If OpenWebUI is not configured, return fallback immediately
            if (_useMockData)
            {
                _logger.LogDebug("OpenWebUI not configured, returning fallback response for message: {Message}", message);
                return CreateContextualFallbackResponse(message, history);
            }

            try
            {
                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = "You are an AI interview scheduling assistant. Provide helpful, clear, and professional responses."
                    }
                };

                // Add conversation history
                if (history != null && history.Any())
                {
                    foreach (var item in history.TakeLast(10)) // Limit history to last 10 messages
                    {
                        messages.Add(new
                        {
                            role = item.IsFromBot ? "assistant" : "user",
                            content = item.Message
                        });
                    }
                }

                // Add current message
                messages.Add(new
                {
                    role = "user",
                    content = message
                });

                var request = new
                {
                    model = _selectedModel,
                    messages = messages.ToArray(),
                    temperature = _configuration.GetValue<double>("OpenWebUI:Temperature", 0.7),
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 1000),
                    stream = false
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
                
                var response = await _httpClient.PostAsync("chat/completions", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (responseObj.TryGetProperty("choices", out var choicesElement) &&
                        choicesElement.GetArrayLength() > 0)
                    {
                        var firstChoice = choicesElement[0];
                        if (firstChoice.TryGetProperty("message", out var messageElement) &&
                            messageElement.TryGetProperty("content", out var contentElement))
                        {
                            var result = contentElement.GetString();
                            if (!string.IsNullOrEmpty(result))
                            {
                                _logger.LogInformation("Received direct response from OpenWebUI");
                                return result;
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("OpenWebUI API returned error: {StatusCode} - {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("OpenWebUI request timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting direct response from OpenWebUI");
            }

            return CreateContextualFallbackResponse(message, history);
        }

        public async Task<string> GenerateSlotResponseAsync(List<TimeSlot> slots, InterviewSchedulingBot.Services.Business.SlotQueryCriteria criteria)
        {
            // If OpenWebUI is not configured, return fallback immediately
            if (_useMockData)
            {
                return GenerateSlotResponseFallback(slots, criteria);
            }

            try
            {
                // Create a detailed context with proper slot information
                var slotContext = new {
                    query = "Slot search for " + criteria.DurationMinutes + " minutes",
                    slots = slots.Select(s => new {
                        date = s.StartTime.ToString("dddd, MMMM d"),
                        day = s.StartTime.ToString("dddd"),
                        start_time = s.StartTime.ToString("h:mm tt"),
                        end_time = s.EndTime.ToString("h:mm tt"),
                        duration = (s.EndTime - s.StartTime).TotalMinutes,
                        available_participants = s.AvailableParticipants,
                        total_participants = s.TotalParticipants,
                        confidence_score = s.AvailabilityScore,
                        participants_busy = new List<string>() // Since BusyParticipants doesn't exist, use empty list
                    }).ToList(),
                    participants = criteria.ParticipantEmails,
                    requested_duration = criteria.DurationMinutes,
                    requested_day = criteria.SpecificDay,
                    formatting_instructions = @"
                        - Group slots by day
                        - Include specific times with AM/PM
                        - List at least 3-5 slots if available
                        - For each slot, mention who is available and who has conflicts
                        - Use bullet points for clarity
                        - Format the response in a conversational, helpful tone
                        - Always include specific times, not just general statements
                    "
                };

                var request = new {
                    model = _selectedModel,
                    messages = new[] {
                        new {
                            role = "system",
                            content = GetSystemPrompt("slot_finding")
                        },
                        new {
                            role = "user",
                            content = $"Please format these available time slots in a helpful way: {JsonSerializer.Serialize(slotContext)}"
                        }
                    },
                    temperature = _configuration.GetValue<double>("OpenWebUI:Temperature", 0.7),
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 1000),
                    stream = false
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("chat/completions", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseString);

                    if (responseObj.TryGetProperty("choices", out var choicesElement) &&
                        choicesElement.GetArrayLength() > 0)
                    {
                        var firstChoice = choicesElement[0];
                        if (firstChoice.TryGetProperty("message", out var messageElement) &&
                            messageElement.TryGetProperty("content", out var contentElement))
                        {
                            var result = contentElement.GetString();
                            if (!string.IsNullOrEmpty(result))
                            {
                                return result;
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("OpenWebUI API returned error: {StatusCode} - {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating slot response from OpenWebUI");
            }

            return GenerateSlotResponseFallback(slots, criteria);
        }

        public async Task<string> GetConversationalResponseAsync(string message, string conversationId, List<MessageHistoryItem> history, ConversationOptions options)
        {
            // If OpenWebUI is not configured, return fallback immediately
            if (_useMockData)
            {
                return CreateVariedFallbackResponse(message, history, options);
            }

            try
            {
                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = GetSystemPrompt("general")
                    }
                };

                // Add conversation history
                if (history != null && history.Any())
                {
                    foreach (var item in history.TakeLast(5)) // Limit history to last 5 messages
                    {
                        messages.Add(new
                        {
                            role = item.IsFromBot ? "assistant" : "user",
                            content = item.Message
                        });
                    }
                }

                // Add current message
                messages.Add(new
                {
                    role = "user",
                    content = message
                });

                var request = new
                {
                    model = _selectedModel,
                    messages = messages.ToArray(),
                    temperature = options.ResponseTemperature,
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 1000),
                    stream = false
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("chat/completions", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (responseObj.TryGetProperty("choices", out var choicesElement) &&
                        choicesElement.GetArrayLength() > 0)
                    {
                        var firstChoice = choicesElement[0];
                        if (firstChoice.TryGetProperty("message", out var messageElement) &&
                            messageElement.TryGetProperty("content", out var contentElement))
                        {
                            var result = contentElement.GetString();
                            if (!string.IsNullOrEmpty(result))
                            {
                                return result;
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("OpenWebUI API returned error: {StatusCode} - {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversational response from OpenWebUI");
            }

            return CreateVariedFallbackResponse(message, history, options);
        }

        public async Task<string> GetClarificationRequestAsync(string message, string context, List<MessageHistoryItem> history)
        {
            // If OpenWebUI is not configured, return fallback immediately
            if (_useMockData)
            {
                return CreateClarificationFallback(message, context);
            }

            try
            {
                var request = new
                {
                    message = message,
                    context = context,
                    history = history.Select(h => new { role = h.IsFromBot ? "assistant" : "user", content = h.Message }).ToArray(),
                    task = "Generate a clarification request for scheduling"
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("clarify", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (responseObj.TryGetProperty("response", out var responseElement))
                    {
                        var result = responseElement.GetString();
                        if (!string.IsNullOrEmpty(result))
                        {
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting clarification request from OpenWebUI");
            }

            return CreateClarificationFallback(message, context);
        }

        public async Task<string> GenerateNoSlotsResponseAsync(InterviewSchedulingBot.Services.Business.SlotQueryCriteria criteria, List<MessageHistoryItem> history)
        {
            // If OpenWebUI is not configured, return fallback immediately
            if (_useMockData)
            {
                return CreateNoSlotsFallback(criteria);
            }

            try
            {
                var request = new
                {
                    criteria = criteria,
                    history = history.Select(h => new { role = h.IsFromBot ? "assistant" : "user", content = h.Message }).ToArray(),
                    task = "Generate a helpful response when no slots are available"
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("no-slots", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (responseObj.TryGetProperty("response", out var responseElement))
                    {
                        var result = responseElement.GetString();
                        if (!string.IsNullOrEmpty(result))
                        {
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating no slots response from OpenWebUI");
            }

            return CreateNoSlotsFallback(criteria);
        }

        public async Task<string> GenerateSlotSuggestionsWithConflicts(List<TimeSlot> availableSlots, List<string> participants, Dictionary<string, List<ConflictDetail>> conflicts)
        {
            // If OpenWebUI is not configured, return fallback immediately
            if (_useMockData)
            {
                return GenerateSlotSuggestionsFallback(availableSlots, participants, conflicts);
            }

            try
            {
                var request = new
                {
                    slots = availableSlots.Select(s => new {
                        date = s.StartTime.ToString("yyyy-MM-dd"),
                        start = s.StartTime.ToString("HH:mm"),
                        end = s.EndTime.ToString("HH:mm"),
                        availability_score = s.AvailabilityScore
                    }).ToList(),
                    participants = participants,
                    conflicts = conflicts.Select(c => new {
                        participant = c.Key,
                        busy_times = c.Value.Select(v => new {
                            start = v.StartTime.ToString("yyyy-MM-dd HH:mm"),
                            end = v.EndTime.ToString("yyyy-MM-dd HH:mm"),
                            title = v.Title
                        }).ToList()
                    }).ToList(),
                    task = "Generate a conversational response explaining available slots and conflicts"
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("generate", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    if (responseObj.TryGetProperty("text", out var textElement))
                    {
                        var result = textElement.GetString();
                        if (!string.IsNullOrEmpty(result))
                        {
                            return result;
                        }
                    }
                }

                _logger.LogWarning("OpenWebUI API generate returned error: {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating slot suggestions with OpenWebUI");
            }

            return GenerateSlotSuggestionsFallback(availableSlots, participants, conflicts);
        }
        
        public async Task<OpenWebUIResponse> ProcessQueryAsync(string query, OpenWebUIRequestType requestType, CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // If OpenWebUI is not configured, return fallback immediately
            if (_useMockData)
            {
                _logger.LogDebug("OpenWebUI not configured, returning fallback response for query: {Query}", query);
                var immediateResponse = CreateFallbackResponse(query, requestType);
                immediateResponse.ProcessingTime = stopwatch.Elapsed.TotalSeconds;
                return immediateResponse;
            }
            
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
            
            // If OpenWebUI is not configured, return fallback immediately
            if (_useMockData)
            {
                _logger.LogDebug("OpenWebUI not configured, returning fallback response for prompt");
                return CreateFallbackTextResponse(context);
            }
            
            try
            {
                _logger.LogInformation("Generating response with Open WebUI using model: {Model}", _selectedModel);
                
                var request = new
                {
                    model = _selectedModel,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "You are an AI interview scheduling assistant. Provide helpful, clear responses."
                        },
                        new
                        {
                            role = "user",
                            content = $"Context: {JsonSerializer.Serialize(context)}\n\nPrompt: {prompt}"
                        }
                    },
                    temperature = _configuration.GetValue<double>("OpenWebUI:Temperature", 0.7),
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 500),
                    stream = false
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");
                
                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
                
                var response = await _httpClient.PostAsync("chat/completions", content, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    if (responseObj.TryGetProperty("choices", out var choicesElement) &&
                        choicesElement.GetArrayLength() > 0)
                    {
                        var firstChoice = choicesElement[0];
                        if (firstChoice.TryGetProperty("message", out var messageElement) &&
                            messageElement.TryGetProperty("content", out var contentElement))
                        {
                            var result = contentElement.GetString();
                            if (!string.IsNullOrEmpty(result))
                            {
                                _logger.LogInformation("Generated response in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                                return result;
                            }
                        }
                    }
                }
                
                _logger.LogWarning("Open WebUI API generate returned error: {StatusCode} - {ReasonPhrase}", 
                    response.StatusCode, response.ReasonPhrase);
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

        // Generate actually useful mock responses with variety
        private string GenerateMockResponse(string query)
        {
            if (query.Contains("hi") || query.Contains("hello"))
            {
                return "Hello! How can I help with your scheduling needs today?";
            }
            
            if (query.ToLower().Contains("find") && query.ToLower().Contains("slot"))
            {
                // Generate different responses for different queries
                if (query.Contains("tomorrow"))
                {
                    return "I found 3 available slots for tomorrow:\n\n" +
                           "• 9:00 AM - 10:00 AM\n" +
                           "• 2:00 PM - 3:00 PM\n" +
                           "• 4:30 PM - 5:30 PM\n\n" +
                           "Would any of these work for you?";
                }
                else if (query.Contains("Friday"))
                {
                    return "Here are 2 available slots for Friday:\n\n" +
                           "• 11:00 AM - 12:00 PM\n" +
                           "• 3:30 PM - 4:30 PM\n\n" +
                           "Let me know if you'd like to see more options.";
                }
                else
                {
                    return "I found these available slots:\n\n" +
                           "• Tomorrow, 10:00 AM - 11:00 AM\n" +
                           "• Wednesday, 2:00 PM - 3:00 PM\n" +
                           "• Friday, 9:30 AM - 10:30 AM\n\n" +
                           "Would any of these times work?";
                }
            }
            
            return "I'm not sure how to help with that. You can ask me to find available time slots for meetings.";
        }
        
        // Generate random mock slots based on request parameters
        private List<TimeSlot> GenerateRandomMockSlots(SlotRequest request)
        {
            var random = new Random();
            var slots = new List<TimeSlot>();
            
            // Generate between 0-5 slots based on request
            var slotCount = random.Next(0, 6);
            
            DateTime startDate = request.StartDate ?? DateTime.Today;
            DateTime endDate = request.EndDate ?? DateTime.Today.AddDays(7);
            
            for (int i = 0; i < slotCount; i++)
            {
                // Generate random date between start and end
                int daysToAdd = random.Next(0, (endDate - startDate).Days + 1);
                DateTime slotDate = startDate.AddDays(daysToAdd);
                
                // Generate random hour (9-16 for business hours)
                int hour = random.Next(9, 17);
                
                // Create slot
                var slot = new TimeSlot
                {
                    StartTime = new DateTime(slotDate.Year, slotDate.Month, slotDate.Day, hour, 0, 0),
                    EndTime = new DateTime(slotDate.Year, slotDate.Month, slotDate.Day, hour + 1, 0, 0),
                    AvailableParticipants = request.Participants ?? new List<string>(),
                    TotalParticipants = request.Participants?.Count ?? 0
                };
                
                slots.Add(slot);
            }
            
            return slots;
        }

        private string CreateContextualFallbackResponse(string message, List<MessageHistoryItem> history)
        {
            var lowerMessage = message.ToLowerInvariant();

            // Analyze message content for better contextual responses
            if (lowerMessage.Contains("hello") || lowerMessage.Contains("hi") || lowerMessage.Contains("hey"))
            {
                return "Hello! 👋 I'm your AI-powered Interview Scheduling assistant. I can help you find available time slots and check calendar availability using natural language. What would you like me to help you with today?";
            }

            if (lowerMessage.Contains("help") || lowerMessage.Contains("what can you do"))
            {
                return "I can help you with interview scheduling! Here's what I can do:\n\n• Find available time slots using natural language\n• Check calendar availability for multiple participants\n• Analyze scheduling conflicts\n• Suggest optimal meeting times\n\nJust ask me in plain English what you need!";
            }

            if (lowerMessage.Contains("slots") || lowerMessage.Contains("available") || lowerMessage.Contains("time") || lowerMessage.Contains("schedule"))
            {
                return "I'd be happy to help you find available time slots! Could you please tell me more details like:\n\n• When would you like to check availability?\n• How long should the meeting be?\n• Who should be included?\n\nFor example, you could say 'Find slots tomorrow morning' or 'Check availability for a 1-hour meeting next week'.";
            }

            if (lowerMessage.Contains("thank") || lowerMessage.Contains("thanks"))
            {
                return "You're welcome! I'm here to help with finding availability and checking schedules. Is there anything else you'd like me to assist you with?";
            }

            // Check conversation context for better responses
            if (history.Count > 0)
            {
                var recentBotMessage = history.LastOrDefault(h => h.IsFromBot)?.Message ?? "";
                if (recentBotMessage.Contains("slots") || recentBotMessage.Contains("available"))
                {
                    return "I understand you're interested in checking availability. Could you provide more specific details about when you'd like to meet, the duration, and who should be included? This will help me find the best available slots for you.";
                }
            }

            // Default conversational response
            return "I'm here to help with finding available time slots! You can ask me to check availability, find open times, or analyze schedules using natural language. For example, try asking 'Find slots tomorrow afternoon' or 'When are we available for a meeting next week?' How can I assist you today?";
        }

        private string GenerateSlotSuggestionsFallback(List<TimeSlot> availableSlots, List<string> participants, Dictionary<string, List<ConflictDetail>> conflicts)
        {
            if (!availableSlots.Any())
            {
                return "I couldn't find any available slots that work for all participants. You might want to try a different time range or consider having fewer participants.";
            }

            var response = $"✨ I found {availableSlots.Count} available time slot{(availableSlots.Count > 1 ? "s" : "")} for you!\n\n";

            for (int i = 0; i < Math.Min(3, availableSlots.Count); i++)
            {
                var slot = availableSlots[i];
                response += $"🗓️ **Option {i + 1}:** {slot.StartTime:dddd, MMMM d} from {slot.StartTime:h:mm tt} to {slot.EndTime:h:mm tt}\n";
                response += $"   👥 {slot.AvailableParticipants.Count} of {slot.TotalParticipants} participants available\n\n";
            }

            if (conflicts.Any())
            {
                response += "⚠️ **Conflicts detected:**\n";
                foreach (var conflict in conflicts.Take(2))
                {
                    response += $"• {conflict.Key} has conflicts during some time periods\n";
                }
                if (conflicts.Count > 2)
                {
                    response += $"• And {conflicts.Count - 2} other participant{(conflicts.Count - 2 > 1 ? "s" : "")} with conflicts\n";
                }
            }

            response += "\nWould you like me to help you check other time options or show you different availability?";
            return response;
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
            // Analyze the context to provide more appropriate fallback responses
            var contextStr = context?.ToString() ?? "";
            var contextJson = JsonSerializer.Serialize(context);
            
            // Check if this is a slot suggestions context
            if (contextJson.Contains("Slots") || contextJson.Contains("slots") || contextJson.Contains("SlotCount"))
            {
                return "✨ I found some great time slots for you! Based on your criteria, I've identified several options that should work well. The slots are ranked by how well they match your preferences and participant availability. Would you like me to help you check other time options or explore different availability?";
            }
            
            // Check if this is a greeting context
            if (contextJson.Contains("greeting") || contextJson.Contains("hello") || contextJson.Contains("UserName"))
            {
                return "Hello! 👋 I'm your AI-powered Interview Scheduling assistant. I can help you find available time slots and check calendar availability using natural language. What would you like me to help you with today?";
            }
            
            // Check if this is a help context
            if (contextJson.Contains("help") || contextJson.Contains("Help"))
            {
                return "I can help you with finding available time slots! Here's what I can do:\n\n• Find available time slots using natural language\n• Check calendar availability for multiple participants\n• Analyze scheduling conflicts\n• Suggest optimal meeting times\n\nJust ask me in plain English what you need!";
            }
            
            // Check if this is an error context
            if (contextJson.Contains("error") || contextJson.Contains("Error"))
            {
                return "I apologize, but I encountered an issue. Please try rephrasing your request or ask me something like 'Find slots tomorrow morning' or 'Check availability next week'.";
            }
            
            // Check if this is a follow-up context
            if (contextJson.Contains("follow") || contextJson.Contains("Follow"))
            {
                return "What would you like me to help you with next? I can help you find different time options, check other availability, or answer any questions you might have about the scheduling process.";
            }
            
            // Default response for other contexts
            return "I'm here to help you find available time slots! You can ask me to check availability, find open times, or analyze schedules using natural language. How can I assist you today?";
        }

        private string GetSystemPrompt(string conversationType)
        {
            switch (conversationType.ToLower())
            {
                case "slot_finding":
                    return @"You are an AI interview scheduling assistant with access to calendar data. 
                            Your responses should be helpful, clear, and professional. 
                            When presenting available time slots:
                            
                            1. ALWAYS include specific dates and times in your response
                            2. Group time slots by day for clarity
                            3. Mention participant availability for each slot
                            4. Use a varied vocabulary and different phrasings
                            5. Be conversational but professional
                            6. Format times in a readable way (e.g., '2:00 PM - 3:00 PM')
                            7. Include brief explanations for why certain slots are recommended
                            
                            Never respond with generic messages about finding slots without including the actual time slots.";
                            
                case "general":
                    return @"You are an AI-powered interview scheduling assistant. Your personality is helpful, 
                            efficient, and slightly enthusiastic. Use a varied vocabulary and different sentence 
                            structures in your responses to sound natural. Keep responses concise but informative.
                            
                            Avoid repetitive phrases like 'I'm here to help' in every message.
                            
                            When responding to greetings, be warm and professional, but vary your responses.
                            
                            Always maintain a professional tone suitable for a workplace assistant.";
                            
                default:
                    return @"You are an AI interview scheduling assistant. Provide helpful, clear, and 
                            professional responses. Use varied language and be conversational while staying efficient.";
            }
        }

        private string GenerateSlotResponseFallback(List<TimeSlot> slots, InterviewSchedulingBot.Services.Business.SlotQueryCriteria criteria)
        {
            if (!slots.Any())
            {
                return "I couldn't find any available slots that match your criteria. You might want to try a different time range or consider having fewer participants.";
            }

            var response = $"✨ I found {slots.Count} available time slot{(slots.Count > 1 ? "s" : "")} for you!\n\n";
            
            // Group slots by day
            var slotsByDay = slots.GroupBy(s => s.StartTime.Date).OrderBy(g => g.Key);
            
            foreach (var dayGroup in slotsByDay)
            {
                response += $"**{dayGroup.Key:dddd, MMMM d}:**\n";
                
                foreach (var slot in dayGroup.Take(3))
                {
                    response += $"• {slot.StartTime:h:mm tt} - {slot.EndTime:h:mm tt}";
                    response += $" ({slot.AvailableParticipants.Count}/{slot.TotalParticipants} participants available)\n";
                }
                
                response += "\n";
            }

            response += "Would you like me to check other time options or show you different availability?";
            return response;
        }

        private string CreateVariedFallbackResponse(string message, List<MessageHistoryItem> history, ConversationOptions options)
        {
            var lowerMessage = message.ToLowerInvariant();
            var responseVariations = new Dictionary<string, string[]>();

            // Greetings
            if (lowerMessage.Contains("hello") || lowerMessage.Contains("hi") || lowerMessage.Contains("hey"))
            {
                var greetings = new[]
                {
                    "Hello! 👋 I'm your AI-powered Interview Scheduling assistant. I can help you find available time slots and check calendar availability using natural language. What would you like me to help you with today?",
                    "Hi there! Welcome to the Interview Scheduling Bot. I specialize in finding time slots and checking calendar availability. How can I assist you?",
                    "Hey! Great to see you. I'm here to make finding available time slots easier for you. Just tell me what you need - find slots, check availability, or analyze schedules. What's on your agenda?",
                    "Hello! I'm your scheduling assistant, ready to help with finding available time slots and checking calendar availability. What can I do for you?"
                };
                return greetings[new Random().Next(greetings.Length)];
            }

            // Help requests
            if (lowerMessage.Contains("help") || lowerMessage.Contains("what can you do"))
            {
                var helpResponses = new[]
                {
                    "I can help you find available time slots! Here's what I can do:\n\n• Find available time slots using natural language\n• Check calendar availability for multiple participants\n• Analyze scheduling conflicts\n• Suggest optimal meeting times\n\nJust ask me in plain English what you need!",
                    "I'm your availability specialist! My capabilities include:\n\n• Intelligent time slot discovery\n• Calendar availability checking\n• Conflict analysis and resolution\n• Natural language understanding\n\nTry asking something like 'Find slots tomorrow morning' or 'Check availability next week'.",
                    "Here's how I can assist with finding availability:\n\n• Smart time slot search\n• Multi-participant availability checking\n• Schedule conflict analysis\n• Flexible time options\n\nSimply describe what you need in everyday language, and I'll help you find the best available times!"
                };
                return helpResponses[new Random().Next(helpResponses.Length)];
            }

            // Scheduling requests
            if (lowerMessage.Contains("slots") || lowerMessage.Contains("available") || lowerMessage.Contains("time") || lowerMessage.Contains("schedule"))
            {
                var schedulingResponses = new[]
                {
                    "I'd be happy to help you find available time slots! Could you please tell me more details like:\n\n• When would you like to check availability?\n• How long should the meeting be?\n• Who should be included?\n\nFor example, you could say 'Find slots tomorrow morning' or 'Check availability for a 1-hour meeting next week'.",
                    "Perfect! I can help you find the ideal time slots. To get started, I'll need a few details:\n\n• Your preferred timeframe\n• Meeting duration\n• Participants to include\n\nTry something like 'Find 90-minute slots Thursday afternoon' or 'Check availability with John and Mary next week'.",
                    "Excellent! Let me help you discover available time options. Please share:\n\n• When you'd like to meet\n• How long the session should be\n• Who needs to attend\n\nJust describe it naturally - 'Find an hour slot tomorrow' or 'Check availability for 3 people Friday'."
                };
                return schedulingResponses[new Random().Next(schedulingResponses.Length)];
            }

            // Thank you responses
            if (lowerMessage.Contains("thank") || lowerMessage.Contains("thanks"))
            {
                var thankYouResponses = new[]
                {
                    "You're welcome! I'm here to help with your scheduling needs. Is there anything else you'd like me to assist you with?",
                    "My pleasure! Feel free to ask if you need help with more scheduling tasks.",
                    "Glad I could help! Let me know if you have any other scheduling questions or needs.",
                    "You're very welcome! I'm always ready to help with your interview and meeting coordination."
                };
                return thankYouResponses[new Random().Next(thankYouResponses.Length)];
            }

            // Default varied responses
            var defaultResponses = new[]
            {
                "I'm here to help with interview scheduling! You can ask me to find time slots, schedule meetings, or check availability using natural language. How can I assist you today?",
                "I specialize in making scheduling easier! Whether you need to find available times, book meetings, or coordinate interviews, just tell me what you need.",
                "Ready to help with your scheduling needs! I can find time slots, check availability, and coordinate meetings. What would you like to work on?",
                "Let's get your scheduling sorted! I can help you find open time slots, arrange meetings, and manage calendar coordination. What's your goal today?"
            };
            
            return defaultResponses[new Random().Next(defaultResponses.Length)];
        }

        private string CreateClarificationFallback(string message, string context)
        {
            var clarificationResponses = new[]
            {
                "I'd like to help you with scheduling, but I need a bit more information. Could you tell me:\n\n• When you'd like to schedule the meeting\n• How long it should be\n• Who should attend\n\nFor example: 'Find 60-minute slots tomorrow afternoon with John and Sarah'",
                "To find the best available slots, I'll need some additional details:\n\n• Your preferred time or day\n• Duration of the meeting\n• Participants to include\n\nTry something like 'Schedule a 90-minute interview next Tuesday'",
                "I want to make sure I find exactly what you need. Please provide:\n\n• Timeframe preference (day/time)\n• Meeting length\n• Attendee list\n\nJust describe it naturally, like 'Book an hour with the team Thursday morning'"
            };
            
            return clarificationResponses[new Random().Next(clarificationResponses.Length)];
        }

        private string CreateNoSlotsFallback(InterviewSchedulingBot.Services.Business.SlotQueryCriteria criteria)
        {
            var noSlotsResponses = new[]
            {
                $"I couldn't find any available slots that work for all participants during {criteria.SpecificDay ?? "the requested time"}. Here are some suggestions:\n\n• Try a different time range\n• Consider fewer participants\n• Look at alternative days\n• Split into multiple shorter meetings\n\nWould you like me to search with different criteria?",
                $"Unfortunately, no slots are available that meet your requirements for {criteria.DurationMinutes} minutes. You might want to:\n\n• Expand the time window\n• Reduce the participant list\n• Consider scheduling on different days\n• Break into smaller sessions\n\nShall I help you explore other options?",
                $"No matching slots found for your request. To increase availability, consider:\n\n• Flexible timing (earlier/later in the day)\n• Shorter meeting duration\n• Optional attendees\n• Alternative dates\n\nI'm happy to search again with adjusted parameters!"
            };
            
            return noSlotsResponses[new Random().Next(noSlotsResponses.Length)];
        }

        // New methods for intent recognition and slot finding
        public async Task<IntentResponse> RecognizeIntentAsync(string message, List<MessageHistoryItem> history)
        {
            // If OpenWebUI is not configured, use fallback intent recognition
            if (_useMockData)
            {
                return RecognizeIntentFallback(message);
            }

            try
            {
                var request = new
                {
                    message = message,
                    history = history.Select(h => new { role = h.IsFromBot ? "assistant" : "user", content = h.Message }).ToArray(),
                    task = "intent_recognition",
                    system_prompt = "Classify the user's intent. Return one of: FindSlots, Help, Logout, General. For any request about finding time, availability, slots, scheduling, or meeting times, return FindSlots."
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("intent", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (responseObj.TryGetProperty("intent", out var intentElement))
                    {
                        var intent = intentElement.GetString() ?? "";
                        var confidence = responseObj.TryGetProperty("confidence", out var confElement) ? confElement.GetDouble() : 0.8;
                        
                        return new IntentResponse
                        {
                            TopIntent = intent,
                            Confidence = confidence
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recognizing intent from OpenWebUI");
            }

            return RecognizeIntentFallback(message);
        }

        public async Task<SlotParameters> ExtractSlotParametersAsync(string message, List<MessageHistoryItem> history)
        {
            // If OpenWebUI is not configured, use fallback parameter extraction
            if (_useMockData)
            {
                return ExtractSlotParametersFallback(message);
            }

            try
            {
                var request = new
                {
                    message = message,
                    history = history.Select(h => new { role = h.IsFromBot ? "assistant" : "user", content = h.Message }).ToArray(),
                    task = "extract_slot_parameters",
                    system_prompt = "Extract scheduling parameters from the message. Return JSON with: participants (emails), startDate, endDate, durationMinutes, timeOfDay, specificDay."
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("extract", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var parameters = JsonSerializer.Deserialize<SlotParameters>(responseContent, 
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    
                    if (parameters != null)
                    {
                        return parameters;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting slot parameters from OpenWebUI");
            }

            return ExtractSlotParametersFallback(message);
        }

        public async Task<string> FormatAvailableSlotsAsync(List<TimeSlot> slots, SlotParameters parameters)
        {
            // If OpenWebUI is not configured, use fallback formatting
            if (_useMockData)
            {
                return FormatAvailableSlotsFallback(slots, parameters);
            }

            try
            {
                var request = new
                {
                    slots = slots.Select(s => new
                    {
                        date = s.StartTime.ToString("dddd, MMMM d"),
                        day = s.StartTime.ToString("dddd"),
                        startTime = s.StartTime.ToString("h:mm tt"),
                        endTime = s.EndTime.ToString("h:mm tt"),
                        duration = (s.EndTime - s.StartTime).TotalMinutes,
                        availableParticipants = s.AvailableParticipants.Count,
                        totalParticipants = s.TotalParticipants,
                        availabilityScore = s.AvailabilityScore
                    }).ToList(),
                    parameters = parameters,
                    task = "format_available_slots",
                    system_prompt = "Format the available time slots in a conversational, helpful way. Group by day, include specific times, mention availability. Be enthusiastic but professional."
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("format", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (responseObj.TryGetProperty("response", out var responseElement))
                    {
                        var result = responseElement.GetString();
                        if (!string.IsNullOrEmpty(result))
                        {
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error formatting available slots from OpenWebUI");
            }

            return FormatAvailableSlotsFallback(slots, parameters);
        }

        public async Task<string> GenerateClarificationAsync(SlotParameters parameters, List<MessageHistoryItem> history)
        {
            // If OpenWebUI is not configured, use fallback clarification
            if (_useMockData)
            {
                return GenerateClarificationFallback(parameters);
            }

            try
            {
                var request = new
                {
                    parameters = parameters,
                    history = history.Select(h => new { role = h.IsFromBot ? "assistant" : "user", content = h.Message }).ToArray(),
                    task = "generate_clarification",
                    system_prompt = "Ask for missing information needed to find time slots. Be helpful and specific about what's needed."
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("clarify", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (responseObj.TryGetProperty("response", out var responseElement))
                    {
                        var result = responseElement.GetString();
                        if (!string.IsNullOrEmpty(result))
                        {
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating clarification from OpenWebUI");
            }

            return GenerateClarificationFallback(parameters);
        }

        public async Task<string> GenerateNoSlotsResponseAsync(SlotParameters parameters, List<MessageHistoryItem> history)
        {
            // If OpenWebUI is not configured, use fallback no slots response
            if (_useMockData)
            {
                return GenerateNoSlotsFallback(parameters);
            }

            try
            {
                var request = new
                {
                    parameters = parameters,
                    history = history.Select(h => new { role = h.IsFromBot ? "assistant" : "user", content = h.Message }).ToArray(),
                    task = "generate_no_slots_response",
                    system_prompt = "Generate a helpful response when no slots are available. Suggest alternatives and offer to help with different criteria."
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("no-slots", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (responseObj.TryGetProperty("response", out var responseElement))
                    {
                        var result = responseElement.GetString();
                        if (!string.IsNullOrEmpty(result))
                        {
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating no slots response from OpenWebUI");
            }

            return GenerateNoSlotsFallback(parameters);
        }

        // Fallback methods for when OpenWebUI is not available
        private IntentResponse RecognizeIntentFallback(string message)
        {
            var lowerMessage = message.ToLowerInvariant();
            
            if (lowerMessage.Contains("find") && (lowerMessage.Contains("slot") || lowerMessage.Contains("time") || lowerMessage.Contains("available")) ||
                lowerMessage.Contains("schedule") || lowerMessage.Contains("meeting") || lowerMessage.Contains("interview") ||
                lowerMessage.Contains("when") && (lowerMessage.Contains("free") || lowerMessage.Contains("available")) ||
                lowerMessage.Contains("book") || lowerMessage.Contains("reserve"))
            {
                return new IntentResponse { TopIntent = "FindSlots", Confidence = 0.8 };
            }
            
            if (lowerMessage.Contains("help") || lowerMessage.Contains("what can you do"))
            {
                return new IntentResponse { TopIntent = "Help", Confidence = 0.9 };
            }
            
            if (lowerMessage.Contains("logout") || lowerMessage.Contains("sign out"))
            {
                return new IntentResponse { TopIntent = "Logout", Confidence = 0.9 };
            }
            
            return new IntentResponse { TopIntent = "General", Confidence = 0.6 };
        }

        private SlotParameters ExtractSlotParametersFallback(string message)
        {
            var lowerMessage = message.ToLowerInvariant();
            var parameters = new SlotParameters();
            
            // Extract time of day
            if (lowerMessage.Contains("morning"))
                parameters.TimeOfDay = "morning";
            else if (lowerMessage.Contains("afternoon"))
                parameters.TimeOfDay = "afternoon";
            else if (lowerMessage.Contains("evening"))
                parameters.TimeOfDay = "evening";
            
            // Extract specific days
            if (lowerMessage.Contains("monday"))
                parameters.SpecificDay = "Monday";
            else if (lowerMessage.Contains("tuesday"))
                parameters.SpecificDay = "Tuesday";
            else if (lowerMessage.Contains("wednesday"))
                parameters.SpecificDay = "Wednesday";
            else if (lowerMessage.Contains("thursday"))
                parameters.SpecificDay = "Thursday";
            else if (lowerMessage.Contains("friday"))
                parameters.SpecificDay = "Friday";
            else if (lowerMessage.Contains("tomorrow"))
                parameters.StartDate = DateTime.Today.AddDays(1);
            else if (lowerMessage.Contains("next week"))
                parameters.StartDate = DateTime.Today.AddDays(7);
            
            // Extract duration
            if (lowerMessage.Contains("30 min"))
                parameters.DurationMinutes = 30;
            else if (lowerMessage.Contains("90 min") || lowerMessage.Contains("1.5 hour"))
                parameters.DurationMinutes = 90;
            else if (lowerMessage.Contains("2 hour"))
                parameters.DurationMinutes = 120;
            else
                parameters.DurationMinutes = 60; // Default
            
            // Set default date range if not specified
            if (!parameters.StartDate.HasValue && string.IsNullOrEmpty(parameters.SpecificDay))
            {
                parameters.StartDate = DateTime.Today;
                parameters.EndDate = DateTime.Today.AddDays(14);
            }
            
            return parameters;
        }

        private string FormatAvailableSlotsFallback(List<TimeSlot> slots, SlotParameters parameters)
        {
            if (!slots.Any())
            {
                return "I couldn't find any available slots that match your criteria.";
            }

            var response = $"✨ Great! I found {slots.Count} available time slot{(slots.Count > 1 ? "s" : "")} for you:\n\n";
            
            // Group slots by day
            var slotsByDay = slots.GroupBy(s => s.StartTime.Date).OrderBy(g => g.Key);
            
            foreach (var dayGroup in slotsByDay.Take(3)) // Limit to 3 days
            {
                response += $"**{dayGroup.Key:dddd, MMMM d}:**\n";
                
                foreach (var slot in dayGroup.Take(3)) // Limit to 3 slots per day
                {
                    response += $"• {slot.StartTime:h:mm tt} - {slot.EndTime:h:mm tt}";
                    if (slot.AvailableParticipants.Any())
                    {
                        response += $" ({slot.AvailableParticipants.Count}/{slot.TotalParticipants} participants available)";
                    }
                    response += "\n";
                }
                
                response += "\n";
            }

            if (slots.Count > 9) // If we have more than 3 days × 3 slots
            {
                response += $"...and {slots.Count - 9} more available slots.\n\n";
            }

            response += "Would you like me to check other time options or show you different availability?";
            return response;
        }

        private string GenerateClarificationFallback(SlotParameters parameters)
        {
            var clarificationResponses = new[]
            {
                "I'd like to help you find available time slots, but I need a bit more information. Could you tell me:\n\n• When you'd like to schedule the meeting (day/time)\n• How long it should be\n• Who should attend\n\nFor example: 'Find 60-minute slots tomorrow afternoon with John and Sarah'",
                "To find the best available slots, I'll need some additional details:\n\n• Your preferred time or day\n• Duration of the meeting\n• Participants to include\n\nTry something like 'Schedule a 90-minute interview next Tuesday'",
                "I want to make sure I find exactly what you need. Please provide:\n\n• Timeframe preference (day/time)\n• Meeting length\n• Attendee list\n\nJust describe it naturally, like 'Book an hour with the team Thursday morning'"
            };
            
            return clarificationResponses[new Random().Next(clarificationResponses.Length)];
        }

        private string GenerateNoSlotsFallback(SlotParameters parameters)
        {
            var timeFrame = parameters.SpecificDay ?? parameters.TimeOfDay ?? "the requested time";
            var duration = parameters.DurationMinutes?.ToString() ?? "the specified";
            
            var noSlotsResponses = new[]
            {
                $"I couldn't find any available slots during {timeFrame}. Here are some suggestions:\n\n• Try a different time range\n• Consider fewer participants\n• Look at alternative days\n• Split into multiple shorter meetings\n\nWould you like me to search with different criteria?",
                $"Unfortunately, no slots are available that meet your requirements for {duration} minutes. You might want to:\n\n• Expand the time window\n• Reduce the participant list\n• Consider scheduling on different days\n• Break into smaller sessions\n\nShall I help you explore other options?",
                $"No matching slots found for your request. To increase availability, consider:\n\n• Flexible timing (earlier/later in the day)\n• Shorter meeting duration\n• Optional attendees\n• Alternative dates\n\nI'm happy to search again with adjusted parameters!"
            };
            
            return noSlotsResponses[new Random().Next(noSlotsResponses.Length)];
        }

        // Specialized parameter extraction assistant implementation
        public async Task<ParameterExtractionResponse> ExtractParametersAsAssistantAsync(string message)
        {
            // If OpenWebUI is not configured, use fallback parameter extraction
            if (_useMockData)
            {
                return ExtractParametersAsAssistantFallback(message);
            }

            try
            {
                var systemPrompt = @"You are a specialized parameter extraction assistant for an Interview Scheduling Bot. Your ONLY job is to analyze user messages and extract scheduling parameters in structured JSON format.

When a user asks about finding available times or scheduling meetings, ONLY respond with the JSON structure below. Do NOT provide general scheduling advice or conversation.

Required JSON format:
{
  ""isSlotRequest"": true,
  ""parameters"": {
    ""duration"": 60,
    ""timeFrame"": {
      ""type"": ""specific_day"", 
      ""startDate"": ""2025-07-31"",
      ""endDate"": ""2025-07-31"",
      ""timeOfDay"": ""morning""
    },
    ""participants"": [""john.doe@example.com"", ""jane.smith@example.com""]
  }
}

For timeFrame.type, use one of: ""specific_day"", ""this_week"", ""next_week"", ""date_range""
For timeOfDay, use one of: ""morning"", ""afternoon"", ""evening"", ""all_day"", or null if not specified

For non-slot requests, respond with:
{
  ""isSlotRequest"": false,
  ""suggestedResponse"": ""A helpful message the bot can use to respond""
}

IMPORTANT:
- ONLY output valid JSON
- Today's date is " + DateTime.Today.ToString("yyyy-MM-dd") + @"
- Default duration is 60 minutes if not specified
- Extract emails or names for participants when available
- Output NOTHING except the JSON structure";

                var request = new
                {
                    model = _selectedModel,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = systemPrompt
                        },
                        new
                        {
                            role = "user",
                            content = message
                        }
                    },
                    temperature = 0.1, // Low temperature for consistent structured output
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 500),
                    stream = false
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

                var response = await _httpClient.PostAsync("chat/completions", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (responseObj.TryGetProperty("choices", out var choicesElement) &&
                        choicesElement.GetArrayLength() > 0)
                    {
                        var firstChoice = choicesElement[0];
                        if (firstChoice.TryGetProperty("message", out var messageElement) &&
                            messageElement.TryGetProperty("content", out var contentElement))
                        {
                            var jsonResult = contentElement.GetString();
                            if (!string.IsNullOrEmpty(jsonResult))
                            {
                                try
                                {
                                    var extractedResponse = JsonSerializer.Deserialize<ParameterExtractionResponse>(jsonResult,
                                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                    
                                    if (extractedResponse != null)
                                    {
                                        _logger.LogInformation("Successfully extracted parameters using OpenWebUI");
                                        return extractedResponse;
                                    }
                                }
                                catch (JsonException ex)
                                {
                                    _logger.LogWarning(ex, "Failed to parse JSON response from OpenWebUI: {Response}", jsonResult);
                                }
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("OpenWebUI API returned error: {StatusCode} - {ReasonPhrase}", 
                        response.StatusCode, response.ReasonPhrase);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting parameters from OpenWebUI");
            }

            return ExtractParametersAsAssistantFallback(message);
        }

        private ParameterExtractionResponse ExtractParametersAsAssistantFallback(string message)
        {
            var lowerMessage = message.ToLowerInvariant();
            
            // Check if this is a slot finding request
            bool isSlotRequest = lowerMessage.Contains("find") && (lowerMessage.Contains("slot") || lowerMessage.Contains("time") || lowerMessage.Contains("available")) ||
                                lowerMessage.Contains("schedule") || lowerMessage.Contains("meeting") || lowerMessage.Contains("interview") ||
                                lowerMessage.Contains("when") && (lowerMessage.Contains("free") || lowerMessage.Contains("available")) ||
                                lowerMessage.Contains("book") || lowerMessage.Contains("reserve") ||
                                lowerMessage.Contains("availability") || lowerMessage.Contains("open");

            if (!isSlotRequest)
            {
                // Generate appropriate suggested response for non-slot requests
                string suggestedResponse;
                
                if (lowerMessage.Contains("hello") || lowerMessage.Contains("hi") || lowerMessage.Contains("hey"))
                {
                    suggestedResponse = "Hello! 👋 I'm your AI-powered Interview Scheduling assistant. I can help you find available time slots and check calendar availability using natural language. What would you like me to help you with today?";
                }
                else if (lowerMessage.Contains("help") || lowerMessage.Contains("what can you do"))
                {
                    suggestedResponse = "I can help you with interview scheduling! Here's what I can do:\n\n• Find available time slots using natural language\n• Check calendar availability for multiple participants\n• Analyze scheduling conflicts\n• Suggest optimal meeting times\n\nJust ask me in plain English what you need!";
                }
                else if (lowerMessage.Contains("thank") || lowerMessage.Contains("thanks"))
                {
                    suggestedResponse = "You're welcome! I'm here to help with your scheduling needs. Is there anything else you'd like me to assist you with?";
                }
                else
                {
                    suggestedResponse = "I'm here to help with finding available time slots! You can ask me to check availability, find open times, or analyze schedules using natural language. How can I assist you today?";
                }

                return new ParameterExtractionResponse
                {
                    IsSlotRequest = false,
                    SuggestedResponse = suggestedResponse
                };
            }

            // Extract parameters for slot requests
            var parameters = new ParameterExtractionData();
            var timeFrame = new TimeFrameData();
            
            // Extract duration
            if (lowerMessage.Contains("30 min") || lowerMessage.Contains("thirty min"))
                parameters.Duration = 30;
            else if (lowerMessage.Contains("90 min") || lowerMessage.Contains("1.5 hour") || lowerMessage.Contains("one and half hour"))
                parameters.Duration = 90;
            else if (lowerMessage.Contains("2 hour") || lowerMessage.Contains("two hour"))
                parameters.Duration = 120;
            else if (lowerMessage.Contains("45 min") || lowerMessage.Contains("forty-five min"))
                parameters.Duration = 45;
            else
                parameters.Duration = 60; // Default
            
            // Extract time of day
            if (lowerMessage.Contains("morning"))
                timeFrame.TimeOfDay = "morning";
            else if (lowerMessage.Contains("afternoon"))
                timeFrame.TimeOfDay = "afternoon";
            else if (lowerMessage.Contains("evening"))
                timeFrame.TimeOfDay = "evening";
            else if (lowerMessage.Contains("all day") || lowerMessage.Contains("any time"))
                timeFrame.TimeOfDay = "all_day";
            
            // Extract time frame type and dates
            var today = DateTime.Today;
            
            if (lowerMessage.Contains("tomorrow"))
            {
                timeFrame.Type = "specific_day";
                var tomorrow = today.AddDays(1);
                timeFrame.StartDate = tomorrow.ToString("yyyy-MM-dd");
                timeFrame.EndDate = tomorrow.ToString("yyyy-MM-dd");
            }
            else if (lowerMessage.Contains("this week"))
            {
                timeFrame.Type = "this_week";
                // Start from today, end on Sunday of this week
                var daysUntilSunday = 7 - (int)today.DayOfWeek;
                timeFrame.StartDate = today.ToString("yyyy-MM-dd");
                timeFrame.EndDate = today.AddDays(daysUntilSunday).ToString("yyyy-MM-dd");
            }
            else if (lowerMessage.Contains("next week"))
            {
                timeFrame.Type = "next_week";
                // Start from next Monday, end on next Sunday
                var daysUntilNextMonday = 7 - (int)today.DayOfWeek + 1;
                if (today.DayOfWeek == DayOfWeek.Sunday) daysUntilNextMonday = 1;
                
                var nextMonday = today.AddDays(daysUntilNextMonday);
                timeFrame.StartDate = nextMonday.ToString("yyyy-MM-dd");
                timeFrame.EndDate = nextMonday.AddDays(6).ToString("yyyy-MM-dd");
            }
            else if (lowerMessage.Contains("monday"))
            {
                timeFrame.Type = "specific_day";
                var nextMonday = GetNextWeekday(today, DayOfWeek.Monday);
                timeFrame.StartDate = nextMonday.ToString("yyyy-MM-dd");
                timeFrame.EndDate = nextMonday.ToString("yyyy-MM-dd");
            }
            else if (lowerMessage.Contains("tuesday"))
            {
                timeFrame.Type = "specific_day";
                var nextTuesday = GetNextWeekday(today, DayOfWeek.Tuesday);
                timeFrame.StartDate = nextTuesday.ToString("yyyy-MM-dd");
                timeFrame.EndDate = nextTuesday.ToString("yyyy-MM-dd");
            }
            else if (lowerMessage.Contains("wednesday"))
            {
                timeFrame.Type = "specific_day";
                var nextWednesday = GetNextWeekday(today, DayOfWeek.Wednesday);
                timeFrame.StartDate = nextWednesday.ToString("yyyy-MM-dd");
                timeFrame.EndDate = nextWednesday.ToString("yyyy-MM-dd");
            }
            else if (lowerMessage.Contains("thursday"))
            {
                timeFrame.Type = "specific_day";
                var nextThursday = GetNextWeekday(today, DayOfWeek.Thursday);
                timeFrame.StartDate = nextThursday.ToString("yyyy-MM-dd");
                timeFrame.EndDate = nextThursday.ToString("yyyy-MM-dd");
            }
            else if (lowerMessage.Contains("friday"))
            {
                timeFrame.Type = "specific_day";
                var nextFriday = GetNextWeekday(today, DayOfWeek.Friday);
                timeFrame.StartDate = nextFriday.ToString("yyyy-MM-dd");
                timeFrame.EndDate = nextFriday.ToString("yyyy-MM-dd");
            }
            else
            {
                // Default to specific day (today or next business day)
                timeFrame.Type = "specific_day";
                var targetDate = today.DayOfWeek == DayOfWeek.Saturday || today.DayOfWeek == DayOfWeek.Sunday 
                    ? GetNextWeekday(today, DayOfWeek.Monday)
                    : today;
                timeFrame.StartDate = targetDate.ToString("yyyy-MM-dd");
                timeFrame.EndDate = targetDate.ToString("yyyy-MM-dd");
            }
            
            parameters.TimeFrame = timeFrame;
            
            // Extract participants (look for email addresses or names with @)
            var participants = new List<string>();
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var word in words)
            {
                if (word.Contains("@") && word.Contains("."))
                {
                    // Clean up the email (remove punctuation at the end)
                    var cleanedEmail = word.TrimEnd('.', ',', ';', ':', '!', '?');
                    participants.Add(cleanedEmail);
                }
            }
            
            // Look for common name patterns like "with John" or "John and Mary"
            if (participants.Count == 0)
            {
                var nameKeywords = new[] { "with", "include", "and" };
                foreach (var keyword in nameKeywords)
                {
                    var keywordIndex = Array.FindIndex(words, w => w.ToLowerInvariant() == keyword);
                    if (keywordIndex >= 0 && keywordIndex < words.Length - 1)
                    {
                        // Look for names after the keyword
                        for (int i = keywordIndex + 1; i < words.Length && i < keywordIndex + 4; i++)
                        {
                            var potentialName = words[i].Trim(',', '.', ';', ':', '!', '?');
                            if (potentialName.Length > 1 && char.IsUpper(potentialName[0]))
                            {
                                participants.Add($"{potentialName.ToLowerInvariant()}@example.com");
                            }
                        }
                    }
                }
            }
            
            parameters.Participants = participants;

            return new ParameterExtractionResponse
            {
                IsSlotRequest = true,
                Parameters = parameters
            };
        }

        private DateTime GetNextWeekday(DateTime start, DayOfWeek day)
        {
            var daysToAdd = ((int)day - (int)start.DayOfWeek + 7) % 7;
            return start.AddDays(daysToAdd == 0 ? 7 : daysToAdd);
        }
    }

    public class ConversationOptions
    {
        public bool IncludeTimeSlots { get; set; }
        public bool PersonalizeResponse { get; set; }
        public bool ExpandVocabulary { get; set; }
        public double ResponseTemperature { get; set; } = 0.7;
    }
}