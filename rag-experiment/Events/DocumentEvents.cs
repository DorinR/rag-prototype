namespace rag_experiment.Services.Events
{
    public class DocumentUploadedEvent
    {
        public int DocumentId { get; }

        public DocumentUploadedEvent(int documentId)
        {
            DocumentId = documentId;
        }
    }

    public class DocumentProcessingStartedEvent
    {
        public int DocumentId { get; }
        public string Status { get; } = "Processing";
        public DateTime Timestamp { get; }

        public DocumentProcessingStartedEvent(int documentId)
        {
            DocumentId = documentId;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class DocumentProcessingCompletedEvent
    {
        public int DocumentId { get; }
        public bool Success { get; }
        public string Status { get; }
        public string? ErrorMessage { get; }
        public DateTime Timestamp { get; }

        public DocumentProcessingCompletedEvent(int documentId, bool success, string? errorMessage = null)
        {
            DocumentId = documentId;
            Success = success;
            Status = success ? "Completed" : "Failed";
            ErrorMessage = errorMessage;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class DocumentDeletedEvent
    {
        public int DocumentId { get; }
        public DateTime Timestamp { get; }

        public DocumentDeletedEvent(int documentId)
        {
            DocumentId = documentId;
            Timestamp = DateTime.UtcNow;
        }
    }
}