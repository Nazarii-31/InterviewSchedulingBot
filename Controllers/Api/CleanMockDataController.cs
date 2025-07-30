using Microsoft.AspNetCore.Mvc;
using InterviewSchedulingBot.Services.Business;
using InterviewSchedulingBot.Services.Integration;

namespace InterviewSchedulingBot.Controllers.Api
{
    /// <summary>
    /// Clean API controller for mock data management with explicit routing
    /// </summary>
    [ApiController]
    [Route("api/clean-mock-data")]
    public class CleanMockDataController : ControllerBase
    {
        private readonly ICleanMockDataGenerator _mockDataGenerator;
        private readonly ISimpleOpenWebUIParameterExtractor _parameterExtractor;

        public CleanMockDataController(
            ICleanMockDataGenerator mockDataGenerator,
            ISimpleOpenWebUIParameterExtractor parameterExtractor)
        {
            _mockDataGenerator = mockDataGenerator;
            _parameterExtractor = parameterExtractor;
        }

        [HttpGet("interface")]
        public IActionResult GetInterface()
        {
            return Content(GenerateCleanInterface(), "text/html");
        }

        [HttpPost("reset-default")]
        public IActionResult ResetToDefault()
        {
            _mockDataGenerator.ResetToDefault();
            return Ok(new { success = true, message = "Reset to default settings" });
        }

        [HttpPost("generate-random")]
        public IActionResult GenerateRandomData()
        {
            _mockDataGenerator.GenerateRandomData();
            return Ok(new { success = true, message = "Generated random mock data" });
        }

        [HttpPost("regenerate-calendar")]
        public IActionResult RegenerateCalendarEvents()
        {
            _mockDataGenerator.RegenerateCalendarEvents();
            return Ok(new { success = true, message = "Regenerated calendar events" });
        }

        [HttpGet("export")]
        public IActionResult ExportData()
        {
            var data = _mockDataGenerator.ExportData();
            return Content(data, "application/json");
        }

        [HttpPost("update-settings")]
        public IActionResult UpdateSettings([FromBody] MockDataSettings settings)
        {
            _mockDataGenerator.UpdateSettings(settings);
            return Ok(new { success = true, message = "Settings updated", settings });
        }

        [HttpGet("profiles")]
        public IActionResult GetUserProfiles()
        {
            var profiles = _mockDataGenerator.GetUserProfiles();
            return Ok(profiles);
        }

        [HttpPost("extract-parameters")]
        public async Task<IActionResult> ExtractParameters([FromBody] ParameterRequest request)
        {
            var result = await _parameterExtractor.ExtractParametersAsync(request.Message);
            return Ok(result);
        }

        private string GenerateCleanInterface()
        {
            var settings = _mockDataGenerator.GetCurrentSettings();
            var profiles = _mockDataGenerator.GetUserProfiles();

            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Clean Mock Data Management Interface</title>
    <link href=""https://cdnjs.cloudflare.com/ajax/libs/bootstrap/5.3.0/css/bootstrap.min.css"" rel=""stylesheet"">
    <link href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"" rel=""stylesheet"">
    <style>
        .settings-card {{ background: #f8f9fa; border-radius: 12px; padding: 24px; margin-bottom: 24px; }}
        .profile-card {{ background: white; border: 1px solid #e9ecef; border-radius: 8px; padding: 16px; margin-bottom: 12px; }}
        .action-buttons {{ display: flex; gap: 12px; flex-wrap: wrap; margin-top: 20px; }}
        .btn-action {{ padding: 10px 20px; border-radius: 6px; font-weight: 500; }}
        .range-value {{ font-weight: bold; color: #0d6efd; }}
        .status-message {{ margin-top: 16px; padding: 12px; border-radius: 6px; }}
    </style>
</head>
<body>
    <div class=""container mt-4"">
        <h1 class=""mb-4""><i class=""fas fa-database""></i> Clean Mock Data Management Interface</h1>
        
        <!-- Calendar Generation Settings -->
        <div class=""settings-card"">
            <h3><i class=""fas fa-cog""></i> Calendar Generation Settings</h3>
            
            <div class=""row mb-3"">
                <div class=""col-md-6"">
                    <label class=""form-label"">Generate calendar events for:</label>
                    <select id=""generation-duration"" class=""form-select"">
                        <option value=""1"">1 Day</option>
                        <option value=""3"">3 Days</option>
                        <option value=""7"" {(settings.CalendarRangeDays == 7 ? "selected" : "")}>1 Week</option>
                        <option value=""14"" {(settings.CalendarRangeDays == 14 ? "selected" : "")}>2 Weeks</option>
                        <option value=""30"" {(settings.CalendarRangeDays == 30 ? "selected" : "")}>1 Month</option>
                    </select>
                </div>
                <div class=""col-md-6"">
                    <label class=""form-label"">Meeting density:</label>
                    <select id=""generation-density"" class=""form-select"">
                        <option value=""Low"" {(settings.MeetingDensity.ToString() == "Low" ? "selected" : "")}>Low (0-1 per day)</option>
                        <option value=""Medium"" {(settings.MeetingDensity.ToString() == "Medium" ? "selected" : "")}>Medium (1-3 per day)</option>
                        <option value=""High"" {(settings.MeetingDensity.ToString() == "High" ? "selected" : "")}>High (3-5 per day)</option>
                    </select>
                </div>
            </div>
        </div>

        <!-- User Profiles -->
        <div class=""settings-card"">
            <h3><i class=""fas fa-users""></i> User Profiles</h3>
            <div id=""user-profiles-container"">
                {GenerateUserProfilesHtml(profiles)}
            </div>
        </div>

        <!-- Action Buttons -->
        <div class=""action-buttons"">
            <button type=""button"" class=""btn btn-primary btn-action"" onclick=""resetToDefault()"">
                <i class=""fas fa-refresh""></i> Reset to Default
            </button>
            <button type=""button"" class=""btn btn-secondary btn-action"" onclick=""generateRandomData()"">
                <i class=""fas fa-random""></i> Generate Random Data
            </button>
            <button type=""button"" class=""btn btn-secondary btn-action"" onclick=""regenerateCalendarData()"">
                <i class=""fas fa-calendar-plus""></i> Regenerate Calendar Events
            </button>
            <button type=""button"" class=""btn btn-secondary btn-action"" onclick=""exportData()"">
                <i class=""fas fa-download""></i> Export Data
            </button>
        </div>

        <div id=""status-message"" class=""status-message"" style=""display: none;""></div>
    </div>

    <script>
        async function resetToDefault() {{
            await callApi('reset-default', 'POST');
            location.reload();
        }}

        async function generateRandomData() {{
            await callApi('generate-random', 'POST');
            location.reload();
        }}

        async function regenerateCalendarData() {{
            await callApi('regenerate-calendar', 'POST');
            showMessage('Calendar events regenerated successfully', 'success');
        }}

        async function exportData() {{
            try {{
                const response = await fetch('/api/clean-mock-data/export');
                const data = await response.text();
                downloadFile('mock-data.json', data);
                showMessage('Data exported successfully', 'success');
            }} catch (error) {{
                showMessage('Export failed: ' + error.message, 'error');
            }}
        }}

        async function callApi(endpoint, method = 'GET', data = null) {{
            try {{
                const options = {{
                    method,
                    headers: {{ 'Content-Type': 'application/json' }}
                }};
                
                if (data) {{
                    options.body = JSON.stringify(data);
                }}

                const response = await fetch(`/api/clean-mock-data/${{endpoint}}`, options);
                const result = await response.json();
                
                if (result.success) {{
                    showMessage(result.message, 'success');
                }} else {{
                    showMessage('Operation failed', 'error');
                }}
                
                return result;
            }} catch (error) {{
                showMessage('Error: ' + error.message, 'error');
                throw error;
            }}
        }}

        function showMessage(message, type) {{
            const messageDiv = document.getElementById('status-message');
            messageDiv.className = `status-message alert alert-${{type === 'success' ? 'success' : 'danger'}}`;
            messageDiv.textContent = message;
            messageDiv.style.display = 'block';
            
            setTimeout(() => {{
                messageDiv.style.display = 'none';
            }}, 3000);
        }}

        function downloadFile(filename, content) {{
            const blob = new Blob([content], {{ type: 'application/json' }});
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = filename;
            link.click();
            URL.revokeObjectURL(url);
        }}

        // Update settings when form values change
        document.getElementById('generation-duration').addEventListener('change', updateSettings);
        document.getElementById('generation-density').addEventListener('change', updateSettings);

        async function updateSettings() {{
            const settings = {{
                CalendarRangeDays: parseInt(document.getElementById('generation-duration').value),
                MeetingDensity: document.getElementById('generation-density').value,
                UserCount: {settings.UserCount}
            }};
            
            await callApi('update-settings', 'POST', settings);
        }}
    </script>
</body>
</html>";
        }

        private string GenerateUserProfilesHtml(List<UserProfile> profiles)
        {
            var html = "";
            foreach (var profile in profiles)
            {
                html += $@"
                <div class=""profile-card"">
                    <h5>{profile.Name}</h5>
                    <p><strong>Email:</strong> {profile.Email}</p>
                    <p><strong>Title:</strong> {profile.JobTitle}</p>
                    <p><strong>Department:</strong> {profile.Department}</p>
                    <p><strong>Time Zone:</strong> {profile.TimeZone}</p>
                    <p><strong>Working Hours:</strong> {profile.WorkingHours.StartTime} - {profile.WorkingHours.EndTime}</p>
                </div>";
            }
            return html;
        }
    }

    public class ParameterRequest
    {
        public string Message { get; set; } = "";
    }
}