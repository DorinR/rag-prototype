using rag_experiment.Services.Ingestion;

namespace rag_experiment.Services.BackgroundJobs
{
    public class DocumentProcessingJobService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<DocumentProcessingJobService> _logger;

        public DocumentProcessingJobService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<DocumentProcessingJobService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task ProcessDocumentAsync(int documentId, int userId, int conversationId)
        {
            _logger.LogInformation("Starting document processing for Document ID: {DocumentId}, User ID: {UserId}, Conversation ID: {ConversationId}",
                documentId, userId, conversationId);

            try
            {
                // Create a new scope for this background job to get fresh instances of scoped services
                using var scope = _serviceScopeFactory.CreateScope();
                var ingestionService = scope.ServiceProvider.GetRequiredService<IDocumentIngestionService>();

                await ingestionService.IngestDocumentAsync(documentId, userId, conversationId);

                _logger.LogInformation("Successfully completed document processing for Document ID: {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process document with ID: {DocumentId}, User ID: {UserId}, Conversation ID: {ConversationId}",
                    documentId, userId, conversationId);
                throw; // Hangfire will handle retries
            }
        }
    }
}