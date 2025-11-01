namespace rag_experiment.Models
{
    /// <summary>
    /// Represents the performance and cost tier for LLM model selection.
    /// Different tiers offer different trade-offs between speed, quality, and cost.
    /// </summary>
    public enum LlmModelTier
    {
        /// <summary>
        /// Fast and cost-effective model (GPT-5 Nano).
        /// Best for: Simple queries, classification, summarization, high-volume requests.
        /// Cost: ~$0.05 per 1M input tokens, $0.40 per 1M output tokens.
        /// Context: 64K tokens.
        /// </summary>
        Fast,

        /// <summary>
        /// Balanced performance and cost model (GPT-5 Mini).
        /// Best for: Well-defined RAG tasks, general-purpose queries, moderate complexity.
        /// Cost: ~$0.25 per 1M input tokens, $2.00 per 1M output tokens (5x Fast).
        /// Context: 128K tokens.
        /// </summary>
        Standard,

        /// <summary>
        /// High-performance model (GPT-5).
        /// Best for: Complex reasoning, multi-step analysis, highest quality responses.
        /// Cost: ~$1.25 per 1M input tokens, $10.00 per 1M output tokens (25x Fast).
        /// Context: 256K tokens.
        /// </summary>
        Premium
    }
}

