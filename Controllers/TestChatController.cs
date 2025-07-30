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
            return Content(GetChatHtml(), "text/html");
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

        private string GetChatHtml()
        {
            return @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Interview Bot Test Chat</title>
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

        .chat-header {
            background-color: #464775;
            color: white;
            padding: 16px 20px;
            font-size: 18px;
            font-weight: 600;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }

        .chat-container {
            flex: 1;
            display: flex;
            flex-direction: column;
            max-width: 1200px;
            margin: 0 auto;
            width: 100%;
            background-color: white;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }

        .messages-container {
            flex: 1;
            overflow-y: auto;
            padding: 20px;
            background-color: #fafafa;
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

        .examples-panel {
            background-color: #fff4ce;
            border: 1px solid #fed100;
            border-radius: 4px;
            padding: 16px;
            margin: 16px 0;
        }

        .examples-title {
            font-weight: 600;
            margin-bottom: 8px;
            color: #323130;
        }

        .example-query {
            background-color: #f8f8f8;
            border-radius: 4px;
            padding: 8px 12px;
            margin: 4px 0;
            cursor: pointer;
            transition: background-color 0.2s;
            border: 1px solid #e1dfdd;
        }

        .example-query:hover {
            background-color: #e1dfdd;
        }

        .card-content {
            background-color: #f8f8f8;
            border: 1px solid #e1dfdd;
            border-radius: 4px;
            padding: 12px;
            margin-top: 8px;
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
            top: 20px;
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
    </style>
</head>
<body>
    <div class=""chat-header"">
        🤖 Interview Scheduling Bot - Test Chat Interface
    </div>
    
    <div class=""chat-container"">
        <div class=""messages-container"" id=""messagesContainer"">
            <div class=""examples-panel"">
                <div class=""examples-title"">💡 Try these natural language queries:</div>
                <div class=""example-query"" onclick=""sendExampleQuery('Find slots on Thursday afternoon')"">Find slots on Thursday afternoon</div>
                <div class=""example-query"" onclick=""sendExampleQuery('Are there any slots next Monday?')"">Are there any slots next Monday?</div>
                <div class=""example-query"" onclick=""sendExampleQuery('Show me morning availability tomorrow')"">Show me morning availability tomorrow</div>
                <div class=""example-query"" onclick=""sendExampleQuery('Find a 30-minute slot this week')"">Find a 30-minute slot this week</div>
                <div class=""example-query"" onclick=""sendExampleQuery('Schedule an interview')"">Schedule an interview</div>
                <div class=""example-query"" onclick=""sendExampleQuery('Help')"">Help</div>
            </div>
            <div class=""typing-indicator"" id=""typingIndicator"">Bot is typing...</div>
        </div>
        
        <div class=""input-container"">
            <input type=""text"" id=""messageInput"" class=""message-input"" placeholder=""Type your message here..."" />
            <button id=""sendButton"" class=""send-button"" onclick=""sendMessage()"">Send</button>
        </div>
    </div>

    <div id=""statusIndicator"" class=""status-indicator status-connected"" style=""display: none;"">
        Connected
    </div>

    <script>
        let conversationId = null;
        let isLoading = false;

        // Initialize the chat
        document.addEventListener('DOMContentLoaded', function() {
            showWelcomeMessage();
            setupEventListeners();
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
            addMessage('Welcome to the Interview Scheduling Bot! 👋\\n\\nI can help you find interview slots using natural language. Try asking me something like:\\n\\n• ""Find slots on Thursday afternoon""\\n• ""Are there any slots next Monday?""\\n• ""Show me morning availability tomorrow""\\n\\nWhat would you like me to help you with today?', 'Interview Bot', true);
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

        function sendExampleQuery(query) {
            const messageInput = document.getElementById('messageInput');
            messageInput.value = query;
            sendMessage();
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
            
            // Handle attachments (like adaptive cards)
            if (attachments && attachments.length > 0) {
                attachments.forEach(attachment => {
                    if (attachment.contentType === 'application/vnd.microsoft.card.adaptive') {
                        const cardDiv = document.createElement('div');
                        cardDiv.className = 'card-content';
                        cardDiv.textContent = 'Adaptive Card: ' + JSON.stringify(attachment.content, null, 2);
                        contentDiv.appendChild(cardDiv);
                    }
                });
            }
            
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