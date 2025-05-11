namespace rag_experiment.Services
{
    public interface IEmbeddingGenerationService
    {
        /// <summary>
        /// Generates embeddings for a list of text chunks using OpenAI's API
        /// </summary>
        /// <param name="chunks">List of text chunks to generate embeddings for</param>
        /// <returns>Dictionary mapping each chunk to its embedding vector</returns>
        Task<Dictionary<string, float[]>> GenerateEmbeddingsAsync(IEnumerable<string> chunks);

        /// <summary>
        /// Generates an embedding for a single text chunk
        /// </summary>
        /// <param name="text">Text to generate embedding for</param>
        /// <returns>Embedding vector for the text</returns>
        Task<float[]> GenerateEmbeddingAsync(string text);
    }
}