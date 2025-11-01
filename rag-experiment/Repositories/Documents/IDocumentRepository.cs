using rag_experiment.Models;

namespace rag_experiment.Repositories.Documents
{
    /// <summary>
    /// Repository interface for document operations
    /// </summary>
    public interface IDocumentRepository
    {
        /// <summary>
        /// Retrieves a document by its ID
        /// </summary>
        /// <param name="documentId">The document ID to retrieve</param>
        /// <returns>The document if found, null otherwise</returns>
        Task<Document?> GetByIdAsync(int documentId);

        /// <summary>
        /// Retrieves multiple documents by their IDs
        /// </summary>
        /// <param name="documentIds">Collection of document IDs to retrieve</param>
        /// <returns>List of found documents (empty if none found)</returns>
        Task<List<Document>> GetByIdsAsync(IEnumerable<int> documentIds);

        /// <summary>
        /// Retrieves documents by conversation ID
        /// </summary>
        /// <param name="conversationId">The conversation ID</param>
        /// <returns>Collection of documents in the conversation</returns>
        Task<IEnumerable<Document>> GetByConversationIdAsync(int conversationId);

        /// <summary>
        /// Retrieves a document by ID with authorization check (user ownership through conversation)
        /// </summary>
        /// <param name="documentId">The document ID to retrieve</param>
        /// <param name="userId">The user ID for authorization</param>
        /// <returns>The document if found and accessible, null otherwise</returns>
        Task<Document?> GetByIdWithAuthorizationAsync(int documentId, int userId);

        /// <summary>
        /// Retrieves all documents from the database
        /// </summary>
        /// <returns>Collection of all documents</returns>
        Task<List<Document>> GetAllAsync();
    }
}
