using rag_experiment.Models;

namespace rag_experiment.Services.Query
{
    /// <summary>
    /// Service for mapping query intent to optimal retrieval configurations.
    /// Determines the appropriate maxK and similarity thresholds based on query type.
    /// </summary>
    public interface IAdaptiveRetrievalStrategy
    {
        /// <summary>
        /// Gets the optimal retrieval configuration for a given query intent
        /// </summary>
        /// <param name="intent">The classified query intent</param>
        /// <param name="query">Optional: the original query text for additional analysis</param>
        /// <returns>RetrievalConfig with maxK and minSimilarity settings</returns>
        RetrievalConfig GetConfigForIntent(QueryIntent intent, string? query = null);
    }
}


