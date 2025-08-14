using Microsoft.Extensions.Options;
using rag_experiment.Models;
using rag_experiment.Services.Ingestion.TextExtraction;
using rag_experiment.Services.Ingestion.VectorStorage;

namespace rag_experiment.Services
{

    public class DocumentIngestionService : IDocumentIngestionService
    {
        private readonly ChunkingSettings _textChunkingSettings;
        private readonly ITextExtractor _textExtractor;
        private readonly ITextProcessor _textProcessor;
        private readonly ITextChunker _textChunker;
        private readonly IEmbeddingGenerationService _embeddingGenerationService;
        private readonly EmbeddingRepository _embeddingRepositoryRepository;
        private readonly AppDbContext _dbContext;

        public DocumentIngestionService(
            IOptions<ChunkingSettings> textChunkingSettings,
            ITextExtractor textExtractor,
            ITextProcessor textProcessor,
            ITextChunker textChunker,
            IEmbeddingGenerationService embeddingGenerationService,
            EmbeddingRepository embeddingRepositoryRepository,
            AppDbContext dbContext)
        {
            _textChunkingSettings = textChunkingSettings.Value;
            _textExtractor = textExtractor;
            _textProcessor = textProcessor;
            _textChunker = textChunker;
            _embeddingGenerationService = embeddingGenerationService;
            _embeddingRepositoryRepository = embeddingRepositoryRepository;
            _dbContext = dbContext;
        }

        public async Task<List<DocumentEmbedding>> IngestDocumentAsync(int documentId, int userId, int conversationId)
        {
            int maxChunkSize = _textChunkingSettings.ChunkSize;
            int overlap = _textChunkingSettings.ChunkOverlap;

            var document = await _dbContext.Documents.FindAsync(documentId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID {documentId} not found");
            }

            // Extract text based on file type
            string text;
            if (document.ContentType == "application/pdf")
            {
                var pdfContents = await _textExtractor.ExtractTextAsync(Path.GetDirectoryName(document.FilePath));
                text = pdfContents;
            }
            else
            {
                throw new NotSupportedException($"Document type {document.ContentType} is not supported");
            }

            // Process the text
            var processedText = _textProcessor.ProcessText(text);

            // Split into chunks
            var chunks = _textChunker.ChunkText(processedText, maxChunkSize, overlap);

            // Generate embeddings for each chunk
            var embeddings = await _embeddingGenerationService.GenerateEmbeddingsAsync(chunks);

            // Persist the embeddings
            var result = new List<DocumentEmbedding>();
            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var embedding = embeddings[chunk];

                var documentEmbedding = new DocumentEmbedding
                {
                    DocumentId = $"{document.FilePath}_{i}", // Unique ID for each chunk
                    ChunkText = chunk,
                    Embedding = embedding,
                    Metadata = new Dictionary<string, string>
                    {
                        { "source_file", document.FilePath },
                        { "chunk_index", i.ToString() },
                        { "source_type", "uploaded_document" },
                        { "document_id", document.Id.ToString() },
                        { "document_title", document.OriginalFileName },
                        { "user_id", userId.ToString() },
                        { "conversation_id", conversationId.ToString() }
                    }
                };

                // Add to result list
                result.Add(documentEmbedding);

                // Store in the vector database using the embedding service
                _embeddingRepositoryRepository.AddEmbedding(
                    text: chunk,
                    embeddingData: embedding,
                    documentId: document.Id.ToString(),
                    userId: userId,
                    conversationId: conversationId,
                    documentTitle: document.OriginalFileName,
                    owner: EmbeddingOwner.UserDocument
                );
            }

            // Save changes to the database
            await _dbContext.SaveChangesAsync();

            return result;
        }
    }
}