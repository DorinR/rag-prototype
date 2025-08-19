namespace rag_experiment.Models
{
    public class Document
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string OriginalFileName { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public string FilePath { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string Description { get; set; }

        /// <summary>
        /// The full extracted and processed text content of the document.
        /// This is used for adding complete document context to LLM queries.
        /// </summary>
        public string? DocumentText { get; set; }

        // Conversation association (instead of direct user association) - Optional
        public int? ConversationId { get; set; }
        public Conversation? Conversation { get; set; }
    }
}