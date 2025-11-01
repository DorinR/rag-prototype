using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;
using rag_experiment.Services;

namespace rag_experiment.Repositories.Documents
{
    /// <summary>
    /// Repository implementation for document operations using Entity Framework
    /// </summary>
    public class DocumentRepository : IDocumentRepository
    {
        private readonly AppDbContext _dbContext;

        public DocumentRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Retrieves a document by its ID with full text content
        /// </summary>
        /// <param name="documentId">The document ID to retrieve</param>
        /// <returns>The document if found, null otherwise</returns>
        public async Task<Document?> GetByIdAsync(int documentId)
        {
            return await _dbContext.Documents
                .Include(d => d.Conversation)
                .FirstOrDefaultAsync(d => d.Id == documentId);
        }

        /// <summary>
        /// Retrieves multiple documents by their IDs
        /// </summary>
        /// <param name="documentIds">Collection of document IDs to retrieve</param>
        /// <returns>List of found documents (empty if none found)</returns>
        public async Task<List<Document>> GetByIdsAsync(IEnumerable<int> documentIds)
        {
            if (!documentIds.Any())
                return new List<Document>();

            return await _dbContext.Documents
                .Include(d => d.Conversation)
                .Where(d => documentIds.Contains(d.Id))
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves documents by conversation ID
        /// </summary>
        /// <param name="conversationId">The conversation ID</param>
        /// <returns>Collection of documents in the conversation</returns>
        public async Task<IEnumerable<Document>> GetByConversationIdAsync(int conversationId)
        {
            return await _dbContext.Documents
                .Include(d => d.Conversation)
                .Where(d => d.ConversationId == conversationId)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves a document by ID with authorization check (user ownership through conversation)
        /// </summary>
        /// <param name="documentId">The document ID to retrieve</param>
        /// <param name="userId">The user ID for authorization</param>
        /// <returns>The document if found and accessible, null otherwise</returns>
        public async Task<Document?> GetByIdWithAuthorizationAsync(int documentId, int userId)
        {
            return await _dbContext.Documents
                .Include(d => d.Conversation)
                .FirstOrDefaultAsync(d => d.Id == documentId && d.Conversation.UserId == userId);
        }

        /// <summary>
        /// Retrieves all documents from the database
        /// </summary>
        /// <returns>Collection of all documents</returns>
        public async Task<List<Document>> GetAllAsync()
        {
            return await _dbContext.Documents.ToListAsync();
        }
    }
}
