namespace rag_experiment.Services
{
    /// <summary>
    /// Interface for pre-processing user queries before embedding and semantic search
    /// </summary>
    public interface IQueryPreprocessor
    {
        /// <summary>
        /// Pre-processes a user query to improve retrieval results
        /// </summary>
        /// <param name="query">The original user query</param>
        /// <returns>The transformed query</returns>
        Task<string> ProcessQueryAsync(string query);

        /// <summary>
        /// Pre-processes a user query with conversation history context to improve retrieval results
        /// </summary>
        /// <param name="query">The original user query</param>
        /// <param name="conversationHistory">The formatted conversation history for context</param>
        /// <returns>The transformed query</returns>
        Task<string> ProcessQueryAsync(string query, string conversationHistory);

        /// <summary>
        /// Checks if the query matches any special patterns and returns a modified version if needed
        /// </summary>
        /// <param name="query">The original user query</param>
        /// <returns>The transformed query based on pattern matching</returns>
        string ApplyQueryPatterns(string query);

        /// <summary>
        /// Expands the query with additional context or terms to improve retrieval
        /// </summary>
        /// <param name="query">The user query</param>
        /// <returns>The expanded query</returns>
        Task<string> ExpandQueryAsync(string query);
    }
}