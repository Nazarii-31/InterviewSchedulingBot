using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using System.Text.Json;

namespace InterviewSchedulingBot.Controllers
{
    [Route("api/chat")]
    [ApiController]
    public class TestChatController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly IBot _bot;
        private readonly ConversationState _conversationState;
        private readonly UserState _userState;
        private static readonly Dictionary<string, List<Activity>> _messageHistory = new();

        public TestChatController(
            IBotFrameworkHttpAdapter adapter, 
            IBot bot,
            ConversationState conversationState,
            UserState userState)
        {
            _adapter = adapter;
            _bot = bot;
            _conversationState = conversationState;
            _userState = userState;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return Content(GetEnhancedChatHtml(), "text/html");
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessage message)
        {
            try
            {
                var conversationId = message.ConversationId ?? Guid.NewGuid().ToString();
                
                if (!_messageHistory.ContainsKey(conversationId))
                {
                    _messageHistory[conversationId] = new List<Activity>();
                }

                // Create the activity for the user message
                var userActivity = new Activity(ActivityTypes.Message)
                {
                    Id = Guid.NewGuid().ToString(),
                    Timestamp = DateTimeOffset.UtcNow,
                    Text = message.Text,
                    From = new ChannelAccount("user", "Test User"),
                    Recipient = new ChannelAccount("bot", "Interview Bot"),
                    Conversation = new ConversationAccount(false, "personal", conversationId),
                    ChannelId = "test",
                    ServiceUrl = Request.Scheme + "://" + Request.Host.Value
                };

                // Store user message in history
                _messageHistory[conversationId].Add(userActivity);

                // Create a list to capture bot responses
                var botResponses = new List<Activity>();

                // Create a mock turn context with response capture
                var mockContext = new TestTurnContext(userActivity, botResponses);

                // Process the message through the bot
                await _bot.OnTurnAsync(mockContext, default);

                // Store bot responses in history
                _messageHistory[conversationId].AddRange(botResponses);

                // Return the conversation history
                var chatHistory = _messageHistory[conversationId]
                    .Select(a => new ChatHistoryItem
                    {
                        Id = a.Id,
                        Text = a.Text,
                        From = a.From?.Name ?? "Unknown",
                        Timestamp = a.Timestamp?.ToString("HH:mm:ss") ?? DateTime.Now.ToString("HH:mm:ss"),
                        IsBot = a.From?.Name != "Test User",
                        Attachments = a.Attachments?.Select(att => new ChatAttachment
                        {
                            ContentType = att.ContentType,
                            Content = att.Content
                        }).ToList() ?? new List<ChatAttachment>()
                    })
                    .ToList();

                return Ok(new ChatResponse
                {
                    ConversationId = conversationId,
                    Messages = chatHistory
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("history/{conversationId}")]
        public IActionResult GetHistory(string conversationId)
        {
            if (!_messageHistory.ContainsKey(conversationId))
            {
                return Ok(new ChatResponse { ConversationId = conversationId, Messages = new List<ChatHistoryItem>() });
            }

            var chatHistory = _messageHistory[conversationId]
                .Select(a => new ChatHistoryItem
                {
                    Id = a.Id,
                    Text = a.Text,
                    From = a.From?.Name ?? "Unknown",
                    Timestamp = a.Timestamp?.ToString("HH:mm:ss") ?? DateTime.Now.ToString("HH:mm:ss"),
                    IsBot = a.From?.Name != "Test User",
                    Attachments = a.Attachments?.Select(att => new ChatAttachment
                    {
                        ContentType = att.ContentType,
                        Content = att.Content
                    }).ToList() ?? new List<ChatAttachment>()
                })
                .ToList();

            return Ok(new ChatResponse
            {
                ConversationId = conversationId,
                Messages = chatHistory
            });
        }

        private string GetEnhancedChatHtml()
        {
            return @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Interview Scheduling Bot - AI Chat Interface</title>
    <link href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0/css/all.min.css"" rel=""stylesheet"">
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f3f2f1;
            height: 100vh;
            display: flex;
            flex-direction: column;
        }

        .main-header {
            background: linear-gradient(135deg, #464775 0%, #5b5a8a 100%);
            color: white;
            padding: 16px 20px;
            font-size: 20px;
            font-weight: 600;
            box-shadow: 0 2px 8px rgba(0,0,0,0.15);
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .container {
            flex: 1;
            display: flex;
            flex-direction: column;
            max-width: 1400px;
            margin: 0 auto;
            width: 100%;
            background-color: white;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }

        .tabs {
            display: flex;
            background-color: #f8f7fa;
            border-bottom: 1px solid #e1dfdd;
        }

        .tab-button {
            background: none;
            border: none;
            padding: 16px 24px;
            cursor: pointer;
            font-size: 14px;
            font-weight: 600;
            color: #605e5c;
            border-bottom: 3px solid transparent;
            transition: all 0.2s;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .tab-button:hover {
            background-color: #e1dfdd;
            color: #323130;
        }

        .tab-button.active {
            color: #0078d4;
            border-bottom-color: #0078d4;
            background-color: white;
        }

        .tab-content {
            flex: 1;
            display: none;
            flex-direction: column;
            overflow: hidden;
        }

        .tab-content.active {
            display: flex;
        }

        /* Chat Interface Styles */
        .chat-container {
            flex: 1;
            display: flex;
            flex-direction: column;
            height: 100%;
        }

        .chat-info {
            background-color: #fff4ce;
            border: 1px solid #fed100;
            border-radius: 4px;
            padding: 16px;
            margin: 16px;
        }

        .chat-info h3 {
            margin-bottom: 8px;
            color: #323130;
            font-size: 16px;
        }

        .chat-info p {
            color: #605e5c;
            line-height: 1.4;
        }

        .messages-container {
            flex: 1;
            overflow-y: auto;
            padding: 20px;
            background-color: #fafafa;
            min-height: 400px;
        }

        .message {
            margin-bottom: 16px;
            display: flex;
            align-items: flex-start;
            animation: fadeIn 0.3s ease-in;
        }

        .message.user {
            flex-direction: row-reverse;
        }

        .message-avatar {
            width: 32px;
            height: 32px;
            border-radius: 50%;
            margin: 0 8px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            color: white;
            font-size: 14px;
        }

        .message.user .message-avatar {
            background-color: #0078d4;
        }

        .message.bot .message-avatar {
            background-color: #464775;
        }

        .message-content {
            max-width: 70%;
            padding: 12px 16px;
            border-radius: 8px;
            position: relative;
            word-wrap: break-word;
        }

        .message.user .message-content {
            background-color: #0078d4;
            color: white;
            border-bottom-right-radius: 4px;
        }

        .message.bot .message-content {
            background-color: white;
            color: #323130;
            border: 1px solid #e1dfdd;
            border-bottom-left-radius: 4px;
        }

        .message-text {
            line-height: 1.4;
            white-space: pre-wrap;
        }

        .message-timestamp {
            font-size: 11px;
            opacity: 0.7;
            margin-top: 4px;
        }

        .input-container {
            padding: 16px 20px;
            background-color: white;
            border-top: 1px solid #e1dfdd;
            display: flex;
            align-items: center;
            gap: 12px;
        }

        .message-input {
            flex: 1;
            border: 1px solid #e1dfdd;
            border-radius: 4px;
            padding: 12px 16px;
            font-size: 14px;
            outline: none;
            transition: border-color 0.2s;
        }

        .message-input:focus {
            border-color: #0078d4;
        }

        .send-button {
            background-color: #0078d4;
            color: white;
            border: none;
            border-radius: 4px;
            padding: 12px 24px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
            transition: background-color 0.2s;
        }

        .send-button:hover:not(:disabled) {
            background-color: #106ebe;
        }

        .send-button:disabled {
            background-color: #a19f9d;
            cursor: not-allowed;
        }



        .typing-indicator {
            display: none;
            padding: 12px 16px;
            font-style: italic;
            color: #605e5c;
            background-color: #f3f2f1;
            border-radius: 8px;
            margin-bottom: 16px;
            animation: pulse 1.5s infinite;
        }

        /* Mock Data Styles */
        .mock-data-container {
            flex: 1;
            padding: 20px;
            overflow-y: auto;
        }

        .mock-data-section {
            background-color: white;
            border-radius: 8px;
            padding: 20px;
            margin-bottom: 20px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }

        .mock-data-section h3 {
            color: #323130;
            margin-bottom: 16px;
            font-size: 18px;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .form-row {
            display: flex;
            gap: 20px;
            margin-bottom: 16px;
            flex-wrap: wrap;
        }

        .form-group {
            flex: 1;
            min-width: 200px;
        }

        .form-group label {
            display: block;
            margin-bottom: 4px;
            font-weight: 600;
            color: #323130;
        }

        .form-group select, .form-group input {
            width: 100%;
            padding: 8px 12px;
            border: 1px solid #e1dfdd;
            border-radius: 4px;
            font-size: 14px;
        }

        .mock-data-actions {
            display: flex;
            gap: 12px;
            flex-wrap: wrap;
            margin-top: 20px;
        }

        .btn-primary, .btn-secondary {
            padding: 10px 16px;
            border: none;
            border-radius: 4px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
            transition: background-color 0.2s;
            display: flex;
            align-items: center;
            gap: 6px;
        }

        .btn-primary {
            background-color: #0078d4;
            color: white;
        }

        .btn-primary:hover {
            background-color: #106ebe;
        }

        .btn-secondary {
            background-color: #e1dfdd;
            color: #323130;
        }

        .btn-secondary:hover {
            background-color: #d2d0ce;
        }

        .user-card {
            background-color: #f8f7fa;
            border: 1px solid #e1dfdd;
            border-radius: 8px;
            padding: 16px;
            margin-bottom: 12px;
        }

        .user-card h4 {
            color: #323130;
            margin-bottom: 8px;
        }

        .user-card p {
            color: #605e5c;
            margin: 4px 0;
            font-size: 14px;
        }

        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(10px); }
            to { opacity: 1; transform: translateY(0); }
        }

        @keyframes pulse {
            0%, 100% { opacity: 1; }
            50% { opacity: 0.5; }
        }

        .status-indicator {
            position: fixed;
            top: 80px;
            right: 20px;
            padding: 8px 16px;
            border-radius: 4px;
            color: white;
            font-weight: 600;
            z-index: 1000;
            transition: all 0.3s;
        }

        .status-connected {
            background-color: #107c10;
        }

        .status-error {
            background-color: #d13438;
        }

        .info-panel {
            background-color: #deecf9;
            border: 1px solid #0078d4;
            border-radius: 4px;
            padding: 16px;
            margin-bottom: 20px;
        }

        .info-panel h4 {
            color: #0078d4;
            margin-bottom: 8px;
            font-size: 16px;
        }

        .info-panel p {
            color: #323130;
            line-height: 1.4;
        }
    </style>
</head>
<body>
    <div class=""main-header"">
        <i class=""fas fa-robot""></i>
        Interview Scheduling Bot - AI Chat Interface
    </div>
    
    <div class=""container"">
        <div class=""tabs"">
            <button class=""tab-button active"" onclick=""openTab(event, 'chat-interface')"">
                <i class=""fas fa-comments""></i> AI Chat Interface
            </button>
            <button class=""tab-button"" onclick=""openTab(event, 'mock-data')"">
                <i class=""fas fa-database""></i> Mock Data Management
            </button>
        </div>

        <!-- Chat Interface Tab -->
        <div id=""chat-interface"" class=""tab-content active"">
            <div class=""info-panel"">
                <h4><i class=""fas fa-info-circle""></i> AI-Powered Conversational Interface</h4>
                <p>This interface demonstrates the bot's AI-driven natural language processing capabilities. 
                   All responses are dynamically generated using AI without hardcoded templates. 
                   Speak naturally and the bot will understand your scheduling needs.</p>
            </div>
            
            <div class=""chat-container"">
                <div class=""messages-container"" id=""messagesContainer"">
                    <div class=""typing-indicator"" id=""typingIndicator"">Bot is thinking...</div>
                </div>
                
                <div class=""input-container"">
                    <input type=""text"" id=""messageInput"" class=""message-input"" placeholder=""Type your message here..."" />
                    <button id=""sendButton"" class=""send-button"" onclick=""sendMessage()"">Send</button>
                </div>
            </div>
        </div>

        <!-- Mock Data Tab -->
        <div id=""mock-data"" class=""tab-content"">
            <div class=""info-panel"">
                <h4><i class=""fas fa-info-circle""></i> Mock Data Management</h4>
                <p>Control the test data used by the bot to simulate different scheduling scenarios. 
                   Modify user availability, working hours, and calendar events to test various use cases 
                   and see how the AI responds to different constraints.</p>
            </div>
            
            <div class=""mock-data-container"">
                <div class=""mock-data-section"">
                    <h3><i class=""fas fa-cog""></i> Calendar Generation Settings</h3>
                    <div class=""form-row"">
                        <div class=""form-group"">
                            <label for=""generation-duration"">Generate calendar events for:</label>
                            <select id=""generation-duration"">
                                <option value=""1"">1 Day</option>
                                <option value=""3"">3 Days</option>
                                <option value=""7"" selected>1 Week</option>
                                <option value=""14"">2 Weeks</option>
                                <option value=""30"">1 Month</option>
                            </select>
                        </div>
                        <div class=""form-group"">
                            <label for=""generation-density"">Meeting density:</label>
                            <select id=""generation-density"">
                                <option value=""low"">Low (0-1 per day)</option>
                                <option value=""medium"" selected>Medium (1-3 per day)</option>
                                <option value=""high"">High (3-5 per day)</option>
                            </select>
                        </div>
                    </div>
                </div>

                <div class=""mock-data-section"">
                    <h3><i class=""fas fa-users""></i> User Profiles</h3>
                    <div id=""unified-user-data"">
                        <!-- User data will be populated by JavaScript -->
                    </div>
                </div>

                <div class=""mock-data-actions"">
                    <button type=""button"" class=""btn-primary"" onclick=""resetMockData()"">
                        <i class=""fas fa-refresh""></i> Reset to Default
                    </button>
                    <button type=""button"" class=""btn-secondary"" onclick=""generateRandomData()"">
                        <i class=""fas fa-random""></i> Generate Random Data
                    </button>
                    <button type=""button"" class=""btn-secondary"" onclick=""regenerateCalendarData()"">
                        <i class=""fas fa-calendar-plus""></i> Regenerate Calendar Events
                    </button>
                    <button type=""button"" class=""btn-secondary"" onclick=""exportMockData()"">
                        <i class=""fas fa-download""></i> Export Data
                    </button>
                </div>
            </div>
        </div>
    </div>

    <div id=""statusIndicator"" class=""status-indicator status-connected"" style=""display: none;"">
        Connected
    </div>

    <script>
        let conversationId = null;
        let isLoading = false;

        // Tab functionality
        function openTab(evt, tabName) {
            var i, tabcontent, tablinks;
            
            // Hide all tab content
            tabcontent = document.getElementsByClassName('tab-content');
            for (i = 0; i < tabcontent.length; i++) {
                tabcontent[i].classList.remove('active');
            }
            
            // Remove active class from all tab buttons
            tablinks = document.getElementsByClassName('tab-button');
            for (i = 0; i < tablinks.length; i++) {
                tablinks[i].classList.remove('active');
            }
            
            // Show the current tab and mark button as active
            document.getElementById(tabName).classList.add('active');
            evt.currentTarget.classList.add('active');
        }

        // Initialize the chat
        document.addEventListener('DOMContentLoaded', function() {
            showWelcomeMessage();
            setupEventListeners();
            initializeMockData();
        });

        function setupEventListeners() {
            const messageInput = document.getElementById('messageInput');
            const sendButton = document.getElementById('sendButton');
            
            messageInput.addEventListener('keypress', function(e) {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    sendMessage();
                }
            });

            messageInput.addEventListener('input', function() {
                sendButton.disabled = this.value.trim() === '' || isLoading;
            });
        }

        function showWelcomeMessage() {
            addMessage('Welcome to the AI-Powered Interview Scheduling Bot! 🤖\\n\\nI can help you find interview slots using natural language. Every response is generated by AI in real-time - no hardcoded templates! Try asking me something like:\\n\\n• ""Find slots on Thursday afternoon""\\n• ""Are there any slots next Monday?""\\n• ""Show me morning availability tomorrow""\\n\\nWhat would you like me to help you with today?', 'Interview Bot', true);
        }

        async function sendMessage() {
            const messageInput = document.getElementById('messageInput');
            const message = messageInput.value.trim();
            
            if (message === '' || isLoading) return;

            setLoading(true);
            messageInput.value = '';

            // Add user message to chat
            addMessage(message, 'Test User', false);

            // Show typing indicator
            showTypingIndicator();

            try {
                const response = await fetch('/api/chat/send', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({
                        text: message,
                        conversationId: conversationId
                    })
                });

                if (response.ok) {
                    const data = await response.json();
                    conversationId = data.conversationId;
                    
                    // Clear existing messages and show full history
                    clearMessages();
                    data.messages.forEach(msg => {
                        addMessage(msg.text, msg.from, msg.isBot, msg.attachments);
                    });
                    
                    showStatus('Connected', 'connected');
                } else {
                    const error = await response.json();
                    addMessage('❌ Error: ' + (error.error || 'Failed to send message'), 'System', true);
                    showStatus('Error', 'error');
                }
            } catch (error) {
                addMessage('❌ Network error: ' + error.message, 'System', true);
                showStatus('Error', 'error');
            } finally {
                hideTypingIndicator();
                setLoading(false);
            }
        }

        function addMessage(text, from, isBot, attachments = []) {
            const messagesContainer = document.getElementById('messagesContainer');
            const messageDiv = document.createElement('div');
            messageDiv.className = `message ${isBot ? 'bot' : 'user'}`;

            const avatarDiv = document.createElement('div');
            avatarDiv.className = 'message-avatar';
            avatarDiv.textContent = isBot ? '🤖' : 'U';

            const contentDiv = document.createElement('div');
            contentDiv.className = 'message-content';

            const textDiv = document.createElement('div');
            textDiv.className = 'message-text';
            textDiv.textContent = text || '';

            const timestampDiv = document.createElement('div');
            timestampDiv.className = 'message-timestamp';
            timestampDiv.textContent = new Date().toLocaleTimeString();

            contentDiv.appendChild(textDiv);
            contentDiv.appendChild(timestampDiv);

            messageDiv.appendChild(avatarDiv);
            messageDiv.appendChild(contentDiv);

            // Insert before typing indicator
            const typingIndicator = document.getElementById('typingIndicator');
            messagesContainer.insertBefore(messageDiv, typingIndicator);

            // Scroll to bottom
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }

        function clearMessages() {
            const messagesContainer = document.getElementById('messagesContainer');
            const messages = messagesContainer.querySelectorAll('.message');
            messages.forEach(msg => msg.remove());
        }

        function showTypingIndicator() {
            const typingIndicator = document.getElementById('typingIndicator');
            typingIndicator.style.display = 'block';
            
            const messagesContainer = document.getElementById('messagesContainer');
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }

        function hideTypingIndicator() {
            const typingIndicator = document.getElementById('typingIndicator');
            typingIndicator.style.display = 'none';
        }

        function setLoading(loading) {
            isLoading = loading;
            const sendButton = document.getElementById('sendButton');
            const messageInput = document.getElementById('messageInput');
            
            sendButton.disabled = loading || messageInput.value.trim() === '';
            messageInput.disabled = loading;
            
            if (loading) {
                sendButton.textContent = 'Sending...';
            } else {
                sendButton.textContent = 'Send';
            }
        }

        function showStatus(message, type) {
            const statusIndicator = document.getElementById('statusIndicator');
            statusIndicator.textContent = message;
            statusIndicator.className = `status-indicator status-${type}`;
            statusIndicator.style.display = 'block';
            
            setTimeout(() => {
                statusIndicator.style.display = 'none';
            }, 3000);
        }

        // Mock Data Management
        let mockData = {
            userProfiles: [
                {
                    id: '1',
                    name: 'John Doe',
                    email: 'john.doe@company.com',
                    jobTitle: 'Senior Software Engineer',
                    department: 'Engineering',
                    timeZone: 'Pacific Standard Time'
                },
                {
                    id: '2',
                    name: 'Jane Smith',
                    email: 'jane.smith@company.com',
                    jobTitle: 'Product Manager',
                    department: 'Product',
                    timeZone: 'Eastern Standard Time'
                },
                {
                    id: '3',
                    name: 'Bob Wilson',
                    email: 'interviewer@company.com',
                    jobTitle: 'Engineering Manager',
                    department: 'Engineering',
                    timeZone: 'Pacific Standard Time'
                }
            ],
            workingHours: [
                {
                    userEmail: 'john.doe@company.com',
                    timeZone: 'Pacific Standard Time',
                    daysOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
                    startTime: '09:00:00',
                    endTime: '17:00:00'
                },
                {
                    userEmail: 'jane.smith@company.com',
                    timeZone: 'Eastern Standard Time',
                    daysOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
                    startTime: '08:00:00',
                    endTime: '16:00:00'
                },
                {
                    userEmail: 'interviewer@company.com',
                    timeZone: 'Pacific Standard Time',
                    daysOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
                    startTime: '08:30:00',
                    endTime: '17:30:00'
                }
            ],
            calendarAvailability: []
        };

        function initializeMockData() {
            displayUserData();
            generateCalendarData();
        }

        function displayUserData() {
            const container = document.getElementById('unified-user-data');
            if (!container) return;

            container.innerHTML = '';
            
            mockData.userProfiles.forEach(user => {
                const userCard = document.createElement('div');
                userCard.className = 'user-card';
                
                const workingHours = mockData.workingHours.find(wh => wh.userEmail === user.email);
                
                userCard.innerHTML = `
                    <h4>${user.name}</h4>
                    <p><strong>Email:</strong> ${user.email}</p>
                    <p><strong>Title:</strong> ${user.jobTitle}</p>
                    <p><strong>Department:</strong> ${user.department}</p>
                    <p><strong>Time Zone:</strong> ${user.timeZone}</p>
                    <p><strong>Working Hours:</strong> ${workingHours ? workingHours.startTime + ' - ' + workingHours.endTime : 'Not set'}</p>
                `;
                
                container.appendChild(userCard);
            });
        }

        function generateCalendarData() {
            const duration = parseInt(document.getElementById('generation-duration')?.value || '7');
            const density = document.getElementById('generation-density')?.value || 'medium';
            
            // Generate calendar events based on settings
            mockData.calendarAvailability = mockData.userProfiles.map(user => ({
                userEmail: user.email,
                busySlots: generateBusySlots(user.email, duration, density)
            }));
        }

        function generateBusySlots(userEmail, duration, density) {
            const slots = [];
            const now = new Date();
            const eventsPerDay = density === 'low' ? 1 : density === 'medium' ? 2 : 4;
            
            for (let i = 0; i < duration; i++) {
                const date = new Date(now);
                date.setDate(now.getDate() + i);
                
                // Skip weekends
                if (date.getDay() === 0 || date.getDay() === 6) continue;
                
                for (let j = 0; j < eventsPerDay; j++) {
                    const startHour = 9 + Math.floor(Math.random() * 8);
                    const startMinute = Math.random() < 0.5 ? 0 : 30;
                    const durationMins = [30, 60, 90][Math.floor(Math.random() * 3)];
                    
                    const start = new Date(date);
                    start.setHours(startHour, startMinute, 0, 0);
                    
                    const end = new Date(start);
                    end.setMinutes(end.getMinutes() + durationMins);
                    
                    slots.push({
                        start: start.toISOString(),
                        end: end.toISOString(),
                        status: 'Busy',
                        subject: `Meeting ${j + 1}`
                    });
                }
            }
            
            return slots;
        }

        function resetMockData() {
            initializeMockData();
            showStatus('Mock data reset to defaults', 'connected');
        }

        function generateRandomData() {
            generateCalendarData();
            showStatus('Random calendar data generated', 'connected');
        }

        function regenerateCalendarData() {
            generateCalendarData();
            showStatus('Calendar events regenerated', 'connected');
        }

        function exportMockData() {
            const dataStr = JSON.stringify(mockData, null, 2);
            const dataBlob = new Blob([dataStr], {type: 'application/json'});
            const url = URL.createObjectURL(dataBlob);
            const link = document.createElement('a');
            link.href = url;
            link.download = 'mock-data.json';
            link.click();
            URL.revokeObjectURL(url);
            showStatus('Mock data exported', 'connected');
        }
    </script>
</body>
</html>";
        }
    }

    // Custom adapter to capture bot responses
    public class TestChatAdapter
    {
        private readonly List<Activity> _responses;

        public TestChatAdapter(List<Activity> responses)
        {
            _responses = responses;
        }
    }

    // Custom turn context to capture responses
    public class TestTurnContext : ITurnContext
    {
        private readonly List<Activity> _responses;
        private readonly Activity _activity;
        private readonly TurnContextStateCollection _turnState;

        public TestTurnContext(Activity activity, List<Activity> responses)
        {
            _responses = responses;
            _activity = activity;
            _turnState = new TurnContextStateCollection();
        }

        public BotAdapter Adapter => null!; // Not used for testing
        public TurnContextStateCollection TurnState => _turnState;
        public Activity Activity => _activity;
        public bool Responded { get; private set; }

        public async Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            var activityAsConcrete = activity as Activity;
            var activityToSend = activityAsConcrete ?? new Activity
            {
                Type = activity.Type,
                Text = activityAsConcrete?.Text ?? "",
                From = new ChannelAccount("bot", "Interview Bot"),
                Recipient = Activity.From,
                Conversation = Activity.Conversation,
                Timestamp = DateTimeOffset.UtcNow,
                Id = Guid.NewGuid().ToString(),
                Attachments = activityAsConcrete?.Attachments
            };

            _responses.Add(activityToSend);
            Responded = true;

            return new ResourceResponse(activityToSend.Id);
        }

        public async Task<ResourceResponse> SendActivityAsync(string textReplyToSend, string? speak = null, string? inputHint = null, CancellationToken cancellationToken = default)
        {
            return await SendActivityAsync(MessageFactory.Text(textReplyToSend, speak, inputHint), cancellationToken);
        }

        public async Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
        {
            var responses = new List<ResourceResponse>();
            foreach (var activity in activities)
            {
                responses.Add(await SendActivityAsync(activity, cancellationToken));
            }
            return responses.ToArray();
        }

        public async Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            // Not implemented for testing
            return new ResourceResponse();
        }

        public async Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default)
        {
            // Not implemented for testing
        }

        public async Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
        {
            // Not implemented for testing
        }

        public ITurnContext OnSendActivities(SendActivitiesHandler handler)
        {
            // Not implemented for testing
            return this;
        }

        public ITurnContext OnUpdateActivity(UpdateActivityHandler handler)
        {
            // Not implemented for testing
            return this;
        }

        public ITurnContext OnDeleteActivity(DeleteActivityHandler handler)
        {
            // Not implemented for testing
            return this;
        }
    }

    // Data models for the API
    public class ChatMessage
    {
        public string Text { get; set; } = string.Empty;
        public string? ConversationId { get; set; }
    }

    public class ChatResponse
    {
        public string ConversationId { get; set; } = string.Empty;
        public List<ChatHistoryItem> Messages { get; set; } = new();
    }

    public class ChatHistoryItem
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public bool IsBot { get; set; }
        public List<ChatAttachment> Attachments { get; set; } = new();
    }

    public class ChatAttachment
    {
        public string ContentType { get; set; } = string.Empty;
        public object? Content { get; set; }
    }
}