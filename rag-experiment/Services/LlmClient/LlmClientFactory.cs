using Microsoft.Extensions.Options;
using rag_experiment.Models;

namespace rag_experiment.Services
{
    /// <summary>
    /// Factory implementation for creating LLM clients with different model tier configurations.
    /// Manages the instantiation of ConfigurableLlmClient with appropriate settings from configuration.
    /// </summary>
    public class LlmClientFactory : ILlmClientFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly LlmModelsSettings _modelsSettings;
        private readonly OpenAISettings _openAiSettings;
        private readonly ILogger<LlmClientFactory> _logger;
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Creates a new LLM client factory.
        /// </summary>
        /// <param name="httpClientFactory">Factory for creating HTTP clients</param>
        /// <param name="modelsSettings">Configuration for all model tiers</param>
        /// <param name="openAiSettings">OpenAI API settings (key, base URL)</param>
        /// <param name="logger">Logger for factory operations</param>
        /// <param name="loggerFactory">Factory for creating client-specific loggers</param>
        public LlmClientFactory(
            IHttpClientFactory httpClientFactory,
            IOptions<LlmModelsSettings> modelsSettings,
            IOptions<OpenAISettings> openAiSettings,
            ILogger<LlmClientFactory> logger,
            ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _modelsSettings = modelsSettings?.Value ?? throw new ArgumentNullException(nameof(modelsSettings));
            _openAiSettings = openAiSettings?.Value ?? throw new ArgumentNullException(nameof(openAiSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

            // Validate OpenAI API key is configured
            if (string.IsNullOrWhiteSpace(_openAiSettings.ApiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured. Please set OpenAI:ApiKey in appsettings.json");
            }

            _logger.LogInformation("LlmClientFactory initialized with model tiers: Fast={FastModel}, Standard={StandardModel}, Premium={PremiumModel}",
                _modelsSettings.Fast.ModelName,
                _modelsSettings.Standard.ModelName,
                _modelsSettings.Premium.ModelName);
        }

        /// <summary>
        /// Creates an LLM client configured for the specified model tier.
        /// </summary>
        /// <param name="tier">The model tier to use (Fast, Standard, or Premium)</param>
        /// <returns>An ILlmService instance configured with the tier's settings</returns>
        /// <exception cref="ArgumentException">Thrown if the tier configuration is invalid</exception>
        public ILlmService CreateClient(LlmModelTier tier)
        {
            _logger.LogDebug("Creating LLM client for tier: {Tier}", tier);

            // Get configuration for the requested tier
            LlmModelConfiguration config;
            try
            {
                config = _modelsSettings.GetConfiguration(tier);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _logger.LogError(ex, "Invalid LLM model tier requested: {Tier}", tier);
                throw new ArgumentException($"Invalid LLM model tier: {tier}", nameof(tier), ex);
            }

            // Validate configuration
            if (string.IsNullOrWhiteSpace(config.ModelName))
            {
                var errorMessage = $"Model name not configured for tier: {tier}. Please check LlmModels:{tier}:ModelName in appsettings.json";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            if (config.MaxTokens <= 0)
            {
                var errorMessage = $"Invalid MaxTokens configuration for tier: {tier}. Must be greater than 0.";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            if (config.Temperature < 0 || config.Temperature > 1)
            {
                _logger.LogWarning(
                    "Temperature for tier {Tier} is outside recommended range [0, 1]: {Temperature}",
                    tier,
                    config.Temperature);
            }

            // Create HTTP client
            var httpClient = _httpClientFactory.CreateClient();

            // Create client logger
            var clientLogger = _loggerFactory.CreateLogger<ConfigurableLlmClient>();

            // Instantiate the configurable LLM client
            var client = new ConfigurableLlmClient(
                httpClient,
                config,
                _openAiSettings.ApiKey,
                clientLogger);

            _logger.LogInformation(
                "Created LLM client for tier: {Tier}, Model: {ModelName}, MaxTokens: {MaxTokens}, Temp: {Temperature}, EstCost: ${InputCost}/${OutputCost} per 1k tokens",
                tier,
                config.ModelName,
                config.MaxTokens,
                config.Temperature,
                config.InputCostPer1kTokens,
                config.OutputCostPer1kTokens);

            return client;
        }
    }
}

