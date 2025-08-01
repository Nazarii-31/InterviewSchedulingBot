using System;
using System.Collections.Generic;
using System.Linq;
using InterviewBot.Models;

namespace InterviewBot.Services
{
    public class DeterministicSlotRecommendationService
    {
        private readonly DateRangeInterpreter _dateInterpreter;
        
        public DeterministicSlotRecommendationService(DateRangeInterpreter dateInterpreter)
        {
            _dateInterpreter = dateInterpreter;
        }
        
        // Add this method to interpret request directly
        public (DateTime startDate, DateTime endDate) InterpretDateRangeFromRequest(string userRequest, DateTime currentDate)
        {
            return _dateInterpreter.InterpretDateRange(userRequest, currentDate);
        }

        public List<EnhancedTimeSlot> GenerateConsistentTimeSlots(
            DateTime startDate, 
            DateTime endDate,
            int durationMinutes, 
            List<string> participantEmails,
            int maxInitialResults = 5) // Limit initial results
        {
            // Create a deterministic seed based on inputs
            string seedInput = string.Join(",", participantEmails.OrderBy(e => e)) + 
                              startDate.ToString("yyyy-MM-dd") + 
                              endDate.ToString("yyyy-MM-dd") + 
                              durationMinutes.ToString();
            int seed = seedInput.GetHashCode();
            var random = new Random(seed);
            
            var result = new List<EnhancedTimeSlot>();
            
            // Generate slots for each day in the range
            for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
            {
                // Skip weekends
                if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday)
                    continue;
                
                // Generate slots aligned to 15-minute intervals
                var dayStart = new DateTime(day.Year, day.Month, day.Day, startDate.Hour, 0, 0);
                var dayEnd = new DateTime(day.Year, day.Month, day.Day, endDate.Hour, 0, 0);
                
                // Start times always at quarter hours (00, 15, 30, 45)
                for (var time = dayStart; time < dayEnd; time = time.AddMinutes(15))
                {
                    var slotEnd = time.AddMinutes(durationMinutes);
                    
                    // Skip if slot end exceeds day end
                    if (slotEnd > dayEnd)
                        continue;
                    
                    // Deterministically determine participant availability
                    var availableParticipants = new List<string>();
                    var unavailableParticipants = new List<string>();
                    
                    foreach (var email in participantEmails)
                    {
                        // Generate deterministic availability based on email and time
                        int participantSeed = (email + time.ToString("HH:mm")).GetHashCode();
                        var participantRandom = new Random(participantSeed);
                        
                        // Adjust availability to create more realistic conflicts
                        // Different users have different patterns
                        double availabilityChance = email.Contains("jane") ? 0.7 : 0.8;
                        
                        // Lower availability during lunch time and late afternoon
                        if (time.Hour >= 12 && time.Hour < 14)
                            availabilityChance *= 0.5; // Lunch conflicts
                        else if (time.Hour >= 16)
                            availabilityChance *= 0.6; // Late day conflicts
                            
                        bool isAvailable = participantRandom.NextDouble() < availabilityChance;
                        
                        if (isAvailable)
                            availableParticipants.Add(email);
                        else
                            unavailableParticipants.Add(email);
                    }
                    
                    // Skip slots where no one is available
                    if (availableParticipants.Count == 0)
                        continue;
                        
                    // Calculate slot score
                    double availabilityScore = (double)availableParticipants.Count / participantEmails.Count * 100;
                    double timeOfDayScore = GetTimeOfDayScore(time);
                    double totalScore = (availabilityScore * 0.7) + (timeOfDayScore * 0.3);
                    
                    var slot = new EnhancedTimeSlot
                    {
                        StartTime = time,
                        EndTime = slotEnd,
                        AvailableParticipants = availableParticipants,
                        UnavailableParticipants = unavailableParticipants,
                        Score = totalScore,
                        Reason = GenerateReason(availableParticipants, participantEmails, time)
                    };
                    
                    result.Add(slot);
                }
            }
            
            // Group slots by day
            var slotsByDay = result
                .GroupBy(s => s.StartTime.Date)
                .ToDictionary(g => g.Key, g => g.ToList());
                
            var finalSlots = new List<EnhancedTimeSlot>();
            
            // Get top N slots per day (better user experience)
            foreach (var day in slotsByDay.Keys.OrderBy(d => d))
            {
                var topSlotsForDay = slotsByDay[day]
                    .OrderByDescending(s => s.Score)
                    .ThenBy(s => s.StartTime)
                    .Take(maxInitialResults)
                    .ToList();
                    
                if (topSlotsForDay.Any())
                {
                    // Mark highest-scoring slot for each day as recommended
                    topSlotsForDay.First().IsRecommended = true;
                    topSlotsForDay.First().Reason = "⭐ RECOMMENDED";
                    
                    finalSlots.AddRange(topSlotsForDay);
                }
            }
            
            return finalSlots.OrderBy(s => s.StartTime).ToList();
        }
        
        private double GetTimeOfDayScore(DateTime time)
        {
            int hour = time.Hour;
            
            // Morning premium (9-11 AM)
            if (hour >= 9 && hour < 11)
                return 100;
                
            // Mid-morning (11 AM-12 PM)
            if (hour >= 11 && hour < 12)
                return 90;
                
            // After lunch (1-3 PM)
            if (hour >= 13 && hour < 15)
                return 80;
                
            // Late afternoon (3-5 PM)
            if (hour >= 15 && hour < 17)
                return 70;
                
            // Early morning or evening
            return 60;
        }
        
        private string GenerateReason(List<string> availableParticipants, List<string> allParticipants, DateTime time)
        {
            if (availableParticipants.Count == allParticipants.Count)
                return "(All participants available)";
                
            double availabilityPercent = (double)availableParticipants.Count / allParticipants.Count * 100;
            
            // Time-based reasons
            int hour = time.Hour;
            
            if (hour >= 9 && hour < 11)
                return $"({availableParticipants.Count}/{allParticipants.Count} participants - Morning productivity peak)";
                
            if (hour >= 11 && hour < 12)
                return $"({availableParticipants.Count}/{allParticipants.Count} participants - Mid-morning slot)";
                
            if (hour >= 13 && hour < 14)
                return $"({availableParticipants.Count}/{allParticipants.Count} participants - After lunch)";
                
            if (hour >= 14 && hour < 16)
                return $"({availableParticipants.Count}/{allParticipants.Count} participants - Afternoon slot)";
                
            if (hour >= 16)
                return $"({availableParticipants.Count}/{allParticipants.Count} participants - Late day slot)";
                
            return $"({availableParticipants.Count}/{allParticipants.Count} participants available)";
        }
    }
}