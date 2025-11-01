using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using rag_experiment.Models;

namespace rag_experiment.Services
{
    /// <summary>
    /// Configurable LLM client that can be instantiated with different model configurations.
    /// Implements ILlmService for compatibility with existing RAG pipeline.
    /// Created via ILlmClientFactory for flexible model selection at runtime.
    /// </summary>
    public class ConfigurableLlmClient : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly LlmModelConfiguration _configuration;
        private readonly string _apiKey;
        private readonly ILogger<ConfigurableLlmClient>? _logger;

        /// <summary>
        /// Creates a new configurable LLM client with specific model settings.
        /// </summary>
        /// <param name="httpClient">HTTP client for API calls</param>
        /// <param name="configuration">Model-specific configuration (tier settings)</param>
        /// <param name="apiKey">OpenAI API key</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public ConfigurableLlmClient(
            HttpClient httpClient,
            LlmModelConfiguration configuration,
            string apiKey,
            ILogger<ConfigurableLlmClient>? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _logger = logger;

            // Validate configuration
            if (string.IsNullOrWhiteSpace(_configuration.ModelName))
            {
                throw new ArgumentException("Model name cannot be empty", nameof(configuration));
            }

            // Set authorization header
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            _logger?.LogDebug(
                "Created ConfigurableLlmClient with model: {ModelName}, MaxTokens: {MaxTokens}, Temperature: {Temperature}",
                _configuration.ModelName,
                _configuration.MaxTokens,
                _configuration.Temperature);
        }

        /// <summary>
        /// Generates an AI response to a user query using the configured model, based only on the provided context.
        /// </summary>
        /// <param name="query">The user's question to answer</param>
        /// <param name="context">The retrieved document context to base the answer on</param>
        /// <returns>AI-generated response string</returns>
        public async Task<string> GenerateResponseAsync(string query, string context)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger?.LogWarning("Empty query provided to GenerateResponseAsync");
                return "No query provided.";
            }

            if (string.IsNullOrWhiteSpace(context))
            {
                _logger?.LogWarning("Empty context provided for query: {Query}", query);
                return "No relevant information found to answer this query.";
            }

            try
            {
                var chatMessage = new ChatMessage
                {
                    Messages = new List<Message>
                    {
                        new Message
                        {
                            Role = "system",
                            Content = "You are a helpful assistant that answers questions based ONLY on the provided context. Do not use any prior knowledge or information that is not explicitly provided in the context. If the context doesn't contain an answer to the question, admit that you don't know rather than making up an answer."
                        },
                        new Message
                        {
                            Role = "user",
                            Content = $"Here is the context information to use for answering my question:\n\n{context}\n\nMy question is: {query}\n\nAnswer my question strictly using only the information in the provided context. Do not add any information from outside sources or your own knowledge."
                        }
                    },
                    Model = _configuration.ModelName,
                    MaxTokens = _configuration.MaxTokens,
                    Temperature = _configuration.Temperature
                };

                var jsonContent = JsonSerializer.Serialize(chatMessage);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger?.LogDebug(
                    "Sending request to OpenAI with model: {ModelName}, MaxTokens: {MaxTokens}",
                    _configuration.ModelName,
                    _configuration.MaxTokens);

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseContent);

                var generatedResponse = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

                if (string.IsNullOrEmpty(generatedResponse))
                {
                    _logger?.LogWarning("OpenAI returned empty response for query with model: {ModelName}", _configuration.ModelName);
                    return "Unable to generate a response based on the available information.";
                }

                _logger?.LogInformation(
                    "Successfully generated response using {ModelName} (cost estimate: ~${Cost})",
                    _configuration.ModelName,
                    EstimateCost(query.Length + context.Length, generatedResponse.Length));

                return generatedResponse;
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogError(ex, "HTTP error calling OpenAI API with model: {ModelName}", _configuration.ModelName);
                return $"An error occurred while communicating with the AI service: {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating response from OpenAI API with model: {ModelName}", _configuration.ModelName);
                return $"An error occurred while generating a response: {ex.Message}";
            }
        }

        /// <summary>
        /// Estimates the cost of a request based on input and output token counts.
        /// </summary>
        /// <param name="inputCharCount">Approximate number of input characters</param>
        /// <param name="outputCharCount">Approximate number of output characters</param>
        /// <returns>Estimated cost in USD</returns>
        private double EstimateCost(int inputCharCount, int outputCharCount)
        {
            // Rough estimation: 1 token ≈ 4 characters
            var inputTokens = inputCharCount / 4.0;
            var outputTokens = outputCharCount / 4.0;

            var inputCost = (inputTokens / 1000.0) * _configuration.InputCostPer1kTokens;
            var outputCost = (outputTokens / 1000.0) * _configuration.OutputCostPer1kTokens;

            return inputCost + outputCost;
        }

        #region OpenAI API Request/Response Models

        private class ChatMessage
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public List<Message> Messages { get; set; } = new();

            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; set; }

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; }
        }

        private class Message
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private class ChatResponse
        {
            [JsonPropertyName("choices")]
            public List<Choice>? Choices { get; set; }
        }

        private class Choice
        {
            [JsonPropertyName("message")]
            public Message? Message { get; set; }
        }

        #endregion
    }
}

