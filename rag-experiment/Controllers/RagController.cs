using System;
using Microsoft.AspNetCore.Mvc;
using rag_experiment.Services;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace rag_experiment.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RagController : ControllerBase
    {
        private readonly IDocumentIngestionService _ingestionService;
        private readonly EmbeddingService _embeddingService;
        private readonly IEmbeddingService _openAIEmbeddingService;
        private readonly IQueryPreprocessor _queryPreprocessor;

        public RagController(
            IDocumentIngestionService ingestionService, 
            EmbeddingService embeddingService,
            IEmbeddingService openAIEmbeddingService,
            IQueryPreprocessor queryPreprocessor)
        {
            _ingestionService = ingestionService;
            _embeddingService = embeddingService;
            _openAIEmbeddingService = openAIEmbeddingService;
            _queryPreprocessor = queryPreprocessor;
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
        public async Task<IActionResult> Query([FromBody] QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.Query))
            {
                return BadRequest("Query is required");
            }

            try
            {
                // Pre-process the query
                string processedQuery = await _queryPreprocessor.ProcessQueryAsync(request.Query);
                
                // Generate embedding for the processed query
                var queryEmbedding = await _openAIEmbeddingService.GenerateEmbeddingAsync(processedQuery);
                
                // Find similar documents
                var limit = request.Limit > 0 ? request.Limit : 10;
                var similarDocuments = _embeddingService.FindSimilarEmbeddings(queryEmbedding, limit);
                
                // Format the response
                var result = similarDocuments.Select(doc => new
                {
                    text = doc.Text,
                    similarity = doc.Similarity
                }).ToList();
                
                return Ok(new
                {
                    originalQuery = request.Query,
                    processedQuery = processedQuery,
                    results = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred processing the query: {ex.Message}");
            }
        }
    }

    public class QueryRequest
    {
        public string Query { get; set; }
        public int Limit { get; set; } = 10;
    }
} 