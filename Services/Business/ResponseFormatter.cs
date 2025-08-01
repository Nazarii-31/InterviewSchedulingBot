using System.Globalization;
using System.Text;

namespace InterviewSchedulingBot.Services.Business
{
    /// <summary>
    /// Service for formatting interview scheduling responses with consistent formatting
    /// </summary>
    public interface IResponseFormatter
    {
        string FormatSlotResponse(
            List<EnhancedRankedTimeSlot> slots, 
            SlotQueryCriteria criteria);
        
        string FormatNoSlotsResponse(SlotQueryCriteria criteria);
    }

    public class ResponseFormatter : IResponseFormatter
    {
        private readonly ILogger<ResponseFormatter> _logger;
        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

        public ResponseFormatter(ILogger<ResponseFormatter> logger)
        {
            _logger = logger;
        }

        public string FormatSlotResponse(
            List<EnhancedRankedTimeSlot> slots, 
            SlotQueryCriteria criteria)
        {
            if (!slots.Any())
            {
                return FormatNoSlotsResponse(criteria);
            }

            var response = new StringBuilder();
            
            // Ensure all slots start at quarter hours (00, 15, 30, 45)
            var quarterHourSlots = slots.Where(slot => slot.StartTime.Minute % 15 == 0).ToList();
            
            if (!quarterHourSlots.Any())
            {
                return FormatNoSlotsResponse(criteria);
            }

            // Group slots by day
            var slotsByDay = quarterHourSlots
                .GroupBy(s => s.StartTime.Date)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Add header with time period information
            var timeRangeInfo = GetTimeRangeDescription(criteria);
            response.AppendLine($"Here are the available {criteria.DurationMinutes}-minute time slots{timeRangeInfo}:");

            // Display all days with their slots
            foreach (var dayGroup in slotsByDay.OrderBy(kvp => kvp.Key))
            {
                var day = dayGroup.Key;
                var dayName = day.ToString("dddd", EnglishCulture);
                var dateStr = day.ToString("dd.MM.yyyy", EnglishCulture);
                
                // Add day header with format "Monday [04.08.2025]"
                response.AppendLine();
                response.AppendLine($"{dayName} [{dateStr}]");
                response.AppendLine();

                // Add all slots for this day
                var daySlots = dayGroup.Value.OrderBy(s => s.StartTime).ToList();
                foreach (var slot in daySlots)
                {
                    var timeRange = FormatTimeRange(slot);
                    var explanation = $"({slot.Explanation})";
                    
                    if (slot.IsRecommended)
                    {
                        response.AppendLine($"{timeRange} {explanation} ⭐ RECOMMENDED - {slot.RecommendationReason}");
                    }
                    else
                    {
                        response.AppendLine($"{timeRange} {explanation}");
                    }
                }
            }

            response.AppendLine();
            response.AppendLine("Please let me know which time slot works best for you.");

            return response.ToString();
        }

        public string FormatNoSlotsResponse(SlotQueryCriteria criteria)
        {
            var timeRangeInfo = GetTimeRangeDescription(criteria);
            var response = new StringBuilder();
            
            response.AppendLine($"I couldn't find any available {criteria.DurationMinutes}-minute time slots{timeRangeInfo}.");
            
            if (criteria.ParticipantEmails.Any())
            {
                response.AppendLine();
                response.AppendLine("This might be because:");
                response.AppendLine("• All participants have conflicts during the requested time");
                response.AppendLine("• The time range is outside normal working hours");
                response.AppendLine("• All available slots are already booked");
                response.AppendLine();
                response.AppendLine("Would you like me to suggest alternative times or check a different date range?");
            }

            return response.ToString();
        }

        private string GetTimeRangeDescription(SlotQueryCriteria criteria)
        {
            if (!string.IsNullOrEmpty(criteria.RelativeDay))
            {
                return criteria.RelativeDay.ToLower() switch
                {
                    "tomorrow" => GetTomorrowDescription(criteria.StartDate),
                    "next week" => GetNextWeekDescription(criteria.StartDate, criteria.EndDate),
                    "today" => " for today",
                    _ => $" for {criteria.RelativeDay}"
                };
            }

            if (!string.IsNullOrEmpty(criteria.SpecificDay))
            {
                var dayName = criteria.SpecificDay;
                var dateStr = criteria.StartDate.ToString("dd.MM.yyyy", EnglishCulture);
                return $" for {dayName} [{dateStr}]";
            }

            if (criteria.StartDate.Date == criteria.EndDate.Date)
            {
                var dayName = criteria.StartDate.ToString("dddd", EnglishCulture);
                var dateStr = criteria.StartDate.ToString("dd.MM.yyyy", EnglishCulture);
                return $" for {dayName} [{dateStr}]";
            }

            var startDateStr = criteria.StartDate.ToString("dd.MM.yyyy", EnglishCulture);
            var endDateStr = criteria.EndDate.ToString("dd.MM.yyyy", EnglishCulture);
            return $" for the period [{startDateStr} - {endDateStr}]";
        }

        private string GetTomorrowDescription(DateTime tomorrowDate)
        {
            var dayName = tomorrowDate.ToString("dddd", EnglishCulture);
            var dateStr = tomorrowDate.ToString("dd.MM.yyyy", EnglishCulture);
            return $" for {dayName} [{dateStr}]";
        }

        private string GetNextWeekDescription(DateTime startDate, DateTime endDate)
        {
            var startDateStr = startDate.ToString("dd.MM.yyyy", EnglishCulture);
            var endDateStr = endDate.ToString("dd.MM.yyyy", EnglishCulture);
            return $" for next week [{startDateStr} - {endDateStr}]";
        }

        private string FormatTimeRange(EnhancedRankedTimeSlot slot)
        {
            var startTimeStr = slot.StartTime.ToString("HH:mm", EnglishCulture);
            var endTimeStr = slot.EndTime.ToString("HH:mm", EnglishCulture);
            return $"{startTimeStr} - {endTimeStr}";
        }
    }

    /// <summary>
    /// Extension methods for consistent date formatting
    /// </summary>
    public static class DateFormattingExtensions
    {
        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

        public static string ToSchedulingDayFormat(this DateTime date)
        {
            var dayName = date.ToString("dddd", EnglishCulture);
            var dateStr = date.ToString("dd.MM.yyyy", EnglishCulture);
            return $"{dayName} [{dateStr}]";
        }

        public static string ToSchedulingDateFormat(this DateTime date)
        {
            return date.ToString("dd.MM.yyyy", EnglishCulture);
        }

        public static string ToSchedulingTimeFormat(this DateTime time)
        {
            return time.ToString("HH:mm", EnglishCulture);
        }

        public static string ToSchedulingTimeRangeFormat(this DateTime startTime, DateTime endTime)
        {
            return $"{startTime.ToSchedulingTimeFormat()} - {endTime.ToSchedulingTimeFormat()}";
        }
    }
}
