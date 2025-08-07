using System.Globalization;

namespace InterviewBot.Models
{
    public class EnhancedTimeSlot
    {
        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
        
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<string> AvailableParticipants { get; set; } = new List<string>();
        public List<string> UnavailableParticipants { get; set; } = new List<string>();
        public double Score { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool IsRecommended { get; set; }

        public string GetFormattedTimeRange() => 
            $"{StartTime:HH:mm} - {EndTime:HH:mm}";

        public string GetFormattedDateWithDay() =>
            $"{StartTime.ToString("dddd", EnglishCulture)} [{StartTime:dd.MM.yyyy}]";
    }
}
