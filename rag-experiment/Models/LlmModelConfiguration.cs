namespace rag_experiment.Models
{
    /// <summary>
    /// Configuration settings for a specific LLM model tier.
    /// Contains parameters that control the model's behavior and API usage.
    /// </summary>
    public class LlmModelConfiguration
    {
        /// <summary>
        /// The OpenAI model identifier (e.g., "gpt-5-nano", "gpt-5-mini", "gpt-5").
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Maximum number of tokens the model should generate in the response.
        /// </summary>
        public int MaxTokens { get; set; } = 1000;

        /// <summary>
        /// Controls randomness in the output (0.0 = deterministic, 1.0 = very random).
        /// Lower values are better for factual responses, higher for creative tasks.
        /// </summary>
        public double Temperature { get; set; } = 0.2;

        /// <summary>
        /// Cost per 1,000 input tokens in USD (for cost tracking and budgeting).
        /// </summary>
        public double InputCostPer1kTokens { get; set; }

        /// <summary>
        /// Cost per 1,000 output tokens in USD (for cost tracking and budgeting).
        /// </summary>
        public double OutputCostPer1kTokens { get; set; }

        /// <summary>
        /// Maximum context window size in tokens.
        /// </summary>
        public int ContextWindow { get; set; } = 64000;

        /// <summary>
        /// Human-readable description of when to use this model tier.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Container for all LLM model tier configurations.
    /// Maps each tier (Fast, Standard, Premium) to its specific configuration.
    /// </summary>
    public class LlmModelsSettings
    {
        /// <summary>
        /// Configuration section name in appsettings.json.
        /// </summary>
        public const string SectionName = "LlmModels";

        /// <summary>
        /// Configuration for the Fast tier (GPT-5 Nano).
        /// </summary>
        public LlmModelConfiguration Fast { get; set; } = new();

        /// <summary>
        /// Configuration for the Standard tier (GPT-5 Mini).
        /// </summary>
        public LlmModelConfiguration Standard { get; set; } = new();

        /// <summary>
        /// Configuration for the Premium tier (GPT-5).
        /// </summary>
        public LlmModelConfiguration Premium { get; set; } = new();

        /// <summary>
        /// Gets the configuration for a specific tier.
        /// </summary>
        /// <param name="tier">The model tier to retrieve configuration for</param>
        /// <returns>The configuration for the specified tier</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if tier is not recognized</exception>
        public LlmModelConfiguration GetConfiguration(LlmModelTier tier)
        {
            return tier switch
            {
                LlmModelTier.Fast => Fast,
                LlmModelTier.Standard => Standard,
                LlmModelTier.Premium => Premium,
                _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Invalid LLM model tier")
            };
        }
    }
}

