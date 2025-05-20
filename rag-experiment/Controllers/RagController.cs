using Microsoft.AspNetCore.Mvc;
using rag_experiment.Services;
using rag_experiment.Models;
using Microsoft.Extensions.Options;
using System.Text;
using rag_experiment.Services.Ingestion.VectorStorage;

namespace rag_experiment.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RagController : ControllerBase
    {
        private readonly IDocumentIngestionService _ingestionService;
        private readonly EmbeddingStorage _embeddingStorage;
        private readonly IEmbeddingGenerationService _openAiEmbeddingGenerationService;
        private readonly IQueryPreprocessor _queryPreprocessor;
        private readonly IEvaluationService _evaluationService;
        private readonly IExperimentService _experimentService;
        private readonly ICsvExportService _csvExportService;
        private readonly RagSettings _ragSettings;
        private readonly ILlmService _llmService;

        public RagController(
            IDocumentIngestionService ingestionService,
            EmbeddingStorage embeddingStorage,
            IEmbeddingGenerationService openAiEmbeddingGenerationService,
            IQueryPreprocessor queryPreprocessor,
            IEvaluationService evaluationService,
            IExperimentService experimentService,
            ICsvExportService csvExportService,
            IOptions<RagSettings> ragSettings,
            ILlmService llmService)
        {
            _ingestionService = ingestionService;
            _embeddingStorage = embeddingStorage;
            _openAiEmbeddingGenerationService = openAiEmbeddingGenerationService;
            _queryPreprocessor = queryPreprocessor;
            _evaluationService = evaluationService;
            _experimentService = experimentService;
            _csvExportService = csvExportService;
            _ragSettings = ragSettings.Value;
            _llmService = llmService;
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
                var queryEmbedding = await _openAiEmbeddingGenerationService.GenerateEmbeddingAsync(processedQuery);

                // Find similar documents
                var limit = request.Limit > 0 ? request.Limit : 10;
                var similarDocuments = _embeddingStorage.FindSimilarEmbeddings(queryEmbedding, limit);

                // Format the retrieved passages
                var retrievedResults = similarDocuments.Select(doc => new
                {
                    text = doc.Text,
                    documentId = doc.DocumentId,
                    documentTitle = doc.DocumentTitle,
                    similarity = doc.Similarity
                }).ToList();

                // Combine the top chunks into a single context string
                var contextBuilder = new StringBuilder();
                foreach (var doc in retrievedResults)
                {
                    contextBuilder.AppendLine($"--- {doc.documentTitle} ---");
                    contextBuilder.AppendLine(doc.text);
                    contextBuilder.AppendLine();
                }
                string combinedContext = contextBuilder.ToString();

                // Generate LLM response using the combined context
                string llmResponse = await _llmService.GenerateResponseAsync(request.Query, combinedContext);

                // Return the formatted response with both retrieved chunks and LLM answer
                return Ok(new
                {
                    originalQuery = request.Query,
                    processedQuery = processedQuery,
                    llmResponse = llmResponse,
                    retrievedChunks = retrievedResults
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

                // Generate CSV export of all experiment results
                string csvFilePath = await _csvExportService.ExportExperimentsToCSVAsync();

                // Return both the evaluation result and the saved experiment ID
                return Ok(new
                {
                    result,
                    experimentId = experiment.Id,
                    message = $"Results automatically saved to experiment with ID {experiment.Id}",
                    csvExportPath = csvFilePath
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

        [HttpPost("regenerate-markdown-table")]
        public async Task<IActionResult> RegenerateMarkdownTable()
        {
            try
            {
                await _experimentService.RegenerateMarkdownTableAsync();

                // Also regenerate the CSV file
                string csvPath = await _csvExportService.ExportExperimentsToCSVAsync();

                return Ok(new
                {
                    message = "Markdown table and CSV export regenerated successfully",
                    csvPath = csvPath
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred regenerating the Markdown table or CSV: {ex.Message}");
            }
        }

        [HttpGet("export-csv")]
        public async Task<IActionResult> ExportToCsv([FromQuery] string filePath = null)
        {
            try
            {
                string csvPath = await _csvExportService.ExportExperimentsToCSVAsync(filePath);
                return Ok(new
                {
                    message = "CSV export completed successfully",
                    csvPath = csvPath
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred exporting to CSV: {ex.Message}");
            }
        }
    }

    public class QueryRequest
    {
        public string Query { get; set; }
        public int Limit { get; set; } = 10;
    }
}