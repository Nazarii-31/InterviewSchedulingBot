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
            
            // More conversational opening line
            bool isSingleDay = startDate.Date == endDate.Date;
            
            if (isSingleDay)
            {
                sb.AppendLine($"I've found the following {durationMinutes}-minute time slots for {DateFormattingService.FormatDateWithDay(startDate)}:");
            }
            else
            {
                string dateRange = DateFormattingService.FormatDateRange(startDate, endDate);
                sb.AppendLine($"I've found the following {durationMinutes}-minute time slots for {(startDate.AddDays(7) >= endDate.Date ? "next week" : "the requested period")} {dateRange}:");
            }
            
            sb.AppendLine();
            
            // Group slots by day
            var slotsByDay = slots
                .GroupBy(s => s.StartTime.Date)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartTime).ToList());
            
            // Ensure ALL requested days are shown (even those without slots)
            var allDays = new List<DateTime>();
            for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
            {
                if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday)
                    allDays.Add(day);
            }
            
            // Process each day in range
            foreach (var day in allDays)
            {
                // Add day header
                sb.AppendLine($"{DateFormattingService.FormatDateWithDay(day)}");
                
                if (slotsByDay.ContainsKey(day) && slotsByDay[day].Any())
                {
                    sb.AppendLine();
                    // Show slots for this day
                    foreach (var slot in slotsByDay[day])
                    {
                        sb.AppendLine($"{slot.GetFormattedTimeRange()} {slot.Reason}");
                    }
                }
                else
                {
                    sb.AppendLine();
                    sb.AppendLine("No available time slots for this day. Please let me know which other day works best for you.");
                }
                
                sb.AppendLine();
            }
            
            sb.Append("Please let me know which time slot works best for you.");
            
            return sb.ToString();
        }
    }
}