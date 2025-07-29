using InterviewBot.Domain.Interfaces;

namespace InterviewBot.Infrastructure.Telemetry
{
    public class TelemetryService : ITelemetryService
    {
        private readonly ILogger<TelemetryService> _logger;
        
        public TelemetryService(ILogger<TelemetryService> logger)
        {
            _logger = logger;
        }
        
        public void TrackAvailabilityLookup(string userId, int participantCount, TimeSpan duration)
        {
            _logger.LogInformation("Availability lookup: User={UserId}, Participants={ParticipantCount}, Duration={Duration}", 
                userId, participantCount, duration);
                
            // In a real implementation, this would send telemetry to Application Insights
            // or another telemetry service
        }
        
        public void TrackInterviewScheduled(string userId, Guid interviewId, int participantCount)
        {
            _logger.LogInformation("Interview scheduled: User={UserId}, InterviewId={InterviewId}, Participants={ParticipantCount}", 
                userId, interviewId, participantCount);
                
            // Track custom metrics
        }
        
        public void TrackException(Exception ex, string? userId = null, string? operation = null)
        {
            _logger.LogError(ex, "Exception in operation {Operation} for user {UserId}: {Message}", 
                operation ?? "Unknown", userId ?? "Anonymous", ex.Message);
                
            // Track exceptions for monitoring and alerting
        }
    }
}