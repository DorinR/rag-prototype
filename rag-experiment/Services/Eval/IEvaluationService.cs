using rag_experiment.Models;

namespace rag_experiment.Services
{
    public interface IEvaluationService
    {
        /// <summary>
        /// Reads the predefined queries from the CISI.QRY file
        /// </summary>
        /// <returns>Dictionary mapping query IDs to query text</returns>
        Task<Dictionary<int, string>> ReadQueriesAsync();
        
        /// <summary>
        /// Reads the relevance judgments from the CISI.REL file
        /// </summary>
        /// <returns>Dictionary mapping query IDs to lists of relevant document IDs</returns>
        Task<Dictionary<int, List<string>>> ReadRelevanceJudgmentsAsync();
        
        /// <summary>
        /// Evaluates the system by running predefined queries and calculating metrics
        /// </summary>
        /// <param name="topK">Number of results to retrieve for each query</param>
        /// <returns>Evaluation metrics including precision and recall for each query</returns>
        Task<EvaluationResult> EvaluateSystemAsync(int topK = 10);
    }
} 