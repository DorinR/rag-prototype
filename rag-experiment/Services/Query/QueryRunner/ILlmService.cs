namespace rag_experiment.Services
{
    public interface ILlmService
    {
        /// <summary>
        /// Generates a response to a query using retrieved context chunks
        /// </summary>
        /// <param name="query">The original user query</param>
        /// <param name="context">The retrieved context chunks</param>
        /// <returns>Generated response from the LLM</returns>
        Task<string> GenerateResponseAsync(string query, string context);
    }
} 