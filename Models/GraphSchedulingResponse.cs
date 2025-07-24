using Microsoft.Graph.Models;
using LocalMeetingTimeSuggestion = InterviewSchedulingBot.Models.MeetingTimeSuggestion;

namespace InterviewSchedulingBot.Models
{
    public class GraphSchedulingResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<LocalMeetingTimeSuggestion> MeetingTimeSuggestions { get; set; } = new List<LocalMeetingTimeSuggestion>();
        public GraphSchedulingRequest? OriginalRequest { get; set; }
        public DateTime SearchTimestamp { get; set; }

        public bool HasSuggestions => MeetingTimeSuggestions.Count > 0;

        public string FormattedSuggestionsText
        {
            get
            {
                if (!HasSuggestions)
                    return "No meeting time suggestions available.";

                var suggestions = MeetingTimeSuggestions.Take(10).Select((suggestion, index) => 
                {
                    if (suggestion.MeetingTimeSlot?.Start?.DateTime == null || 
                        suggestion.MeetingTimeSlot?.End?.DateTime == null)
                        return $"{index + 1}. Invalid meeting time data";
                        
                    var startTime = DateTime.Parse(suggestion.MeetingTimeSlot.Start.DateTime);
                    var endTime = DateTime.Parse(suggestion.MeetingTimeSlot.End.DateTime);
                    var confidence = suggestion.Confidence > 0 ? 
                        $" (Confidence: {suggestion.Confidence * 100:F0}%)" : "";
                    
                    return $"{index + 1}. {startTime:ddd, MMM dd} from {startTime:HH:mm} to {endTime:HH:mm}{confidence}";
                });

                return string.Join("\n", suggestions);
            }
        }

        public string FormattedSuggestionsWithoutBooking
        {
            get
            {
                if (!HasSuggestions)
                    return "No meeting time suggestions available.";

                var suggestions = MeetingTimeSuggestions.Take(10).Select((suggestion, index) => 
                {
                    if (suggestion.MeetingTimeSlot?.Start?.DateTime == null || 
                        suggestion.MeetingTimeSlot?.End?.DateTime == null)
                        return $"**Option {index + 1}**: Invalid meeting time data";
                        
                    var startTime = DateTime.Parse(suggestion.MeetingTimeSlot.Start.DateTime);
                    var endTime = DateTime.Parse(suggestion.MeetingTimeSlot.End.DateTime);
                    var confidence = suggestion.Confidence > 0 ? 
                        $" (Confidence: {suggestion.Confidence * 100:F0}%)" : "";
                    var reason = !string.IsNullOrEmpty(suggestion.SuggestionReason) ? 
                        $"\n   📋 {suggestion.SuggestionReason}" : "";
                    
                    return $"**Option {index + 1}**: {startTime:dddd, MMM dd} from {startTime:HH:mm} to {endTime:HH:mm}{confidence}{reason}";
                });

                return string.Join("\n\n", suggestions);
            }
        }

        public string FormattedSuggestionsWithBookingText
        {
            get
            {
                if (!HasSuggestions)
                    return "No meeting time suggestions available.";

                var suggestions = MeetingTimeSuggestions.Take(10).Select((suggestion, index) => 
                {
                    if (suggestion.MeetingTimeSlot?.Start?.DateTime == null || 
                        suggestion.MeetingTimeSlot?.End?.DateTime == null)
                        return $"**Option {index + 1}**: Invalid meeting time data";
                        
                    var startTime = DateTime.Parse(suggestion.MeetingTimeSlot.Start.DateTime);
                    var endTime = DateTime.Parse(suggestion.MeetingTimeSlot.End.DateTime);
                    var confidence = suggestion.Confidence > 0 ? 
                        $" (Confidence: {suggestion.Confidence * 100:F0}%)" : "";
                    
                    return $"**Option {index + 1}**: {startTime:ddd, MMM dd} from {startTime:HH:mm} to {endTime:HH:mm}{confidence}\n" +
                           $"   _Reply with 'book {index + 1}' to book this time_";
                });

                return string.Join("\n\n", suggestions);
            }
        }

        public static GraphSchedulingResponse CreateSuccess(
            List<LocalMeetingTimeSuggestion> suggestions, 
            GraphSchedulingRequest request)
        {
            return new GraphSchedulingResponse
            {
                IsSuccess = true,
                Message = $"Found {suggestions.Count} optimal meeting time suggestions",
                MeetingTimeSuggestions = suggestions,
                OriginalRequest = request,
                SearchTimestamp = DateTime.Now
            };
        }

        public static GraphSchedulingResponse CreateFailure(
            string message, 
            GraphSchedulingRequest request)
        {
            return new GraphSchedulingResponse
            {
                IsSuccess = false,
                Message = message,
                MeetingTimeSuggestions = new List<LocalMeetingTimeSuggestion>(),
                OriginalRequest = request,
                SearchTimestamp = DateTime.Now
            };
        }
    }
}