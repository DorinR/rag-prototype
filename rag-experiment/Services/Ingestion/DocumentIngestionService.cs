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
        private readonly EmbeddingService _embeddingStorage;
        private readonly AppDbContext _dbContext;

        public DocumentIngestionService(
            IObsidianVaultReader vaultReader,
            IPdfDocumentReader pdfDocumentReader,
            ITextProcessor textProcessor,
            ITextChunker textChunker,
            IEmbeddingGenerationService embeddingGenerationService,
            EmbeddingService embeddingStorage,
            AppDbContext dbContext)
        {
            _vaultReader = vaultReader;
            _pdfDocumentReader = pdfDocumentReader;
            _textProcessor = textProcessor;
            _textChunker = textChunker;
            _embeddingGenerationService = embeddingGenerationService;
            _embeddingStorage = embeddingStorage;
            _dbContext = dbContext;
        }

        public async Task<List<DocumentEmbedding>> IngestDocumentAsync(int documentId, int maxChunkSize = 1000, int overlap = 100)
        {
            // Get the document from the database
            var document = await _dbContext.Documents.FindAsync(documentId);
            if (document == null)
            {
                throw new ArgumentException($"Document with ID {documentId} not found");
            }

            // Read the document content
            string content;
            if (document.ContentType.Contains("pdf"))
            {
                var pdfContent = await _pdfDocumentReader.ReadPdfFilesAsync(Path.GetDirectoryName(document.FilePath));
                content = pdfContent[document.FilePath];
            }
            else
            {
                throw new NotSupportedException($"Document type {document.ContentType} is not supported");
            }

            // Process the text
            var processedText = _textProcessor.ProcessText(content);

            // Chunk the processed text
            var chunks = _textChunker.ChunkText(processedText, maxChunkSize, overlap);

            // Generate embeddings for all chunks
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
                        { "document_title", document.OriginalFileName }
                    }
                };

                // Add to result list
                result.Add(documentEmbedding);

                // Store in the vector database using the embedding service
                _embeddingStorage.AddEmbedding(
                    text: chunk,
                    embeddingData: embedding,
                    documentId: document.Id.ToString(),
                    documentTitle: document.OriginalFileName
                );
            }

            // Save changes to the database
            await _dbContext.SaveChangesAsync();

            return result;
        }

        public async Task<List<DocumentEmbedding>> IngestVaultAsync(string vaultPath, int maxChunkSize = 1000, int overlap = 100)
        {
            // Read all markdown files from the vault
            var files = await _vaultReader.ReadMarkdownFilesAsync(vaultPath);
            var allChunks = new List<(string FilePath, string Chunk, string DocumentId, string DocumentTitle)>();

            // Process each file and create chunks
            foreach (var (filePath, content) in files)
            {
                // Process the text
                var processedText = _textProcessor.ProcessText(content);

                // Chunk the processed text
                var chunks = _textChunker.ChunkText(processedText, maxChunkSize, overlap);

                // Generate document ID for this file
                var documentId = $"file://{Path.GetFullPath(filePath)}";

                // Use the file name as the document title
                var documentTitle = Path.GetFileNameWithoutExtension(filePath);

                // Store chunks with their file path, document ID and title
                allChunks.AddRange(chunks.Select(chunk => (filePath, chunk, documentId, documentTitle)));
            }

            // Generate embeddings for all chunks
            var chunksOnly = allChunks.Select(x => x.Chunk).ToList();
            var embeddings = await _embeddingGenerationService.GenerateEmbeddingsAsync(chunksOnly);

            // Create DocumentEmbedding objects
            var result = new List<DocumentEmbedding>();
            for (var i = 0; i < allChunks.Count; i++)
            {
                var (filePath, chunk, documentId, documentTitle) = allChunks[i];
                var embedding = embeddings[chunk];

                result.Add(new DocumentEmbedding
                {
                    DocumentId = $"{filePath}_{i}", // Unique ID for each chunk
                    ChunkText = chunk,
                    Embedding = embedding,
                    Metadata = new Dictionary<string, string>
                    {
                        { "source_file", filePath },
                        { "chunk_index", i.ToString() },
                        { "source_type", "obsidian_vault" },
                        { "document_id", documentId },
                        { "document_title", documentTitle }
                    }
                });
            }

            return result;
        }

        public async Task<List<DocumentEmbedding>> IngestPdfDocumentsAsync(string directoryPath, int maxChunkSize = 1000, int overlap = 100)
        {
            // Read all PDF files from the directory
            var files = await _pdfDocumentReader.ReadPdfFilesAsync(directoryPath);
            var allChunks = new List<(string FilePath, string Chunk, string DocumentId, string DocumentTitle)>();

            // Process each file and create chunks
            foreach (var (filePath, content) in files)
            {
                // Process the text
                var processedText = _textProcessor.ProcessText(content);

                // Chunk the processed text
                var chunks = _textChunker.ChunkText(processedText, maxChunkSize, overlap);

                // Generate document ID for this file
                var documentId = $"file://{Path.GetFullPath(filePath)}";

                // Use the file name as the document title
                var documentTitle = Path.GetFileNameWithoutExtension(filePath);

                // Store chunks with their file path, document ID and title
                allChunks.AddRange(chunks.Select(chunk => (filePath, chunk, documentId, documentTitle)));
            }

            // Generate embeddings for all chunks
            var chunksOnly = allChunks.Select(x => x.Chunk).ToList();
            var embeddings = await _embeddingGenerationService.GenerateEmbeddingsAsync(chunksOnly);

            // Create DocumentEmbedding objects
            var result = new List<DocumentEmbedding>();
            for (var i = 0; i < allChunks.Count; i++)
            {
                var (filePath, chunk, documentId, documentTitle) = allChunks[i];
                var embedding = embeddings[chunk];

                result.Add(new DocumentEmbedding
                {
                    DocumentId = $"{filePath}_{i}", // Unique ID for each chunk
                    ChunkText = chunk,
                    Embedding = embedding,
                    Metadata = new Dictionary<string, string>
                    {
                        { "source_file", filePath },
                        { "chunk_index", i.ToString() },
                        { "source_type", "pdf_document" },
                        { "document_id", documentId },
                        { "document_title", documentTitle }
                    }
                });
            }

            return result;
        }
    }
}