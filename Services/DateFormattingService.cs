using System;

namespace InterviewBot.Services
{
    public static class DateFormattingService
    {
        public static string FormatDateWithDay(DateTime date)
            => $"{date:dddd} [{date:dd.MM.yyyy}]";

        public static string FormatDateRange(DateTime start, DateTime end)
            => $"[{start:dd.MM.yyyy} - {end:dd.MM.yyyy}]";
            
        public static string FormatTimeRange(DateTime start, DateTime end)
            => $"{start:HH:mm} - {end:HH:mm}";
            
        // Get next business day (skip weekends)
        public static DateTime GetNextBusinessDay(DateTime date)
        {
            var nextDay = date.AddDays(1);
            while (nextDay.DayOfWeek == DayOfWeek.Saturday || nextDay.DayOfWeek == DayOfWeek.Sunday)
            {
                nextDay = nextDay.AddDays(1);
            }
            return nextDay;
        }
        
        public static string GetRelativeDateDescription(DateTime targetDate, DateTime currentDate)
        {
            if (targetDate.Date == currentDate.Date)
                return "today";
            if (targetDate.Date == currentDate.AddDays(1).Date)
                return "tomorrow";
                
            int daysDifference = (targetDate.Date - currentDate.Date).Days;
            if (daysDifference > 1 && daysDifference <= 7)
                return $"in {daysDifference} days on {FormatDateWithDay(targetDate)}";
                
            return FormatDateWithDay(targetDate);
        }
    }
}