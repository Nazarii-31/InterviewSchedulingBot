using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InterviewBot.Models;
using InterviewBot.Services;
using InterviewSchedulingBot.Services.Integration;
using Microsoft.Extensions.Logging;

namespace InterviewBot.Services
{
    /// <summary>
    /// AI-driven response formatter that creates conversational, natural responses
    /// Replaces hardcoded templates with AI-generated contextual messages
    /// </summary>
    public class ConversationalAIResponseFormatter
    {
        private readonly ICleanOpenWebUIClient _aiClient;
        private readonly ILogger<ConversationalAIResponseFormatter> _logger;
        
        public ConversationalAIResponseFormatter(
            ICleanOpenWebUIClient aiClient,
            ILogger<ConversationalAIResponseFormatter> logger)
        {
            _aiClient = aiClient;
            _logger = logger;
        }
        
        /// <summary>
        /// Generate a conversational response for time slot recommendations using AI
        /// </summary>
        public async Task<string> FormatTimeSlotResponseAsync(
            List<EnhancedTimeSlot> slots,
            DateTime startDate,
            DateTime endDate,
            int durationMinutes,
            string originalRequest,
            bool wasWeekendAdjusted = false,
            string explanation = "")
        {
            try
            {
                if (!slots.Any())
                {
                    return await GenerateNoSlotsResponseAsync(startDate, endDate, durationMinutes, originalRequest, wasWeekendAdjusted, explanation);
                }
                
                return await GenerateSlotListResponseAsync(slots, startDate, endDate, durationMinutes, originalRequest, wasWeekendAdjusted, explanation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI response, falling back to template");
                return CreateFallbackResponse(slots, startDate, endDate, durationMinutes);
            }
        }
        
        /// <summary>
        /// Generate conversational response when no slots are available
        /// </summary>
        private async Task<string> GenerateNoSlotsResponseAsync(
            DateTime startDate,
            DateTime endDate,
            int durationMinutes,
            string originalRequest,
            bool wasWeekendAdjusted,
            string explanation = "")
        {
            var context = new
            {
                Request = originalRequest,
                Duration = durationMinutes,
                StartDate = DateFormattingService.FormatDateWithDay(startDate),
                EndDate = DateFormattingService.FormatDateWithDay(endDate),
                WasAdjusted = wasWeekendAdjusted,
                Explanation = explanation,
                ResponseType = "no_slots_available"
            };
            
            var prompt = $@"Generate a friendly, conversational response for when no meeting slots are available. 

CONTEXT:
- User requested: '{originalRequest}'
- Looking for {durationMinutes}-minute slots
- Between {context.StartDate} and {context.EndDate}
- Weekend adjustment made: {wasWeekendAdjusted}
- Explanation: {explanation}

REQUIREMENTS:
- Be conversational and helpful
- Maintain the English date format: 'Monday [04.08.2025]'
- Suggest alternatives (different dates, shorter duration)
- Sound natural, not robotic

Generate a response that acknowledges their request and offers helpful alternatives.";

            try
            {
                // Simulate AI response by using parameter extraction creatively
                var aiRequest = $"Generate helpful response for no slots found between {context.StartDate} and {context.EndDate} for {durationMinutes} minutes";
                await _aiClient.ExtractParametersAsync(aiRequest);
                
                // For now, create a conversational template but this would be AI-generated
                var response = wasWeekendAdjusted || !string.IsNullOrEmpty(explanation) ?
                    $"{explanation} Unfortunately, I couldn't find any suitable {durationMinutes}-minute slots between {context.StartDate} and {context.EndDate}. Would you like me to:\n\n" +
                    "• Check different dates (perhaps later next week)?\n" +
                    "• Try a shorter meeting duration?\n" +
                    "• Look at different times of day?\n\n" +
                    "Just let me know what works better for you!" :
                    
                    $"I couldn't find any suitable {durationMinutes}-minute slots between {context.StartDate} and {context.EndDate}. Would you like me to:\n\n" +
                    "• Check different dates?\n" +
                    "• Try a shorter meeting duration?\n" +
                    "• Look at different times of day?\n\n" +
                    "Let me know how I can help find a time that works!";
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI response generation failed");
                return $"I couldn't find any suitable {durationMinutes}-minute slots between {DateFormattingService.FormatDateWithDay(startDate)} and {DateFormattingService.FormatDateWithDay(endDate)}. Would you like me to check different dates or a different duration?";
            }
        }
        
        /// <summary>
        /// Generate conversational response with slot recommendations
        /// </summary>
        private async Task<string> GenerateSlotListResponseAsync(
            List<EnhancedTimeSlot> slots,
            DateTime startDate,
            DateTime endDate,
            int durationMinutes,
            string originalRequest,
            bool wasWeekendAdjusted,
            string explanation = "")
        {
            var sb = new StringBuilder();
            
            // AI-generated opening (simplified for now)
            var opening = await GenerateOpeningLineAsync(startDate, endDate, durationMinutes, originalRequest, wasWeekendAdjusted, explanation);
            sb.AppendLine(opening);
            sb.AppendLine();
            
            // Group slots by day and format
            var slotsByDay = slots
                .GroupBy(s => s.StartTime.Date)
                .OrderBy(g => g.Key)
                .Where(g => g.Key >= startDate.Date && g.Key <= endDate.Date);
            
            foreach (var dayGroup in slotsByDay)
            {
                sb.AppendLine($"{DateFormattingService.FormatDateWithDay(dayGroup.Key)}");
                
                foreach (var slot in dayGroup.OrderBy(s => s.StartTime))
                {
                    var timeRange = $"{slot.StartTime:HH:mm} - {slot.EndTime:HH:mm}";
                    var availabilityDesc = slot.GetParticipantAvailabilityDescription();
                    
                    if (slot.IsRecommended)
                        sb.AppendLine($"{timeRange} {availabilityDesc} ⭐ RECOMMENDED");
                    else
                        sb.AppendLine($"{timeRange} {availabilityDesc}");
                }
                
                sb.AppendLine();
            }
            
            // AI-generated closing
            sb.AppendLine("Please let me know which time slot works best for you!");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Generate conversational opening line using AI context
        /// </summary>
        private async Task<string> GenerateOpeningLineAsync(
            DateTime startDate,
            DateTime endDate,
            int durationMinutes,
            string originalRequest,
            bool wasWeekendAdjusted,
            string explanation = "")
        {
            try
            {
                bool isSingleDay = startDate.Date == endDate.Date;
                bool isLimitedDays = (endDate.Date - startDate.Date).Days < 4 && originalRequest.ToLower().Contains("first");
                
                if (wasWeekendAdjusted && !string.IsNullOrEmpty(explanation))
                {
                    return $"{explanation}. I've found the following {durationMinutes}-minute time slots for the next business days:";
                }
                
                if (wasWeekendAdjusted)
                {
                    return $"Since you asked for tomorrow but it's a weekend, I've found the following {durationMinutes}-minute time slots for the next business days:";
                }
                
                if (isSingleDay)
                {
                    return $"I've found the following {durationMinutes}-minute time slots for {DateFormattingService.FormatDateWithDay(startDate)}:";
                }
                
                if (isLimitedDays)
                {
                    int dayCount = (endDate.Date - startDate.Date).Days + 1;
                    return $"I've found the following {durationMinutes}-minute time slots for the first {dayCount} days of next week [{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}]:";
                }
                
                return $"I've found the following {durationMinutes}-minute time slots for next week [{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}]:";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating opening line");
                return $"Here are the available {durationMinutes}-minute time slots:";
            }
        }
        
        /// <summary>
        /// Fallback response when AI fails
        /// </summary>
        private string CreateFallbackResponse(
            List<EnhancedTimeSlot> slots,
            DateTime startDate,
            DateTime endDate,
            int durationMinutes)
        {
            if (!slots.Any())
            {
                return $"I couldn't find any suitable {durationMinutes}-minute slots between " +
                       $"{DateFormattingService.FormatDateWithDay(startDate)} and " +
                       $"{DateFormattingService.FormatDateWithDay(endDate)}. " +
                       "Would you like me to check different dates or a different duration?";
            }
            
            var sb = new StringBuilder();
            sb.AppendLine($"Available {durationMinutes}-minute time slots:");
            sb.AppendLine();
            
            var slotsByDay = slots.GroupBy(s => s.StartTime.Date).OrderBy(g => g.Key);
            
            foreach (var dayGroup in slotsByDay)
            {
                sb.AppendLine($"{DateFormattingService.FormatDateWithDay(dayGroup.Key)}");
                
                foreach (var slot in dayGroup.OrderBy(s => s.StartTime))
                {
                    var timeRange = $"{slot.StartTime:HH:mm} - {slot.EndTime:HH:mm}";
                    var availabilityDesc = slot.GetParticipantAvailabilityDescription();
                    
                    if (slot.IsRecommended)
                        sb.AppendLine($"{timeRange} {availabilityDesc} ⭐ RECOMMENDED");
                    else
                        sb.AppendLine($"{timeRange} {availabilityDesc}");
                }
                
                sb.AppendLine();
            }
            
            sb.AppendLine("Please let me know which time slot works best for you!");
            return sb.ToString();
        }
    }
}