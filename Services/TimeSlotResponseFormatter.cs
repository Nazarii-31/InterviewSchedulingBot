using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InterviewBot.Models;
using InterviewBot.Services;

namespace InterviewBot.Services
{
    public class TimeSlotResponseFormatter
    {
        public string FormatTimeSlotResponse(
            List<EnhancedTimeSlot> slots,
            DateTime startDate,
            DateTime endDate,
            int durationMinutes,
            string originalRequest) // Add this parameter
        {
            var sb = new StringBuilder();
            
            // Handle no slots found
            if (!slots.Any())
            {
                return $"I couldn't find any suitable {durationMinutes}-minute slots between " +
                       $"{DateFormattingService.FormatDateWithDay(startDate)} and " +
                       $"{DateFormattingService.FormatDateWithDay(endDate)}. " +
                       "Would you like me to check different dates or a different duration?";
            }
            
            // Make opening more conversational while keeping date format
            bool isSingleDay = startDate.Date == endDate.Date;
            
            if (isSingleDay)
            {
                sb.AppendLine($"I've found the following {durationMinutes}-minute time slots for {DateFormattingService.FormatDateWithDay(startDate)}:");
            }
            else
            {
                // Check if this is "first X days" scenario
                bool isLimitedDays = (endDate.Date - startDate.Date).Days < 4 && originalRequest.ToLower().Contains("first");
                
                if (isLimitedDays)
                {
                    int dayCount = (endDate.Date - startDate.Date).Days + 1;
                    sb.AppendLine($"I've found the following {durationMinutes}-minute time slots for the first {dayCount} days of next week [{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}]:");
                }
                else
                {
                    sb.AppendLine($"I've found the following {durationMinutes}-minute time slots for next week [{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}]:");
                }
            }
            
            sb.AppendLine();
            
            // Group slots by day
            var slotsByDay = slots
                .GroupBy(s => s.StartTime.Date)
                .OrderBy(g => g.Key);
            
            // Only show days that fall within the requested range
            foreach (var dayGroup in slotsByDay)
            {
                // Only include days within the requested range
                if (dayGroup.Key < startDate.Date || dayGroup.Key > endDate.Date)
                    continue;
                    
                sb.AppendLine($"{DateFormattingService.FormatDateWithDay(dayGroup.Key)}");
                sb.AppendLine();
                
                // Add slots for this day
                foreach (var slot in dayGroup.OrderBy(s => s.StartTime))
                {
                    // Show time range + participant info + recommendation
                    string timeRange = $"{slot.StartTime:HH:mm} - {slot.EndTime:HH:mm}";
                    
                    // Get specific availability description (showing who is unavailable)
                    string availabilityDesc = slot.GetParticipantAvailabilityDescription();
                    
                    if (slot.IsRecommended)
                        sb.AppendLine($"{timeRange} {availabilityDesc} ⭐ RECOMMENDED");
                    else
                        sb.AppendLine($"{timeRange} {availabilityDesc}");
                }
                
                sb.AppendLine();
            }
            
            sb.AppendLine("Please let me know which time slot works best for you.");
            
            return sb.ToString();
        }

        // Keep original method for backward compatibility
        public string FormatResponse(
            List<EnhancedTimeSlot> slots, 
            int durationMinutes, 
            DateTime startDate, 
            DateTime endDate)
        {
            return FormatTimeSlotResponse(slots, startDate, endDate, durationMinutes, "");
        }
    }
}