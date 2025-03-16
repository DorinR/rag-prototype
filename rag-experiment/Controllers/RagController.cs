using System;
using Microsoft.AspNetCore.Mvc;
using rag_experiment.Services;
using System.Threading.Tasks;
using System.Linq;

namespace rag_experiment.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RagController : ControllerBase
    {
        private readonly IDocumentIngestionService _ingestionService;

        public RagController(IDocumentIngestionService ingestionService)
        {
            _ingestionService = ingestionService;
        }

        [HttpPost("ingest")]
        public async Task<IActionResult> Ingest([FromQuery] string vaultPath)
        {
            if (string.IsNullOrEmpty(vaultPath))
            {
                return BadRequest("Vault path is required");
            }

            try
            {
                var result = await _ingestionService.IngestVaultAsync(vaultPath);
                return Ok(new { 
                    message = "Ingestion completed successfully", 
                    filesProcessed = result.Count,
                    totalChunks = result.Values.Sum(chunks => chunks.Count)
                });
            }
            catch (DirectoryNotFoundException)
            {
                return NotFound($"Vault directory not found at: {vaultPath}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred during ingestion: {ex.Message}");
            }
        }

        [HttpPost("query")]
        public IActionResult Query()
        {
            // Placeholder response for testing
            return Ok(new { message = "Query endpoint reached successfully", timestamp = DateTime.UtcNow });
        }
    }
} 