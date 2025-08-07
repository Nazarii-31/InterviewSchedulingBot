using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InterviewBot.Models;
using InterviewBot.Services;

namespace InterviewBot.Services
{
    public class SlotResponseFormatter
    {
        public string FormatTimeSlotResponse(
            List<EnhancedTimeSlot> slots,
            DateTime startDate,
            DateTime endDate,
            int durationMinutes)
        {
            if (!slots.Any())
            {
                return $"I couldn't find any available {durationMinutes}-minute time slots between " +
                       $"{DateFormattingService.FormatDateWithDay(startDate)} and " +
                       $"{DateFormattingService.FormatDateWithDay(endDate)}. " +
                       "Would you like to try different dates or duration?";
            }
            
            var sb = new StringBuilder();
            
            // Determine if this is a single-day or multi-day request
            var isSingleDay = startDate.Date == endDate.Date;
            
            if (isSingleDay)
            {
                sb.AppendLine($"Here are the available {durationMinutes}-minute time slots for {DateFormattingService.FormatDateWithDay(startDate)}:");
                sb.AppendLine();
                sb.AppendLine($"{DateFormattingService.FormatDateWithDay(startDate)}");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine($"Here are the available {durationMinutes}-minute time slots between " +
                             $"{DateFormattingService.FormatDateWithDay(startDate)} and " +
                             $"{DateFormattingService.FormatDateWithDay(endDate)}:");
                sb.AppendLine();
            }
            
            // Group slots by day
            var slotsByDay = slots.GroupBy(s => s.StartTime.Date).OrderBy(g => g.Key);
            
            foreach (var dayGroup in slotsByDay)
            {
                // Skip day header if single day (already included in the intro)
                if (!isSingleDay)
                {
                    sb.AppendLine($"{DateFormattingService.FormatDateWithDay(dayGroup.Key)}");
                    sb.AppendLine();
                }
                
                // Add slots for this day
                foreach (var slot in dayGroup.OrderBy(s => s.StartTime))
                {
                    sb.Append($"{slot.GetFormattedTimeRange()} {slot.Reason}");
                    sb.AppendLine();
                }
                
                sb.AppendLine();
            }
            
            sb.AppendLine("Please let me know which time slot works best for you.");
            
            return sb.ToString();
        }
    }
}
