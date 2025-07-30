using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using InterviewBot.Domain.Entities;
using InterviewSchedulingBot.Services.Integration;
using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces.Business;

namespace InterviewSchedulingBot.Services.Business
{
    public class AIResponseRequest
    {
        public string ResponseType { get; set; } = string.Empty;
        public string UserQuery { get; set; } = string.Empty;
        public object Context { get; set; } = new object();
        public string ConversationHistory { get; set; } = string.Empty;
        public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
    }

    public class ParsedQueryResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public SlotQueryCriteria? Criteria { get; set; }
        public string IntentDetected { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
    }

    public class ConversationContext
    {
        public List<string> PreviousMessages { get; set; } = new List<string>();
        public Dictionary<string, object> SessionData { get; set; } = new Dictionary<string, object>();
        public string CurrentIntent { get; set; } = string.Empty;
        public List<string> ParticipantIds { get; set; } = new List<string>();
    }

    public class AvailableSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Score { get; set; }
        public int AvailableParticipants { get; set; }
        public int TotalParticipants { get; set; }
        public List<string> ParticipantEmails { get; set; } = new List<string>();
    }

    public interface IAIResponseService
    {
        Task<string> GenerateResponseAsync(AIResponseRequest request, CancellationToken cancellationToken = default);
        Task<ParsedQueryResult> ParseUserQueryAsync(string query, ConversationContext context, CancellationToken cancellationToken = default);
        Task<string> GenerateSlotSuggestionsAsync(List<AvailableSlot> slots, SlotQueryCriteria criteria, CancellationToken cancellationToken = default);
        Task<string> GenerateConflictExplanationAsync(List<string> participants, Dictionary<string, List<InterviewBot.Domain.Entities.TimeSlot>> availability, SlotQueryCriteria criteria, CancellationToken cancellationToken = default);
        Task<string> GenerateWelcomeMessageAsync(string userName, CancellationToken cancellationToken = default);
        Task<string> GenerateErrorMessageAsync(string errorType, string contextInfo, CancellationToken cancellationToken = default);
        Task<string> GenerateFollowUpQuestionAsync(string currentContext, List<string> suggestedActions, CancellationToken cancellationToken = default);
        Task<string> GenerateHelpMessageAsync(string currentIntent, CancellationToken cancellationToken = default);
        Task<string> GenerateConfirmationMessageAsync(string actionType, object actionDetails, CancellationToken cancellationToken = default);
    }

    public class AIResponseService : IAIResponseService
    {
        private readonly IOpenWebUIClient _openWebUIClient;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIResponseService> _logger;

        public AIResponseService(
            IOpenWebUIClient openWebUIClient,
            IMemoryCache memoryCache,
            IConfiguration configuration,
            ILogger<AIResponseService> logger)
        {
            _openWebUIClient = openWebUIClient;
            _memoryCache = memoryCache;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GenerateResponseAsync(AIResponseRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Generating AI response for type: {ResponseType}", request.ResponseType);

                // Check cache first
                var cacheKey = $"ai_response_{request.ResponseType}_{request.UserQuery?.GetHashCode()}";
                if (_memoryCache.TryGetValue(cacheKey, out string? cachedResponse) && !string.IsNullOrEmpty(cachedResponse))
                {
                    _logger.LogDebug("Returning cached response for: {ResponseType}", request.ResponseType);
                    return cachedResponse;
                }

                // Build the prompt based on response type
                var prompt = BuildPromptForResponseType(request.ResponseType, request.Context, request.UserQuery, request.AdditionalData);
                
                // Generate response using OpenWebUI
                var response = await _openWebUIClient.GenerateResponseAsync(prompt, request.Context, cancellationToken);

                // If OpenWebUI fails, use intelligent fallback
                if (string.IsNullOrWhiteSpace(response) || response.Contains("fallback"))
                {
                    response = GenerateIntelligentFallback(request);
                }

                // Cache the response (expire after 10 minutes)
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    SlidingExpiration = TimeSpan.FromMinutes(5)
                };
                _memoryCache.Set(cacheKey, response, cacheOptions);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI response for type: {ResponseType}", request.ResponseType);
                return GenerateIntelligentFallback(request);
            }
        }

        public async Task<ParsedQueryResult> ParseUserQueryAsync(string query, ConversationContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Parsing user query with AI: {Query}", query);

                var parseRequest = new AIResponseRequest
                {
                    ResponseType = "query_parsing",
                    UserQuery = query,
                    Context = context,
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["conversation_history"] = string.Join("\n", context.PreviousMessages.TakeLast(5)),
                        ["current_intent"] = context.CurrentIntent,
                        ["session_data"] = context.SessionData
                    }
                };

                // Use OpenWebUI to parse the query intelligently
                var parseResponse = await _openWebUIClient.ProcessQueryAsync(query, OpenWebUIRequestType.SlotQuery, cancellationToken);

                if (parseResponse.Success)
                {
                    var criteria = new SlotQueryCriteria
                    {
                        StartDate = parseResponse.DateRange?.Start ?? DateTime.Today,
                        EndDate = parseResponse.DateRange?.End ?? DateTime.Today.AddDays(7),
                        DurationMinutes = parseResponse.Duration ?? 60,
                        ParticipantEmails = parseResponse.Participants,
                        SpecificDay = parseResponse.SpecificDay,
                        RelativeDay = parseResponse.RelativeDay,
                        TimeOfDay = !string.IsNullOrEmpty(parseResponse.TimeOfDay) ? CreateTimeOfDayRange(parseResponse.TimeOfDay) : null,
                        MinRequiredParticipants = parseResponse.MinRequiredParticipants ?? 0
                    };

                    return new ParsedQueryResult
                    {
                        Success = true,
                        Criteria = criteria,
                        IntentDetected = "find_slots",
                        ConfidenceScore = 0.9
                    };
                }

                // Fallback parsing if OpenWebUI fails
                return PerformFallbackParsing(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing query: {Query}", query);
                return PerformFallbackParsing(query);
            }
        }

        public async Task<string> GenerateSlotSuggestionsAsync(List<AvailableSlot> slots, SlotQueryCriteria criteria, CancellationToken cancellationToken = default)
        {
            var request = new AIResponseRequest
            {
                ResponseType = "slot_suggestions",
                Context = new
                {
                    Slots = slots,
                    Criteria = criteria,
                    SlotCount = slots.Count,
                    BestSlot = slots.OrderByDescending(s => s.Score).FirstOrDefault()
                }
            };

            return await GenerateResponseAsync(request, cancellationToken);
        }

        public async Task<string> GenerateConflictExplanationAsync(List<string> participants, Dictionary<string, List<InterviewBot.Domain.Entities.TimeSlot>> availability, SlotQueryCriteria criteria, CancellationToken cancellationToken = default)
        {
            var request = new AIResponseRequest
            {
                ResponseType = "conflict_explanation",
                Context = new
                {
                    Participants = participants,
                    Availability = availability,
                    Criteria = criteria,
                    ParticipantCount = participants.Count
                }
            };

            return await GenerateResponseAsync(request, cancellationToken);
        }

        public async Task<string> GenerateWelcomeMessageAsync(string userName, CancellationToken cancellationToken = default)
        {
            var request = new AIResponseRequest
            {
                ResponseType = "welcome_message",
                Context = new { UserName = userName }
            };

            return await GenerateResponseAsync(request, cancellationToken);
        }

        public async Task<string> GenerateErrorMessageAsync(string errorType, string contextInfo, CancellationToken cancellationToken = default)
        {
            var request = new AIResponseRequest
            {
                ResponseType = "error_message",
                Context = new
                {
                    ErrorType = errorType,
                    ContextInfo = contextInfo
                }
            };

            return await GenerateResponseAsync(request, cancellationToken);
        }

        public async Task<string> GenerateFollowUpQuestionAsync(string currentContext, List<string> suggestedActions, CancellationToken cancellationToken = default)
        {
            var request = new AIResponseRequest
            {
                ResponseType = "follow_up_question",
                Context = new
                {
                    CurrentContext = currentContext,
                    SuggestedActions = suggestedActions
                }
            };

            return await GenerateResponseAsync(request, cancellationToken);
        }

        public async Task<string> GenerateHelpMessageAsync(string currentIntent, CancellationToken cancellationToken = default)
        {
            var request = new AIResponseRequest
            {
                ResponseType = "help_message",
                Context = new { CurrentIntent = currentIntent }
            };

            return await GenerateResponseAsync(request, cancellationToken);
        }

        public async Task<string> GenerateConfirmationMessageAsync(string actionType, object actionDetails, CancellationToken cancellationToken = default)
        {
            var request = new AIResponseRequest
            {
                ResponseType = "confirmation_message",
                Context = new
                {
                    ActionType = actionType,
                    ActionDetails = actionDetails
                }
            };

            return await GenerateResponseAsync(request, cancellationToken);
        }

        private string BuildPromptForResponseType(string responseType, object context, string userQuery, Dictionary<string, object> additionalData)
        {
            var contextJson = JsonSerializer.Serialize(context, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            
            // Build dynamic prompt based on response type and context
            var prompt = responseType switch
            {
                "slot_suggestions" => 
                    $"You are an AI assistant helping with interview scheduling. Generate a conversational response about these available time slots. " +
                    $"Context: {contextJson}. " +
                    $"Be helpful, highlight the best recommendations with reasoning, group by day/time, explain scores and participant counts. " +
                    $"Use a professional but friendly tone and end with a question about next steps.",
                
                "conflict_explanation" => 
                    $"You are an AI assistant explaining scheduling conflicts. " +
                    $"Context: {contextJson}. " +
                    $"Explain why no suitable slots were found, identify conflict patterns, suggest alternatives. " +
                    $"Be empathetic but practical with actionable next steps.",
                
                "welcome_message" => 
                    $"You are an AI assistant for an interview scheduling bot. Generate a warm, professional welcome message. " +
                    $"Context: {contextJson}. " +
                    $"Greet professionally, explain what you can help with, provide 2-3 example commands, " +
                    $"set expectations for natural language interaction.",
                
                "error_message" => 
                    $"You are an AI assistant handling an error gracefully. " +
                    $"Context: {contextJson}. " +
                    $"Acknowledge the problem without technical jargon, provide helpful guidance, " +
                    $"maintain supportive tone, offer specific alternatives, keep user engaged.",
                
                "follow_up_question" => 
                    $"You are an AI assistant generating a natural follow-up question. " +
                    $"Context: {contextJson}. " +
                    $"Flow naturally from conversation, present options in easy-to-choose format, " +
                    $"use conversational language, encourage continued interaction.",
                
                "help_message" => 
                    $"You are an AI assistant providing context-aware help. " +
                    $"Context: {contextJson}. " +
                    $"Tailor to their current situation, provide relevant examples, explain features accessibly, " +
                    $"include specific and general help options, encourage natural language experimentation.",
                
                "confirmation_message" => 
                    $"You are an AI assistant generating a confirmation message. " +
                    $"Context: {contextJson}. " +
                    $"Clearly state what action will be taken, include relevant details to verify, " +
                    $"use professional but friendly tone, provide easy confirmation/modification options.",
                
                "general_response" => 
                    $"You are a helpful AI assistant for interview scheduling. The user said: '{userQuery}'. " +
                    $"Context: {contextJson}. " +
                    $"Respond naturally and conversationally. If they're asking about scheduling, offer to help. " +
                    $"If it's general conversation, be friendly but guide toward scheduling topics. " +
                    $"Keep responses concise, helpful, and encouraging.",
                
                _ => $"You are a helpful AI assistant for interview scheduling. Generate an appropriate, conversational response for: {contextJson}"
            };

            // Add user query if available
            if (!string.IsNullOrEmpty(userQuery))
            {
                prompt += $" User query: '{userQuery}'";
            }

            return prompt;
        }

        private string GenerateIntelligentFallback(AIResponseRequest request)
        {
            // Generate contextually appropriate fallbacks based on response type
            return request.ResponseType switch
            {
                "greeting_message" => "Hi there! 👋 Welcome to the Interview Scheduling Bot! I'm here to help you find and schedule interview slots using natural language. You can ask me things like 'Find slots on Thursday afternoon' or 'Are there any slots next Monday?' How can I help you today?",
                "slot_suggestions" => "I found some scheduling options for you. Let me know if you'd like to see more details or try different criteria.",
                "conflict_explanation" => "I couldn't find suitable slots that work for everyone. Would you like to try different dates or times?",
                "welcome_message" => "Welcome to the Interview Scheduling Bot! 👋 I can help you find interview slots using natural language. Try asking me something like 'Find slots on Thursday afternoon'",
                "error_message" => GetContextualErrorMessage(request),
                "follow_up_question" => "What would you like me to help you with next?",
                "help_message" => GetContextualHelpMessage(request),
                "confirmation_message" => "I'm ready to proceed with your request. Please confirm if this looks correct.",
                "general_response" => GetGeneralAIResponse(request),
                _ => GetGeneralAIResponse(request)
            };
        }

        private string GetContextualErrorMessage(AIResponseRequest request)
        {
            var contextInfo = request.Context?.ToString() ?? "";
            if (contextInfo.Contains("unknown_intent"))
            {
                return "I'm not sure I understand what you're asking for. Could you try rephrasing? For example, you could say 'Find slots tomorrow morning' or 'Schedule an interview next week'.";
            }
            return "I encountered an issue processing your request. Please try again or rephrase your query.";
        }

        private string GetContextualHelpMessage(AIResponseRequest request)
        {
            var contextJson = JsonSerializer.Serialize(request.Context);
            if (contextJson.Contains("general_help"))
            {
                return "I can help you with interview scheduling! Here's what I can do:\n\n• Find available time slots (\"Find slots Thursday afternoon\")\n• Schedule interviews (\"Schedule an interview\")\n• Show upcoming interviews (\"Show my interviews\")\n• Natural language queries (\"Are there any morning slots next week?\")\n\nJust ask me in plain English what you need help with!";
            }
            return "I can help you find and schedule interview slots. Try natural language queries like 'Find slots tomorrow morning' or 'Schedule an interview for next week'.";
        }

        private string GetGeneralAIResponse(AIResponseRequest request)
        {
            var userQuery = request.UserQuery?.ToLowerInvariant() ?? "";
            
            // Check if this is a greeting
            if (IsGreeting(userQuery))
            {
                return "Hello! 👋 I'm your AI-powered Interview Scheduling assistant. I can help you find available interview slots, schedule meetings, and manage your calendar using natural language. What would you like me to help you with today?";
            }
            
            // Check if this contains time/scheduling keywords
            if (ContainsSchedulingKeywords(userQuery))
            {
                return "I can help you with that scheduling request! Let me know more details about what you're looking for, such as the preferred day, time, or duration.";
            }
            
            return "I'm here to help with your scheduling needs! You can ask me to find interview slots, schedule meetings, or check availability using natural language. How can I assist you today?";
        }

        private bool IsGreeting(string message)
        {
            var greetingKeywords = new[] { "hi", "hello", "hey", "good morning", "good afternoon", "good evening", "start", "greetings" };
            return greetingKeywords.Any(keyword => message.Contains(keyword));
        }

        private bool ContainsSchedulingKeywords(string message)
        {
            var schedulingKeywords = new[] { "schedule", "slot", "time", "meeting", "interview", "available", "book", "find", "when", "calendar" };
            return schedulingKeywords.Any(keyword => message.Contains(keyword));
        }

        private ParsedQueryResult PerformFallbackParsing(string query)
        {
            var lowerQuery = query.ToLowerInvariant();
            var criteria = new SlotQueryCriteria
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                DurationMinutes = 60,
                ParticipantEmails = new List<string>()
            };

            // Extract basic information using keyword matching
            if (lowerQuery.Contains("morning"))
                criteria.TimeOfDay = new TimeOfDayRange { Start = TimeSpan.FromHours(9), End = TimeSpan.FromHours(12) };
            else if (lowerQuery.Contains("afternoon"))
                criteria.TimeOfDay = new TimeOfDayRange { Start = TimeSpan.FromHours(13), End = TimeSpan.FromHours(17) };
            else if (lowerQuery.Contains("evening"))
                criteria.TimeOfDay = new TimeOfDayRange { Start = TimeSpan.FromHours(17), End = TimeSpan.FromHours(20) };

            // Detect specific days
            if (lowerQuery.Contains("monday")) criteria.SpecificDay = "Monday";
            else if (lowerQuery.Contains("tuesday")) criteria.SpecificDay = "Tuesday";
            else if (lowerQuery.Contains("wednesday")) criteria.SpecificDay = "Wednesday";
            else if (lowerQuery.Contains("thursday")) criteria.SpecificDay = "Thursday";
            else if (lowerQuery.Contains("friday")) criteria.SpecificDay = "Friday";

            // Detect relative days
            if (lowerQuery.Contains("tomorrow")) criteria.RelativeDay = "tomorrow";
            else if (lowerQuery.Contains("next week")) criteria.RelativeDay = "next week";

            // Extract duration
            if (lowerQuery.Contains("30 min")) criteria.DurationMinutes = 30;
            else if (lowerQuery.Contains("90 min")) criteria.DurationMinutes = 90;

            return new ParsedQueryResult
            {
                Success = true,
                Criteria = criteria,
                IntentDetected = "find_slots",
                ConfidenceScore = 0.7
            };
        }

        private TimeOfDayRange? CreateTimeOfDayRange(string timeOfDay)
        {
            return timeOfDay.ToLowerInvariant() switch
            {
                "morning" => new TimeOfDayRange { Start = TimeSpan.FromHours(9), End = TimeSpan.FromHours(12) },
                "afternoon" => new TimeOfDayRange { Start = TimeSpan.FromHours(13), End = TimeSpan.FromHours(17) },
                "evening" => new TimeOfDayRange { Start = TimeSpan.FromHours(17), End = TimeSpan.FromHours(20) },
                _ => null
            };
        }
    }
}