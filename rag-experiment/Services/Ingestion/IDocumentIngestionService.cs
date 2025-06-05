using rag_experiment.Services.Ingestion.VectorStorage;

namespace rag_experiment.Services
{
    public interface IDocumentIngestionService
    {
        /// <summary>
        /// Processes a single document and generates embeddings for its content
        /// </summary>
        /// <param name="documentId">The ID of the document to process</param>
        /// <param name="userId">The ID of the user who is ingesting the document</param>
        /// <param name="conversationId">The ID of the conversation the document belongs to</param>
        /// <returns>List of document embeddings generated from the document</returns>
        Task<List<DocumentEmbedding>> IngestDocumentAsync(int documentId, int userId, int conversationId);
    }
}