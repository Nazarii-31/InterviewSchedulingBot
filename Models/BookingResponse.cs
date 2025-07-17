namespace InterviewSchedulingBot.Models
{
    public class BookingResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string EventId { get; set; } = string.Empty;
        public BookingRequest? OriginalRequest { get; set; }
        public DateTime BookingTimestamp { get; set; }

        public static BookingResponse CreateSuccess(string eventId, BookingRequest request)
        {
            return new BookingResponse
            {
                IsSuccess = true,
                Message = "Meeting booked successfully",
                EventId = eventId,
                OriginalRequest = request,
                BookingTimestamp = DateTime.Now
            };
        }

        public static BookingResponse CreateFailure(string message, BookingRequest request)
        {
            return new BookingResponse
            {
                IsSuccess = false,
                Message = message,
                EventId = string.Empty,
                OriginalRequest = request,
                BookingTimestamp = DateTime.Now
            };
        }
    }
}