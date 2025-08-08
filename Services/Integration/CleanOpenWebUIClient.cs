using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using InterviewSchedulingBot.Models;
using System.Globalization;

namespace InterviewSchedulingBot.Services.Integration
{
    /// <summary>
    /// Clean OpenWebUI client for parameter extraction with proper weekend handling
    /// Uses AI for natural language understanding with business day intelligence
    /// </summary>
    public interface ICleanOpenWebUIClient
    {
        // Legacy method (kept for backward compatibility)
        Task<MeetingParameters> ExtractParametersAsync(string userMessage);
        // New AI-first extraction with JSON contract and guardrails
        Task<AiExtractionResult> ExtractParametersAsync(string userMessage, DateTime nowUtc, IConfiguration config, string? correctionNote = null);
        // Legacy formatting (kept for backward compatibility)
        Task<string> GenerateResponseAsync(string systemPrompt, MeetingContext context);
        // New AI-first formatting for slots
        Task<string> FormatSlotsAsync(MeetingContext context, IConfiguration config);
    }

    public record EnhancedMeetingParameters
    {
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public int DurationMinutes { get; init; } = 60;
        public List<string> ParticipantEmails { get; init; } = new List<string>();
        public string TimeOfDay { get; init; } = "all";
    }
    
    public record MeetingContext
    {
        public List<TimeSlot> AvailableSlots { get; init; } = new();
        public required EnhancedMeetingParameters Parameters { get; init; }
        public string OriginalRequest { get; init; } = "";
    }

    public class CleanOpenWebUIClient : ICleanOpenWebUIClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration? _configuration;
        private readonly ILogger<CleanOpenWebUIClient> _logger;
        private readonly bool _useMockData;
        
    private const string BaseUrl = "https://openwebui.ai.godeltech.com/api/";
    private const string Model = "mistral:7b"; // Legacy use
    private const string DefaultModelName = "mistral:7b"; // Unified default
    private const string DefaultModel = "mistral:7b";

        public CleanOpenWebUIClient(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CleanOpenWebUIClient> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            // Get configuration
            var baseUrl = _configuration?["OpenWebUI:BaseUrl"] ?? BaseUrl;
            _useMockData = bool.Parse(_configuration?["OpenWebUI:UseMockData"] ?? "true");
            
            // Configure HttpClient
            if (!string.IsNullOrEmpty(baseUrl))
            {
                // Ensure URL ends with slash
                if (!baseUrl.EndsWith("/"))
                {
                    baseUrl += "/";
                }
                
                _httpClient.BaseAddress = new Uri(baseUrl);
                
                // Set API key header if provided
                var apiKey = _configuration?["OpenWebUI:ApiKey"];
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                }
            }
        }

        // New AI-first extraction with guardrails
        public async Task<AiExtractionResult> ExtractParametersAsync(string userMessage, DateTime nowUtc, IConfiguration config, string? correctionNote = null)
        {
            try
            {
                var baseUrl = config["OpenWebUI:BaseUrl"] ?? _httpClient.BaseAddress?.ToString() ?? string.Empty;
                var model = config["OpenWebUI:Model"] ?? DefaultModelName;
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    return AiExtractionResult.Clarify("I can’t reach the AI service to interpret your request. Please try again later.");
                }

                if (_httpClient.BaseAddress == null)
                {
                    _httpClient.BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/");
                }

                var workStart = config["Scheduling:WorkingHours:StartTime"] ?? "09:00";
                var workEnd = config["Scheduling:WorkingHours:EndTime"] ?? "17:00";
                var defaultDurationStr = config["Scheduling:DefaultDurationMinutes"] ?? "60";

                var sbPrompt = new StringBuilder();
                sbPrompt.AppendLine("You are an expert AI that extracts interview scheduling parameters from one user message.");
                sbPrompt.AppendLine();
                sbPrompt.AppendLine("Business constraints:");
                sbPrompt.AppendLine("- Business days: Monday–Friday only.");
                sbPrompt.AppendLine($"- Business hours: {workStart}–{workEnd} (24‑hour).");
                sbPrompt.AppendLine($"- Current UTC datetime: {nowUtc:yyyy-MM-dd HH:mm} (DayOfWeek: {nowUtc:dddd}).");
                sbPrompt.AppendLine();
                sbPrompt.AppendLine("Interpret, without hardcoded rules in code: “tomorrow”, “next week”, “first N days of next week”, “Tue–Thu next week”, “in N weeks”, “after <date>”, “between <dateA> and <dateB>”, “next N business days”, “few days”.");
                sbPrompt.AppendLine("If ambiguity exists (e.g., “few days”), do not guess—ask for clarification via needClarification.");
                sbPrompt.AppendLine();
                sbPrompt.AppendLine("Participants:");
                sbPrompt.AppendLine("- NEVER invent participants. If none are in the message, return an empty array.");
                sbPrompt.AppendLine();
                sbPrompt.AppendLine("Output format:");
                sbPrompt.AppendLine("Return ONLY ONE compact JSON object. No markdown, no code fences, no prose.");
                sbPrompt.AppendLine("{");
                sbPrompt.AppendLine("  \"startDate\": \"ISO 8601\",");
                sbPrompt.AppendLine("  \"endDate\": \"ISO 8601\",");
                sbPrompt.AppendLine("  \"timeOfDay\": \"morning|afternoon|all\",");
                sbPrompt.AppendLine("  \"durationMinutes\": number|null,");
                sbPrompt.AppendLine("  \"participantEmails\": string[],");
                sbPrompt.AppendLine("  \"daysSelector\": {");
                sbPrompt.AppendLine("    \"mode\": \"fullRange|firstN|specificDays\",");
                sbPrompt.AppendLine("    \"n\": number|null,");
                sbPrompt.AppendLine("    \"daysOfWeek\": [\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"]");
                sbPrompt.AppendLine("  },");
                sbPrompt.AppendLine("  \"needClarification\": false|{ \"question\": \"...\" }");
                sbPrompt.AppendLine("}");
                var systemPrompt = sbPrompt.ToString();

                // Per spec: if retrying, append the correction note to the user message, not the system prompt
                var userContent = string.IsNullOrWhiteSpace(correctionNote)
                    ? userMessage
                    : userMessage + "\n\n" + correctionNote;

                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userContent }
                    },
                    max_tokens = 800,
                    temperature = 0.1,
                    response_format = new { type = "json_object" }
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync("chat/completions", content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenWebUI extraction call failed: {Status}", response.StatusCode);
                    return AiExtractionResult.Clarify("I can’t reach the AI service to interpret your request. Please try again later.");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseData = JsonDocument.Parse(responseJson);
                var aiText = responseData.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";

                // 1st attempt: safe JSON extraction & parse
                var jsonCandidate = ExtractJsonFromText(aiText);
                try
                {
                    var result = JsonSerializer.Deserialize<AiExtractionResult>(jsonCandidate, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? AiExtractionResult.Clarify("I couldn’t interpret your request. Could you rephrase it?");
                    result.participantEmails ??= new List<string>();
                    // Basic validation: ensure dates are present and sensible
                    if (result.startDate == default || result.endDate == default)
                    {
                        return AiExtractionResult.Clarify("Please specify the date range (start and end) and, if needed, the time of day.");
                    }
                    if (result.endDate < result.startDate)
                    {
                        return AiExtractionResult.Clarify("The end date seems before the start date. Could you clarify the range?");
                    }
                    return result;
                }
                catch (JsonException jx) when (string.IsNullOrEmpty(correctionNote))
                {
                    // Redact emails and log truncated model text for diagnostics
                    var redacted = System.Text.RegularExpressions.Regex.Replace(aiText ?? string.Empty, @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", "<email>");
                    var snippet = redacted.Length > 2000 ? redacted.Substring(0, 2000) + " …[truncated]" : redacted;
                    _logger.LogWarning(jx, "AI extraction JSON parse failed. Raw model text (truncated): {Snippet}", snippet);
                    // Retry once asking the model for strict JSON only
                    return await ExtractParametersAsync(userMessage, nowUtc, config, "Return only ONE compact JSON object. No code fences. No lists. No prose.");
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "AI extraction failed after safe parse attempt.");
                    return AiExtractionResult.Clarify("Could you rephrase your request (days, dates, and duration)?");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI extraction error");
                // Distinguish parsing vs. connectivity indirectly already handled above; generic clarification here
                return AiExtractionResult.Clarify("Could you rephrase your request (days, dates, and duration)?");
            }
        }

        public async Task<string> FormatSlotsAsync(MeetingContext context, IConfiguration config)
        {
            try
            {
                var baseUrl = config["OpenWebUI:BaseUrl"] ?? _httpClient.BaseAddress?.ToString() ?? string.Empty;
                var model = config["OpenWebUI:Model"] ?? DefaultModelName;
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    return "System error: AI formatter unavailable.";
                }
                if (_httpClient.BaseAddress == null)
                {
                    _httpClient.BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/");
                }

                                var systemPrompt = $@"
You are an assistant that formats interview scheduling results (English only).

Input structure:
- You will receive SLOTS lines, one per slot, in this exact pipe-separated format:
    YYYY-MM-DDTHH:mm|YYYY-MM-DDTHH:mm|score|avail/total|available_emails
    Where the first timestamp is StartTime and the second is EndTime.

Critical rules:
- USE THE PROVIDED StartTime and EndTime EXACTLY AS-IS. Do not perform any arithmetic.
- Never derive or guess the end time from duration; never show invalid minutes like :60, :66, :90.

Formatting rules:
- Group slots by day; header: Monday [dd.MM.yyyy].
- Time: HH:mm - HH:mm (24‑hour), copied from the provided timestamps.
- Show ALL days in the requested span that have slots (do not collapse to one day).
- If participants exist: include availability counts and who is unavailable. If not, omit availability details entirely.
- Mark the best slot per day: ⭐ RECOMMENDED.
- If no slots exist, suggest trying different dates, shorter duration, or other times of day.
- Keep it concise, professional, and helpful.
";

                var slotsDesc = new StringBuilder();
                foreach (var s in context.AvailableSlots.OrderBy(x => x.StartTime))
                {
                    var names = (s.AvailableParticipants?.Count ?? 0) > 0
                        ? string.Join(",", s.AvailableParticipants!)
                        : string.Empty;
                    var availCount = s.AvailableParticipants?.Count ?? 0;
                    slotsDesc.AppendLine($"{s.StartTime:yyyy-MM-ddTHH:mm}|{s.EndTime:yyyy-MM-ddTHH:mm}|{s.AvailabilityScore:F2}|{availCount}/{s.TotalParticipants}|{names}");
                }

                var participants = context.Parameters.ParticipantEmails ?? new List<string>();
                var userPrompt = $@"
Original request: {context.OriginalRequest}
Range: {context.Parameters.StartDate:yyyy-MM-dd} to {context.Parameters.EndDate:yyyy-MM-dd}
DurationMinutes: {context.Parameters.DurationMinutes}
TimeOfDay: {context.Parameters.TimeOfDay}
Participants: {(participants.Count > 0 ? string.Join(", ", participants) : "<none>")}
SLOTS:
{slotsDesc}
";

                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    max_tokens = 1200,
                    temperature = 0.5
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseData = JsonDocument.Parse(responseJson);
                var aiText = responseData.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
                return aiText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI formatting error");
                return "System error: could not format the response.";
            }
        }
        public async Task<MeetingParameters> ExtractParametersAsync(string userMessage)
        {
            try
            {
                // Create enhanced system prompt for parameter extraction with business day logic
                string systemPrompt = @"
You are an AI assistant specialized in scheduling interviews. 
Extract scheduling parameters from the user's message.

IMPORTANT BUSINESS RULES:
- Business days are Monday through Friday ONLY
- If today is Friday and user asks for 'tomorrow', interpret as next Monday
- If today is Saturday/Sunday and user asks for 'tomorrow', interpret as next Monday

DATE RANGE INTERPRETATION RULES:
- For 'next week': include ALL business days of the next week (Monday through Friday)
- For phrases like 'first X days of next week': include exactly X business days starting from next Monday
- For 'in X weeks': calculate the correct date range X weeks from current date
- For 'after [date]': use the specified date as the start date

PARTICIPANT RULES:
- ONLY include participants if they are explicitly mentioned in the request
- Do NOT add default or placeholder participant emails
- If no participants mentioned, return empty participantEmails array

FORMAT:
Return JSON with these fields:
- startDate: ISO date for the start of the requested time period
- endDate: ISO date for the end of the requested time period
- durationMinutes: integer for meeting duration (default 60 if not specified)
- participantEmails: array of email addresses from the request (empty array if none specified)
- timeOfDay: ""morning"", ""afternoon"", or ""all""

Current date: " + DateTime.Now.ToString("yyyy-MM-dd") + " (" + DateTime.Now.DayOfWeek + ")";

                // Send request to AI service or use mock data
                if (_useMockData || _httpClient.BaseAddress == null)
                {
                    _logger.LogWarning("Using mock data for parameter extraction");
                    var mockParams = CreateMockParameters(userMessage);
                    return new MeetingParameters 
                    { 
                        Duration = mockParams.DurationMinutes,
                        TimeFrame = $"{mockParams.StartDate:yyyy-MM-dd} to {mockParams.EndDate:yyyy-MM-dd}",
                        Participants = mockParams.ParticipantEmails
                    };
                }
                
                var requestBody = new
                {
                    model = Model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userMessage }
                    },
                    max_tokens = 500,
                    temperature = 0.1 // Low temperature for deterministic results
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");
                
                // Make API call
                var response = await _httpClient.PostAsync("chat/completions", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenWebUI API request failed with status {StatusCode}", response.StatusCode);
                    var mockParams = CreateMockParameters(userMessage);
                    return new MeetingParameters 
                    { 
                        Duration = mockParams.DurationMinutes,
                        TimeFrame = $"{mockParams.StartDate:yyyy-MM-dd} to {mockParams.EndDate:yyyy-MM-dd}",
                        Participants = mockParams.ParticipantEmails
                    };
                }
                
                // Parse response
                var responseJson = await response.Content.ReadAsStringAsync();
                var responseData = JsonDocument.Parse(responseJson);
                
                string aiText = responseData
                    .RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "{}";
                
                // Extract JSON from response (in case there's surrounding text)
                aiText = ExtractJsonFromText(aiText);
                
                // Deserialize the enhanced parameters first, then convert to legacy format
                var enhancedParams = JsonSerializer.Deserialize<EnhancedMeetingParameters>(aiText) ?? CreateMockParameters(userMessage);
                enhancedParams = ValidateParameters(enhancedParams, userMessage);
                
                return new MeetingParameters 
                { 
                    Duration = enhancedParams.DurationMinutes,
                    TimeFrame = $"{enhancedParams.StartDate:yyyy-MM-dd} to {enhancedParams.EndDate:yyyy-MM-dd}",
                    Participants = enhancedParams.ParticipantEmails
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting parameters from message: {Message}", userMessage);
                var mockParams = CreateMockParameters(userMessage);
                return new MeetingParameters 
                { 
                    Duration = mockParams.DurationMinutes,
                    TimeFrame = $"{mockParams.StartDate:yyyy-MM-dd} to {mockParams.EndDate:yyyy-MM-dd}",
                    Participants = mockParams.ParticipantEmails
                };
            }
        }
        
        public async Task<string> GenerateResponseAsync(string systemPrompt, MeetingContext context)
        {
            try
            {
                if (_useMockData || _httpClient.BaseAddress == null)
                {
                    _logger.LogWarning("Using mock data for response generation");
                    return GenerateMockResponse(context);
                }
                
                // Format available slots for the AI
                var slotsDescription = new StringBuilder();
                foreach (var slot in context.AvailableSlots)
                {
                    string availabilityInfo = slot.TotalParticipants > 0 ?
                        $"({slot.AvailableParticipants.Count}/{slot.TotalParticipants} participants available)" :
                        "(All participants available)";
                        
                    slotsDescription.AppendLine($"- {slot.StartTime:yyyy-MM-dd HH:mm} - {slot.EndTime:HH:mm}, {availabilityInfo}, Score: {slot.AvailabilityScore:F2}");
                }
                
                // Format participants
                string participants = string.Join(", ", context.Parameters.ParticipantEmails);
                
                // Create user prompt
                string userPrompt = $@"
Original request: ""{context.OriginalRequest}""
Requested date range: {context.Parameters.StartDate:yyyy-MM-dd} to {context.Parameters.EndDate:yyyy-MM-dd}
Requested duration: {context.Parameters.DurationMinutes} minutes
Participants: {participants}
Time of day preference: {context.Parameters.TimeOfDay}

Available slots:
{slotsDescription}
";

                var requestBody = new
                {
                    model = Model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    max_tokens = 1000,
                    temperature = 0.7
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");
                
                // Make API call
                var response = await _httpClient.PostAsync("chat/completions", content);
                response.EnsureSuccessStatusCode();
                
                // Parse response
                var responseJson = await response.Content.ReadAsStringAsync();
                var responseData = JsonDocument.Parse(responseJson);
                
                string aiText = responseData
                    .RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? GenerateMockResponse(context);
                
                return aiText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response");
                return GenerateMockResponse(context);
            }
        }

        private string ExtractJsonFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "{}";

            // Remove common code fencing ```json ... ``` or ``` ... ```
            string cleaned = text.Trim();
            if (cleaned.StartsWith("```"))
            {
                int firstNewline = cleaned.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    cleaned = cleaned.Substring(firstNewline + 1);
                }
                if (cleaned.EndsWith("```"))
                {
                    cleaned = cleaned.Substring(0, cleaned.Length - 3);
                }
            }

            // Find first balanced JSON object with brace stack
            int start = -1;
            int depth = 0;
            for (int i = 0; i < cleaned.Length; i++)
            {
                if (cleaned[i] == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (cleaned[i] == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        var candidate = cleaned.Substring(start, i - start + 1);
                        candidate = RemoveTrailingCommas(candidate);
                        candidate = SanitizeJsonLike(candidate);
                        return candidate;
                    }
                }
            }
            return "{}";
        }

        private string RemoveTrailingCommas(string json)
        {
            // Remove trailing commas before } or ]
            // This is a conservative cleanup to help strict parsers
            json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*([}\]])", "$1");
            return json;
        }

        private string SanitizeJsonLike(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;

            // Normalize quotes and remove markdown artifacts that models sometimes include
            json = json.Replace('\u201c', '"').Replace('\u201d', '"'); // curly quotes to straight
            json = json.Replace("**", string.Empty);
            json = json.Replace("`", string.Empty);
            // Remove stray asterisks commonly used for bullets/emphasis
            json = json.Replace("*", string.Empty);

            // Quote unquoted keys: match { key: or , key: and insert quotes
            // This avoids interfering with keys already in quotes
            json = System.Text.RegularExpressions.Regex.Replace(
                json,
                @"(?m)([{,]\s*)([A-Za-z_][A-Za-z0-9_]*)(\s*:)",
                @"$1""$2""$3");

            return json;
        }
        
        private EnhancedMeetingParameters ValidateParameters(EnhancedMeetingParameters parameters, string userMessage)
        {
            // Only non-semantic safety checks; no calendar heuristics here (AI-driven only)
            if (parameters.ParticipantEmails.Count == 0)
            {
                var extractedEmails = ExtractEmailsFromMessage(userMessage);
                parameters = parameters with { ParticipantEmails = extractedEmails };
            }
            if (parameters.EndDate < parameters.StartDate)
            {
                parameters = parameters with { EndDate = parameters.StartDate };
            }
            if (parameters.DurationMinutes < 15)
            {
                parameters = parameters with { DurationMinutes = 60 };
            }
            return parameters;
        }
        
    // Removed business-day helpers to avoid encoding calendar rules in code
        
        private List<string> ExtractEmailsFromMessage(string message)
        {
            var emails = new List<string>();
            var matches = System.Text.RegularExpressions.Regex.Matches(
                message, 
                @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
                
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                emails.Add(match.Value);
            }
            
            // CRITICAL: Do NOT add default emails if none found
            // Return empty list if no participants specified
            
            return emails;
        }
        
        // Mock implementations for fallback when AI service fails
        private EnhancedMeetingParameters CreateMockParameters(string userMessage)
        {
            var now = DateTime.Now;
            var emails = ExtractEmailsFromMessage(userMessage);
            
            // Extract basic date references from message for mock response
            var lowerMessage = userMessage.ToLowerInvariant();
            var startDate = now.Date;
            var endDate = now.Date;
            
            if (lowerMessage.Contains("tomorrow"))
            {
                // Avoid hardcoded calendar rules; this mock is only a fallback
                startDate = now.Date.AddDays(1);
                endDate = startDate;
            }
            else if (lowerMessage.Contains("next week"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Monday);
                endDate = startDate.AddDays(4); // Friday of the same week
            }
            else if (lowerMessage.Contains("monday"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Monday);
                endDate = startDate;
            }
            else if (lowerMessage.Contains("tuesday"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Tuesday);
                endDate = startDate;
            }
            else if (lowerMessage.Contains("wednesday"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Wednesday);
                endDate = startDate;
            }
            else if (lowerMessage.Contains("thursday"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Thursday);
                endDate = startDate;
            }
            else if (lowerMessage.Contains("friday"))
            {
                startDate = GetNextSpecificDay(now, DayOfWeek.Friday);
                endDate = startDate;
            }
            else
            {
                startDate = now.Date.AddDays(1);
                endDate = startDate;
            }
            
            // Extract duration if present
            int duration = 60;
            var durationMatch = System.Text.RegularExpressions.Regex.Match(
                userMessage, 
                @"(\d+)\s*(?:min|mins|minutes)");
                
            if (durationMatch.Success && int.TryParse(durationMatch.Groups[1].Value, out int parsedDuration))
            {
                duration = parsedDuration;
            }
            
            // Determine time of day
            string timeOfDay = "all";
            if (lowerMessage.Contains("morning"))
            {
                timeOfDay = "morning";
            }
            else if (lowerMessage.Contains("afternoon"))
            {
                timeOfDay = "afternoon";
            }
            
            return new EnhancedMeetingParameters
            {
                StartDate = startDate,
                EndDate = endDate,
                DurationMinutes = duration,
                ParticipantEmails = emails,
                TimeOfDay = timeOfDay
            };
        }
        
        private DateTime GetNextSpecificDay(DateTime start, DayOfWeek targetDay)
        {
            int daysToAdd = ((int)targetDay - (int)start.DayOfWeek + 7) % 7;
            if (daysToAdd == 0) daysToAdd = 7; // If today is the target day, get next week
            return start.AddDays(daysToAdd);
        }
        
        private string GenerateMockResponse(MeetingContext context)
        {
            if (context.AvailableSlots.Count == 0)
            {
                var day = context.Parameters.StartDate.ToString("dddd [dd.MM.yyyy]", CultureInfo.GetCultureInfo("en-US"));
                return $@"
I couldn't find any suitable {context.Parameters.DurationMinutes}-minute slots for {day}. Would you like me to:

• Check different dates?
• Try a shorter meeting duration?
• Look at different times of day?

Let me know how I can help find a time that works!
";
            }
            
            var sb = new StringBuilder();
            sb.AppendLine($"Based on your request, I've found the following {context.Parameters.DurationMinutes}-minute slots:");
            sb.AppendLine();
            
            // Group by day
            var slotsByDay = context.AvailableSlots
                .GroupBy(s => s.StartTime.Date)
                .OrderBy(g => g.Key);
                
            foreach (var dayGroup in slotsByDay)
            {
                string day = dayGroup.Key.ToString("dddd [dd.MM.yyyy]", CultureInfo.GetCultureInfo("en-US"));
                sb.AppendLine(day);
                sb.AppendLine();
                
                int slotNumber = 1;
                foreach (var slot in dayGroup.OrderBy(s => s.StartTime))
                {
                    string availabilityInfo = slot.TotalParticipants > 0 ?
                        $"({slot.AvailableParticipants.Count}/{slot.TotalParticipants} participants available)" :
                        "(All participants available)";
                        
                    string recommendation = slot.AvailabilityScore > 80 ? "⭐ " : "";
                    
                    sb.AppendLine($"{slotNumber}. {recommendation}{slot.StartTime:HH:mm} - {slot.EndTime:HH:mm} {availabilityInfo}");
                    slotNumber++;
                }
                
                sb.AppendLine();
            }
            
            sb.AppendLine("Please let me know which time slot works best for you!");
            
            return sb.ToString();
        }
    }

    /// <summary>
    /// Original data record for meeting parameters (maintained for compatibility)
    /// </summary>
    public record MeetingParameters
    {
        public int Duration { get; init; } = 30;
        public string TimeFrame { get; init; } = "";
        public List<string> Participants { get; init; } = new();
    }

    // DTOs for new AI extraction contract
    public record DaysSelector
    {
        public string mode { get; init; } = "fullRange"; // fullRange|firstN|specificDays
        public int? n { get; init; }
        public List<string> daysOfWeek { get; init; } = new();
    }

    public record AiClarification
    {
        public string question { get; init; } = string.Empty;
    }

    public record AiExtractionResult
    {
        public DateTime startDate { get; init; }
        public DateTime endDate { get; init; }
        public string timeOfDay { get; init; } = "all";
        public int? durationMinutes { get; init; }
        public List<string> participantEmails { get; set; } = new();
        public DaysSelector daysSelector { get; init; } = new();
        public object? needClarification { get; init; }

        public static AiExtractionResult Clarify(string q) => new AiExtractionResult
        {
            needClarification = new AiClarification { question = q },
            participantEmails = new List<string>(),
            daysSelector = new DaysSelector(),
            timeOfDay = "all",
            startDate = DateTime.UtcNow,
            endDate = DateTime.UtcNow
        };
    }
}