using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace rag_experiment.Services
{
    public class DocumentIngestionService : IDocumentIngestionService
    {
        private readonly IObsidianVaultReader _vaultReader;
        private readonly IPdfDocumentReader _pdfDocumentReader;
        private readonly ITextProcessor _textProcessor;
        private readonly ITextChunker _textChunker;
        private readonly IEmbeddingService _embeddingService;

        public DocumentIngestionService(
            IObsidianVaultReader vaultReader,
            IPdfDocumentReader pdfDocumentReader,
            ITextProcessor textProcessor,
            ITextChunker textChunker,
            IEmbeddingService embeddingService)
        {
            _vaultReader = vaultReader;
            _pdfDocumentReader = pdfDocumentReader;
            _textProcessor = textProcessor;
            _textChunker = textChunker;
            _embeddingService = embeddingService;
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
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunksOnly);

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
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunksOnly);

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