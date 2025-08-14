using rag_experiment.Models;

namespace rag_experiment.Services.Ingestion.VectorStorage
{
    /// <summary>
    /// Represents a single embedding upsert item with stable identity for idempotent writes.
    /// </summary>
    public record EmbeddingUpsertItem
    {
        /// <summary>
        /// The exact chunk text. Keep as-is (no lowercasing/trim) to match computed hash.
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// The embedding vector for the chunk.
        /// </summary>
        public required float[] Vector { get; init; }

        /// <summary>
        /// The source/owner of the embedding (UserDocument or SystemKnowledgeBase).
        /// </summary>
        public required EmbeddingOwner Owner { get; init; }

        /// <summary>
        /// The owning user id (multi-tenant scoping).
        /// </summary>
        public required int UserId { get; init; }

        /// <summary>
        /// The conversation id (additional scope boundary).
        /// </summary>
        public required int ConversationId { get; init; }

        /// <summary>
        /// Logical document id for grouping embeddings by source document.
        /// </summary>
        public required string DocumentId { get; init; }

        /// <summary>
        /// Stable position of the chunk within the document. Used for uniqueness.
        /// </summary>
        public required int ChunkIndex { get; init; }

        /// <summary>
        /// SHA-256 hash of the canonicalized chunk text. Used for change detection.
        /// </summary>
        public required byte[] ChunkHash { get; init; }

        /// <summary>
        /// Optional document title for display/query convenience.
        /// </summary>
        public string? DocumentTitle { get; init; }
    }

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

    public interface IEmbeddingRepository
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
        /// <param name="owner">The source/owner of the embedding (UserDocument or SystemKnowledgeBase)</param>
        void AddEmbedding(string text, float[] embeddingData, string documentId, int userId, int conversationId, string documentTitle, EmbeddingOwner owner);

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
        /// <param name="owner">Optional new source/owner of the embedding</param>
        void UpdateEmbedding(int id, string newText, float[] newEmbeddingData, string? documentId = null, string? documentTitle = null, EmbeddingOwner? owner = null);

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
        /// Finds the most similar embeddings in the database to the query embedding, scoped to a user's conversation and UserDocument embeddings only
        /// </summary>
        /// <param name="queryEmbedding">The query embedding vector</param>
        /// <param name="conversationId">The conversation ID to scope the search to</param>
        /// <param name="topK">Number of results to return</param>
        /// <returns>List of text chunks, document IDs, document titles, and their similarity scores, ordered by similarity</returns>
        List<(string Text, string DocumentId, string DocumentTitle, float Similarity)> FindSimilarEmbeddingsFromUsersDocuments(float[] queryEmbedding, int conversationId, int topK = 10);

        /// <summary>
        /// Finds the most similar embeddings in the database to the query embedding across all user's conversations, limited to UserDocument embeddings only
        /// </summary>
        /// <param name="queryEmbedding">The query embedding vector</param>
        /// <param name="topK">Number of results to return</param>
        /// <returns>List of text chunks, document IDs, document titles, and their similarity scores, ordered by similarity</returns>
        List<(string Text, string DocumentId, string DocumentTitle, float Similarity)> FindSimilarEmbeddingsAllConversations(float[] queryEmbedding, int topK = 10);

        /// <summary>
        /// Finds the most similar embeddings in the database to the query embedding across ALL SystemKnowledgeBase embeddings.
        /// This searches only through system knowledge base content, excluding user-uploaded documents.
        /// </summary>
        /// <param name="queryEmbedding">The query embedding vector</param>
        /// <param name="topK">Number of results to return</param>
        /// <returns>Task containing a list of text chunks, document IDs, document titles, and their similarity scores, ordered by similarity</returns>
        Task<List<(string Text, string DocumentId, string DocumentTitle, float Similarity)>> FindSimilarEmbeddingsAsync(float[] queryEmbedding, int topK = 10);

        /// <summary>
        /// Upserts a batch of embeddings using a stable uniqueness key such as (UserId, ConversationId, DocumentId, ChunkIndex).
        /// Implementations should insert missing rows and update existing rows only when content (e.g., ChunkHash or Vector) changed.
        /// The operation SHOULD be executed in as few database roundtrips as possible (ideally 1 transaction/batch).
        /// </summary>
        /// <param name="items">Batch of embedding items to upsert.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task that completes when the batch upsert finishes.</returns>
        Task UpsertEmbeddingsAsync(IEnumerable<EmbeddingUpsertItem> items, CancellationToken cancellationToken = default);
    }
}