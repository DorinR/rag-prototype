using rag_experiment.Models;

namespace rag_experiment.Services.Query
{
    /// <summary>
    /// Service for classifying user query intent to enable adaptive retrieval strategies.
    /// Determines whether a query is factual, comprehensive, exploratory, or comparative.
    /// </summary>
    public interface IQueryIntentClassifier
    {
        /// <summary>
        /// Classifies the intent of a user query using LLM-based analysis
        /// </summary>
        /// <param name="query">The user's query text</param>
        /// <returns>QueryIntentResult containing the detected intent and reasoning</returns>
        Task<QueryIntentResult> ClassifyQueryAsync(string query);
    }
}


