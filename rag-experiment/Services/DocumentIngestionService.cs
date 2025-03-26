using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace rag_experiment.Services
{
    public class DocumentIngestionService : IDocumentIngestionService
    {
        private readonly IObsidianVaultReader _vaultReader;
        private readonly ITextProcessor _textProcessor;
        private readonly ITextChunker _textChunker;
        private readonly IEmbeddingService _embeddingService;

        public DocumentIngestionService(
            IObsidianVaultReader vaultReader,
            ITextProcessor textProcessor,
            ITextChunker textChunker,
            IEmbeddingService embeddingService)
        {
            _vaultReader = vaultReader;
            _textProcessor = textProcessor;
            _textChunker = textChunker;
            _embeddingService = embeddingService;
        }

        public async Task<List<DocumentEmbedding>> IngestVaultAsync(string vaultPath, int maxChunkSize = 1000, int overlap = 100)
        {
            // Read all markdown files from the vault
            var files = await _vaultReader.ReadMarkdownFilesAsync(vaultPath);
            var allChunks = new List<(string FilePath, string Chunk)>();

            // Process each file and create chunks
            foreach (var (filePath, content) in files)
            {
                // Process the text
                var processedText = _textProcessor.ProcessText(content);

                // Chunk the processed text
                var chunks = _textChunker.ChunkText(processedText, maxChunkSize, overlap);

                // Store chunks with their file path
                allChunks.AddRange(chunks.Select(chunk => (filePath, chunk)));
            }

            // Generate embeddings for all chunks
            var chunksOnly = allChunks.Select(x => x.Chunk).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunksOnly);

            // Create DocumentEmbedding objects
            var result = new List<DocumentEmbedding>();
            for (var i = 0; i < allChunks.Count; i++)
            {
                var (filePath, chunk) = allChunks[i];
                var embedding = embeddings[chunk];

                result.Add(new DocumentEmbedding
                {
                    DocumentId = $"{filePath}_{i}", // Unique ID for each chunk
                    ChunkText = chunk,
                    Embedding = embedding,
                    Metadata = new Dictionary<string, string>
                    {
                        { "source_file", filePath },
                        { "chunk_index", i.ToString() }
                    }
                });
            }

            return result;
        }
    }
} 