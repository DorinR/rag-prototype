using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using rag_experiment.Models;

namespace rag_experiment.Services.Query
{
    /// <summary>
    /// LLM-based query intent classifier that analyzes user queries to determine
    /// the appropriate retrieval strategy (factual, comprehensive, exploratory, or comparative)
    /// </summary>
    public class QueryIntentClassifier : IQueryIntentClassifier
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _openAiModel;
        private readonly ILogger<QueryIntentClassifier> _logger;

        public QueryIntentClassifier(
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<QueryIntentClassifier> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new ArgumentException("OpenAI API key not found in configuration");
            _openAiModel = configuration["OpenAI:ChatModel"] ?? "gpt-3.5-turbo";
            _logger = logger;
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<QueryIntentResult> ClassifyQueryAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new QueryIntentResult
                {
                    Intent = QueryIntent.Factual,
                    Reasoning = "Empty query defaulted to factual intent"
                };
            }

            try
            {
                var systemPrompt = @"You are an expert at analyzing user queries to determine their intent for information retrieval.

Classify the query into ONE of these categories:

1. FACTUAL: Query asks for specific information, definitions, explanations, or answers to direct questions.
   Examples: ""What is X?"", ""How does Y work?"", ""When did Z happen?"", ""Explain the concept of...""

2. COMPREHENSIVE: Query explicitly or implicitly wants ALL instances, a complete list, or exhaustive coverage.
   Examples: ""List all cases"", ""Find every mention"", ""Show me all instances"", ""What are all the..."", ""Give me every...""
   Keywords: ""all"", ""every"", ""complete"", ""exhaustive"", ""list"", ""entire""

3. EXPLORATORY: Query is open-ended, seeking examples, patterns, or general exploration of a topic.
   Examples: ""What are some examples of..."", ""Tell me about..."", ""Give me insights on..."", ""What can you find about...""

4. COMPARATIVE: Query compares multiple things, asks for differences, or contrasts concepts.
   Examples: ""Compare X and Y"", ""What's the difference between..."", ""X vs Y"", ""How do X and Y differ?""

Respond ONLY with valid JSON in this exact format:
{
  ""intent"": ""FACTUAL"" | ""COMPREHENSIVE"" | ""EXPLORATORY"" | ""COMPARATIVE"",
  ""reasoning"": ""brief explanation of why this intent was chosen""
}";

                var chatMessage = new
                {
                    model = _openAiModel,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = $"Classify this query: \"{query}\"" }
                    },
                    temperature = 0.1, // Low temperature for consistent classification
                    max_tokens = 150
                };

                var jsonContent = JsonSerializer.Serialize(chatMessage);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseContent);

                var generatedResponse = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

                if (string.IsNullOrEmpty(generatedResponse))
                {
                    _logger.LogWarning("Empty response from LLM for intent classification");
                    return FallbackClassification(query);
                }

                // Parse the JSON response
                var intentResponse = JsonSerializer.Deserialize<IntentClassificationResponse>(generatedResponse);

                if (intentResponse == null || !Enum.TryParse<QueryIntent>(intentResponse.Intent, true, out var intent))
                {
                    _logger.LogWarning("Failed to parse intent from LLM response: {Response}", generatedResponse);
                    return FallbackClassification(query);
                }

                _logger.LogInformation("Classified query intent as {Intent}: {Reasoning}", intent, intentResponse.Reasoning);

                return new QueryIntentResult
                {
                    Intent = intent,
                    Reasoning = intentResponse.Reasoning ?? "No reasoning provided"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error classifying query intent, using fallback");
                return FallbackClassification(query);
            }
        }

        /// <summary>
        /// Fallback pattern-based classification when LLM fails
        /// </summary>
        private QueryIntentResult FallbackClassification(string query)
        {
            var lowerQuery = query.ToLowerInvariant();

            // Check for comprehensive indicators
            var comprehensiveKeywords = new[] { "list all", "find all", "show all", "every", "all cases",
                "all instances", "all documents", "all mentions", "complete list", "exhaustive", "entire" };

            if (comprehensiveKeywords.Any(keyword => lowerQuery.Contains(keyword)))
            {
                return new QueryIntentResult
                {
                    Intent = QueryIntent.Comprehensive,
                    Reasoning = "Pattern-based fallback: Contains comprehensive keywords"
                };
            }

            // Check for comparative indicators
            var comparativeKeywords = new[] { "compare", "difference between", "vs", "versus", "contrast",
                "how do", "differ", "similar", "unlike" };

            if (comparativeKeywords.Any(keyword => lowerQuery.Contains(keyword)))
            {
                return new QueryIntentResult
                {
                    Intent = QueryIntent.Comparative,
                    Reasoning = "Pattern-based fallback: Contains comparative keywords"
                };
            }

            // Check for exploratory indicators
            var exploratoryKeywords = new[] { "some examples", "tell me about", "insights", "what can you find",
                "explore", "overview" };

            if (exploratoryKeywords.Any(keyword => lowerQuery.Contains(keyword)))
            {
                return new QueryIntentResult
                {
                    Intent = QueryIntent.Exploratory,
                    Reasoning = "Pattern-based fallback: Contains exploratory keywords"
                };
            }

            // Default to factual
            return new QueryIntentResult
            {
                Intent = QueryIntent.Factual,
                Reasoning = "Pattern-based fallback: Default to factual intent"
            };
        }

        // Internal classes for JSON deserialization
        private class ChatCompletionResponse
        {
            [JsonPropertyName("choices")]
            public List<Choice>? Choices { get; set; }
        }

        private class Choice
        {
            [JsonPropertyName("message")]
            public MessageContent? Message { get; set; }
        }

        private class MessageContent
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }

        private class IntentClassificationResponse
        {
            [JsonPropertyName("intent")]
            public string Intent { get; set; }

            [JsonPropertyName("reasoning")]
            public string Reasoning { get; set; }
        }
    }
}


