using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InterviewSchedulingBot.Services.Integration;

namespace InterviewBot.Services
{
    /// <summary>
    /// Pure AI-driven date interpreter that handles natural language date references
    /// with intelligent business day adjustments and fallback logic
    /// </summary>
    public class AIDateInterpreter
    {
        private readonly IOpenWebUIIntegration _aiService;
        private readonly ILogger<AIDateInterpreter> _logger;
        
        public AIDateInterpreter(IOpenWebUIIntegration aiService, ILogger<AIDateInterpreter> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }
        
        public async Task<DateInterpretationResult> InterpretDateReferenceAsync(
            string userQuery, 
            DateTime currentDate)
        {
            try
            {
                _logger.LogInformation("Interpreting date reference: '{Query}' with current date: {CurrentDate:yyyy-MM-dd dddd}", 
                    userQuery, currentDate);

                // Create a specialized system prompt for date extraction
                string systemPrompt = $@"You are a business calendar date interpreter. Parse the user's natural language date request into specific dates.

CURRENT CONTEXT:
- Today: {currentDate:yyyy-MM-dd} ({currentDate:dddd})
- Business hours: Monday-Friday, 9 AM to 5 PM

BUSINESS RULES:
1. ""tomorrow"" = next business day if tomorrow is weekend, otherwise literal tomorrow
2. ""next week"" = Monday through Friday of the upcoming week
3. ""first X days of next week"" = exactly X consecutive business days starting from next Monday
4. Always prefer business days unless explicitly asked for weekends

USER REQUEST: ""{userQuery}""

Respond with ONLY valid JSON:
{{
  ""startDate"": ""yyyy-MM-dd"",
  ""endDate"": ""yyyy-MM-dd"",
  ""explanation"": ""brief explanation if weekend was adjusted"",
  ""wasAdjusted"": true/false
}}

Examples:
- ""tomorrow"" when today is Friday → {{""startDate"": ""2025-08-04"", ""endDate"": ""2025-08-04"", ""explanation"": """", ""wasAdjusted"": false}}
- ""tomorrow"" when today is Friday and tomorrow is Saturday → {{""startDate"": ""2025-08-04"", ""endDate"": ""2025-08-04"", ""explanation"": ""Adjusted to Monday as tomorrow is Saturday"", ""wasAdjusted"": true}}
- ""first 2 days of next week"" → {{""startDate"": ""2025-08-04"", ""endDate"": ""2025-08-05"", ""explanation"": """", ""wasAdjusted"": false}}";

                // Try AI interpretation first
                var aiResult = await TryAIInterpretationAsync(systemPrompt, userQuery);
                if (aiResult != null)
                {
                    _logger.LogInformation("AI interpretation successful: {StartDate} to {EndDate}", 
                        aiResult.StartDate, aiResult.EndDate);
                    return aiResult;
                }

                // Fallback to intelligent semantic parsing
                var fallbackResult = CreateIntelligentFallback(userQuery, currentDate);
                _logger.LogInformation("Using intelligent fallback: {StartDate} to {EndDate}", 
                    fallbackResult.StartDate, fallbackResult.EndDate);
                return fallbackResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error interpreting date reference");
                return CreateIntelligentFallback(userQuery, currentDate);
            }
        }

        private async Task<DateInterpretationResult?> TryAIInterpretationAsync(string systemPrompt, string userQuery)
        {
            try
            {
                // Use the OpenWebUI integration to get AI interpretation
                var response = await _aiService.GenerateConversationalResponseAsync(systemPrompt, new { query = userQuery });
                
                if (string.IsNullOrEmpty(response))
                    return null;

                // Try to extract JSON from the response
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var result = JsonSerializer.Deserialize<DateInterpretationJson>(jsonStr);
                    
                    if (DateTime.TryParse(result?.StartDate, out var startDate) && 
                        DateTime.TryParse(result?.EndDate, out var endDate))
                    {
                        return new DateInterpretationResult
                        {
                            StartDate = startDate,
                            EndDate = endDate,
                            Explanation = result.Explanation ?? "",
                            WasAdjusted = result.WasAdjusted
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI interpretation failed, will use fallback");
            }
            
            return null;
        }

        private DateInterpretationResult CreateIntelligentFallback(string userQuery, DateTime currentDate)
        {
            var query = userQuery.ToLowerInvariant();
            
            // Handle "tomorrow" with business day intelligence
            if (query.Contains("tomorrow"))
            {
                var tomorrow = currentDate.AddDays(1);
                bool wasAdjusted = false;
                string explanation = "";
                
                // If tomorrow is weekend, adjust to next Monday
                if (IsWeekend(tomorrow))
                {
                    tomorrow = GetNextMonday(currentDate);
                    wasAdjusted = true;
                    explanation = $"Adjusted to Monday as tomorrow is {currentDate.AddDays(1):dddd}";
                }
                
                return new DateInterpretationResult
                {
                    StartDate = tomorrow,
                    EndDate = tomorrow,
                    Explanation = explanation,
                    WasAdjusted = wasAdjusted
                };
            }
            
            // Handle "first X days of next week"
            if (query.Contains("first") && query.Contains("next week"))
            {
                var dayCount = ExtractDayCount(query);
                var nextMonday = GetNextMonday(currentDate);
                var endDate = nextMonday.AddDays(Math.Max(0, dayCount - 1));
                
                return new DateInterpretationResult
                {
                    StartDate = nextMonday,
                    EndDate = endDate,
                    Explanation = "",
                    WasAdjusted = false
                };
            }
            
            // Handle "next week"
            if (query.Contains("next week"))
            {
                var nextMonday = GetNextMonday(currentDate);
                var nextFriday = nextMonday.AddDays(4);
                
                return new DateInterpretationResult
                {
                    StartDate = nextMonday,
                    EndDate = nextFriday,
                    Explanation = "",
                    WasAdjusted = false
                };
            }
            
            // Handle specific days (Monday, Tuesday, etc.)
            var specificDay = ExtractSpecificDay(query);
            if (specificDay.HasValue)
            {
                var targetDate = GetNextWeekday(currentDate, specificDay.Value);
                return new DateInterpretationResult
                {
                    StartDate = targetDate,
                    EndDate = targetDate,
                    Explanation = "",
                    WasAdjusted = false
                };
            }
            
            // Default: next business day
            var nextBusinessDay = GetNextBusinessDay(currentDate);
            bool defaultWasAdjusted = IsWeekend(currentDate.AddDays(1));
            
            return new DateInterpretationResult
            {
                StartDate = nextBusinessDay,
                EndDate = nextBusinessDay,
                Explanation = defaultWasAdjusted ? "Defaulted to next business day" : "",
                WasAdjusted = defaultWasAdjusted
            };
        }

        // Helper methods for date calculations
        private bool IsWeekend(DateTime date) => 
            date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

        private DateTime GetNextBusinessDay(DateTime currentDate)
        {
            var nextDay = currentDate.AddDays(1);
            while (IsWeekend(nextDay))
            {
                nextDay = nextDay.AddDays(1);
            }
            return nextDay;
        }

        private DateTime GetNextMonday(DateTime currentDate)
        {
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)currentDate.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7; // If today is Monday, get next Monday
            return currentDate.AddDays(daysUntilMonday);
        }

        private DateTime GetNextWeekday(DateTime currentDate, DayOfWeek targetDay)
        {
            var daysUntilTarget = ((int)targetDay - (int)currentDate.DayOfWeek + 7) % 7;
            if (daysUntilTarget == 0) daysUntilTarget = 7; // If today is the target day, get next week
            return currentDate.AddDays(daysUntilTarget);
        }

        private int ExtractDayCount(string query)
        {
            if (query.Contains("2") || query.Contains("two")) return 2;
            if (query.Contains("3") || query.Contains("three")) return 3;
            if (query.Contains("4") || query.Contains("four")) return 4;
            if (query.Contains("5") || query.Contains("five")) return 5;
            return 1; // Default
        }

        private DayOfWeek? ExtractSpecificDay(string query)
        {
            if (query.Contains("monday")) return DayOfWeek.Monday;
            if (query.Contains("tuesday")) return DayOfWeek.Tuesday;
            if (query.Contains("wednesday")) return DayOfWeek.Wednesday;
            if (query.Contains("thursday")) return DayOfWeek.Thursday;
            if (query.Contains("friday")) return DayOfWeek.Friday;
            if (query.Contains("saturday")) return DayOfWeek.Saturday;
            if (query.Contains("sunday")) return DayOfWeek.Sunday;
            return null;
        }
    }

    public class DateInterpretationResult
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Explanation { get; set; } = "";
        public bool WasAdjusted { get; set; }
    }

    // Internal class for JSON deserialization
    internal class DateInterpretationJson
    {
        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; }
        
        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }
        
        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }
        
        [JsonPropertyName("wasAdjusted")]
        public bool WasAdjusted { get; set; }
    }
}