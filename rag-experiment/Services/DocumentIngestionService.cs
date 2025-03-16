namespace rag_experiment.Services
{
    public class DocumentIngestionService : IDocumentIngestionService
    {
        private readonly IObsidianVaultReader _vaultReader;
        private readonly ITextProcessor _textProcessor;
        private readonly ITextChunker _textChunker;

        public DocumentIngestionService(
            IObsidianVaultReader vaultReader,
            ITextProcessor textProcessor,
            ITextChunker textChunker)
        {
            _vaultReader = vaultReader;
            _textProcessor = textProcessor;
            _textChunker = textChunker;
        }

        public async Task<Dictionary<string, List<string>>> IngestVaultAsync(string vaultPath, int maxChunkSize = 1000, int overlap = 100)
        {
            // Read all markdown files from the vault
            var files = await _vaultReader.ReadMarkdownFilesAsync(vaultPath);
            var result = new Dictionary<string, List<string>>();

            foreach (var (filePath, content) in files)
            {
                // Process the text
                var processedText = _textProcessor.ProcessText(content);

                // Chunk the processed text
                var chunks = _textChunker.ChunkText(processedText, maxChunkSize, overlap);

                // Store the chunks
                result[filePath] = chunks;
            }

            return result;
        }
    }
} 