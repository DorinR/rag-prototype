namespace rag_experiment.Services
{
    public class TextChunker : ITextChunker
    {
        public List<string> ChunkText(string text, int maxChunkSize = 1000, int overlap = 100)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            var chunks = new List<string>();
            var sentences = SplitIntoSentences(text);
            var currentChunk = new List<string>();
            var currentLength = 0;

            foreach (var sentence in sentences)
            {
                // If adding this sentence would exceed maxChunkSize
                if (currentLength + sentence.Length > maxChunkSize && currentChunk.Any())
                {
                    // Add the current chunk to our list of chunks
                    chunks.Add(string.Join(" ", currentChunk));
                    
                    // Start a new chunk with overlap
                    currentChunk = GetOverlappingContent(currentChunk, overlap);
                    currentLength = currentChunk.Sum(s => s.Length + 1); // +1 for space
                }

                currentChunk.Add(sentence);
                currentLength += sentence.Length + 1; // +1 for space
            }

            // Add the last chunk if there's anything left
            if (currentChunk.Any())
            {
                chunks.Add(string.Join(" ", currentChunk));
            }

            return chunks;
        }

        private List<string> SplitIntoSentences(string text)
        {
            // Simple sentence splitting - can be made more sophisticated
            return text.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => s.Trim())
                      .Where(s => !string.IsNullOrWhiteSpace(s))
                      .ToList();
        }

        private List<string> GetOverlappingContent(List<string> currentChunk, int overlapSize)
        {
            var overlappingContent = new List<string>();
            var currentSize = 0;

            // Work backwards through the current chunk
            for (int i = currentChunk.Count - 1; i >= 0; i--)
            {
                var sentence = currentChunk[i];
                if (currentSize + sentence.Length > overlapSize)
                    break;

                overlappingContent.Insert(0, sentence);
                currentSize += sentence.Length + 1; // +1 for space
            }

            return overlappingContent;
        }
    }
} 