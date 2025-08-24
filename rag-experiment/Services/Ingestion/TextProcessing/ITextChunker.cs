namespace rag_experiment.Services
{
    public interface ITextChunker
    {
        /// <summary>
        /// Splits the input text into smaller chunks while preserving semantic meaning.
        /// Uses configured chunk size and overlap from RagSettings.
        /// </summary>
        /// <param name="text">Preprocessed text to be chunked</param>
        /// <returns>List of text chunks</returns>
        List<string> ChunkText(string text);
    }
}