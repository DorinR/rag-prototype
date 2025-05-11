using System.ComponentModel.DataAnnotations.Schema;

namespace rag_experiment.Models
{
    public class ExperimentResult
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        // Experiment identification
        public string ExperimentName { get; set; }
        public string Description { get; set; }
        
        // Embedding model parameters
        public string EmbeddingModelName { get; set; }
        public int EmbeddingDimension { get; set; }
        
        // Chunking parameters
        public int ChunkSize { get; set; }
        public int ChunkOverlap { get; set; }
        
        // Text processing parameters
        public bool StopwordRemoval { get; set; }
        public bool Stemming { get; set; }
        public bool Lemmatization { get; set; }
        
        // Query parameters
        public bool QueryExpansion { get; set; }
        public int TopK { get; set; }
        
        // Metrics
        public double AveragePrecision { get; set; }
        public double AverageRecall { get; set; }
        public double AverageF1Score { get; set; }
        
        // Results storage
        public string DetailedResults { get; set; } // JSON string containing per-query results
        public string Notes { get; set; }
        
        // Transient property (not stored in database) to track if text processing settings were explicitly set
        [NotMapped]
        public bool IsTextProcessingExplicitlySet { get; set; } = false;
    }
} 