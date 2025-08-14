namespace rag_experiment.Models
{
    public class Embedding
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public byte[] EmbeddingData { get; set; }
        public string DocumentId { get; set; }
        public string DocumentTitle { get; set; }
        public int ChunkIndex { get; set; }
        public byte[] ChunkHash { get; set; }

        // User association (for access control)
        public int UserId { get; set; }
        public User User { get; set; }

        // Conversation association (for scoping)
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; }
    }
}