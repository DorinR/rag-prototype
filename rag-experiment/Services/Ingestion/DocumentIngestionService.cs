using rag_experiment.Services.Ingestion.VectorStorage;

namespace rag_experiment.Services
{

    public class DocumentIngestionService : IDocumentIngestionService
    {
        private readonly IObsidianVaultReader _vaultReader;
        private readonly IPdfDocumentReader _pdfDocumentReader;
        private readonly ITextProcessor _textProcessor;
        private readonly ITextChunker _textChunker;
        private readonly IEmbeddingGenerationService _embeddingGenerationService;
        private readonly EmbeddingStorage _embeddingStorageStorage;
        private readonly AppDbContext _dbContext;

        public DocumentIngestionService(
            IObsidianVaultReader vaultReader,
            IPdfDocumentReader pdfDocumentReader,
            ITextProcessor textProcessor,
            ITextChunker textChunker,
            IEmbeddingGenerationService embeddingGenerationService,
            EmbeddingStorage embeddingStorageStorage,
            AppDbContext dbContext)
        {
            _vaultReader = vaultReader;
            _pdfDocumentReader = pdfDocumentReader;
            _textProcessor = textProcessor;
            _textChunker = textChunker;
            _embeddingGenerationService = embeddingGenerationService;
            _embeddingStorageStorage = embeddingStorageStorage;
            _dbContext = dbContext;
        }

        public async Task<List<DocumentEmbedding>> IngestDocumentAsync(int documentId, int userId, int conversationId)
        {
            int maxChunkSize = 1000;
            int overlap = 200;


            var document = await _dbContext.Documents.FindAsync(documentId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID {documentId} not found");
            }

            // Extract text based on file type
            string text;
            if (document.ContentType == "application/pdf")
            {
                var pdfContents = await _pdfDocumentReader.ReadPdfFilesAsync(Path.GetDirectoryName(document.FilePath));
                text = pdfContents[document.FilePath];
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

            // Create DocumentEmbedding objects and store them
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
                _embeddingStorageStorage.AddEmbedding(
                    text: chunk,
                    embeddingData: embedding,
                    documentId: document.Id.ToString(),
                    userId: userId,
                    conversationId: conversationId,
                    documentTitle: document.OriginalFileName
                );
            }

            // Save changes to the database
            await _dbContext.SaveChangesAsync();

            return result;
        }
    }
}