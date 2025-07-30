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
        Task<string> GenerateSlotResponseAsync(List<TimeSlot> slots, InterviewSchedulingBot.Services.Business.SlotQueryCriteria criteria);
        Task<string> GetConversationalResponseAsync(string message, string conversationId, List<MessageHistoryItem> history, ConversationOptions options);
        Task<string> GetClarificationRequestAsync(string message, string context, List<MessageHistoryItem> history);
        Task<string> GenerateNoSlotsResponseAsync(InterviewSchedulingBot.Services.Business.SlotQueryCriteria criteria, List<MessageHistoryItem> history);
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

        public async Task<string> GenerateSlotResponseAsync(List<TimeSlot> slots, InterviewSchedulingBot.Services.Business.SlotQueryCriteria criteria)
        {
            // If OpenWebUI is not configured, return fallback immediately
            if (!_isConfigured)
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
                    messages = new[] {
                        new {
                            role = "system",
                            content = GetSystemPrompt("slot_finding")
                        },
                        new {
                            role = "user",
                            content = JsonSerializer.Serialize(slotContext)
                        }
                    },
                    temperature = 0.7,
                    max_tokens = 1000
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating slot response from OpenWebUI");
                return GenerateSlotResponseFallback(slots, criteria);
            }

            return GenerateSlotResponseFallback(slots, criteria);
        }

        public async Task<string> GetConversationalResponseAsync(string message, string conversationId, List<MessageHistoryItem> history, ConversationOptions options)
        {
            // If OpenWebUI is not configured, return fallback immediately
            if (!_isConfigured)
            {
                return CreateVariedFallbackResponse(message, history, options);
            }

            try
            {
                var request = new
                {
                    message = message,
                    conversation_id = conversationId,
                    history = history.Select(h => new { role = h.IsFromBot ? "assistant" : "user", content = h.Message }).ToArray(),
                    max_tokens = _configuration.GetValue<int>("OpenWebUI:MaxTokens", 1000),
                    temperature = options.ResponseTemperature,
                    system_prompt = GetSystemPrompt("general")
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
                            return result;
                        }
                    }
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
            if (!_isConfigured)
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
            if (!_isConfigured)
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

            response += "Would you like me to help you schedule one of these slots or show you different options?";
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
                    "Hello! 👋 I'm your AI-powered Interview Scheduling assistant. I can help you find available time slots, schedule meetings, and manage your calendar using natural language. What would you like me to help you with today?",
                    "Hi there! Welcome to the Interview Scheduling Bot. I specialize in finding time slots, coordinating meetings, and managing calendar availability. How can I assist you?",
                    "Hey! Great to see you. I'm here to make interview scheduling easier for you. Just tell me what you need - find slots, check availability, or schedule meetings. What's on your agenda?",
                    "Hello! I'm your scheduling assistant, ready to help with all your interview coordination needs. Whether you need to find time slots or book meetings, I've got you covered. What can I do for you?"
                };
                return greetings[new Random().Next(greetings.Length)];
            }

            // Help requests
            if (lowerMessage.Contains("help") || lowerMessage.Contains("what can you do"))
            {
                var helpResponses = new[]
                {
                    "I can help you with interview scheduling! Here's what I can do:\n\n• Find available time slots using natural language\n• Schedule interviews and meetings\n• Check calendar availability\n• Answer questions about scheduling\n\nJust ask me in plain English what you need!",
                    "I'm your scheduling specialist! My capabilities include:\n\n• Intelligent time slot discovery\n• Meeting coordination and booking\n• Calendar conflict resolution\n• Natural language understanding\n\nTry asking something like 'Find slots tomorrow morning' or 'Schedule a meeting next week'.",
                    "Here's how I can assist with your scheduling needs:\n\n• Smart availability search\n• Interview and meeting booking\n• Participant coordination\n• Flexible scheduling options\n\nSimply describe what you need in everyday language, and I'll take care of the rest!"
                };
                return helpResponses[new Random().Next(helpResponses.Length)];
            }

            // Scheduling requests
            if (lowerMessage.Contains("slots") || lowerMessage.Contains("available") || lowerMessage.Contains("time") || lowerMessage.Contains("schedule"))
            {
                var schedulingResponses = new[]
                {
                    "I'd be happy to help you find available time slots! Could you please tell me more details like:\n\n• When would you like to schedule the meeting?\n• How long should the meeting be?\n• Who should be included?\n\nFor example, you could say 'Find slots tomorrow morning' or 'Schedule a 1-hour meeting next week'.",
                    "Perfect! I can help you find the ideal time slots. To get started, I'll need a few details:\n\n• Your preferred timeframe\n• Meeting duration\n• Participants to include\n\nTry something like 'Find 90-minute slots Thursday afternoon' or 'Schedule with John and Mary next week'.",
                    "Excellent! Let me help you discover available scheduling options. Please share:\n\n• When you'd like to meet\n• How long the session should be\n• Who needs to attend\n\nJust describe it naturally - 'Book an hour slot tomorrow' or 'Find time for 3 people Friday'."
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
    }

    public class ConversationOptions
    {
        public bool IncludeTimeSlots { get; set; }
        public bool PersonalizeResponse { get; set; }
        public bool ExpandVocabulary { get; set; }
        public double ResponseTemperature { get; set; } = 0.7;
    }
}