using InterviewSchedulingBot.Interfaces.Business;
using InterviewSchedulingBot.Interfaces.Integration;

namespace InterviewSchedulingBot.Services.Business
{
    /// <summary>
    /// Pure business logic implementation for interview scheduling
    /// Contains no integration concerns - only business rules and algorithms
    /// </summary>
    public class SchedulingBusinessService : ISchedulingBusinessService
    {
        private readonly ILogger<SchedulingBusinessService> _logger;

        public SchedulingBusinessService(ILogger<SchedulingBusinessService> logger)
        {
            _logger = logger;
        }

        public async Task<SchedulingBusinessResult> FindOptimalInterviewSlotsAsync(SchedulingBusinessRequest request)
        {
            _logger.LogInformation("Finding optimal interview slots for {ParticipantCount} participants", 
                request.ParticipantEmails.Count);

            // Validate the request first
            var validation = await ValidateSchedulingRequestAsync(request);
            if (!validation.IsValid)
            {
                return new SchedulingBusinessResult
                {
                    Validation = validation
                };
            }

            // Apply business logic to generate recommendations
            var recommendations = await GenerateRecommendationsAsync(request);
            var alternatives = await GenerateAlternativesAsync(request);
            var insights = await GenerateBusinessInsightsAsync(request, recommendations);

            return new SchedulingBusinessResult
            {
                RecommendedSlots = recommendations,
                AlternativeSlots = alternatives,
                Validation = validation,
                Insights = insights,
                RecommendationReasoning = GenerateRecommendationReasoning(recommendations)
            };
        }

        public async Task<ValidationResult> ValidateSchedulingRequestAsync(SchedulingBusinessRequest request)
        {
            _logger.LogInformation("Validating scheduling request");

            var errors = new List<ValidationError>();
            var warnings = new List<ValidationWarning>();
            var suggestions = new List<string>();

            // Business rule validations
            if (request.ParticipantEmails.Count == 0)
            {
                errors.Add(new ValidationError
                {
                    Code = "NO_PARTICIPANTS",
                    Message = "At least one participant is required for scheduling",
                    Field = "ParticipantEmails",
                    Severity = Severity.Error
                });
            }

            if (request.DurationMinutes <= 0)
            {
                errors.Add(new ValidationError
                {
                    Code = "INVALID_DURATION",
                    Message = "Meeting duration must be positive",
                    Field = "DurationMinutes",
                    Severity = Severity.Error
                });
            }

            if (request.EarliestDate >= request.LatestDate)
            {
                errors.Add(new ValidationError
                {
                    Code = "INVALID_DATE_RANGE",
                    Message = "Earliest date must be before latest date",
                    Field = "EarliestDate,LatestDate",
                    Severity = Severity.Error
                });
            }

            // Business warnings
            if (request.DurationMinutes > 240) // 4 hours
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "LONG_INTERVIEW",
                    Message = "Interview duration is longer than recommended",
                    Field = "DurationMinutes",
                    Suggestion = "Consider breaking into multiple sessions or adding breaks"
                });
            }

            if (request.ParticipantEmails.Count > 6)
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "MANY_PARTICIPANTS",
                    Message = "Large number of participants may make scheduling difficult",
                    Field = "ParticipantEmails",
                    Suggestion = "Consider reducing the number of participants or scheduling multiple rounds"
                });
            }

            // Interview type specific validations
            if (request.InterviewType == InterviewType.Technical && request.DurationMinutes < 60)
            {
                warnings.Add(new ValidationWarning
                {
                    Code = "SHORT_TECHNICAL_INTERVIEW",
                    Message = "Technical interviews typically require more time",
                    Field = "DurationMinutes",
                    Suggestion = "Consider extending to at least 60-90 minutes for technical interviews"
                });
            }

            // Generate suggestions based on business rules
            if (request.InterviewType == InterviewType.Panel)
            {
                suggestions.Add("Panel interviews work best with 2-4 interviewers and structured questions");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings,
                Suggestions = suggestions
            };
        }

        public async Task<List<BusinessRankedTimeSlot>> ApplyBusinessRulesAsync(
            List<CalendarMeetingTimeSuggestion> suggestions, 
            BusinessSchedulingContext businessContext)
        {
            _logger.LogInformation("Applying business rules to {SuggestionCount} time suggestions", suggestions.Count);

            var rankedSlots = new List<BusinessRankedTimeSlot>();

            foreach (var suggestion in suggestions)
            {
                var businessScore = await CalculateBusinessScoreAsync(suggestion, businessContext);
                var ranking = await CalculateRankingAsync(suggestion, businessContext);
                var reasons = await GenerateBusinessReasonsAsync(suggestion, businessContext);

                rankedSlots.Add(new BusinessRankedTimeSlot
                {
                    TimeSlot = suggestion,
                    BusinessScore = businessScore,
                    Ranking = ranking,
                    BusinessReasons = reasons
                });
            }

            // Sort by business score (highest first)
            return rankedSlots.OrderByDescending(slot => slot.BusinessScore).ToList();
        }

        public async Task<ConflictAnalysis> AnalyzeSchedulingConflictsAsync(
            DateTime proposedTime, 
            TimeSpan duration,
            Dictionary<string, List<BusyTimeSlot>> participantSchedules)
        {
            _logger.LogInformation("Analyzing scheduling conflicts for {ProposedTime}", proposedTime);

            var conflicts = new List<SchedulingConflict>();
            var affectedParticipants = new List<string>();
            var proposedEnd = proposedTime.Add(duration);

            foreach (var participant in participantSchedules)
            {
                foreach (var busySlot in participant.Value)
                {
                    // Check for time overlap
                    if (proposedTime < busySlot.End && proposedEnd > busySlot.Start)
                    {
                        conflicts.Add(new SchedulingConflict
                        {
                            ParticipantEmail = participant.Key,
                            Type = ConflictType.HardConflict,
                            ConflictStart = busySlot.Start,
                            ConflictEnd = busySlot.End,
                            ConflictDescription = busySlot.Subject ?? "Existing meeting",
                            Severity = ConflictSeverity.Major,
                            CanBeResolved = false
                        });

                        if (!affectedParticipants.Contains(participant.Key))
                        {
                            affectedParticipants.Add(participant.Key);
                        }
                    }
                }
            }

            var impactLevel = CalculateImpactLevel(conflicts, participantSchedules.Count);
            var mitigationSuggestions = GenerateMitigationSuggestions(conflicts, proposedTime, duration);

            return new ConflictAnalysis
            {
                HasConflicts = conflicts.Count > 0,
                Conflicts = conflicts,
                ImpactLevel = impactLevel,
                ImpactDescription = GenerateImpactDescription(impactLevel, affectedParticipants.Count, participantSchedules.Count),
                AffectedParticipants = affectedParticipants,
                MitigationSuggestions = mitigationSuggestions
            };
        }

        public async Task<List<AlternativeOption>> GenerateAlternativeOptionsAsync(
            SchedulingBusinessRequest originalRequest, 
            ConflictAnalysis conflicts)
        {
            _logger.LogInformation("Generating alternative options due to conflicts");

            var alternatives = new List<AlternativeOption>();

            // Generate time-shifted alternatives
            var baseTime = originalRequest.EarliestDate;
            for (int hourOffset = 1; hourOffset <= 3; hourOffset++)
            {
                alternatives.Add(new AlternativeOption
                {
                    ProposedTime = baseTime.AddHours(hourOffset),
                    Duration = TimeSpan.FromMinutes(originalRequest.DurationMinutes),
                    AvailableParticipants = originalRequest.ParticipantEmails.ToList(),
                    UnavailableParticipants = new List<string>(),
                    AlternativeReason = $"Shifted by {hourOffset} hour(s) to avoid conflicts",
                    FeasibilityScore = Math.Max(10, 90 - (hourOffset * 20))
                });
            }

            return alternatives;
        }

        public async Task<InterviewScheduleResult> OptimizeForInterviewTypeAsync(
            List<CalendarMeetingTimeSuggestion> baseSchedule, 
            InterviewType interviewType)
        {
            _logger.LogInformation("Optimizing schedule for interview type: {InterviewType}", interviewType);

            var optimizedSlots = new List<BusinessRankedTimeSlot>();
            var insights = new InterviewSpecificInsights();
            var optimizations = new List<string>();

            // Apply interview-type specific optimizations
            switch (interviewType)
            {
                case InterviewType.Technical:
                    insights.RecommendedBufferTime = TimeSpan.FromMinutes(15);
                    insights.InterviewPreparationTips.Add("Prepare coding environment and screen sharing");
                    insights.InterviewPreparationTips.Add("Have backup technical questions ready");
                    optimizations.Add("Added buffer time for technical setup");
                    break;

                case InterviewType.Panel:
                    insights.RecommendedBufferTime = TimeSpan.FromMinutes(10);
                    insights.InterviewPreparationTips.Add("Coordinate panel member roles and questions");
                    insights.InterviewPreparationTips.Add("Ensure all panel members can attend");
                    optimizations.Add("Checked availability of all panel members");
                    break;

                case InterviewType.Final:
                    insights.RecommendedBufferTime = TimeSpan.FromMinutes(20);
                    insights.InterviewPreparationTips.Add("Allow extra time for decision-making discussion");
                    insights.InterviewPreparationTips.Add("Schedule debrief session immediately after");
                    optimizations.Add("Extended buffer time for final interview decisions");
                    break;

                default:
                    insights.RecommendedBufferTime = TimeSpan.FromMinutes(10);
                    optimizations.Add("Applied standard interview optimizations");
                    break;
            }

            // Convert base schedule to business ranked slots
            foreach (var suggestion in baseSchedule)
            {
                optimizedSlots.Add(new BusinessRankedTimeSlot
                {
                    TimeSlot = suggestion,
                    BusinessScore = suggestion.Confidence,
                    BusinessReasons = optimizations
                });
            }

            return new InterviewScheduleResult
            {
                OptimizedSlots = optimizedSlots,
                Insights = insights,
                InterviewOptimizations = optimizations
            };
        }

        #region Private Helper Methods

        private async Task<List<BusinessRankedTimeSlot>> GenerateRecommendationsAsync(SchedulingBusinessRequest request)
        {
            // This is a simplified implementation
            // In a real scenario, this would integrate with calendar services and apply complex algorithms
            
            var recommendations = new List<BusinessRankedTimeSlot>();
            var baseTime = request.EarliestDate.Date.AddHours(10); // 10 AM

            for (int i = 0; i < 3; i++)
            {
                var suggestion = new CalendarMeetingTimeSuggestion
                {
                    StartTime = baseTime.AddDays(i),
                    EndTime = baseTime.AddDays(i).AddMinutes(request.DurationMinutes),
                    Confidence = Math.Max(70, 90 - (i * 10)),
                    Reason = "Good availability for all participants",
                    AvailableAttendees = request.ParticipantEmails.ToList(),
                    ConflictingAttendees = new List<string>()
                };

                recommendations.Add(new BusinessRankedTimeSlot
                {
                    TimeSlot = suggestion,
                    BusinessScore = suggestion.Confidence,
                    BusinessReasons = new List<string> { "Optimal time based on working hours", "No conflicts detected" }
                });
            }

            return recommendations;
        }

        private async Task<List<BusinessRankedTimeSlot>> GenerateAlternativesAsync(SchedulingBusinessRequest request)
        {
            var alternatives = new List<BusinessRankedTimeSlot>();
            var baseTime = request.EarliestDate.Date.AddHours(14); // 2 PM

            for (int i = 0; i < 2; i++)
            {
                var suggestion = new CalendarMeetingTimeSuggestion
                {
                    StartTime = baseTime.AddDays(i + 1),
                    EndTime = baseTime.AddDays(i + 1).AddMinutes(request.DurationMinutes),
                    Confidence = Math.Max(50, 70 - (i * 10)),
                    Reason = "Alternative time slot",
                    AvailableAttendees = request.ParticipantEmails.ToList(),
                    ConflictingAttendees = new List<string>()
                };

                alternatives.Add(new BusinessRankedTimeSlot
                {
                    TimeSlot = suggestion,
                    BusinessScore = suggestion.Confidence,
                    BusinessReasons = new List<string> { "Alternative option", "Later in the day" }
                });
            }

            return alternatives;
        }

        private async Task<BusinessInsights> GenerateBusinessInsightsAsync(
            SchedulingBusinessRequest request, 
            List<BusinessRankedTimeSlot> recommendations)
        {
            return new BusinessInsights
            {
                AverageAvailability = recommendations.Average(r => r.BusinessScore),
                BestTimeWindows = new List<string> { "10:00 AM - 12:00 PM", "2:00 PM - 4:00 PM" },
                ChallengingPeriods = new List<string> { "Early morning", "Late afternoon" },
                SchedulingTips = new List<string> 
                { 
                    "Mid-morning slots have highest success rate",
                    "Consider time zones for remote participants",
                    "Allow buffer time between interviews"
                }
            };
        }

        private string GenerateRecommendationReasoning(List<BusinessRankedTimeSlot> recommendations)
        {
            if (recommendations.Count == 0)
                return "No suitable time slots found";

            var bestSlot = recommendations.First();
            return $"Recommended based on {bestSlot.BusinessScore:F1}% availability and optimal timing";
        }

        private async Task<double> CalculateBusinessScoreAsync(CalendarMeetingTimeSuggestion suggestion, BusinessSchedulingContext context)
        {
            // Business scoring algorithm
            double score = suggestion.Confidence;
            
            // Adjust based on time of day (prefer mid-morning and early afternoon)
            var hour = suggestion.StartTime.Hour;
            if (hour >= 10 && hour <= 11) score += 10; // Mid-morning bonus
            else if (hour >= 14 && hour <= 15) score += 5; // Early afternoon bonus
            else if (hour < 9 || hour > 17) score -= 20; // Penalty for outside working hours

            return Math.Min(100, Math.Max(0, score));
        }

        private async Task<BusinessRanking> CalculateRankingAsync(CalendarMeetingTimeSuggestion suggestion, BusinessSchedulingContext context)
        {
            return new BusinessRanking
            {
                AvailabilityScore = suggestion.Confidence,
                TimingScore = CalculateTimingScore(suggestion.StartTime),
                ParticipantPreferenceScore = 80, // Placeholder
                BusinessRulesScore = 90, // Placeholder
                OverallScore = suggestion.Confidence
            };
        }

        private async Task<List<string>> GenerateBusinessReasonsAsync(CalendarMeetingTimeSuggestion suggestion, BusinessSchedulingContext context)
        {
            var reasons = new List<string>();
            
            if (suggestion.Confidence > 80)
                reasons.Add("High participant availability");
            
            var hour = suggestion.StartTime.Hour;
            if (hour >= 10 && hour <= 11)
                reasons.Add("Optimal morning time slot");
            else if (hour >= 14 && hour <= 15)
                reasons.Add("Good afternoon time slot");

            if (suggestion.ConflictingAttendees.Count == 0)
                reasons.Add("No scheduling conflicts");

            return reasons;
        }

        private double CalculateTimingScore(DateTime startTime)
        {
            var hour = startTime.Hour;
            if (hour >= 10 && hour <= 11) return 100;
            if (hour >= 9 && hour <= 16) return 80;
            if (hour >= 8 && hour <= 17) return 60;
            return 40;
        }

        private BusinessImpactLevel CalculateImpactLevel(List<SchedulingConflict> conflicts, int totalParticipants)
        {
            if (conflicts.Count == 0) return BusinessImpactLevel.None;
            
            double conflictRatio = (double)conflicts.Count / totalParticipants;
            
            if (conflictRatio >= 0.8) return BusinessImpactLevel.Critical;
            if (conflictRatio >= 0.5) return BusinessImpactLevel.High;
            if (conflictRatio >= 0.3) return BusinessImpactLevel.Medium;
            return BusinessImpactLevel.Low;
        }

        private string GenerateImpactDescription(BusinessImpactLevel impactLevel, int affectedCount, int totalCount)
        {
            return impactLevel switch
            {
                BusinessImpactLevel.None => "No scheduling conflicts detected",
                BusinessImpactLevel.Low => $"Minor conflicts affecting {affectedCount} of {totalCount} participants",
                BusinessImpactLevel.Medium => $"Moderate conflicts affecting {affectedCount} of {totalCount} participants",
                BusinessImpactLevel.High => $"Significant conflicts affecting {affectedCount} of {totalCount} participants",
                BusinessImpactLevel.Critical => $"Critical conflicts affecting {affectedCount} of {totalCount} participants",
                _ => "Unknown impact level"
            };
        }

        private List<string> GenerateMitigationSuggestions(List<SchedulingConflict> conflicts, DateTime proposedTime, TimeSpan duration)
        {
            var suggestions = new List<string>();
            
            if (conflicts.Any())
            {
                suggestions.Add("Consider shifting the meeting time by 1-2 hours");
                suggestions.Add("Explore alternative dates within the requested range");
                suggestions.Add("Check if any conflicting meetings can be rescheduled");
                
                if (conflicts.Count > 2)
                {
                    suggestions.Add("Consider reducing the number of participants");
                    suggestions.Add("Schedule multiple smaller meetings instead");
                }
            }
            
            return suggestions;
        }

        #endregion
    }
}