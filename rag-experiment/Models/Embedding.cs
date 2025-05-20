namespace rag_experiment.Models
{
    public class Embedding
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public byte[] EmbeddingData { get; set; }
        public string DocumentId { get; set; }
        public string DocumentTitle { get; set; }

        // User association
        public int UserId { get; set; }
        public User User { get; set; }
    }
}