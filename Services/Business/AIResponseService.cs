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
        Task<string> GenerateConflictExplanationAsync(List<string> participants, Dictionary<string, List<TimeSlot>> availability, SlotQueryCriteria criteria, CancellationToken cancellationToken = default);
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
        
        private static readonly Dictionary<string, string> ResponseTemplates = new()
        {
            ["slot_suggestions"] = @"You are an AI assistant helping with interview scheduling. Generate a conversational, friendly response about available time slots.

Context: {context}
Available slots: {slots}
Query criteria: {criteria}

Generate a response that:
- Is conversational and helpful
- Highlights the best recommendations with reasoning
- Groups slots by day and time period
- Explains availability scores and participant counts
- Includes relevant emojis for visual appeal
- Ends with a question about next steps

Keep the tone professional but friendly, and make it easy to understand.",

            ["conflict_explanation"] = @"You are an AI assistant explaining scheduling conflicts in a helpful way.

Context: {context}
Participants: {participants}
Availability data: {availability}
Query criteria: {criteria}

Generate a response that:
- Explains why no suitable slots were found
- Identifies specific conflict patterns
- Suggests alternative times or approaches
- Maintains a helpful, solution-oriented tone
- Includes actionable next steps

Be empathetic but practical in your suggestions.",

            ["welcome_message"] = @"You are an AI assistant for an interview scheduling bot. Generate a warm, professional welcome message.

User: {userName}
Context: New conversation start

Create a welcome message that:
- Greets the user professionally
- Briefly explains what you can help with
- Provides 2-3 example commands they can try
- Sets expectations for natural language interaction
- Uses a friendly, helpful tone",

            ["error_message"] = @"You are an AI assistant handling an error situation gracefully.

Error type: {errorType}
Context: {contextInfo}

Generate an error message that:
- Acknowledges the problem without technical jargon
- Provides helpful guidance on what to try next
- Maintains a supportive tone
- Offers specific alternative actions when possible
- Keeps the user engaged rather than frustrated",

            ["follow_up_question"] = @"You are an AI assistant generating a natural follow-up question.

Current context: {currentContext}
Suggested actions: {suggestedActions}

Generate a follow-up question that:
- Flows naturally from the current conversation
- Presents options in an easy-to-choose format
- Uses conversational language
- Encourages the user to continue the interaction
- Provides clear guidance on available next steps",

            ["help_message"] = @"You are an AI assistant providing context-aware help.

Current intent: {currentIntent}
Context: User needs assistance

Generate a help message that:
- Is tailored to their current situation
- Provides relevant examples and commands
- Explains features in an accessible way
- Includes both specific and general help options
- Encourages experimentation with natural language",

            ["confirmation_message"] = @"You are an AI assistant generating a confirmation message.

Action type: {actionType}
Action details: {actionDetails}

Generate a confirmation that:
- Clearly states what action will be taken
- Includes relevant details the user should verify
- Uses a professional but friendly tone
- Provides an easy way to confirm or modify
- Builds confidence in the proposed action"
        };

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

        public async Task<string> GenerateConflictExplanationAsync(List<string> participants, Dictionary<string, List<TimeSlot>> availability, SlotQueryCriteria criteria, CancellationToken cancellationToken = default)
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
            if (!ResponseTemplates.TryGetValue(responseType, out var template))
            {
                template = "Generate a helpful, conversational response for: {context}";
            }

            var contextJson = JsonSerializer.Serialize(context, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            
            return template
                .Replace("{context}", contextJson)
                .Replace("{userQuery}", userQuery ?? "")
                .Replace("{slots}", additionalData.ContainsKey("slots") ? JsonSerializer.Serialize(additionalData["slots"]) : "[]")
                .Replace("{criteria}", additionalData.ContainsKey("criteria") ? JsonSerializer.Serialize(additionalData["criteria"]) : "{}")
                .Replace("{participants}", additionalData.ContainsKey("participants") ? JsonSerializer.Serialize(additionalData["participants"]) : "[]")
                .Replace("{availability}", additionalData.ContainsKey("availability") ? JsonSerializer.Serialize(additionalData["availability"]) : "{}")
                .Replace("{userName}", context?.GetType().GetProperty("UserName")?.GetValue(context)?.ToString() ?? "")
                .Replace("{errorType}", context?.GetType().GetProperty("ErrorType")?.GetValue(context)?.ToString() ?? "")
                .Replace("{contextInfo}", context?.GetType().GetProperty("ContextInfo")?.GetValue(context)?.ToString() ?? "")
                .Replace("{currentContext}", context?.GetType().GetProperty("CurrentContext")?.GetValue(context)?.ToString() ?? "")
                .Replace("{suggestedActions}", context?.GetType().GetProperty("SuggestedActions")?.GetValue(context)?.ToString() ?? "")
                .Replace("{currentIntent}", context?.GetType().GetProperty("CurrentIntent")?.GetValue(context)?.ToString() ?? "")
                .Replace("{actionType}", context?.GetType().GetProperty("ActionType")?.GetValue(context)?.ToString() ?? "")
                .Replace("{actionDetails}", context?.GetType().GetProperty("ActionDetails")?.GetValue(context)?.ToString() ?? "");
        }

        private string GenerateIntelligentFallback(AIResponseRequest request)
        {
            // Generate contextually appropriate fallbacks based on response type
            return request.ResponseType switch
            {
                "slot_suggestions" => "I found some scheduling options for you. Let me know if you'd like to see more details or try different criteria.",
                "conflict_explanation" => "I couldn't find suitable slots that work for everyone. Would you like to try different dates or times?",
                "welcome_message" => "Welcome to the Interview Scheduling Bot! 👋 I can help you find interview slots using natural language. Try asking me something like 'Find slots on Thursday afternoon'",
                "error_message" => "I encountered an issue processing your request. Please try again or rephrase your query.",
                "follow_up_question" => "What would you like me to help you with next?",
                "help_message" => "I can help you find and schedule interview slots. Try natural language queries like 'Find slots tomorrow morning' or 'Schedule an interview for next week'.",
                "confirmation_message" => "I'm ready to proceed with your request. Please confirm if this looks correct.",
                _ => "I'm here to help with your scheduling needs. How can I assist you today?"
            };
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