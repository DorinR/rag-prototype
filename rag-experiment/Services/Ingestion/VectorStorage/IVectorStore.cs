using System.Collections.Generic;
using System.Threading.Tasks;

namespace rag_experiment.Services
{
    /// <summary>
    /// Represents a document with its embedding and metadata
    /// </summary>
    public record DocumentEmbedding
    {
        public required string DocumentId { get; init; }
        public required string ChunkText { get; init; }
        public required float[] Embedding { get; init; }
        public required Dictionary<string, string> Metadata { get; init; }
    }

    public interface IVectorStore
    {
        /// <summary>
        /// Stores document embeddings in the vector store
        /// </summary>
        /// <param name="documents">List of documents with their embeddings and metadata</param>
        Task StoreAsync(IEnumerable<DocumentEmbedding> documents);

        /// <summary>
        /// Searches for similar documents using a query embedding
        /// </summary>
        /// <param name="queryEmbedding">Query embedding vector</param>
        /// <param name="limit">Maximum number of results to return</param>
        /// <param name="minScore">Minimum similarity score (0-1) for results</param>
        /// <returns>List of similar documents with their similarity scores</returns>
        Task<IEnumerable<(DocumentEmbedding Document, float Score)>> SearchAsync(
            float[] queryEmbedding,
            int limit = 5,
            float minScore = 0.7f
        );

        /// <summary>
        /// Deletes all documents from the vector store
        /// </summary>
        Task ClearAsync();
    }
} 