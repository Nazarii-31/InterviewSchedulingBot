using InterviewBot.Domain.Entities;
using System.Globalization;

namespace InterviewSchedulingBot.Services.Business
{
    /// <summary>
    /// Service for ranking time slots and generating recommendations with explanations
    /// </summary>
    public interface ISlotRecommendationService
    {
        List<EnhancedRankedTimeSlot> RankAndExplainSlots(
            List<RankedTimeSlot> slots, 
            SlotQueryCriteria criteria);
        
        EnhancedRankedTimeSlot? GetBestRecommendedSlot(List<EnhancedRankedTimeSlot> slots);
    }

    public class SlotRecommendationService : ISlotRecommendationService
    {
        private readonly ILogger<SlotRecommendationService> _logger;

        public SlotRecommendationService(ILogger<SlotRecommendationService> logger)
        {
            _logger = logger;
        }

        public List<EnhancedRankedTimeSlot> RankAndExplainSlots(
            List<RankedTimeSlot> slots, 
            SlotQueryCriteria criteria)
        {
            if (!slots.Any())
                return new List<EnhancedRankedTimeSlot>();

            var enhancedSlots = new List<EnhancedRankedTimeSlot>();

            foreach (var slot in slots)
            {
                var enhancedSlot = new EnhancedRankedTimeSlot
                {
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Score = CalculateEnhancedScore(slot, criteria),
                    AvailableParticipants = slot.AvailableParticipants,
                    TotalParticipants = slot.TotalParticipants,
                    AvailableParticipantEmails = slot.AvailableParticipantEmails,
                    UnavailableParticipants = slot.UnavailableParticipants,
                    Explanation = GenerateExplanation(slot, criteria),
                    IsRecommended = false // Will be set later for the best slot
                };

                enhancedSlots.Add(enhancedSlot);
            }

            // Sort by enhanced score (highest first)
            enhancedSlots = enhancedSlots.OrderByDescending(s => s.Score).ToList();

            // Mark the best slot as recommended
            if (enhancedSlots.Any())
            {
                var bestSlot = enhancedSlots.First();
                bestSlot.IsRecommended = true;
                bestSlot.RecommendationReason = GenerateRecommendationReason(bestSlot, criteria);
            }

            return enhancedSlots;
        }

        public EnhancedRankedTimeSlot? GetBestRecommendedSlot(List<EnhancedRankedTimeSlot> slots)
        {
            return slots.FirstOrDefault(s => s.IsRecommended);
        }

        private double CalculateEnhancedScore(RankedTimeSlot slot, SlotQueryCriteria criteria)
        {
            double score = slot.Score; // Base score from availability

            // Bonus for full participant availability
            if (slot.AvailableParticipants == slot.TotalParticipants)
            {
                score += 20; // +20 points for full availability
            }

            // Bonus for preferred meeting times (9 AM - 5 PM weekdays)
            if (IsPreferredMeetingTime(slot.StartTime))
            {
                score += 15; // +15 points for optimal meeting hours
            }

            // Bonus for morning meetings (9 AM - 12 PM)
            if (IsMorningTime(slot.StartTime))
            {
                score += 10; // +10 points for morning productivity
            }

            // Bonus for quarter-hour alignment
            if (slot.StartTime.Minute % 15 == 0)
            {
                score += 5; // +5 points for standard meeting times
            }

            // Penalty for late afternoon meetings (after 4 PM)
            if (IsLateAfternoonTime(slot.StartTime))
            {
                score -= 5; // -5 points for late meetings
            }

            return Math.Round(score, 2);
        }

        private string GenerateExplanation(RankedTimeSlot slot, SlotQueryCriteria criteria)
        {
            var explanations = new List<string>();

            // Availability explanation
            if (slot.AvailableParticipants == slot.TotalParticipants)
            {
                explanations.Add("All participants available");
            }
            else if (slot.AvailableParticipants > 0)
            {
                explanations.Add($"{slot.AvailableParticipants}/{slot.TotalParticipants} participants available");
            }

            // Time-based explanations
            if (IsPreferredMeetingTime(slot.StartTime))
            {
                explanations.Add("Optimal meeting hours");
            }

            if (IsMorningTime(slot.StartTime))
            {
                explanations.Add("Best for productivity");
            }

            if (IsLateAfternoonTime(slot.StartTime))
            {
                explanations.Add("Late afternoon timing");
            }

            // Default explanation if none apply
            if (!explanations.Any())
            {
                explanations.Add("Available time slot");
            }

            return string.Join(", ", explanations);
        }

        private string GenerateRecommendationReason(EnhancedRankedTimeSlot slot, SlotQueryCriteria criteria)
        {
            var reasons = new List<string>();

            if (slot.AvailableParticipants == slot.TotalParticipants)
            {
                reasons.Add("highest overall availability");
            }

            if (IsPreferredMeetingTime(slot.StartTime))
            {
                reasons.Add("matches optimal meeting hours");
            }

            if (IsMorningTime(slot.StartTime))
            {
                reasons.Add("optimal for participant productivity");
            }

            if (slot.StartTime.Minute % 15 == 0)
            {
                reasons.Add("standard meeting time");
            }

            var reasonText = reasons.Any() 
                ? string.Join(" and ", reasons)
                : "best available option based on participant schedules";

            return $"Optimal time for all participants based on {reasonText}";
        }

        private bool IsPreferredMeetingTime(DateTime dateTime)
        {
            // Monday-Friday, 9 AM - 5 PM
            return dateTime.DayOfWeek >= DayOfWeek.Monday && 
                   dateTime.DayOfWeek <= DayOfWeek.Friday &&
                   dateTime.Hour >= 9 && dateTime.Hour < 17;
        }

        private bool IsMorningTime(DateTime dateTime)
        {
            // 9 AM - 12 PM
            return dateTime.Hour >= 9 && dateTime.Hour < 12;
        }

        private bool IsLateAfternoonTime(DateTime dateTime)
        {
            // After 4 PM
            return dateTime.Hour >= 16;
        }
    }

    /// <summary>
    /// Enhanced version of RankedTimeSlot with explanation and recommendation features
    /// </summary>
    public class EnhancedRankedTimeSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double Score { get; set; }
        public int AvailableParticipants { get; set; }
        public int TotalParticipants { get; set; }
        public List<string> AvailableParticipantEmails { get; set; } = new List<string>();
        public List<ParticipantConflict> UnavailableParticipants { get; set; } = new List<ParticipantConflict>();
        
        // Enhanced properties
        public string Explanation { get; set; } = "";
        public bool IsRecommended { get; set; }
        public string RecommendationReason { get; set; } = "";
        
        public TimeSpan Duration => EndTime - StartTime;
        public double AvailabilityPercentage => TotalParticipants > 0 ? (double)AvailableParticipants / TotalParticipants * 100 : 0;
    }
}
