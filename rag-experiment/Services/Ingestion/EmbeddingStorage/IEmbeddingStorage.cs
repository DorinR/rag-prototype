namespace rag_experiment.Services.Ingestion.VectorStorage
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

    public interface IEmbeddingStorage
    {
        /// <summary>
        /// Adds a new embedding to the database
        /// </summary>
        /// <param name="text">The text content</param>
        /// <param name="embeddingData">The embedding vector</param>
        /// <param name="documentId">document ID</param>
        /// <param name="userId">user ID</param>
        /// <param name="conversationId">conversation ID</param>
        /// <param name="documentTitle">document title</param>
        void AddEmbedding(string text, float[] embeddingData, string documentId, int userId, int conversationId, string documentTitle);

        /// <summary>
        /// Retrieves an embedding by its ID
        /// </summary>
        /// <param name="id">The embedding ID</param>
        /// <returns>Tuple containing the embedding details</returns>
        (int Id, string Text, float[] EmbeddingVector, string DocumentId, string DocumentTitle) GetEmbedding(int id);

        /// <summary>
        /// Updates an existing embedding
        /// </summary>
        /// <param name="id">The embedding ID to update</param>
        /// <param name="newText">New text content</param>
        /// <param name="newEmbeddingData">New embedding vector</param>
        /// <param name="documentId">Optional new document ID</param>
        /// <param name="documentTitle">Optional new document title</param>
        void UpdateEmbedding(int id, string newText, float[] newEmbeddingData, string documentId = null, string documentTitle = null);

        /// <summary>
        /// Deletes an embedding from the database
        /// </summary>
        /// <param name="id">The embedding ID to delete</param>
        void DeleteEmbedding(int id);

        /// <summary>
        /// Deletes all embeddings associated with a specific document ID
        /// </summary>
        /// <param name="documentId">The document ID whose embeddings should be deleted</param>
        void DeleteEmbeddingsByDocumentId(string documentId);

        /// <summary>
        /// Finds the most similar embeddings in the database to the query embedding, scoped to a conversation
        /// </summary>
        /// <param name="queryEmbedding">The query embedding vector</param>
        /// <param name="conversationId">The conversation ID to scope the search to</param>
        /// <param name="topK">Number of results to return</param>
        /// <returns>List of text chunks, document IDs, document titles, and their similarity scores, ordered by similarity</returns>
        List<(string Text, string DocumentId, string DocumentTitle, float Similarity)> FindSimilarEmbeddings(float[] queryEmbedding, int conversationId, int topK = 10);

        /// <summary>
        /// Finds the most similar embeddings in the database to the query embedding across all user's conversations
        /// </summary>
        /// <param name="queryEmbedding">The query embedding vector</param>
        /// <param name="topK">Number of results to return</param>
        /// <returns>List of text chunks, document IDs, document titles, and their similarity scores, ordered by similarity</returns>
        List<(string Text, string DocumentId, string DocumentTitle, float Similarity)> FindSimilarEmbeddingsAllConversations(float[] queryEmbedding, int topK = 10);
    }
}