using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using rag_experiment.Models;

namespace rag_experiment.Services
{
    public class ExperimentService : IExperimentService
    {
        private readonly AppDbContext _dbContext;
        private readonly IEvaluationService _evaluationService;
        private readonly RagSettings _ragSettings;
        private readonly MarkdownTableService _markdownTableService;

        public ExperimentService(
            AppDbContext dbContext, 
            IEvaluationService evaluationService,
            IOptions<RagSettings> ragSettings,
            MarkdownTableService markdownTableService)
        {
            _dbContext = dbContext;
            _evaluationService = evaluationService;
            _ragSettings = ragSettings.Value;
            _markdownTableService = markdownTableService;
        }

        public async Task<ExperimentResult> SaveExperimentResultAsync(ExperimentResult experiment)
        {
            if (experiment == null)
            {
                throw new ArgumentNullException(nameof(experiment), "Experiment cannot be null");
            }

            // Ensure we have default values for all required properties
            experiment.Timestamp = DateTime.UtcNow;
            experiment.ExperimentName = string.IsNullOrEmpty(experiment.ExperimentName) 
                ? $"Experiment_{DateTime.UtcNow:yyyyMMdd_HHmmss}" 
                : experiment.ExperimentName;
            experiment.Description = string.IsNullOrEmpty(experiment.Description) 
                ? "No description provided" 
                : experiment.Description;
                
            // Use configuration from RagSettings if not explicitly set
            experiment.EmbeddingModelName = string.IsNullOrEmpty(experiment.EmbeddingModelName) 
                ? _ragSettings.Embedding.ModelName 
                : experiment.EmbeddingModelName;
            experiment.EmbeddingDimension = experiment.EmbeddingDimension <= 0
                ? _ragSettings.Embedding.Dimension
                : experiment.EmbeddingDimension;
            experiment.ChunkSize = experiment.ChunkSize <= 0
                ? _ragSettings.Chunking.ChunkSize
                : experiment.ChunkSize;
            experiment.ChunkOverlap = experiment.ChunkOverlap < 0
                ? _ragSettings.Chunking.ChunkOverlap
                : experiment.ChunkOverlap;
            experiment.TopK = experiment.TopK <= 0
                ? _ragSettings.Retrieval.DefaultTopK
                : experiment.TopK;
                
            // Use text processing settings from config unless explicitly set differently
            if (!experiment.IsTextProcessingExplicitlySet)
            {
                experiment.StopwordRemoval = _ragSettings.TextProcessing.StopwordRemoval;
                experiment.Stemming = _ragSettings.TextProcessing.Stemming;
                experiment.Lemmatization = _ragSettings.TextProcessing.Lemmatization;
                experiment.QueryExpansion = _ragSettings.TextProcessing.QueryExpansion;
            }
            
            experiment.DetailedResults = string.IsNullOrEmpty(experiment.DetailedResults) 
                ? "{}" 
                : experiment.DetailedResults;
            experiment.Notes = string.IsNullOrEmpty(experiment.Notes) 
                ? "No notes provided" 
                : experiment.Notes;

            // Save to database
            await _dbContext.ExperimentResults.AddAsync(experiment);
            await _dbContext.SaveChangesAsync();
            
            // Update Markdown table
            await _markdownTableService.AddExperimentToTableAsync(experiment);
            
            return experiment;
        }

        public async Task<List<ExperimentResult>> GetAllExperimentsAsync()
        {
            return await _dbContext.ExperimentResults
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();
        }

        public async Task<ExperimentResult> GetExperimentByIdAsync(int id)
        {
            return await _dbContext.ExperimentResults.FindAsync(id);
        }

        public async Task<ExperimentResult> RunAndSaveExperimentAsync(
            string experimentName,
            string description,
            int chunkSize,
            int chunkOverlap,
            int topK,
            string embeddingModelName,
            Dictionary<string, bool> textProcessingOptions)
        {
            // Create experiment result object with parameters - use settings from config for defaults
            var experiment = new ExperimentResult
            {
                ExperimentName = !string.IsNullOrEmpty(experimentName) 
                    ? experimentName 
                    : $"Experiment_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Description = !string.IsNullOrEmpty(description) 
                    ? description 
                    : "Experiment run via API",
                ChunkSize = chunkSize > 0 ? chunkSize : _ragSettings.Chunking.ChunkSize,
                ChunkOverlap = chunkOverlap >= 0 ? chunkOverlap : _ragSettings.Chunking.ChunkOverlap,
                TopK = topK > 0 ? topK : _ragSettings.Retrieval.DefaultTopK,
                EmbeddingModelName = !string.IsNullOrEmpty(embeddingModelName) 
                    ? embeddingModelName 
                    : _ragSettings.Embedding.ModelName,
                EmbeddingDimension = _ragSettings.Embedding.Dimension
            };
            
            // Set text processing values from options or config
            if (textProcessingOptions != null && textProcessingOptions.Count > 0)
            {
                experiment.StopwordRemoval = textProcessingOptions.GetValueOrDefault("StopwordRemoval", _ragSettings.TextProcessing.StopwordRemoval);
                experiment.Stemming = textProcessingOptions.GetValueOrDefault("Stemming", _ragSettings.TextProcessing.Stemming);
                experiment.Lemmatization = textProcessingOptions.GetValueOrDefault("Lemmatization", _ragSettings.TextProcessing.Lemmatization);
                experiment.QueryExpansion = textProcessingOptions.GetValueOrDefault("QueryExpansion", _ragSettings.TextProcessing.QueryExpansion);
                experiment.IsTextProcessingExplicitlySet = true;
            }
            else
            {
                experiment.StopwordRemoval = _ragSettings.TextProcessing.StopwordRemoval;
                experiment.Stemming = _ragSettings.TextProcessing.Stemming;
                experiment.Lemmatization = _ragSettings.TextProcessing.Lemmatization;
                experiment.QueryExpansion = _ragSettings.TextProcessing.QueryExpansion;
            }

            try
            {
                // Run evaluation with the specified parameters
                var evaluationTopK = experiment.TopK;
                var evaluationResult = await _evaluationService.EvaluateSystemAsync(evaluationTopK);

                // Store metrics
                experiment.AveragePrecision = evaluationResult.AveragePrecision;
                experiment.AverageRecall = evaluationResult.AverageRecall;
                experiment.AverageF1Score = evaluationResult.AverageF1Score;
                
                // Store detailed results as JSON
                experiment.DetailedResults = JsonSerializer.Serialize(evaluationResult.QueryMetrics);
                
                // Save to database and update Markdown table
                await SaveExperimentResultAsync(experiment);
                
                return experiment;
            }
            catch (Exception ex)
            {
                experiment.Notes = $"Error during experiment: {ex.Message}";
                await SaveExperimentResultAsync(experiment);
                throw;
            }
        }
        
        public async Task RegenerateMarkdownTableAsync()
        {
            try
            {
                // Get all experiments from the database
                var allExperiments = await GetAllExperimentsAsync();
                
                // Regenerate the Markdown table
                await _markdownTableService.RegenerateTableFromExperimentsAsync(allExperiments);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error regenerating Markdown table: {ex.Message}");
                // Don't throw - this is a non-critical feature
            }
        }
    }
} 