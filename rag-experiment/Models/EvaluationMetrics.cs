namespace rag_experiment.Models
{
    public class EvaluationMetrics
    {
        public int QueryId { get; set; }
        public string Query { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1Score { get; set; }
        public List<string> RetrievedDocumentIds { get; set; } = new List<string>();
        public List<string> RelevantDocumentIds { get; set; } = new List<string>();
        public List<string> RelevantRetrievedDocumentIds { get; set; } = new List<string>();
    }

    public class EvaluationRequest
    {
        // Core evaluation parameter
        public int TopK { get; set; } = 0; // 0 means use default from configuration
        
        // Optional experiment metadata
        public string ExperimentName { get; set; }
        public string Description { get; set; }
    }

    public class EvaluationResult
    {
        public List<EvaluationMetrics> QueryMetrics { get; set; } = new List<EvaluationMetrics>();
        public double AveragePrecision { get; set; }
        public double AverageRecall { get; set; }
        public double AverageF1Score { get; set; }
    }
} 