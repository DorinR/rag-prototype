namespace rag_experiment.Models
{
    public class DocumentProcessingState
    {
        public int DocumentId { get; set; }
        public string FilePath { get; set; }
        public string? ExtractedText { get; set; }
        public List<string>? Chunks { get; set; }
        public List<float[]>? Embeddings { get; set; }
        public ProcessingStatus Status { get; set; }
        public string? JobId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public enum ProcessingStatus
    {
        Pending,
        TextExtracted,
        ChunksCreated,
        EmbeddingsGenerated,
        Completed,
        Failed
    }
}
