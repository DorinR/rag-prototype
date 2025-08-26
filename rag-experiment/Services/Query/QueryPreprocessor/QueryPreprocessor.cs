using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace rag_experiment.Services
{
    public class QueryPreprocessor : IQueryPreprocessor
    {
        private readonly Dictionary<string, string> _synonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "how to", "how do I" },
            { "what is", "explain" },
            { "definition of", "explain" },
            // Add more synonyms as needed
        };

        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _openAiModel;
        private readonly ILogger<QueryPreprocessor> _logger;

        public QueryPreprocessor(IConfiguration configuration, HttpClient httpClient, ILogger<QueryPreprocessor> logger = null)
        {
            _httpClient = httpClient;
            _apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new ArgumentException("OpenAI API key not found in configuration");
            _openAiModel = configuration["OpenAI:ChatModel"] ?? "gpt-3.5-turbo";
            _logger = logger;
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        /// <summary>
        /// Main method to process a query through OpenAI preprocessing with fallback to manual methods
        /// </summary>
        public async Task<string> ProcessQueryAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return query;

            try
            {
                // Use OpenAI to preprocess the query
                return await ProcessQueryWithOpenAIAsync(query);
            }
            catch (Exception ex)
            {
                // Log the error
                _logger?.LogWarning(ex, "Error calling OpenAI API for query preprocessing. Falling back to manual processing.");

                // Fall back to manual processing
                return await ProcessQueryManuallyAsync(query);
            }
        }

        /// <summary>
        /// Uses OpenAI to extract the core matter from the user's query
        /// </summary>
        private async Task<string> ProcessQueryWithOpenAIAsync(string query)
        {
            var chatMessage = new ChatMessage
            {
                Messages = new List<Message>
                {
                    new Message
                    {
                        Role = "system",
                        Content = "Given the following user query, extract the key concepts or subject matter that should be used for a semantic search. Remove question words (e.g., 'what,' 'how,' 'why'), unnecessary phrasing, and focus only on the core ideas or entities. Return the result as a concise phrase or set of keywords."
                    },
                    new Message
                    {
                        Role = "user",
                        Content = query
                    }
                },
                Model = _openAiModel,
                MaxTokens = 100,
                Temperature = 0.1
            };

            var jsonContent = JsonSerializer.Serialize(chatMessage);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseContent);

            var processedQuery = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            if (string.IsNullOrEmpty(processedQuery))
            {
                _logger?.LogWarning("OpenAI returned empty response for query preprocessing. Falling back to original query.");
                return query;
            }

            return processedQuery;
        }

        /// <summary>
        /// Legacy method for manual pattern transformations (kept for fallback)
        /// </summary>
        public string ApplyQueryPatterns(string query)
        {
            // Check for specific query patterns and transform

            // Pattern 1: Detect commands like "find X" or "search for X"
            var searchCommandPattern = new Regex(@"^(find|search for|look up|get|show me)\s+(.+)$", RegexOptions.IgnoreCase);
            var match = searchCommandPattern.Match(query);
            if (match.Success)
            {
                return match.Groups[2].Value;
            }

            // Pattern 2: Detect questions
            if (query.EndsWith("?"))
            {
                // Remove the question mark for embedding
                query = query.TrimEnd('?');

                // Transform common question formats
                var howToPattern = new Regex(@"^how (do|can|should) (i|you|we|one)\s+(.+)$", RegexOptions.IgnoreCase);
                match = howToPattern.Match(query);
                if (match.Success)
                {
                    return $"how to {match.Groups[3].Value}";
                }
            }

            // Pattern 3: Replace synonym phrases
            foreach (var synonym in _synonyms)
            {
                if (query.StartsWith(synonym.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return query.Replace(synonym.Key, synonym.Value, StringComparison.OrdinalIgnoreCase);
                }
            }

            return query;
        }

        /// <summary>
        /// Legacy method for query expansion (kept for fallback)
        /// </summary>
        public Task<string> ExpandQueryAsync(string query)
        {
            // This could include more advanced processing like:
            // 1. Using external APIs for query expansion
            // 2. Adding domain-specific terms
            // 3. Using ML models for query rewriting

            // For now, implement a simple expansion
            string expandedQuery = query;

            // Handle specific cases for expansion
            // Example: technical terms expansion
            if (query.Contains("error") && !query.Contains("exception"))
            {
                expandedQuery += " exception";
            }

            return Task.FromResult(expandedQuery);
        }

        /// <summary>
        /// Clean a query by removing extra spaces, punctuation, etc.
        /// </summary>
        private string CleanQuery(string query)
        {
            // Remove extra whitespace
            query = Regex.Replace(query.Trim(), @"\s+", " ");

            // Remove specific punctuation that may not be helpful for embedding
            query = Regex.Replace(query, @"[,:;]", " ");

            // Convert multiple spaces to single space
            query = Regex.Replace(query, @"\s+", " ");

            return query.Trim();
        }

        /// <summary>
        /// Alternative version of ProcessQueryAsync that uses manual processing methods (for fallback)
        /// </summary>
        public async Task<string> ProcessQueryManuallyAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return query;

            // 1. Clean the query
            query = CleanQuery(query);

            // 2. Apply pattern matching
            query = ApplyQueryPatterns(query);

            // 3. Expand the query (could involve external API calls)
            query = await ExpandQueryAsync(query);

            return query;
        }

        #region OpenAI API Request/Response Models

        private class ChatMessage
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("messages")]
            public List<Message> Messages { get; set; }

            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; set; }

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; }
        }

        private class Message
        {
            [JsonPropertyName("role")]
            public string Role { get; set; }

            [JsonPropertyName("content")]
            public string Content { get; set; }
        }

        private class ChatResponse
        {
            [JsonPropertyName("choices")]
            public List<Choice> Choices { get; set; }
        }

        private class Choice
        {
            [JsonPropertyName("message")]
            public Message Message { get; set; }
        }

        #endregion
    }
}