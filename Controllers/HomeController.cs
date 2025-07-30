using Microsoft.AspNetCore.Mvc;

namespace InterviewSchedulingBot.Controllers
{
    /// <summary>
    /// Controller for serving the main chat interface
    /// </summary>
    public class HomeController : ControllerBase
    {
        /// <summary>
        /// Redirect to the main chat interface
        /// </summary>
        /// <returns>Redirect to chat interface</returns>
        [HttpGet("/")]
        [HttpGet("/ui")]
        [HttpGet("/test")]
        public IActionResult Index()
        {
            return Redirect("/api/chat");
        }
        
        /// <summary>
        /// Health check endpoint
        /// </summary>
        /// <returns>Simple health status</returns>
        [HttpGet("/health")]
        public IActionResult Health()
        {
            return Ok(new { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                message = "Interview Scheduling Bot is running",
                mockServicesEnabled = true
            });
        }
    }
}