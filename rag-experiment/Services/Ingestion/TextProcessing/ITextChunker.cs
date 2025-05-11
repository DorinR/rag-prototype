namespace rag_experiment.Services
{
    public interface ITextChunker
    {
        /// <summary>
        /// Splits the input text into smaller chunks while preserving semantic meaning
        /// </summary>
        /// <param name="text">Preprocessed text to be chunked</param>
        /// <param name="maxChunkSize">Maximum size of each chunk in characters</param>
        /// <param name="overlap">Number of characters to overlap between chunks</param>
        /// <returns>List of text chunks</returns>
        List<string> ChunkText(string text, int maxChunkSize = 1000, int overlap = 100);
    }
} 