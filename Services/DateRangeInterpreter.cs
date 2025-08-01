using System;
using System.Text.RegularExpressions;

namespace InterviewBot.Services
{
    public class DateRangeInterpreter
    {
        public (DateTime startDate, DateTime endDate) InterpretDateRange(string userRequest, DateTime currentDate)
        {
            // Default to tomorrow if nothing specific
            var defaultStart = currentDate.AddDays(1).Date.AddHours(9); // 9 AM
            var defaultEnd = currentDate.AddDays(1).Date.AddHours(17); // 5 PM
            
            // Use regex-free approach with basic string contains for robustness
            string requestLower = userRequest.ToLowerInvariant();
            
            // Extract day count if specified (e.g. "first 3 days", "first 2 days")
            int? specifiedDayCount = null;
            
            if (requestLower.Contains("first"))
            {
                // Look for number after "first"
                int firstIndex = requestLower.IndexOf("first");
                string afterFirst = requestLower.Substring(firstIndex + 5); // "first" is 5 chars
                
                // Find digits
                string digitStr = "";
                foreach (char c in afterFirst)
                {
                    if (char.IsDigit(c))
                        digitStr += c;
                    else if (digitStr.Length > 0)
                        break;
                }
                
                if (!string.IsNullOrEmpty(digitStr) && int.TryParse(digitStr, out int days))
                {
                    specifiedDayCount = days;
                }
            }
            
            // Next week
            if (requestLower.Contains("next week"))
            {
                // Calculate next Monday
                int daysUntilMonday = ((int)DayOfWeek.Monday - (int)currentDate.DayOfWeek + 7) % 7;
                if (daysUntilMonday == 0) daysUntilMonday = 7;
                
                DateTime nextMonday = currentDate.AddDays(daysUntilMonday).Date;
                
                // Default: full work week (Monday-Friday)
                DateTime startDay = nextMonday.AddHours(9); // 9 AM
                DateTime endDay;
                
                // If specific day count mentioned, limit to that
                if (specifiedDayCount.HasValue)
                {
                    endDay = nextMonday.AddDays(specifiedDayCount.Value - 1).AddHours(17); // 5 PM
                }
                else
                {
                    // Full work week (Monday-Friday)
                    endDay = nextMonday.AddDays(4).AddHours(17); // Friday 5 PM
                }
                
                return (startDay, endDay);
            }
            
            // Tomorrow
            if (requestLower.Contains("tomorrow"))
            {
                var tomorrow = currentDate.AddDays(1).Date;
                
                // Morning
                if (requestLower.Contains("morning"))
                {
                    return (tomorrow.AddHours(9), tomorrow.AddHours(12));
                }
                
                // Afternoon
                if (requestLower.Contains("afternoon"))
                {
                    return (tomorrow.AddHours(12), tomorrow.AddHours(17));
                }
                
                // Full day
                return (tomorrow.AddHours(9), tomorrow.AddHours(17));
            }
            
            // This week
            if (requestLower.Contains("this week"))
            {
                // Start from tomorrow
                DateTime startDay = currentDate.AddDays(1).Date.AddHours(9);
                
                // End on Friday
                int daysUntilFriday = ((int)DayOfWeek.Friday - (int)currentDate.DayOfWeek + 7) % 7;
                if (daysUntilFriday == 0 || daysUntilFriday < 0) daysUntilFriday += 7;
                DateTime endDay = currentDate.AddDays(daysUntilFriday).Date.AddHours(17);
                
                // If specific day count mentioned, limit to that
                if (specifiedDayCount.HasValue)
                {
                    endDay = startDay.AddDays(specifiedDayCount.Value - 1).AddHours(17);
                }
                
                return (startDay, endDay);
            }
            
            // Default to tomorrow if nothing matches
            return (defaultStart, defaultEnd);
        }
    }
}