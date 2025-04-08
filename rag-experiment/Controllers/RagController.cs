using System;
using Microsoft.AspNetCore.Mvc;
using rag_experiment.Services;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using rag_experiment.Models;
using System.IO;
using Microsoft.Extensions.Options;

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
        private readonly IEvaluationService _evaluationService;
        private readonly IExperimentService _experimentService;
        private readonly RagSettings _ragSettings;

        public RagController(
            IDocumentIngestionService ingestionService,
            EmbeddingService embeddingService,
            IEmbeddingService openAIEmbeddingService,
            IQueryPreprocessor queryPreprocessor,
            IEvaluationService evaluationService,
            IExperimentService experimentService,
            IOptions<RagSettings> ragSettings)
        {
            _ingestionService = ingestionService;
            _embeddingService = embeddingService;
            _openAIEmbeddingService = openAIEmbeddingService;
            _queryPreprocessor = queryPreprocessor;
            _evaluationService = evaluationService;
            _experimentService = experimentService;
            _ragSettings = ragSettings.Value;
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
                
                // Add embeddings to the store (note: in a real-world scenario, you'd store these in a vector database)
                foreach (var document in documents)
                {
                    _embeddingService.AddEmbedding(
                        document.ChunkText, 
                        document.Embedding,
                        document.Metadata.TryGetValue("document_link", out var link) ? link : "");
                }
                
                return Ok(new { 
                    message = "Ingestion completed successfully", 
                    documentsProcessed = documents.Count,
                    uniqueFiles = documents.Select(d => d.Metadata["source_file"]).Distinct().Count()
                });
            }
            catch (DirectoryNotFoundException)
            {
                return NotFound($"The specified vault directory was not found: {vaultPath}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred during ingestion: {ex.Message}");
            }
        }

        [HttpPost("ingest-cisi-papers")]
        public async Task<IActionResult> IngestCisiPapers()
        {
            try
            {
                var documents = await _ingestionService.IngestCisiPapersAsync();

                // Persist each document's embedding
                foreach (var document in documents)
                {
                    _embeddingService.AddEmbedding(
                        document.ChunkText, 
                        document.Embedding,
                        document.Metadata.TryGetValue("document_link", out var link) ? link : "");
                }

                return Ok(new { 
                    message = "CISI papers ingestion completed successfully", 
                    documentsProcessed = documents.Count,
                    uniqueFiles = documents.Select(d => d.Metadata["source_file"]).Distinct().Count()
                });
            }
            catch (DirectoryNotFoundException ex)
            {
                return NotFound($"CISI papers directory not found: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred during CISI papers ingestion: {ex.Message}");
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
                    documentLink = doc.DocumentLink,
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

        [HttpPost("evaluate")]
        public async Task<IActionResult> Evaluate([FromBody] EvaluationRequest request)
        {
            try
            {
                Console.WriteLine("Starting system evaluation...");
                
                // Set default request object if null
                request ??= new EvaluationRequest();
                
                // Run the evaluation using topK from request or config
                var topK = request.TopK > 0 ? request.TopK : _ragSettings.Retrieval.DefaultTopK;
                var result = await _evaluationService.EvaluateSystemAsync(topK);
                
                // Create experiment result object with values from request or configuration
                var experiment = new ExperimentResult
                {
                    ExperimentName = !string.IsNullOrEmpty(request.ExperimentName) 
                        ? request.ExperimentName 
                        : $"Evaluation_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                    Description = !string.IsNullOrEmpty(request.Description)
                        ? request.Description
                        : $"Auto-generated from evaluation with TopK={topK}",
                    TopK = topK,
                    
                    // Use values from the actual system configuration
                    AveragePrecision = result.AveragePrecision,
                    AverageRecall = result.AverageRecall,
                    AverageF1Score = result.AverageF1Score,
                    DetailedResults = System.Text.Json.JsonSerializer.Serialize(result.QueryMetrics),
                    Notes = "Automatically saved from evaluate endpoint"
                };
                
                // Let the ExperimentService assign the rest of the values from configuration
                await _experimentService.SaveExperimentResultAsync(experiment);
                
                // Return both the evaluation result and the saved experiment ID
                return Ok(new 
                {
                    result,
                    experimentId = experiment.Id,
                    message = $"Results automatically saved to experiment with ID {experiment.Id}"
                });
            }
            catch (FileNotFoundException ex)
            {
                return NotFound($"Evaluation file not found: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred during evaluation: {ex.Message}");
            }
        }
        
        [HttpPost("regenerate-latex-table")]
        public async Task<IActionResult> RegenerateLatexTable()
        {
            try
            {
                await _experimentService.RegenerateLatexTableAsync();
                return Ok(new { message = "LaTeX table regenerated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred regenerating the LaTeX table: {ex.Message}");
            }
        }
    }

    public class QueryRequest
    {
        public string Query { get; set; }
        public int Limit { get; set; } = 10;
    }
} 