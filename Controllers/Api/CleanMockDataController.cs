using Microsoft.AspNetCore.Mvc;
using InterviewSchedulingBot.Services.Business;
using InterviewSchedulingBot.Services.Integration;

namespace InterviewSchedulingBot.Controllers.Api
{
    /// <summary>
    /// Clean API controller for mock data management with explicit routing
    /// </summary>
    [ApiController]
    [Route("api/mock-data")]
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

        [HttpPost("update-user")]
        public IActionResult UpdateUser([FromBody] UserProfile userProfile)
        {
            try
            {
                _mockDataGenerator.UpdateUserProfile(userProfile);
                return Ok(new { success = true, message = "User profile updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
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
                <div class=""col-md-4"">
                    <label class=""form-label"">Number of users:</label>
                    <select id=""user-count"" class=""form-select"">
                        <option value=""3"" {(settings.UserCount == 3 ? "selected" : "")}>3 Users</option>
                        <option value=""5"" {(settings.UserCount == 5 ? "selected" : "")}>5 Users</option>
                        <option value=""7"" {(settings.UserCount == 7 ? "selected" : "")}>7 Users</option>
                        <option value=""10"" {(settings.UserCount == 10 ? "selected" : "")}>10 Users</option>
                        <option value=""15"" {(settings.UserCount == 15 ? "selected" : "")}>15 Users</option>
                    </select>
                </div>
                <div class=""col-md-4"">
                    <label class=""form-label"">Generate calendar events for:</label>
                    <select id=""generation-duration"" class=""form-select"">
                        <option value=""1"">1 Day</option>
                        <option value=""3"">3 Days</option>
                        <option value=""7"" {(settings.CalendarRangeDays == 7 ? "selected" : "")}>1 Week</option>
                        <option value=""14"" {(settings.CalendarRangeDays == 14 ? "selected" : "")}>2 Weeks</option>
                        <option value=""30"" {(settings.CalendarRangeDays == 30 ? "selected" : "")}>1 Month</option>
                    </select>
                </div>
                <div class=""col-md-4"">
                    <label class=""form-label"">Meeting density:</label>
                    <select id=""generation-density"" class=""form-select"">
                        <option value=""Low"" {(settings.MeetingDensity.ToString() == "Low" ? "selected" : "")}>Low (0-1 per day)</option>
                        <option value=""Medium"" {(settings.MeetingDensity.ToString() == "Medium" ? "selected" : "")}>Medium (1-3 per day)</option>
                        <option value=""High"" {(settings.MeetingDensity.ToString() == "High" ? "selected" : "")}>High (3-5 per day)</option>
                    </select>
                </div>
            </div>
            
            <div class=""row mb-3"">
                <div class=""col-12"">
                    <button type=""button"" class=""btn btn-info"" onclick=""applySettings()"">
                        <i class=""fas fa-check""></i> Apply Settings
                    </button>
                </div>
            </div>
        </div>

        <!-- User Profiles -->
        <div class=""settings-card"">
            <h3><i class=""fas fa-users""></i> User Profiles</h3>
            
            <!-- Organizer Section -->
            <div class=""mb-4"">
                <h5><i class=""fas fa-crown""></i> Meeting Organizer</h5>
                <div class=""alert alert-info"">
                    <strong>Note:</strong> The first user in the list acts as the meeting organizer. Their calendar is checked for conflicts along with participants.
                </div>
            </div>
            
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

        async function applySettings() {{
            const userCount = parseInt(document.getElementById('user-count').value);
            const calendarRangeDays = parseInt(document.getElementById('generation-duration').value);
            const meetingDensity = document.getElementById('generation-density').value;
            
            const settings = {{
                UserCount: userCount,
                CalendarRangeDays: calendarRangeDays,
                MeetingDensity: meetingDensity
            }};
            
            await callApi('update-settings', 'POST', settings);
            showMessage('Settings applied successfully', 'success');
            setTimeout(() => location.reload(), 1000);
        }}

        async function exportData() {{
            try {{
                const response = await fetch('/api/mock-data/export');
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

                const response = await fetch(`/api/mock-data/${{endpoint}}`, options);
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

        // User editing functions
        function editUser(email) {{
            const safeEmail = email.replace(/@/g, '-').replace(/\\./g, '-');
            document.getElementById(`view-${{safeEmail}}`).style.display = 'none';
            document.getElementById(`edit-${{safeEmail}}`).style.display = 'block';
        }}

        function cancelEdit(email) {{
            const safeEmail = email.replace(/@/g, '-').replace(/\\./g, '-');
            document.getElementById(`view-${{safeEmail}}`).style.display = 'block';
            document.getElementById(`edit-${{safeEmail}}`).style.display = 'none';
        }}

        async function saveUser(email) {{
            const safeEmail = email.replace(/@/g, '-').replace(/\\./g, '-');
            
            const userData = {{
                Email: email,
                Name: document.getElementById(`name-${{safeEmail}}`).value,
                JobTitle: document.getElementById(`title-${{safeEmail}}`).value,
                Department: document.getElementById(`department-${{safeEmail}}`).value,
                TimeZone: document.getElementById(`timezone-${{safeEmail}}`).value,
                WorkingHours: {{
                    StartTime: document.getElementById(`starttime-${{safeEmail}}`).value,
                    EndTime: document.getElementById(`endtime-${{safeEmail}}`).value,
                    DaysOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday']
                }}
            }};

            try {{
                const response = await fetch('/api/mock-data/update-user', {{
                    method: 'POST',
                    headers: {{ 'Content-Type': 'application/json' }},
                    body: JSON.stringify(userData)
                }});

                const result = await response.json();
                
                if (result.success) {{
                    showMessage('User data updated successfully', 'success');
                    setTimeout(() => location.reload(), 1000);
                }} else {{
                    showMessage('Failed to update user data', 'error');
                }}
            }} catch (error) {{
                showMessage('Error updating user: ' + error.message, 'error');
            }}
        }}

        // Refresh page when regenerating calendar events to show updated data
        async function regenerateCalendarData() {{
            await callApi('regenerate-calendar', 'POST');
            setTimeout(() => location.reload(), 1000);
        }}
    </script>
</body>
</html>";
        }

        private string GenerateUserProfilesHtml(List<UserProfile> profiles)
        {
            var html = "";
            var today = DateTime.Today;
            var endDate = today.AddDays(7);
            var englishCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            
            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                var isOrganizer = i == 0; // First user is the organizer
                var calendarEvents = _mockDataGenerator.GetCalendarEvents(profile.Email, today, endDate);
                
                html += $@"
                <div class=""profile-card {(isOrganizer ? "border-warning" : "")}"">
                    <div class=""row"">
                        <div class=""col-md-6"">
                            <div class=""d-flex justify-content-between align-items-center mb-2"">
                                <h5>
                                    <i class=""fas {(isOrganizer ? "fa-crown text-warning" : "fa-user")}""></i> 
                                    {profile.Name}
                                    {(isOrganizer ? "<span class=\"badge bg-warning text-dark ms-2\">Organizer</span>" : "")}
                                </h5>
                                <button class=""btn btn-sm btn-outline-primary"" onclick=""editUser('{profile.Email}')"">
                                    <i class=""fas fa-edit""></i> Edit
                                </button>
                            </div>
                            <div id=""view-{profile.Email.Replace("@", "-").Replace(".", "-")}"">
                                <p><strong>Email:</strong> {profile.Email}</p>
                                <p><strong>Title:</strong> {profile.JobTitle}</p>
                                <p><strong>Department:</strong> {profile.Department}</p>
                                <p><strong>Time Zone:</strong> {profile.TimeZone}</p>
                                <p><strong>Working Hours:</strong> {profile.WorkingHours.StartTime} - {profile.WorkingHours.EndTime}</p>
                            </div>
                            <div id=""edit-{profile.Email.Replace("@", "-").Replace(".", "-")}"" style=""display: none;"">
                                <div class=""mb-2"">
                                    <label class=""form-label"">Name:</label>
                                    <input type=""text"" class=""form-control form-control-sm"" id=""name-{profile.Email.Replace("@", "-").Replace(".", "-")}"" value=""{profile.Name}"">
                                </div>
                                <div class=""mb-2"">
                                    <label class=""form-label"">Title:</label>
                                    <input type=""text"" class=""form-control form-control-sm"" id=""title-{profile.Email.Replace("@", "-").Replace(".", "-")}"" value=""{profile.JobTitle}"">
                                </div>
                                <div class=""mb-2"">
                                    <label class=""form-label"">Department:</label>
                                    <input type=""text"" class=""form-control form-control-sm"" id=""department-{profile.Email.Replace("@", "-").Replace(".", "-")}"" value=""{profile.Department}"">
                                </div>
                                <div class=""mb-2"">
                                    <label class=""form-label"">Time Zone:</label>
                                    <select class=""form-control form-control-sm"" id=""timezone-{profile.Email.Replace("@", "-").Replace(".", "-")}"">
                                        <option value=""Pacific Standard Time"" {(profile.TimeZone == "Pacific Standard Time" ? "selected" : "")}>Pacific Standard Time</option>
                                        <option value=""Central Standard Time"" {(profile.TimeZone == "Central Standard Time" ? "selected" : "")}>Central Standard Time</option>
                                        <option value=""Eastern Standard Time"" {(profile.TimeZone == "Eastern Standard Time" ? "selected" : "")}>Eastern Standard Time</option>
                                        <option value=""Mountain Standard Time"" {(profile.TimeZone == "Mountain Standard Time" ? "selected" : "")}>Mountain Standard Time</option>
                                        <option value=""UTC"" {(profile.TimeZone == "UTC" ? "selected" : "")}>UTC</option>
                                    </select>
                                </div>
                                <div class=""row"">
                                    <div class=""col-6"">
                                        <label class=""form-label"">Start Time:</label>
                                        <input type=""time"" class=""form-control form-control-sm"" id=""starttime-{profile.Email.Replace("@", "-").Replace(".", "-")}"" value=""{profile.WorkingHours.StartTime}"">
                                    </div>
                                    <div class=""col-6"">
                                        <label class=""form-label"">End Time:</label>
                                        <input type=""time"" class=""form-control form-control-sm"" id=""endtime-{profile.Email.Replace("@", "-").Replace(".", "-")}"" value=""{profile.WorkingHours.EndTime}"">
                                    </div>
                                </div>
                                <div class=""mt-2"">
                                    <button class=""btn btn-sm btn-success me-2"" onclick=""saveUser('{profile.Email}')"">
                                        <i class=""fas fa-save""></i> Save
                                    </button>
                                    <button class=""btn btn-sm btn-secondary"" onclick=""cancelEdit('{profile.Email}')"">
                                        <i class=""fas fa-times""></i> Cancel
                                    </button>
                                </div>
                            </div>
                        </div>
                        <div class=""col-md-6"">
                            <h6><i class=""fas fa-calendar""></i> Calendar Events (Next 7 Days)</h6>";
                
                if (calendarEvents.Any())
                {
                    var eventsByDay = calendarEvents.GroupBy(e => e.StartTime.Date).OrderBy(g => g.Key);
                    
                    foreach (var dayGroup in eventsByDay)
                    {
                        html += $@"
                            <div class=""mb-2"">
                                <strong>{dayGroup.Key.ToString("dddd, MMM d", englishCulture)}</strong>";
                        
                        foreach (var evt in dayGroup.OrderBy(e => e.StartTime))
                        {
                            html += $@"
                                <div class=""small text-muted ms-2"">
                                    • {evt.StartTime:HH:mm} - {evt.EndTime:HH:mm}: {evt.Title}
                                    {(string.IsNullOrEmpty(evt.Location) ? "" : $" ({evt.Location})")}
                                </div>";
                        }
                        
                        html += "</div>";
                    }
                }
                else
                {
                    html += @"
                            <div class=""text-muted small"">No events scheduled</div>";
                }
                
                html += @"
                        </div>
                    </div>
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