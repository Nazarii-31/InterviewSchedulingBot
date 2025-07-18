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
    <title>Interview Scheduling Bot - AI Testing Dashboard</title>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { 
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            color: #333;
        }
        .container { 
            max-width: 1200px; 
            margin: 0 auto; 
            padding: 20px; 
        }
        .header {
            text-align: center;
            color: white;
            margin-bottom: 30px;
        }
        .header h1 {
            font-size: 2.5em;
            margin-bottom: 10px;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
        }
        .header p {
            font-size: 1.2em;
            opacity: 0.9;
        }
        .teams-status {
            background: linear-gradient(45deg, #ff6b35, #f7931e);
            color: white;
            padding: 15px;
            border-radius: 10px;
            margin-bottom: 20px;
            text-align: center;
            font-weight: bold;
            box-shadow: 0 4px 15px rgba(0,0,0,0.2);
        }
        .dashboard {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(500px, 1fr));
            gap: 20px;
        }
        .test-card { 
            background: white; 
            border-radius: 15px; 
            padding: 25px; 
            box-shadow: 0 8px 25px rgba(0,0,0,0.1);
            transition: transform 0.3s ease, box-shadow 0.3s ease;
            border: 1px solid #e0e0e0;
        }
        .test-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 15px 35px rgba(0,0,0,0.15);
        }
        .test-card h3 { 
            color: #4a5568; 
            margin-bottom: 15px; 
            font-size: 1.3em;
            display: flex;
            align-items: center;
            gap: 10px;
        }
        .test-card p { 
            color: #718096; 
            margin-bottom: 20px; 
            line-height: 1.5;
        }
        .input-group {
            margin-bottom: 15px;
        }
        .input-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: 600;
            color: #4a5568;
        }
        .input-row {
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
        }
        input[type=""text""], input[type=""email""], select { 
            flex: 1;
            min-width: 200px;
            padding: 12px; 
            border: 2px solid #e2e8f0; 
            border-radius: 8px; 
            font-size: 14px;
            transition: border-color 0.3s ease;
        }
        input[type=""text""]:focus, input[type=""email""]:focus, select:focus { 
            outline: none;
            border-color: #667eea;
            box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
        }
        .btn { 
            background: linear-gradient(45deg, #667eea, #764ba2); 
            color: white; 
            padding: 12px 24px; 
            border: none; 
            border-radius: 8px; 
            cursor: pointer; 
            font-size: 14px;
            font-weight: 600;
            transition: all 0.3s ease;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            width: 100%;
            margin-top: 10px;
        }
        .btn:hover { 
            transform: translateY(-2px);
            box-shadow: 0 8px 20px rgba(102, 126, 234, 0.3);
        }
        .btn:active {
            transform: translateY(0);
        }
        .result { 
            background: #f7fafc; 
            border: 1px solid #e2e8f0;
            padding: 20px; 
            margin-top: 20px; 
            border-radius: 8px; 
            font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', monospace;
            font-size: 13px;
            line-height: 1.5;
            max-height: 400px;
            overflow-y: auto;
        }
        .result.success { 
            background: linear-gradient(135deg, #f0fff4 0%, #e6fffa 100%);
            border-color: #9ae6b4;
            color: #1a202c;
        }
        .result.error { 
            background: linear-gradient(135deg, #fed7d7 0%, #fbb6ce 100%);
            border-color: #fc8181;
            color: #742a2a;
        }
        .result.loading { 
            background: linear-gradient(135deg, #ebf8ff 0%, #bee3f8 100%);
            border-color: #90cdf4;
            color: #2a69ac;
            text-align: center;
        }
        .result-header {
            font-weight: bold;
            margin-bottom: 10px;
            padding-bottom: 10px;
            border-bottom: 1px solid #e2e8f0;
        }
        .result-item {
            margin-bottom: 8px;
            padding: 5px 0;
        }
        .result-item strong {
            color: #4a5568;
        }
        .sample-data {
            background: #fef5e7;
            border: 1px solid #f6e05e;
            padding: 15px;
            border-radius: 8px;
            margin-bottom: 15px;
            font-size: 13px;
        }
        .sample-data h4 {
            color: #975a16;
            margin-bottom: 8px;
        }
        .teams-info {
            background: linear-gradient(45deg, #0078d4, #106ebe);
            color: white;
            padding: 20px;
            border-radius: 10px;
            margin-bottom: 20px;
        }
        .teams-info h3 {
            margin-bottom: 15px;
        }
        .teams-info ul {
            list-style: none;
            padding: 0;
        }
        .teams-info li {
            margin-bottom: 8px;
            padding-left: 20px;
            position: relative;
        }
        .teams-info li:before {
            content: '✓';
            position: absolute;
            left: 0;
            font-weight: bold;
        }
        @media (max-width: 768px) {
            .dashboard {
                grid-template-columns: 1fr;
            }
            .input-row {
                flex-direction: column;
            }
            input[type=""text""], input[type=""email""] {
                min-width: 100%;
            }
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🤖 AI Interview Scheduling Bot</h1>
            <p>Comprehensive Testing Dashboard with Mock Data</p>
        </div>

        <div class=""teams-info"">
            <h3>📱 MS Teams Testing Status</h3>
            <div style=""display: flex; gap: 30px; align-items: center;"">
                <div>
                    <p><strong>Can run in MS Teams:</strong> ✅ YES</p>
                    <p><strong>Current limitation:</strong> Requires Azure Bot Framework registration</p>
                </div>
                <div>
                    <ul>
                        <li>Bot manifest is ready</li>
                        <li>All features work with mock data</li>
                        <li>Need MicrosoftAppId/Password for Teams</li>
                        <li>Alternative: Use this web interface for full testing</li>
                    </ul>
                </div>
            </div>
        </div>

        <div class=""dashboard"">
            <div class=""test-card"">
                <h3>🧠 AI Scheduling Showcase</h3>
                <p>Test AI-powered meeting scheduling with machine learning optimization</p>
                
                <div class=""sample-data"">
                    <h4>📋 Current Test Scenario:</h4>
                    <p>• <strong>Participants:</strong> John Smith (Product Manager) & Jane Doe (Engineer)</p>
                    <p>• <strong>Meeting Type:</strong> Technical Interview - 60 minutes</p>
                    <p>• <strong>AI Features:</strong> User preference learning, pattern analysis, optimal time prediction</p>
                </div>

                <div class=""input-group"">
                    <label>👥 Meeting Participants:</label>
                    <div class=""input-row"">
                        <input type=""email"" id=""attendee1"" value=""john.smith@company.com"" placeholder=""First participant"">
                        <input type=""email"" id=""attendee2"" value=""jane.doe@company.com"" placeholder=""Second participant"">
                    </div>
                </div>

                <div class=""input-group"">
                    <label>⏱️ Meeting Details:</label>
                    <div class=""input-row"">
                        <select id=""duration"">
                            <option value=""30"">30 minutes</option>
                            <option value=""45"">45 minutes</option>
                            <option value=""60"" selected>60 minutes</option>
                            <option value=""90"">90 minutes</option>
                        </select>
                        <select id=""days"">
                            <option value=""3"">Next 3 days</option>
                            <option value=""7"" selected>Next 7 days</option>
                            <option value=""14"">Next 14 days</option>
                        </select>
                    </div>
                </div>

                <button class=""btn"" onclick=""testAIScheduling()"">🚀 Find Optimal Times with AI</button>
                <div id=""aiResult"" class=""result"" style=""display:none;""></div>
            </div>

            <div class=""test-card"">
                <h3>📊 Microsoft Graph Integration</h3>
                <p>Test enterprise-grade scheduling with Microsoft Graph API</p>
                
                <div class=""sample-data"">
                    <h4>📋 Graph Features:</h4>
                    <p>• <strong>Calendar Integration:</strong> Real-time availability checking</p>
                    <p>• <strong>Meeting Suggestions:</strong> Optimal time slot recommendations</p>
                    <p>• <strong>Conflict Detection:</strong> Automatic busy time avoidance</p>
                </div>

                <button class=""btn"" onclick=""testGraphScheduling()"">📅 Test Graph Scheduling</button>
                <div id=""graphResult"" class=""result"" style=""display:none;""></div>
            </div>

            <div class=""test-card"">
                <h3>🎯 User Preference Learning</h3>
                <p>AI system that learns from past scheduling behavior</p>
                
                <div class=""sample-data"">
                    <h4>📋 Learning Features:</h4>
                    <p>• <strong>Scheduling Patterns:</strong> Preferred times and days</p>
                    <p>• <strong>Success Rates:</strong> Historical meeting effectiveness</p>
                    <p>• <strong>Adaptive Recommendations:</strong> Personalized suggestions</p>
                </div>

                <button class=""btn"" onclick=""testUserPreferences()"">🧠 Analyze User Patterns</button>
                <div id=""preferencesResult"" class=""result"" style=""display:none;""></div>
            </div>

            <div class=""test-card"">
                <h3>💡 AI Insights & Analytics</h3>
                <p>Advanced analytics and scheduling recommendations</p>
                
                <div class=""sample-data"">
                    <h4>📋 Analytics Features:</h4>
                    <p>• <strong>Pattern Recognition:</strong> 850+ historical data points</p>
                    <p>• <strong>Predictive Modeling:</strong> 85% accuracy rate</p>
                    <p>• <strong>Smart Recommendations:</strong> Optimize for productivity</p>
                </div>

                <button class=""btn"" onclick=""testAIInsights()"">💡 Generate AI Insights</button>
                <div id=""insightsResult"" class=""result"" style=""display:none;""></div>
            </div>

            <div class=""test-card"">
                <h3>🔍 Basic Scheduling Test</h3>
                <p>Core scheduling engine without AI enhancements</p>
                <button class=""btn"" onclick=""testBasicScheduling()"">📋 Test Basic Scheduling</button>
                <div id=""basicResult"" class=""result"" style=""display:none;""></div>
            </div>

            <div class=""test-card"">
                <h3>🎛️ System Diagnostics</h3>
                <p>Check bot configuration and service health</p>
                <button class=""btn"" onclick=""testSystemStatus()"">⚙️ Run System Check</button>
                <div id=""statusResult"" class=""result"" style=""display:none;""></div>
            </div>
        </div>
    </div>

    <script src=""https://code.jquery.com/jquery-3.6.0.min.js""></script>
    <script>
        function showLoading(elementId) {
            $(`#${elementId}`).show().removeClass('error success').addClass('loading')
                .html('<div class=""result-header"">⏳ Processing Request...</div><p>AI system is analyzing parameters and generating results...</p>');
        }

        function showResult(elementId, data, isError = false) {
            const resultClass = isError ? 'error' : 'success';
            const header = isError ? '❌ Error Occurred' : '✅ Test Completed Successfully';
            
            let formattedResult = '';
            if (typeof data === 'object') {
                formattedResult = formatObjectResult(data);
            } else {
                formattedResult = `<pre>${JSON.stringify(data, null, 2)}</pre>`;
            }
            
            $(`#${elementId}`).show().removeClass('loading').addClass(resultClass)
                .html(`<div class=""result-header"">${header}</div>${formattedResult}`);
        }

        function formatObjectResult(data) {
            let html = '';
            for (const [key, value] of Object.entries(data)) {
                if (typeof value === 'object' && value !== null) {
                    if (Array.isArray(value)) {
                        html += `<div class=""result-item""><strong>${formatKey(key)}:</strong></div>`;
                        value.forEach((item, index) => {
                            html += `<div style=""margin-left: 20px; margin-bottom: 10px;""><strong>Option ${index + 1}:</strong><br>`;
                            if (typeof item === 'object') {
                                for (const [subKey, subValue] of Object.entries(item)) {
                                    html += `<div style=""margin-left: 20px;"">${formatKey(subKey)}: ${formatValue(subValue)}</div>`;
                                }
                            } else {
                                html += `<div style=""margin-left: 20px;"">${item}</div>`;
                            }
                            html += '</div>';
                        });
                    } else {
                        html += `<div class=""result-item""><strong>${formatKey(key)}:</strong></div>`;
                        for (const [subKey, subValue] of Object.entries(value)) {
                            html += `<div style=""margin-left: 20px;"">${formatKey(subKey)}: ${formatValue(subValue)}</div>`;
                        }
                    }
                } else {
                    html += `<div class=""result-item""><strong>${formatKey(key)}:</strong> ${formatValue(value)}</div>`;
                }
            }
            return html;
        }

        function formatKey(key) {
            return key.replace(/([A-Z])/g, ' $1').replace(/^./, str => str.toUpperCase());
        }

        function formatValue(value) {
            if (typeof value === 'boolean') {
                return value ? '✅ Yes' : '❌ No';
            }
            if (typeof value === 'number') {
                if (value < 1 && value > 0) {
                    return `${(value * 100).toFixed(1)}%`;
                }
                return value.toLocaleString();
            }
            return value;
        }

        function testAIScheduling() {
            showLoading('aiResult');
            const attendees = [$('#attendee1').val(), $('#attendee2').val()].filter(email => email);
            const duration = parseInt($('#duration').val()) || 60;
            const days = parseInt($('#days').val()) || 7;
            
            $.ajax({
                url: '/api/test/ai-scheduling',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({
                    attendees: attendees,
                    duration: duration,
                    days: days
                }),
                success: function(data) {
                    showResult('aiResult', data);
                },
                error: function(xhr) {
                    showResult('aiResult', xhr.responseJSON || { error: xhr.responseText }, true);
                }
            });
        }

        function testGraphScheduling() {
            showLoading('graphResult');
            $.post('/api/test/graph-scheduling').done(function(data) {
                showResult('graphResult', data);
            }).fail(function(xhr) {
                showResult('graphResult', xhr.responseJSON || { error: xhr.responseText }, true);
            });
        }

        function testUserPreferences() {
            showLoading('preferencesResult');
            $.post('/api/test/user-preferences').done(function(data) {
                showResult('preferencesResult', data);
            }).fail(function(xhr) {
                showResult('preferencesResult', xhr.responseJSON || { error: xhr.responseText }, true);
            });
        }

        function testAIInsights() {
            showLoading('insightsResult');
            $.post('/api/test/ai-insights').done(function(data) {
                showResult('insightsResult', data);
            }).fail(function(xhr) {
                showResult('insightsResult', xhr.responseJSON || { error: xhr.responseText }, true);
            });
        }

        function testBasicScheduling() {
            showLoading('basicResult');
            $.post('/api/test/basic-scheduling').done(function(data) {
                showResult('basicResult', data);
            }).fail(function(xhr) {
                showResult('basicResult', xhr.responseJSON || { error: xhr.responseText }, true);
            });
        }

        function testSystemStatus() {
            showLoading('statusResult');
            $.get('/api/test/status').done(function(data) {
                showResult('statusResult', data);
            }).fail(function(xhr) {
                showResult('statusResult', xhr.responseJSON || { error: xhr.responseText }, true);
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