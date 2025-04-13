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
        private readonly ICisiPapersReader _cisiPapersReader;
        private readonly ITextProcessor _textProcessor;
        private readonly ITextChunker _textChunker;
        private readonly IEmbeddingService _embeddingService;

        public DocumentIngestionService(
            IObsidianVaultReader vaultReader,
            ICisiPapersReader cisiPapersReader,
            ITextProcessor textProcessor,
            ITextChunker textChunker,
            IEmbeddingService embeddingService)
        {
            _vaultReader = vaultReader;
            _cisiPapersReader = cisiPapersReader;
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
        
        public async Task<List<DocumentEmbedding>> IngestCisiPapersAsync(int maxChunkSize = 1000, int overlap = 100)
        {
            // Read all paper files from the directory
            var files = await _cisiPapersReader.ReadPapersAsync();
            var allChunks = new List<(string FilePath, string Chunk, string DocumentId, string DocumentTitle)>();

            // Process each file and create chunks
            foreach (var (filePath, content) in files)
            {
                // Process the text
                var processedText = _textProcessor.ProcessText(content);

                // Chunk the processed text
                var chunks = _textChunker.ChunkText(processedText, maxChunkSize, overlap);

                // Extract the doc_id from the content
                string docId = "unknown";
                string docTitle = "Unknown Title";
                
                // Look for the doc_id in the content's frontmatter
                if (content.Contains("doc_id:"))
                {
                    // Extract the line with doc_id
                    int startIndex = content.IndexOf("doc_id:");
                    if (startIndex >= 0)
                    {
                        int endIndex = content.IndexOf('\n', startIndex);
                        if (endIndex > startIndex)
                        {
                            string docIdLine = content.Substring(startIndex, endIndex - startIndex).Trim();
                            // Extract just the ID number
                            docId = docIdLine.Replace("doc_id:", "").Trim();
                        }
                    }
                }
                
                // Try to extract a title from the content
                if (content.Contains("title:"))
                {
                    int startIndex = content.IndexOf("title:");
                    if (startIndex >= 0)
                    {
                        int endIndex = content.IndexOf('\n', startIndex);
                        if (endIndex > startIndex)
                        {
                            string titleLine = content.Substring(startIndex, endIndex - startIndex).Trim();
                            docTitle = titleLine.Replace("title:", "").Trim();
                        }
                    }
                }

                // Store chunks with their file path, document ID and title
                allChunks.AddRange(chunks.Select(chunk => (filePath, chunk, docId, docTitle)));
            }

            // Generate embeddings for all chunks
            var chunksOnly = allChunks.Select(x => x.Chunk).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunksOnly);

            // Create DocumentEmbedding objects
            var result = new List<DocumentEmbedding>();
            for (var i = 0; i < allChunks.Count; i++)
            {
                var (filePath, chunk, docId, docTitle) = allChunks[i];
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
                        { "source_type", "cisi_paper" },
                        { "document_id", docId },
                        { "document_title", docTitle }
                    }
                });
            }

            return result;
        }
    }
} 