using Microsoft.AspNetCore.Mvc;

namespace InterviewSchedulingBot.Controllers
{
    /// <summary>
    /// Controller for serving the UI test interface
    /// </summary>
    public class HomeController : ControllerBase
    {
        /// <summary>
        /// Serve the main UI testing interface
        /// </summary>
        /// <returns>HTML page for testing bot functionality</returns>
        [HttpGet("/")]
        [HttpGet("/ui")]
        [HttpGet("/test")]
        public IActionResult Index()
        {
            return PhysicalFile(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"),
                "text/html"
            );
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