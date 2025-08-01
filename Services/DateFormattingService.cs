using System;
using System.Globalization;

namespace InterviewBot.Services
{
    public static class DateFormattingService
    {
        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
        
        public static string FormatDateWithDay(DateTime date)
            => $"{date.ToString("dddd", EnglishCulture)} [{date:dd.MM.yyyy}]";

        public static string FormatDateRange(DateTime start, DateTime end)
            => $"[{start:dd.MM.yyyy} - {end:dd.MM.yyyy}]";
            
        public static string FormatTimeRange(DateTime start, DateTime end)
            => $"{start:HH:mm} - {end:HH:mm}";
            
        public static string GetRelativeDateDescription(DateTime targetDate, DateTime currentDate)
        {
            if (targetDate.Date == currentDate.Date)
                return "today";
            if (targetDate.Date == currentDate.AddDays(1).Date)
                return "tomorrow";
                
            int daysDifference = (targetDate.Date - currentDate.Date).Days;
            if (daysDifference > 1 && daysDifference <= 7)
                return $"in {daysDifference} days";
                
            return $"on {FormatDateWithDay(targetDate)}";
        }
    }
}
