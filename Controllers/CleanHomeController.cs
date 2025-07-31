using Microsoft.AspNetCore.Mvc;

namespace InterviewSchedulingBot.Controllers
{
    /// <summary>
    /// Clean home controller with explicit routing to avoid conflicts
    /// </summary>
    [Route("clean")]
    public class CleanHomeController : ControllerBase
    {
        /// <summary>
        /// Main interface redirecting to clean mock data management
        /// </summary>
        [HttpGet("")]
        [HttpGet("interface")]
        public IActionResult Index()
        {
            return Redirect("/api/mock-data/interface");
        }
        
        /// <summary>
        /// Clean AI chat interface
        /// </summary>
        [HttpGet("chat")]
        public IActionResult Chat()
        {
            return Content(GetCleanChatInterface(), "text/html");
        }
        
        /// <summary>
        /// Health check with clean response
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                message = "Clean Interview Scheduling Bot is running",
                version = "2.0-clean"
            });
        }

        private string GetCleanChatInterface()
        {
            return @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Clean AI Chat Interface</title>
    <link href=""https://cdnjs.cloudflare.com/ajax/libs/bootstrap/5.3.0/css/bootstrap.min.css"" rel=""stylesheet"">
    <style>
        .chat-container { height: 70vh; border: 1px solid #ddd; border-radius: 8px; }
        .messages { height: 100%; overflow-y: auto; padding: 16px; background: #f8f9fa; }
        .message { margin-bottom: 12px; }
        .message.user { text-align: right; }
        .message-bubble { 
            display: inline-block; 
            padding: 8px 12px; 
            border-radius: 12px; 
            max-width: 70%; 
        }
        .message.user .message-bubble { background: #007bff; color: white; }
        .message.bot .message-bubble { background: white; border: 1px solid #ddd; }
        .input-group { margin-top: 16px; }
    </style>
</head>
<body>
    <div class=""container mt-4"">
        <h1>Clean AI Chat Interface</h1>
        <div class=""row"">
            <div class=""col-md-8"">
                <div class=""chat-container"">
                    <div id=""messages"" class=""messages"">
                        <div class=""message bot"">
                            <div class=""message-bubble"">
                                Hello! I'm your clean AI-powered Interview Scheduling assistant. 
                                Ask me to find time slots using natural language.
                            </div>
                        </div>
                    </div>
                </div>
                <div class=""input-group"">
                    <input type=""text"" id=""messageInput"" class=""form-control"" placeholder=""Type your message..."">
                    <button class=""btn btn-primary"" onclick=""sendMessage()"">Send</button>
                </div>
            </div>
            <div class=""col-md-4"">
                <div class=""card"">
                    <div class=""card-header"">Quick Actions</div>
                    <div class=""card-body"">
                        <a href=""/api/mock-data/interface"" class=""btn btn-outline-primary w-100 mb-2"">
                            Mock Data Management
                        </a>
                        <a href=""/clean/health"" class=""btn btn-outline-success w-100"">
                            Health Check
                        </a>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <script>
        async function sendMessage() {
            const input = document.getElementById('messageInput');
            const message = input.value.trim();
            if (!message) return;

            addMessage(message, 'user');
            input.value = '';

            try {
                // Extract parameters using clean service
                const response = await fetch('/api/mock-data/extract-parameters', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ message })
                });

                const result = await response.json();
                
                let botResponse;
                if (result.isSlotRequest) {
                    botResponse = `I detected a scheduling request! 
                    Duration: ${result.duration} minutes
                    Time: ${result.timeFrame?.timeOfDay || 'any time'}
                    Date: ${result.timeFrame?.date || 'flexible'}
                    Type: ${result.timeFrame?.type || 'flexible'}`;
                } else {
                    botResponse = 'I can help you find available time slots. Try asking something like ""Find slots tomorrow morning"" or ""Schedule 90 minutes next week"".';
                }
                
                addMessage(botResponse, 'bot');
            } catch (error) {
                addMessage('Sorry, I encountered an error. Please try again.', 'bot');
            }
        }

        function addMessage(text, sender) {
            const messagesDiv = document.getElementById('messages');
            const messageDiv = document.createElement('div');
            messageDiv.className = `message ${sender}`;
            messageDiv.innerHTML = `<div class=""message-bubble"">${text}</div>`;
            messagesDiv.appendChild(messageDiv);
            messagesDiv.scrollTop = messagesDiv.scrollHeight;
        }

        document.getElementById('messageInput').addEventListener('keypress', function(e) {
            if (e.key === 'Enter') {
                sendMessage();
            }
        });
    </script>
</body>
</html>";
        }
    }
}