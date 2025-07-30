using System.Text;
using System.Text.Json;
using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Services.Integration
{
    public interface IOpenWebUIClient
    {
        Task<OpenWebUIResponse> ProcessQueryAsync(string query, OpenWebUIRequestType requestType, CancellationToken cancellationToken = default);
        Task<string> GenerateResponseAsync(string prompt, object context, CancellationToken cancellationToken = default);
        Task<string> GetDirectResponseAsync(string message, string conversationId, List<MessageHistoryItem> history);
        Task<string> GenerateSlotSuggestionsWithConflicts(List<TimeSlot> availableSlots, List<string> participants, Dictionary<string, List<ConflictDetail>> conflicts);
    }

    public class OpenWebUIClient : IOpenWebUIClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenWebUIClient> _logger;
        private readonly bool _isConfigured;
        
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
            
            _isConfigured = !string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(apiKey);
            
            if (_isConfigured)
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                _logger.LogInformation("OpenWebUI client configured with base URL: {BaseUrl}", baseUrl);
            }
            else
            {
                _logger.LogInformation("OpenWebUI not configured, using fallback responses");
            }
        }

        public async Task<string> GetDirectResponseAsync(string message, string conversationId, List<MessageHistoryItem> history)
        {
            // If OpenWebUI is not configured, return fallback immediately
            if (!_isConfigured)
            {
                _logger.LogDebug("OpenWebUI not configured, returning fallback response for message: {Message}", message);
                return CreateContextualFallbackResponse(message, history);
            }

            try
            {
                var request = new
                {
                    message = message,
                    conversation_id = conversationId,
                    history = history.Select(h => new { role = h.IsFromBot ? "assistant" : "user", content = h.Message }).ToArray(),
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 1000)
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8,
                    "application/json");

                var timeoutMs = _configuration.GetValue<int>("OpenWebUI:Timeout", 30000);
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
                
                var response = await _httpClient.PostAsync("chat", content, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (responseObj.TryGetProperty("response", out var responseElement))
                    {
                        var result = responseElement.GetString();
                        if (!string.IsNullOrEmpty(result))
                        {
                            _logger.LogInformation("Received direct response from OpenWebUI");
                            return result;
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("OpenWebUI API returned error: {StatusCode}", response.StatusCode);
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

        public async Task<string> GenerateSlotSuggestionsWithConflicts(List<TimeSlot> availableSlots, List<string> participants, Dictionary<string, List<ConflictDetail>> conflicts)
        {
            // If OpenWebUI is not configured, return fallback immediately
            if (!_isConfigured)
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
            if (!_isConfigured)
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
            if (!_isConfigured)
            {
                _logger.LogDebug("OpenWebUI not configured, returning fallback response for prompt");
                return CreateFallbackTextResponse(context);
            }
            
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

        private string CreateContextualFallbackResponse(string message, List<MessageHistoryItem> history)
        {
            var lowerMessage = message.ToLowerInvariant();

            // Analyze message content for better contextual responses
            if (lowerMessage.Contains("hello") || lowerMessage.Contains("hi") || lowerMessage.Contains("hey"))
            {
                return "Hello! 👋 I'm your AI-powered Interview Scheduling assistant. I can help you find available time slots, schedule meetings, and manage your calendar using natural language. What would you like me to help you with today?";
            }

            if (lowerMessage.Contains("help") || lowerMessage.Contains("what can you do"))
            {
                return "I can help you with interview scheduling! Here's what I can do:\n\n• Find available time slots using natural language\n• Schedule interviews and meetings\n• Check calendar availability\n• Answer questions about scheduling\n\nJust ask me in plain English what you need!";
            }

            if (lowerMessage.Contains("slots") || lowerMessage.Contains("available") || lowerMessage.Contains("time") || lowerMessage.Contains("schedule"))
            {
                return "I'd be happy to help you find available time slots! Could you please tell me more details like:\n\n• When would you like to schedule the meeting?\n• How long should the meeting be?\n• Who should be included?\n\nFor example, you could say 'Find slots tomorrow morning' or 'Schedule a 1-hour meeting next week'.";
            }

            if (lowerMessage.Contains("thank") || lowerMessage.Contains("thanks"))
            {
                return "You're welcome! I'm here to help with your scheduling needs. Is there anything else you'd like me to assist you with?";
            }

            // Check conversation context for better responses
            if (history.Count > 0)
            {
                var recentBotMessage = history.LastOrDefault(h => h.IsFromBot)?.Message ?? "";
                if (recentBotMessage.Contains("slots") || recentBotMessage.Contains("available"))
                {
                    return "I understand you're interested in scheduling. Could you provide more specific details about when you'd like to meet, the duration, and who should be included? This will help me find the best available slots for you.";
                }
            }

            // Default conversational response
            return "I'm here to help with interview scheduling! You can ask me to find time slots, schedule meetings, or check availability using natural language. For example, try asking 'Find slots tomorrow afternoon' or 'When can we schedule a meeting next week?' How can I assist you today?";
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

            response += "\nWould you like me to help you schedule one of these slots or show you different options?";
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
                return "✨ I found some great time slots for you! Based on your criteria, I've identified several options that should work well. The slots are ranked by how well they match your preferences and participant availability. Would you like me to help you schedule one of these slots or would you prefer to see different options?";
            }
            
            // Check if this is a greeting context
            if (contextJson.Contains("greeting") || contextJson.Contains("hello") || contextJson.Contains("UserName"))
            {
                return "Hello! 👋 I'm your AI-powered Interview Scheduling assistant. I can help you find available time slots, schedule meetings, and manage your calendar using natural language. What would you like me to help you with today?";
            }
            
            // Check if this is a help context
            if (contextJson.Contains("help") || contextJson.Contains("Help"))
            {
                return "I can help you with interview scheduling! Here's what I can do:\n\n• Find available time slots using natural language\n• Schedule interviews and meetings\n• Check calendar availability\n• Answer questions about scheduling\n\nJust ask me in plain English what you need!";
            }
            
            // Check if this is an error context
            if (contextJson.Contains("error") || contextJson.Contains("Error"))
            {
                return "I apologize, but I encountered an issue. Please try rephrasing your request or ask me something like 'Find slots tomorrow morning' or 'Schedule an interview next week'.";
            }
            
            // Check if this is a follow-up context
            if (contextJson.Contains("follow") || contextJson.Contains("Follow"))
            {
                return "What would you like me to help you with next? I can help you schedule one of these slots, find different options, or answer any questions you might have about the scheduling process.";
            }
            
            // Default response for other contexts
            return "I'm here to help you with interview scheduling! You can ask me to find time slots, schedule meetings, or check availability using natural language. How can I assist you today?";
        }
    }
}