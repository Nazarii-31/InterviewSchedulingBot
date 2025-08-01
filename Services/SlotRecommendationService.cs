using System;
using System.Collections.Generic;
using System.Linq;
using InterviewBot.Models;

namespace InterviewBot.Services
{
    public class SlotRecommendationService
    {
        // Seed to ensure deterministic behavior
        private static readonly int DeterministicSeed = 42;
        private static readonly Random _seededRandom = new Random(DeterministicSeed);
        
        public List<EnhancedTimeSlot> GenerateConsistentTimeSlots(
            DateTime startDate, 
            DateTime endDate,
            int durationMinutes, 
            List<string> participantEmails)
        {
            // Use emails and date as seed for deterministic generation
            var emailsHash = string.Join("", participantEmails).GetHashCode();
            var dateHash = startDate.ToShortDateString().GetHashCode();
            var seed = emailsHash ^ dateHash;
            var random = new Random(seed);
            
            var result = new List<EnhancedTimeSlot>();
            
            // Generate slots only for working hours (9 AM - 5 PM) with 15-minute alignment
            for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
            {
                // Skip weekends for business hours
                if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday)
                    continue;
                    
                // Generate time slots with 15-minute alignment
                for (int hour = 9; hour < 17; hour++)
                {
                    foreach (var minute in new[] { 0, 15, 30, 45 })
                    {
                        var slotStart = new DateTime(day.Year, day.Month, day.Day, hour, minute, 0);
                        var slotEnd = slotStart.AddMinutes(durationMinutes);
                        
                        // Don't create slots that extend past working hours
                        if (slotEnd.Hour >= 17 && slotEnd.Minute > 0)
                            continue;
                            
                        // Deterministically decide if participants are available (based on hash)
                        var slotHash = slotStart.ToString("yyyyMMddHHmm").GetHashCode();
                        var combinedHash = slotHash ^ seed;
                        var rand = new Random(combinedHash);
                        
                        var availableParticipants = new List<string>();
                        var unavailableParticipants = new List<string>();
                        
                        foreach (var email in participantEmails)
                        {
                            // Use email + time hash for consistent participant availability
                            var participantHash = (email + slotStart.Ticks).GetHashCode();
                            var isAvailable = Math.Abs(participantHash % 100) < 80; // 80% chance of availability
                            
                            if (isAvailable)
                                availableParticipants.Add(email);
                            else
                                unavailableParticipants.Add(email);
                        }
                        
                        // Skip slots where nobody is available
                        if (availableParticipants.Count == 0)
                            continue;
                            
                        // Calculate score based on availability percentage and time of day
                        var availabilityScore = (double)availableParticipants.Count / participantEmails.Count * 100;
                        var timeOfDayScore = GetTimeOfDayPreferenceScore(slotStart);
                        var totalScore = availabilityScore * 0.7 + timeOfDayScore * 0.3;
                        
                        var slot = new EnhancedTimeSlot
                        {
                            StartTime = slotStart,
                            EndTime = slotEnd,
                            AvailableParticipants = availableParticipants,
                            UnavailableParticipants = unavailableParticipants,
                            Score = totalScore,
                            Reason = GenerateRecommendationReason(availableParticipants, participantEmails, slotStart)
                        };
                        
                        result.Add(slot);
                    }
                }
            }
            
            // Sort by score, then by start time
            var sortedSlots = result
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.StartTime)
                .Take(10)
                .ToList();
                
            // Mark highest-scoring slot as recommended
            if (sortedSlots.Any())
            {
                sortedSlots.First().IsRecommended = true;
                sortedSlots.First().Reason += " ⭐ RECOMMENDED";
            }
            
            return sortedSlots;
        }
        
        private int GetTimeOfDayPreferenceScore(DateTime time)
        {
            // Morning (9-12): 90 points
            if (time.Hour >= 9 && time.Hour < 12)
                return 90;
                
            // Early afternoon (12-2): 80 points
            if (time.Hour >= 12 && time.Hour < 14)
                return 80;
                
            // Late afternoon (2-5): 70 points
            return 70;
        }
        
        private string GenerateRecommendationReason(List<string> availableParticipants, List<string> allParticipants, DateTime slotTime)
        {
            if (availableParticipants.Count == allParticipants.Count)
                return "(All participants available)";
                
            var availabilityPercentage = (double)availableParticipants.Count / allParticipants.Count * 100;
            
            if (availabilityPercentage >= 75)
                return $"({availableParticipants.Count}/{allParticipants.Count} participants available)";
                
            // Morning preference
            if (slotTime.Hour >= 9 && slotTime.Hour < 12)
                return "(Morning time slot - higher productivity)";
                
            // After lunch preference
            if (slotTime.Hour >= 13 && slotTime.Hour < 15)
                return "(Post-lunch time slot)";
                
            return "(Limited availability time slot)";
        }
    }
}
