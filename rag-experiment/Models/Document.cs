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

        // Conversation association (instead of direct user association)
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; }
    }
}