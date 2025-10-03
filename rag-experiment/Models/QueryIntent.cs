namespace rag_experiment.Models
{
    /// <summary>
    /// Represents the type of user query intent for adaptive retrieval strategies.
    /// Different intents require different retrieval configurations (top-K, similarity thresholds, etc.)
    /// </summary>
    public enum QueryIntent
    {
        /// <summary>
        /// Query asks for specific information, definitions, or explanations.
        /// Example: "What is the capital of France?", "How does X work?"
        /// Strategy: Small K, high similarity threshold (precision-focused)
        /// </summary>
        Factual,

        /// <summary>
        /// Query explicitly or implicitly wants ALL instances, complete list, or exhaustive coverage.
        /// Example: "List all cases", "Find every mention", "Show me all instances"
        /// Strategy: Large K, lower similarity threshold (recall-focused)
        /// </summary>
        Comprehensive,

        /// <summary>
        /// Query is open-ended, seeking examples or patterns.
        /// Example: "What are some examples of...", "Tell me about..."
        /// Strategy: Medium K, balanced threshold
        /// </summary>
        Exploratory,

        /// <summary>
        /// Query compares multiple things or asks for differences.
        /// Example: "Compare X and Y", "What's the difference between..."
        /// Strategy: Medium-high K, higher threshold to get quality contrasts
        /// </summary>
        Comparative
    }

    /// <summary>
    /// Configuration parameters for retrieval based on query intent
    /// </summary>
    public class RetrievalConfig
    {
        /// <summary>
        /// Maximum number of results to return
        /// </summary>
        public int MaxK { get; set; }

        /// <summary>
        /// Minimum similarity score threshold (0.0 to 1.0)
        /// </summary>
        public float MinSimilarity { get; set; }

        /// <summary>
        /// Human-readable description of this configuration
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Result of query intent classification
    /// </summary>
    public class QueryIntentResult
    {
        /// <summary>
        /// The detected intent of the query
        /// </summary>
        public QueryIntent Intent { get; set; }

        /// <summary>
        /// Explanation of why this intent was chosen (for debugging/transparency)
        /// </summary>
        public string Reasoning { get; set; }

        /// <summary>
        /// Confidence score if available (0.0 to 1.0)
        /// </summary>
        public float? Confidence { get; set; }
    }
}


