using Microsoft.AspNetCore.Mvc;

namespace rag_experiment.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RagController : ControllerBase
    {
        [HttpPost("ingest")]
        public IActionResult Ingest()
        {
            // Placeholder response for testing
            return Ok(new { message = "Ingest endpoint reached successfully", timestamp = DateTime.UtcNow });
        }

        [HttpPost("query")]
        public IActionResult Query()
        {
            // Placeholder response for testing
            return Ok(new { message = "Query endpoint reached successfully", timestamp = DateTime.UtcNow });
        }
    }
} 