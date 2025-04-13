namespace rag_experiment.Models
{
    public class RagSettings
    {
        public TextProcessingSettings TextProcessing { get; set; } = new();
        public EmbeddingSettings Embedding { get; set; } = new();
        public ChunkingSettings Chunking { get; set; } = new();
        public RetrievalSettings Retrieval { get; set; } = new();
    }

    public class TextProcessingSettings
    {
        public bool StopwordRemoval { get; set; } = false;
        public bool Stemming { get; set; } = false;
        public bool Lemmatization { get; set; } = false;
        public bool QueryExpansion { get; set; } = false;
    }

    public class EmbeddingSettings
    {
        public string ModelName { get; set; } = "text-embedding-3-small";
        public int Dimension { get; set; } = 1536;
    }

    public class ChunkingSettings
    {
        public int ChunkSize { get; set; } = 1000;
        public int ChunkOverlap { get; set; } = 100;
    }

    public class RetrievalSettings
    {
        public int DefaultTopK { get; set; } = 10;
    }
} 