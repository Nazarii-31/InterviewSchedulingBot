using Microsoft.AspNetCore.Mvc;
using InterviewSchedulingBot.Interfaces.Business;
using InterviewSchedulingBot.Interfaces.Integration;
using System.ComponentModel.DataAnnotations;

namespace InterviewSchedulingBot.Controllers.Api
{
    /// <summary>
    /// RESTful API for interview scheduling operations
    /// Provides communication interface between business and integration layers
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class SchedulingController : ControllerBase
    {
        private readonly ISchedulingBusinessService _businessService;
        private readonly ITeamsIntegrationService _teamsService;
        private readonly ILogger<SchedulingController> _logger;

        public SchedulingController(
            ISchedulingBusinessService businessService,
            ITeamsIntegrationService teamsService,
            ILogger<SchedulingController> logger)
        {
            _businessService = businessService;
            _teamsService = teamsService;
            _logger = logger;
        }

        /// <summary>
        /// Find optimal interview time slots
        /// </summary>
        /// <param name="request">Scheduling requirements</param>
        /// <returns>Optimal time slot suggestions</returns>
        [HttpPost("find-optimal-slots")]
        [ProducesResponseType(typeof(ApiSchedulingResponse), 200)]
        [ProducesResponseType(typeof(ApiErrorResponse), 400)]
        [ProducesResponseType(typeof(ApiErrorResponse), 500)]
        public async Task<ActionResult<ApiSchedulingResponse>> FindOptimalSlots(
            [FromBody] ApiSchedulingRequest request)
        {
            _logger.LogInformation("Finding optimal interview slots for {ParticipantCount} participants", 
                request.ParticipantEmails.Count);

            try
            {
                // Validate request
                var validationResult = ValidateApiRequest(request);
                if (!validationResult.IsValid)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Error = "Validation failed",
                        Details = validationResult.Errors.Select(e => e.Message).ToList()
                    });
                }

                // Convert API request to business request
                var businessRequest = MapToBusinessRequest(request);

                // Use business service to find optimal slots
                var businessResult = await _businessService.FindOptimalInterviewSlotsAsync(businessRequest);

                // Convert business result to API response
                var apiResponse = MapToApiResponse(businessResult);

                return Ok(apiResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding optimal interview slots");
                return StatusCode(500, new ApiErrorResponse
                {
                    Error = "Internal server error",
                    Details = new List<string> { "An error occurred while processing your request" }
                });
            }
        }

        /// <summary>
        /// Validate scheduling requirements
        /// </summary>
        /// <param name="request">Scheduling request to validate</param>
        /// <returns>Validation result</returns>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(ApiValidationResponse), 200)]
        [ProducesResponseType(typeof(ApiErrorResponse), 400)]
        public async Task<ActionResult<ApiValidationResponse>> ValidateSchedulingRequest(
            [FromBody] ApiSchedulingRequest request)
        {
            _logger.LogInformation("Validating scheduling request");

            try
            {
                var businessRequest = MapToBusinessRequest(request);
                var validationResult = await _businessService.ValidateSchedulingRequestAsync(businessRequest);

                return Ok(new ApiValidationResponse
                {
                    IsValid = validationResult.IsValid,
                    Errors = validationResult.Errors.Select(e => new ApiValidationError
                    {
                        Code = e.Code,
                        Message = e.Message,
                        Field = e.Field
                    }).ToList(),
                    Warnings = validationResult.Warnings.Select(w => new ApiValidationWarning
                    {
                        Code = w.Code,
                        Message = w.Message,
                        Field = w.Field,
                        Suggestion = w.Suggestion
                    }).ToList(),
                    Suggestions = validationResult.Suggestions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating scheduling request");
                return BadRequest(new ApiErrorResponse
                {
                    Error = "Validation error",
                    Details = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Analyze scheduling conflicts for a proposed time
        /// Note: In Teams context, calendar access should go through Teams integration
        /// </summary>
        /// <param name="request">Conflict analysis request</param>
        /// <returns>Conflict analysis result</returns>
        [HttpPost("analyze-conflicts")]
        [ProducesResponseType(typeof(ApiConflictAnalysis), 200)]
        [ProducesResponseType(typeof(ApiErrorResponse), 400)]
        public async Task<ActionResult<ApiConflictAnalysis>> AnalyzeConflicts(
            [FromBody] ApiConflictAnalysisRequest request)
        {
            _logger.LogInformation("Analyzing conflicts for proposed time {ProposedTime}", request.ProposedTime);

            try
            {
                // Note: For Teams bot context, calendar access should go through TeamsIntegrationService
                // This REST API endpoint is limited without proper Teams bot context
                // In a real implementation, conflict analysis would be called from within Teams bot flow
                
                // For API demonstration purposes, return a mock analysis
                _logger.LogWarning("Conflict analysis through REST API is limited. Use Teams bot interface for full calendar access.");
                
                return Ok(new ApiConflictAnalysis
                {
                    HasConflicts = false,
                    ImpactLevel = "None",
                    ImpactDescription = "Calendar access requires Teams bot context. Use Teams interface for full functionality.",
                    AffectedParticipants = new List<string>(),
                    MitigationSuggestions = new List<string> { "Use Teams bot interface for calendar-based conflict analysis" },
                    Conflicts = new List<ApiSchedulingConflict>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing conflicts");
                return BadRequest(new ApiErrorResponse
                {
                    Error = "Conflict analysis error",
                    Details = new List<string> { ex.Message }
                });
            }
        }

        #region Private Helper Methods

        private SchedulingBusinessRequest MapToBusinessRequest(ApiSchedulingRequest request)
        {
            return new SchedulingBusinessRequest
            {
                ParticipantEmails = request.ParticipantEmails,
                DurationMinutes = request.DurationMinutes,
                EarliestDate = request.EarliestDate,
                LatestDate = request.LatestDate,
                InterviewType = Enum.Parse<InterviewType>(request.InterviewType, true),
                Priority = Enum.Parse<Priority>(request.Priority, true),
                RequesterId = request.RequesterId,
                Department = request.Department
            };
        }

        private ApiSchedulingResponse MapToApiResponse(SchedulingBusinessResult businessResult)
        {
            return new ApiSchedulingResponse
            {
                RecommendedSlots = businessResult.RecommendedSlots.Select(slot => new ApiTimeSlot
                {
                    StartTime = slot.TimeSlot.StartTime,
                    EndTime = slot.TimeSlot.EndTime,
                    BusinessScore = slot.BusinessScore,
                    Confidence = slot.TimeSlot.Confidence,
                    Reasons = slot.BusinessReasons
                }).ToList(),
                AlternativeSlots = businessResult.AlternativeSlots.Select(slot => new ApiTimeSlot
                {
                    StartTime = slot.TimeSlot.StartTime,
                    EndTime = slot.TimeSlot.EndTime,
                    BusinessScore = slot.BusinessScore,
                    Confidence = slot.TimeSlot.Confidence,
                    Reasons = slot.BusinessReasons
                }).ToList(),
                RecommendationReasoning = businessResult.RecommendationReasoning,
                Insights = new ApiBusinessInsights
                {
                    AverageAvailability = businessResult.Insights.AverageAvailability,
                    BestTimeWindows = businessResult.Insights.BestTimeWindows,
                    ChallengingPeriods = businessResult.Insights.ChallengingPeriods,
                    SchedulingTips = businessResult.Insights.SchedulingTips
                }
            };
        }

        private ApiConflictAnalysis MapToApiConflictAnalysis(ConflictAnalysis conflictAnalysis)
        {
            return new ApiConflictAnalysis
            {
                HasConflicts = conflictAnalysis.HasConflicts,
                ImpactLevel = conflictAnalysis.ImpactLevel.ToString(),
                ImpactDescription = conflictAnalysis.ImpactDescription,
                AffectedParticipants = conflictAnalysis.AffectedParticipants,
                MitigationSuggestions = conflictAnalysis.MitigationSuggestions,
                Conflicts = conflictAnalysis.Conflicts.Select(c => new ApiSchedulingConflict
                {
                    ParticipantEmail = c.ParticipantEmail,
                    Type = c.Type.ToString(),
                    ConflictStart = c.ConflictStart,
                    ConflictEnd = c.ConflictEnd,
                    Description = c.ConflictDescription,
                    Severity = c.Severity.ToString(),
                    CanBeResolved = c.CanBeResolved
                }).ToList()
            };
        }

        private ApiValidationResponse ValidateApiRequest(ApiSchedulingRequest request)
        {
            var errors = new List<ApiValidationError>();
            var warnings = new List<ApiValidationWarning>();

            if (request.ParticipantEmails?.Count == 0)
                errors.Add(new ApiValidationError { Code = "MISSING_PARTICIPANTS", Message = "At least one participant is required", Field = "ParticipantEmails" });

            if (request.DurationMinutes <= 0)
                errors.Add(new ApiValidationError { Code = "INVALID_DURATION", Message = "Duration must be positive", Field = "DurationMinutes" });

            if (request.EarliestDate >= request.LatestDate)
                errors.Add(new ApiValidationError { Code = "INVALID_DATE_RANGE", Message = "Earliest date must be before latest date", Field = "EarliestDate,LatestDate" });

            if (request.DurationMinutes > 480) // 8 hours
                warnings.Add(new ApiValidationWarning { Code = "LONG_DURATION", Message = "Meeting duration is unusually long", Field = "DurationMinutes", Suggestion = "Consider breaking into multiple sessions" });

            return new ApiValidationResponse
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        #endregion
    }

    #region API Models

    /// <summary>
    /// API request model for scheduling operations
    /// </summary>
    public class ApiSchedulingRequest
    {
        [Required]
        public List<string> ParticipantEmails { get; set; } = new();
        
        [Required]
        [Range(1, 480)]
        public int DurationMinutes { get; set; }
        
        [Required]
        public DateTime EarliestDate { get; set; }
        
        [Required]
        public DateTime LatestDate { get; set; }
        
        public string InterviewType { get; set; } = "General";
        public string Priority { get; set; } = "Normal";
        public string? RequesterId { get; set; }
        public string? Department { get; set; }
    }

    /// <summary>
    /// API response model for scheduling operations
    /// </summary>
    public class ApiSchedulingResponse
    {
        public List<ApiTimeSlot> RecommendedSlots { get; set; } = new();
        public List<ApiTimeSlot> AlternativeSlots { get; set; } = new();
        public string? RecommendationReasoning { get; set; }
        public ApiBusinessInsights Insights { get; set; } = new();
    }

    public class ApiTimeSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double BusinessScore { get; set; }
        public double Confidence { get; set; }
        public List<string> Reasons { get; set; } = new();
    }

    public class ApiBusinessInsights
    {
        public double AverageAvailability { get; set; }
        public List<string> BestTimeWindows { get; set; } = new();
        public List<string> ChallengingPeriods { get; set; } = new();
        public List<string> SchedulingTips { get; set; } = new();
    }

    public class ApiConflictAnalysisRequest
    {
        [Required]
        public DateTime ProposedTime { get; set; }
        
        [Required]
        [Range(1, 480)]
        public int DurationMinutes { get; set; }
        
        [Required]
        public List<string> ParticipantEmails { get; set; } = new();
        
        [Required]
        public string AccessToken { get; set; } = string.Empty;
    }

    public class ApiConflictAnalysis
    {
        public bool HasConflicts { get; set; }
        public string ImpactLevel { get; set; } = string.Empty;
        public string? ImpactDescription { get; set; }
        public List<string> AffectedParticipants { get; set; } = new();
        public List<string> MitigationSuggestions { get; set; } = new();
        public List<ApiSchedulingConflict> Conflicts { get; set; } = new();
    }

    public class ApiSchedulingConflict
    {
        public string ParticipantEmail { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime ConflictStart { get; set; }
        public DateTime ConflictEnd { get; set; }
        public string? Description { get; set; }
        public string Severity { get; set; } = string.Empty;
        public bool CanBeResolved { get; set; }
    }

    public class ApiValidationResponse
    {
        public bool IsValid { get; set; }
        public List<ApiValidationError> Errors { get; set; } = new();
        public List<ApiValidationWarning> Warnings { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
    }

    public class ApiValidationError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Field { get; set; }
    }

    public class ApiValidationWarning
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Field { get; set; }
        public string? Suggestion { get; set; }
    }

    public class ApiErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public List<string> Details { get; set; } = new();
    }

    #endregion
}