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
        public string FormatResponse(
            List<EnhancedTimeSlot> slots, 
            int durationMinutes, 
            DateTime startDate, 
            DateTime endDate)
        {
            var sb = new StringBuilder();
            
            // Handle no slots found
            if (!slots.Any())
            {
                return $"I couldn't find any available {durationMinutes}-minute time slots for " +
                       $"{DateFormattingService.FormatDateWithDay(startDate)}. " +
                       "Would you like me to check a different time range?";
            }
            
            // Determine if single day or date range
            bool isSingleDay = startDate.Date == endDate.Date;
            
            // Format header
            if (isSingleDay)
            {
                sb.AppendLine($"Here are the available {durationMinutes}-minute time slots for {DateFormattingService.FormatDateWithDay(startDate)}:");
                sb.AppendLine();
                sb.AppendLine($"{DateFormattingService.FormatDateWithDay(startDate)}");
            }
            else
            {
                string dateRange = DateFormattingService.FormatDateRange(startDate, endDate);
                sb.AppendLine($"Here are the available {durationMinutes}-minute time slots for " +
                             (startDate.Date.AddDays(7) >= endDate.Date ? "next week" : "the specified period") +
                             $" {dateRange}:");
                sb.AppendLine();
            }
            
            // Group by day and format slots
            var slotsByDay = slots
                .GroupBy(s => s.StartTime.Date)
                .OrderBy(g => g.Key);
                
            foreach (var dayGroup in slotsByDay)
            {
                // Skip repeating day header if single day
                if (!isSingleDay)
                {
                    sb.AppendLine();
                    sb.AppendLine($"{DateFormattingService.FormatDateWithDay(dayGroup.Key)}");
                }
                
                sb.AppendLine();
                
                // List slots for this day
                foreach (var slot in dayGroup.OrderBy(s => s.StartTime))
                {
                    sb.AppendLine($"{slot.GetFormattedTimeRange()} {slot.Reason}");
                }
            }
            
            sb.AppendLine();
            sb.Append("Please let me know which time slot works best for you.");
            
            return sb.ToString();
        }
    }
}