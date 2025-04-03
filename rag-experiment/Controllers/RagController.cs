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
        private readonly EmbeddingService _embeddingService;

        public RagController(IDocumentIngestionService ingestionService, EmbeddingService embeddingService)
        {
            _ingestionService = ingestionService;
            _embeddingService = embeddingService;
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
                var documents = await _ingestionService.IngestVaultAsync(vaultPath);

                // Persist each document's embedding
                foreach (var document in documents)
                {
                    _embeddingService.AddEmbedding(document.ChunkText, document.Embedding);
                }

                return Ok(new { 
                    message = "Ingestion completed successfully", 
                    documentsProcessed = documents.Count,
                    uniqueFiles = documents.Select(d => d.Metadata["source_file"]).Distinct().Count()
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