using rag_experiment.Models;

namespace rag_experiment.Services
{
    public interface IExperimentService
    {
        /// <summary>
        /// Creates a new experiment result entry
        /// </summary>
        /// <param name="experiment">The experiment result to save</param>
        /// <returns>The created experiment with its ID</returns>
        Task<ExperimentResult> SaveExperimentResultAsync(ExperimentResult experiment);
        
        /// <summary>
        /// Gets all experiment results from the database
        /// </summary>
        /// <returns>All stored experiment results</returns>
        Task<List<ExperimentResult>> GetAllExperimentsAsync();
        
        /// <summary>
        /// Gets a specific experiment by its ID
        /// </summary>
        /// <param name="id">The experiment ID</param>
        /// <returns>The experiment result if found, null otherwise</returns>
        Task<ExperimentResult> GetExperimentByIdAsync(int id);
        
        /// <summary>
        /// Runs an experiment with the specified parameters and saves the results
        /// </summary>
        /// <param name="experimentName">Name of the experiment</param>
        /// <param name="description">Description of the experiment</param>
        /// <param name="chunkSize">Size of text chunks</param>
        /// <param name="chunkOverlap">Overlap between chunks</param>
        /// <param name="topK">Number of results to retrieve</param>
        /// <param name="embeddingModelName">Name of the embedding model used</param>
        /// <param name="textProcessingOptions">Dictionary of text processing options</param>
        /// <returns>The experiment result with metrics</returns>
        Task<ExperimentResult> RunAndSaveExperimentAsync(
            string experimentName,
            string description,
            int chunkSize,
            int chunkOverlap,
            int topK,
            string embeddingModelName,
            Dictionary<string, bool> textProcessingOptions);
            
        /// <summary>
        /// Regenerates the Markdown table using all experiments from the database
        /// </summary>
        /// <returns>A task that completes when the table has been regenerated</returns>
        Task RegenerateMarkdownTableAsync();
    }
} 