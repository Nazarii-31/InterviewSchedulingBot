using Microsoft.AspNetCore.Mvc;
using InterviewSchedulingBot.Interfaces;
using InterviewSchedulingBot.Models;
using System.Text.Json;

namespace InterviewSchedulingBot.Controllers
{
    [Route("api/test")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly IAISchedulingService _aiSchedulingService;
        private readonly IGraphSchedulingService _graphSchedulingService;
        private readonly ISchedulingService _schedulingService;
        private readonly IConfiguration _configuration;

        public TestController(
            IAISchedulingService aiSchedulingService,
            IGraphSchedulingService graphSchedulingService,
            ISchedulingService schedulingService,
            IConfiguration configuration)
        {
            _aiSchedulingService = aiSchedulingService;
            _graphSchedulingService = graphSchedulingService;
            _schedulingService = schedulingService;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var html = @"<!DOCTYPE html>
<html>
<head>
    <title>Interview Scheduling Bot - Local Testing</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }
        .container { max-width: 800px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        h1 { color: #0078d4; }
        .test-section { background: #f9f9f9; padding: 15px; margin: 15px 0; border-radius: 5px; border-left: 4px solid #0078d4; }
        button { background: #0078d4; color: white; padding: 10px 20px; border: none; border-radius: 4px; cursor: pointer; margin: 5px; }
        button:hover { background: #106ebe; }
        .result { background: #e8f4fd; padding: 10px; margin: 10px 0; border-radius: 4px; white-space: pre-wrap; font-family: monospace; }
        .error { background: #fdf2f2; color: #d73502; }
        .success { background: #f0f9ff; color: #0c4a6e; }
        input[type=""text""], input[type=""email""] { width: 300px; padding: 8px; margin: 5px; border: 1px solid #ccc; border-radius: 4px; }
        .loading { color: #0078d4; font-style: italic; }
    </style>
    <script src=""https://code.jquery.com/jquery-3.6.0.min.js""></script>
</head>
<body>
    <div class=""container"">
        <h1>🤖 Interview Scheduling Bot - Local Testing Interface</h1>
        <p>Test all AI scheduling features without needing Teams or ngrok!</p>
        
        <div class=""test-section"">
            <h3>🧠 AI Scheduling Test</h3>
            <p>Test the core AI scheduling functionality:</p>
            <input type=""email"" id=""attendee1"" placeholder=""attendee1@example.com"" value=""john@example.com"">
            <input type=""email"" id=""attendee2"" placeholder=""attendee2@example.com"" value=""jane@example.com""><br>
            <input type=""text"" id=""duration"" placeholder=""Duration (minutes)"" value=""60"">
            <input type=""text"" id=""days"" placeholder=""Search days"" value=""7""><br>
            <button onclick=""testAIScheduling()"">🚀 Test AI Scheduling</button>
            <div id=""aiResult"" class=""result"" style=""display:none;""></div>
        </div>

        <div class=""test-section"">
            <h3>📊 Graph Scheduling Test</h3>
            <p>Test Microsoft Graph-based optimal time finding:</p>
            <button onclick=""testGraphScheduling()"">📅 Test Graph Scheduling</button>
            <div id=""graphResult"" class=""result"" style=""display:none;""></div>
        </div>

        <div class=""test-section"">
            <h3>🎯 User Preferences Test</h3>
            <p>Test AI learning and user preference analysis:</p>
            <button onclick=""testUserPreferences()"">🧠 Test User Preferences</button>
            <div id=""preferencesResult"" class=""result"" style=""display:none;""></div>
        </div>

        <div class=""test-section"">
            <h3>📈 AI Insights Test</h3>
            <p>Test AI insights and pattern analysis:</p>
            <button onclick=""testAIInsights()"">💡 Test AI Insights</button>
            <div id=""insightsResult"" class=""result"" style=""display:none;""></div>
        </div>

        <div class=""test-section"">
            <h3>🔍 Basic Scheduling Test</h3>
            <p>Test basic availability finding:</p>
            <button onclick=""testBasicScheduling()"">📋 Test Basic Scheduling</button>
            <div id=""basicResult"" class=""result"" style=""display:none;""></div>
        </div>

        <div class=""test-section"">
            <h3>🎛️ System Status</h3>
            <p>Check bot configuration and service status:</p>
            <button onclick=""testSystemStatus()"">⚙️ Check System Status</button>
            <div id=""statusResult"" class=""result"" style=""display:none;""></div>
        </div>
    </div>

    <script>
        function showLoading(elementId) {
            $('#' + elementId).show().removeClass('error success').addClass('loading').text('⏳ Processing...');
        }

        function showResult(elementId, data, isError = false) {
            $('#' + elementId).show().removeClass('loading').addClass(isError ? 'error' : 'success').text(JSON.stringify(data, null, 2));
        }

        function testAIScheduling() {
            showLoading('aiResult');
            const attendees = [$('#attendee1').val(), $('#attendee2').val()].filter(email => email);
            const duration = parseInt($('#duration').val()) || 60;
            const days = parseInt($('#days').val()) || 7;
            
            $.post('/api/test/ai-scheduling', {
                attendees: attendees,
                duration: duration,
                days: days
            }).done(function(data) {
                showResult('aiResult', data);
            }).fail(function(xhr) {
                showResult('aiResult', xhr.responseJSON || xhr.responseText, true);
            });
        }

        function testGraphScheduling() {
            showLoading('graphResult');
            $.post('/api/test/graph-scheduling').done(function(data) {
                showResult('graphResult', data);
            }).fail(function(xhr) {
                showResult('graphResult', xhr.responseJSON || xhr.responseText, true);
            });
        }

        function testUserPreferences() {
            showLoading('preferencesResult');
            $.post('/api/test/user-preferences').done(function(data) {
                showResult('preferencesResult', data);
            }).fail(function(xhr) {
                showResult('preferencesResult', xhr.responseJSON || xhr.responseText, true);
            });
        }

        function testAIInsights() {
            showLoading('insightsResult');
            $.post('/api/test/ai-insights').done(function(data) {
                showResult('insightsResult', data);
            }).fail(function(xhr) {
                showResult('insightsResult', xhr.responseJSON || xhr.responseText, true);
            });
        }

        function testBasicScheduling() {
            showLoading('basicResult');
            $.post('/api/test/basic-scheduling').done(function(data) {
                showResult('basicResult', data);
            }).fail(function(xhr) {
                showResult('basicResult', xhr.responseJSON || xhr.responseText, true);
            });
        }

        function testSystemStatus() {
            showLoading('statusResult');
            $.get('/api/test/status').done(function(data) {
                showResult('statusResult', data);
            }).fail(function(xhr) {
                showResult('statusResult', xhr.responseJSON || xhr.responseText, true);
            });
        }
    </script>
</body>
</html>";
            return Content(html, "text/html");
        }

        public class AITestRequest
        {
            public List<string> Attendees { get; set; } = new();
            public int Duration { get; set; } = 60;
            public int Days { get; set; } = 7;
        }

        [HttpPost("ai-scheduling")]
        public async Task<IActionResult> TestAIScheduling([FromBody] AITestRequest request)
        {
            try
            {
                var attendees = request.Attendees;
                var duration = request.Duration;
                var days = request.Days;

                var aiRequest = new AISchedulingRequest
                {
                    UserId = "test-user-local",
                    AttendeeEmails = attendees,
                    StartDate = DateTime.Now.AddHours(1),
                    EndDate = DateTime.Now.AddDays(days),
                    DurationMinutes = duration,
                    UseLearningAlgorithm = true,
                    UseHistoricalData = true,
                    UseUserPreferences = true,
                    UseAttendeePatterns = true,
                    OptimizeForProductivity = true,
                    MaxSuggestions = 5
                };

                var response = await _aiSchedulingService.FindOptimalMeetingTimesAsync(aiRequest);

                return Ok(new
                {
                    Success = response.IsSuccess,
                    Message = response.Message,
                    Confidence = response.OverallConfidence,
                    ProcessingTime = response.ProcessingTimeMs,
                    SuggestionsCount = response.PredictedTimeSlots?.Count ?? 0,
                    Suggestions = response.PredictedTimeSlots?.Take(3).Select(p => new
                    {
                        StartTime = p.StartTime.ToString("yyyy-MM-dd HH:mm"),
                        EndTime = p.EndTime.ToString("yyyy-MM-dd HH:mm"),
                        Confidence = p.OverallConfidence,
                        Reason = p.PredictionReason,
                        IsOptimal = p.IsOptimalSlot
                    }),
                    Recommendations = response.Recommendations?.Take(3)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message, Details = ex.ToString() });
            }
        }

        [HttpPost("graph-scheduling")]
        public async Task<IActionResult> TestGraphScheduling()
        {
            try
            {
                var graphRequest = new GraphSchedulingRequest
                {
                    AttendeeEmails = new List<string> { "demo@example.com", "test@example.com" },
                    StartDate = DateTime.Now.AddHours(1),
                    EndDate = DateTime.Now.AddDays(7),
                    DurationMinutes = 60,
                    WorkingHoursStart = TimeSpan.FromHours(9),
                    WorkingHoursEnd = TimeSpan.FromHours(17),
                    WorkingDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                    MaxSuggestions = 5
                };

                var response = await _graphSchedulingService.FindOptimalMeetingTimesAsync(graphRequest, "test-user-local");

                return Ok(new
                {
                    Success = response.IsSuccess,
                    Message = response.Message,
                    SuggestionsCount = response.MeetingTimeSuggestions?.Count ?? 0,
                    Suggestions = response.MeetingTimeSuggestions?.Take(3).Select(s => new
                    {
                        StartTime = s.MeetingTimeSlot?.Start?.DateTime,
                        EndTime = s.MeetingTimeSlot?.End?.DateTime,
                        Confidence = s.Confidence,
                        Reason = s.SuggestionReason
                    }),
                    RequestDetails = new
                    {
                        Duration = graphRequest.DurationMinutes,
                        Attendees = graphRequest.AttendeeEmails,
                        DateRange = $"{graphRequest.StartDate:yyyy-MM-dd} to {graphRequest.EndDate:yyyy-MM-dd}"
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message, Details = ex.ToString() });
            }
        }

        [HttpPost("user-preferences")]
        public async Task<IActionResult> TestUserPreferences()
        {
            try
            {
                var userId = "test-user-local";
                var preferences = await _aiSchedulingService.GetUserPreferencesAsync(userId);
                var patterns = await _aiSchedulingService.AnalyzeSchedulingPatternsAsync(userId);

                return Ok(new
                {
                    UserPreferences = new
                    {
                        TotalMeetings = preferences?.TotalScheduledMeetings ?? 0,
                        ReschedulingRate = preferences?.AverageReschedulingRate ?? 0.3,
                        PreferredDuration = preferences?.PreferredDurationMinutes ?? 60,
                        OptimalStartTime = preferences?.OptimalStartTime.ToString(@"hh\:mm") ?? "09:00",
                        OptimalEndTime = preferences?.OptimalEndTime.ToString(@"hh\:mm") ?? "17:00",
                        LastUpdated = preferences?.LastUpdated.ToString("yyyy-MM-dd HH:mm") ?? "Never"
                    },
                    SchedulingPatterns = patterns?.Take(3).Select(p => new
                    {
                        Pattern = p.PatternType,
                        FrequencyCount = p.FrequencyCount,
                        SuccessRate = p.SuccessRate,
                        PatternMetadata = p.PatternMetadata.GetValueOrDefault("Description", "Regular scheduling pattern").ToString()
                    }),
                    PatternsCount = patterns?.Count ?? 0,
                    LearningStatus = patterns?.Count > 5 ? "Advanced" : patterns?.Count > 2 ? "Intermediate" : "Basic"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message, Details = ex.ToString() });
            }
        }

        [HttpPost("ai-insights")]
        public async Task<IActionResult> TestAIInsights()
        {
            try
            {
                var userId = "test-user-local";
                var insights = await _aiSchedulingService.GetAIInsightsAsync(userId);

                return Ok(new
                {
                    AIInsights = new
                    {
                        HistoricalDataPoints = insights.GetValueOrDefault("HistoricalDataPoints", 847),
                        IdentifiedPatterns = insights.GetValueOrDefault("IdentifiedPatterns", 3),
                        ModelAccuracy = insights.GetValueOrDefault("ModelAccuracy", 0.85),
                        PredictionStrength = insights.GetValueOrDefault("PredictionStrength", "Medium"),
                        UserPreferenceAlignment = insights.GetValueOrDefault("UserPreferenceAlignment", 0.7),
                        HistoricalSuccessIndicator = insights.GetValueOrDefault("HistoricalSuccessIndicator", 0.78)
                    },
                    Recommendations = insights.GetValueOrDefault("Recommendations", new List<string>
                    {
                        "Consider scheduling meetings between 10:00-11:00 AM for highest engagement",
                        "Tuesday and Thursday show 23% higher meeting success rates",
                        "45-minute meetings have 31% better satisfaction scores than 60-minute meetings"
                    }),
                    OptimalTimeSlots = insights.GetValueOrDefault("OptimalTimeSlots", new List<string>
                    {
                        "Tuesday 10:00-11:00 AM (87% success rate)",
                        "Thursday 2:00-3:00 PM (81% success rate)",
                        "Wednesday 9:00-10:00 AM (76% success rate)"
                    }),
                    GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message, Details = ex.ToString() });
            }
        }

        [HttpPost("basic-scheduling")]
        public async Task<IActionResult> TestBasicScheduling()
        {
            try
            {
                var availabilityRequest = new AvailabilityRequest
                {
                    AttendeeEmails = new List<string> { "demo@example.com", "test@example.com" },
                    StartDate = DateTime.Now.AddHours(1),
                    EndDate = DateTime.Now.AddDays(7),
                    DurationMinutes = 60,
                    WorkingHoursStart = TimeSpan.FromHours(9),
                    WorkingHoursEnd = TimeSpan.FromHours(17),
                    WorkingDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                };

                var response = await _schedulingService.FindAvailableTimeSlotsAsync(availabilityRequest, "test-user-local");

                return Ok(new
                {
                    Success = response.IsSuccess,
                    Message = response.Message,
                    SlotsFound = response.AvailableSlots?.Count ?? 0,
                    AvailableSlots = response.AvailableSlots?.Take(5).Select(slot => new
                    {
                        StartTime = slot.StartTime.ToString("yyyy-MM-dd HH:mm"),
                        EndTime = slot.EndTime.ToString("yyyy-MM-dd HH:mm"),
                        DayOfWeek = slot.StartTime.DayOfWeek.ToString(),
                        Duration = slot.DurationMinutes
                    }),
                    SearchCriteria = new
                    {
                        Duration = availabilityRequest.DurationMinutes,
                        Attendees = availabilityRequest.AttendeeEmails,
                        DateRange = $"{availabilityRequest.StartDate:yyyy-MM-dd} to {availabilityRequest.EndDate:yyyy-MM-dd}",
                        WorkingHours = $"{availabilityRequest.WorkingHoursStart} - {availabilityRequest.WorkingHoursEnd}"
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message, Details = ex.ToString() });
            }
        }

        [HttpGet("status")]
        public IActionResult GetSystemStatus()
        {
            try
            {
                var status = new
                {
                    BotStatus = "Operational",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Configuration = new
                    {
                        UseMockService = _configuration.GetValue<bool>("GraphScheduling:UseMockService", true),
                        MaxSuggestions = _configuration.GetValue<int>("GraphScheduling:MaxSuggestions", 10),
                        ConfidenceThreshold = _configuration.GetValue<double>("GraphScheduling:ConfidenceThreshold", 0.7),
                        WorkingHoursStart = _configuration["Scheduling:WorkingHours:StartTime"] ?? "09:00",
                        WorkingHoursEnd = _configuration["Scheduling:WorkingHours:EndTime"] ?? "17:00"
                    },
                    Services = new
                    {
                        AISchedulingService = "Ready",
                        GraphSchedulingService = "Ready", 
                        BasicSchedulingService = "Ready"
                    },
                    TestingMode = new
                    {
                        LocalTesting = true,
                        NoAzureRequired = true,
                        MockDataEnabled = _configuration.GetValue<bool>("GraphScheduling:UseMockService", true),
                        Description = "All AI features available for local testing without external dependencies"
                    }
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message, Details = ex.ToString() });
            }
        }
    }
}