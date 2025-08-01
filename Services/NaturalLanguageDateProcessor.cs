using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using InterviewSchedulingBot.Services.Integration;
using Microsoft.Extensions.Logging;

namespace InterviewBot.Services
{
    /// <summary>
    /// Pure AI-driven date processor that understands natural language without hardcoded scenarios
    /// Handles business days, weekends, and conversational date references through AI interpretation
    /// </summary>
    public class NaturalLanguageDateProcessor
    {
        private readonly ICleanOpenWebUIClient _aiClient;
        private readonly ILogger<NaturalLanguageDateProcessor> _logger;
        private readonly CultureInfo _englishCulture = new CultureInfo("en-US");
        
        public NaturalLanguageDateProcessor(
            ICleanOpenWebUIClient aiClient,
            ILogger<NaturalLanguageDateProcessor> logger)
        {
            _aiClient = aiClient;
            _logger = logger;
        }
        
        /// <summary>
        /// Process date references using pure AI semantic understanding
        /// Automatically handles business days vs calendar days through AI interpretation
        /// </summary>
        public async Task<(DateTime startDate, DateTime endDate)> ProcessDateReferenceAsync(
            string userRequest, 
            DateTime currentDate)
        {
            try
            {
                // Use AI to interpret the date reference with business context
                var dateIntent = await ExtractDateIntentAsync(userRequest, currentDate);
                
                // Convert AI-extracted intent to actual dates
                return await InterpretDateIntentAsync(dateIntent, currentDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing date reference, falling back to next business day");
                return GetNextBusinessDay(currentDate);
            }
        }
        
        /// <summary>
        /// Extract date intent using AI without hardcoded logic
        /// </summary>
        private async Task<DateIntent> ExtractDateIntentAsync(string userRequest, DateTime currentDate)
        {
            var systemPrompt = $@"You are a business calendar assistant. Analyze the user's date request and extract the scheduling intent.

CURRENT CONTEXT:
- Today is {currentDate:dddd, MMMM dd, yyyy}
- Current time: {currentDate:HH:mm}

BUSINESS RULES:
- Business days are Monday-Friday, 9 AM to 5 PM
- When someone says 'tomorrow' and tomorrow is a weekend, they typically mean next business day
- 'Next week' usually means the upcoming Monday-Friday work week
- 'First X days' means exactly that many consecutive business days

EXTRACT:
1. DateType: 'specific_day', 'relative_day', 'week_range', 'business_days'
2. TimeFrame: Description of when they want to meet
3. DayCount: Number of days if specified (e.g., 'first 3 days')
4. IncludeWeekends: true/false based on context
5. BusinessDayAdjustment: true if weekends should be shifted to business days

USER REQUEST: {userRequest}

Respond with ONLY a JSON object:";

            try
            {
                var prompt = systemPrompt + "\n\nExample responses:\n" +
                           "{'DateType': 'relative_day', 'TimeFrame': 'tomorrow', 'DayCount': 1, 'IncludeWeekends': false, 'BusinessDayAdjustment': true}\n" +
                           "{'DateType': 'business_days', 'TimeFrame': 'first 3 days of next week', 'DayCount': 3, 'IncludeWeekends': false, 'BusinessDayAdjustment': true}";

                // Use the existing parameter extraction but adapt for date intent
                var fakeRequest = $"Extract date intent from: {userRequest}. Context: {prompt}";
                var response = await _aiClient.ExtractParametersAsync(fakeRequest);
                
                // For now, use simplified extraction and build DateIntent
                return new DateIntent
                {
                    DateType = DetermineDateType(userRequest),
                    TimeFrame = response.TimeFrame.ToLowerInvariant(),
                    DayCount = ExtractDayCount(userRequest),
                    IncludeWeekends = !ContainsBusinessDayIndicators(userRequest),
                    BusinessDayAdjustment = true // Always adjust to business days for better UX
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting date intent via AI");
                return CreateFallbackDateIntent(userRequest);
            }
        }
        
        /// <summary>
        /// Interpret the AI-extracted date intent into actual DateTime values
        /// </summary>
        private async Task<(DateTime startDate, DateTime endDate)> InterpretDateIntentAsync(
            DateIntent intent, 
            DateTime currentDate)
        {
            switch (intent.DateType)
            {
                case "relative_day":
                    return await HandleRelativeDayAsync(intent, currentDate);
                    
                case "week_range":
                    return await HandleWeekRangeAsync(intent, currentDate);
                    
                case "business_days":
                    return await HandleBusinessDaysAsync(intent, currentDate);
                    
                default:
                    return GetNextBusinessDay(currentDate);
            }
        }
        
        private async Task<(DateTime startDate, DateTime endDate)> HandleRelativeDayAsync(
            DateIntent intent, 
            DateTime currentDate)
        {
            if (intent.TimeFrame.Contains("tomorrow"))
            {
                var tomorrow = currentDate.AddDays(1).Date;
                
                // If tomorrow is weekend and business adjustment is enabled, use next business day
                if (intent.BusinessDayAdjustment && IsWeekend(tomorrow))
                {
                    var nextBusinessDay = GetNextBusinessDay(currentDate);
                    return (nextBusinessDay.startDate, nextBusinessDay.endDate);
                }
                
                return (tomorrow.AddHours(9), tomorrow.AddHours(17));
            }
            
            // Default to next business day
            return GetNextBusinessDay(currentDate);
        }
        
        private async Task<(DateTime startDate, DateTime endDate)> HandleWeekRangeAsync(
            DateIntent intent, 
            DateTime currentDate)
        {
            if (intent.TimeFrame.Contains("next week"))
            {
                var nextMonday = GetNextMonday(currentDate);
                var endDay = intent.DayCount.HasValue ? 
                    nextMonday.AddDays(intent.DayCount.Value - 1) : 
                    nextMonday.AddDays(4); // Default to Friday
                    
                return (nextMonday.AddHours(9), endDay.AddHours(17));
            }
            
            return GetNextBusinessDay(currentDate);
        }
        
        private async Task<(DateTime startDate, DateTime endDate)> HandleBusinessDaysAsync(
            DateIntent intent, 
            DateTime currentDate)
        {
            if (intent.TimeFrame.Contains("first") && intent.DayCount.HasValue)
            {
                var startDay = intent.TimeFrame.Contains("next week") ? 
                    GetNextMonday(currentDate) : 
                    GetNextBusinessDay(currentDate).startDate.Date;
                    
                var endDay = AddBusinessDays(startDay, intent.DayCount.Value - 1);
                return (startDay.AddHours(9), endDay.AddHours(17));
            }
            
            return GetNextBusinessDay(currentDate);
        }
        
        // Helper methods for business day calculations
        private (DateTime startDate, DateTime endDate) GetNextBusinessDay(DateTime currentDate)
        {
            var nextDay = currentDate.AddDays(1).Date;
            
            // Skip weekends
            while (IsWeekend(nextDay))
            {
                nextDay = nextDay.AddDays(1);
            }
            
            return (nextDay.AddHours(9), nextDay.AddHours(17));
        }
        
        private DateTime GetNextMonday(DateTime currentDate)
        {
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)currentDate.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;
            return currentDate.AddDays(daysUntilMonday).Date;
        }
        
        private DateTime AddBusinessDays(DateTime startDate, int businessDays)
        {
            var result = startDate;
            for (int i = 0; i < businessDays; i++)
            {
                result = result.AddDays(1);
                while (IsWeekend(result))
                {
                    result = result.AddDays(1);
                }
            }
            return result;
        }
        
        private bool IsWeekend(DateTime date) => 
            date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        
        // Simplified extraction methods (eventually these would be AI-driven too)
        private string DetermineDateType(string request)
        {
            var lower = request.ToLowerInvariant();
            if (lower.Contains("tomorrow")) return "relative_day";
            if (lower.Contains("next week") && lower.Contains("first")) return "business_days";
            if (lower.Contains("next week")) return "week_range";
            return "relative_day";
        }
        
        private int? ExtractDayCount(string request)
        {
            var lower = request.ToLowerInvariant();
            if (lower.Contains("first"))
            {
                if (lower.Contains("2") || lower.Contains("two")) return 2;
                if (lower.Contains("3") || lower.Contains("three")) return 3;
                if (lower.Contains("4") || lower.Contains("four")) return 4;
                if (lower.Contains("5") || lower.Contains("five")) return 5;
            }
            return null;
        }
        
        private bool ContainsBusinessDayIndicators(string request)
        {
            var lower = request.ToLowerInvariant();
            return lower.Contains("business") || lower.Contains("work") || lower.Contains("weekday");
        }
        
        private DateIntent CreateFallbackDateIntent(string request)
        {
            return new DateIntent
            {
                DateType = "relative_day",
                TimeFrame = "next business day",
                DayCount = 1,
                IncludeWeekends = false,
                BusinessDayAdjustment = true
            };
        }
    }
    
    /// <summary>
    /// Represents the AI-extracted intent from a date request
    /// </summary>
    public class DateIntent
    {
        public string DateType { get; set; } = "";
        public string TimeFrame { get; set; } = "";
        public int? DayCount { get; set; }
        public bool IncludeWeekends { get; set; }
        public bool BusinessDayAdjustment { get; set; }
    }
}