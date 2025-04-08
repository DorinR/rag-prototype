namespace rag_experiment.Models
{
    public class Embedding
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public byte[] EmbeddingData { get; set; }
        public string DocumentLink { get; set; }
    }
} 