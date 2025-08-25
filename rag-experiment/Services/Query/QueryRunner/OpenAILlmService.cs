using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace rag_experiment.Services
{
    public class OpenAILlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _openAiModel;
        private readonly ILogger<OpenAILlmService> _logger;

        public OpenAILlmService(IConfiguration configuration, HttpClient httpClient, ILogger<OpenAILlmService> logger = null)
        {
            _httpClient = httpClient;
            _apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new ArgumentException("OpenAI API key not found in configuration");
            _openAiModel = configuration["OpenAI:ChatModel"] ?? "gpt-3.5-turbo";
            _logger = logger;
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        /// <summary>
        /// Generates an AI response to a user query using OpenAI, based only on the provided context.
        /// </summary>
        /// <param name="query">The user's question to answer</param>
        /// <param name="context">The retrieved document context to base the answer on</param>
        /// <returns>AI-generated response string</returns>
        public async Task<string> GenerateResponseAsync(string query, string context)
        {
            Console.WriteLine($"[OpenAILlmService] START - GenerateResponseAsync called");
            Console.WriteLine($"[OpenAILlmService] INPUT - Query: '{query}'");
            Console.WriteLine($"[OpenAILlmService] INPUT - Context length: {context?.Length ?? 0} characters");

            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine($"[OpenAILlmService] EARLY_RETURN - No query provided");
                return "No query provided.";
            }

            if (string.IsNullOrWhiteSpace(context))
            {
                Console.WriteLine($"[OpenAILlmService] EARLY_RETURN - No context provided");
                return "No relevant information found to answer this query.";
            }

            try
            {
                Console.WriteLine($"[OpenAILlmService] STEP 1 - Building chat message request");
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
                    Model = _openAiModel,
                    MaxTokens = 800,
                    Temperature = 0.2
                };

                Console.WriteLine($"[OpenAILlmService] STEP 2 - Serializing request to JSON");
                var jsonContent = JsonSerializer.Serialize(chatMessage);
                Console.WriteLine($"[OpenAILlmService] STEP 2a - Request JSON length: {jsonContent.Length} characters");
                Console.WriteLine($"[OpenAILlmService] STEP 2b - Model: {_openAiModel}, MaxTokens: 800, Temperature: 0.2");
                Console.WriteLine($"[OpenAILlmService] REQUEST_JSON: {jsonContent}");

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                Console.WriteLine($"[OpenAILlmService] STEP 3 - Making HTTP POST request to OpenAI API");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                stopwatch.Stop();

                Console.WriteLine($"[OpenAILlmService] STEP 3a - HTTP request completed in {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"[OpenAILlmService] STEP 3b - HTTP Status Code: {response.StatusCode} ({(int)response.StatusCode})");
                Console.WriteLine($"[OpenAILlmService] STEP 3c - HTTP Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}"))}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[OpenAILlmService] ERROR - HTTP Error Response: {errorContent}");
                }

                response.EnsureSuccessStatusCode();

                Console.WriteLine($"[OpenAILlmService] STEP 4 - Reading response content");
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[OpenAILlmService] STEP 4a - Raw response length: {responseContent?.Length ?? 0} characters");
                Console.WriteLine($"[OpenAILlmService] RAW_RESPONSE: {responseContent ?? "NULL"}");

                Console.WriteLine($"[OpenAILlmService] STEP 5 - Deserializing response JSON");
                var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseContent);
                Console.WriteLine($"[OpenAILlmService] STEP 5a - Deserialization success: {chatResponse != null}");
                Console.WriteLine($"[OpenAILlmService] STEP 5b - Choices count: {chatResponse?.Choices?.Count ?? 0}");

                if (chatResponse?.Choices != null)
                {
                    for (int i = 0; i < chatResponse.Choices.Count; i++)
                    {
                        var choice = chatResponse.Choices[i];
                        Console.WriteLine($"[OpenAILlmService] STEP 5c - Choice[{i}] Message: {choice?.Message?.Content ?? "NULL"}");
                    }
                }

                Console.WriteLine($"[OpenAILlmService] STEP 6 - Extracting generated response");
                var generatedResponse = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
                Console.WriteLine($"[OpenAILlmService] STEP 6a - Generated response: '{generatedResponse ?? "NULL"}'");
                Console.WriteLine($"[OpenAILlmService] STEP 6b - Generated response length: {generatedResponse?.Length ?? 0}");

                if (string.IsNullOrEmpty(generatedResponse))
                {
                    Console.WriteLine($"[OpenAILlmService] WARNING - OpenAI returned empty response");
                    _logger?.LogWarning("OpenAI returned empty response for query.");
                    return "Unable to generate a response based on the available information.";
                }

                Console.WriteLine($"[OpenAILlmService] SUCCESS - Returning generated response");
                return generatedResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpenAILlmService] EXCEPTION - Type: {ex.GetType().Name}");
                Console.WriteLine($"[OpenAILlmService] EXCEPTION - Message: {ex.Message}");
                Console.WriteLine($"[OpenAILlmService] EXCEPTION - StackTrace: {ex.StackTrace}");
                _logger?.LogError(ex, "Error generating response from OpenAI API.");
                return $"An error occurred while generating a response: {ex.Message}";
            }
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