using System;
using System.Collections.Generic;

namespace InterviewBot.Models
{
    public class EnhancedTimeSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<string> AvailableParticipants { get; set; } = new List<string>();
        public List<string> UnavailableParticipants { get; set; } = new List<string>();
        public double Score { get; set; }
        public string Reason { get; set; } = "";
        public bool IsRecommended { get; set; }

        public string GetFormattedTimeRange() => 
            $"{StartTime:HH:mm} - {EndTime:HH:mm}";

        public string GetFormattedDateWithDay() =>
            $"{StartTime:dddd} [{StartTime:dd.MM.yyyy}]";
    }
}