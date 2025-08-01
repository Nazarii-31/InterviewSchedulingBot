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

        // Add method to format participant availability with specific unavailable participants
        public string GetParticipantAvailabilityDescription()
        {
            if (AvailableParticipants.Count == 0)
                return "(No participants available)";
                
            if (UnavailableParticipants.Count == 0)
                return "(All participants available)";
                
            // Format specific unavailable participants
            string unavailableNames = string.Join(", ", UnavailableParticipants
                .Select(email => email.Split('@')[0])); // Extract name from email
                
            return $"({AvailableParticipants.Count}/{AvailableParticipants.Count + UnavailableParticipants.Count} participants - {unavailableNames} unavailable)";
        }
    }
}