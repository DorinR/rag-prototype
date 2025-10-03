using rag_experiment.Models;

namespace rag_experiment.Services.Query
{
    /// <summary>
    /// Implementation of adaptive retrieval strategy that maps query intents
    /// to optimal retrieval configurations (maxK and similarity thresholds)
    /// </summary>
    public class AdaptiveRetrievalStrategy : IAdaptiveRetrievalStrategy
    {
        private readonly ILogger<AdaptiveRetrievalStrategy> _logger;
        private readonly IConfiguration _configuration;

        public AdaptiveRetrievalStrategy(
            ILogger<AdaptiveRetrievalStrategy> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public RetrievalConfig GetConfigForIntent(QueryIntent intent, string? query = null)
        {
            // Base configurations for each intent type
            var config = intent switch
            {
                QueryIntent.Factual => new RetrievalConfig
                {
                    MaxK = GetConfigValue("RetrievalConfig:Factual:MaxK", 10),
                    MinSimilarity = GetConfigValue("RetrievalConfig:Factual:MinSimilarity", 0.75f),
                    Description = "Precision-focused: Only the best, most relevant matches"
                },

                QueryIntent.Exploratory => new RetrievalConfig
                {
                    MaxK = GetConfigValue("RetrievalConfig:Exploratory:MaxK", 20),
                    MinSimilarity = GetConfigValue("RetrievalConfig:Exploratory:MinSimilarity", 0.70f),
                    Description = "Balanced: Diverse examples with quality threshold"
                },

                QueryIntent.Comprehensive => new RetrievalConfig
                {
                    MaxK = GetConfigValue("RetrievalConfig:Comprehensive:MaxK", 100),
                    MinSimilarity = GetConfigValue("RetrievalConfig:Comprehensive:MinSimilarity", 0.60f),
                    Description = "Recall-focused: Cast wide net to catch all relevant content"
                },

                QueryIntent.Comparative => new RetrievalConfig
                {
                    MaxK = GetConfigValue("RetrievalConfig:Comparative:MaxK", 30),
                    MinSimilarity = GetConfigValue("RetrievalConfig:Comparative:MinSimilarity", 0.72f),
                    Description = "Contrasting: Quality matches from multiple sources"
                },

                _ => new RetrievalConfig
                {
                    MaxK = 10,
                    MinSimilarity = 0.70f,
                    Description = "Default configuration"
                }
            };

            // Apply query-specific adjustments if query text is provided
            if (!string.IsNullOrEmpty(query))
            {
                config = ApplyQuerySpecificAdjustments(config, intent, query);
            }

            _logger.LogInformation(
                "Selected retrieval config for {Intent}: MaxK={MaxK}, MinSimilarity={MinSimilarity}",
                intent, config.MaxK, config.MinSimilarity);

            return config;
        }

        /// <summary>
        /// Applies fine-tuned adjustments based on query characteristics
        /// </summary>
        private RetrievalConfig ApplyQuerySpecificAdjustments(
            RetrievalConfig baseConfig,
            QueryIntent intent,
            string query)
        {
            var adjustedConfig = new RetrievalConfig
            {
                MaxK = baseConfig.MaxK,
                MinSimilarity = baseConfig.MinSimilarity,
                Description = baseConfig.Description
            };

            // For comprehensive queries with strong exhaustive language, increase MaxK
            if (intent == QueryIntent.Comprehensive)
            {
                var exhaustiveTerms = new[] { "every", "all", "complete", "exhaustive", "entire", "each" };
                var containsExhaustive = exhaustiveTerms.Any(term =>
                    query.Contains(term, StringComparison.OrdinalIgnoreCase));

                if (containsExhaustive)
                {
                    adjustedConfig.MaxK = Math.Min(adjustedConfig.MaxK + 50, 200); // Cap at 200
                    adjustedConfig.MinSimilarity = baseConfig.MinSimilarity;
                    adjustedConfig.Description += " (Enhanced for exhaustive query)";

                    _logger.LogInformation("Enhanced comprehensive retrieval for exhaustive query");
                }
            }

            // Adjust threshold based on query length/specificity
            var wordCount = query.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

            if (wordCount > 15) // Very detailed query
            {
                // Longer queries are more specific, can afford slightly lower threshold
                // adjustedConfig.MinSimilarity = Math.Max(adjustedConfig.MinSimilarity - 0.03f, 0.50f);
            }
            else if (wordCount < 5) // Very short query
            {
                // Short queries are less specific, need higher threshold to ensure quality
                // adjustedConfig.MinSimilarity = Math.Min(adjustedConfig.MinSimilarity + 0.05f, 0.85f);
            }

            return adjustedConfig;
        }

        /// <summary>
        /// Gets configuration value with fallback to default
        /// </summary>
        private T GetConfigValue<T>(string key, T defaultValue)
        {
            try
            {
                var value = _configuration.GetValue<T>(key);
                return value ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}


