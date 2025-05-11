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
        
        public async Task<string> GenerateResponseAsync(string query, string context)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "No query provided.";
                
            if (string.IsNullOrWhiteSpace(context))
                return "No relevant information found to answer this query.";
                
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
                    Model = _openAiModel,
                    MaxTokens = 800,
                    Temperature = 0.2
                };

                var jsonContent = JsonSerializer.Serialize(chatMessage);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseContent);

                var generatedResponse = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
                
                if (string.IsNullOrEmpty(generatedResponse))
                {
                    _logger?.LogWarning("OpenAI returned empty response for query.");
                    return "Unable to generate a response based on the available information.";
                }
                
                return generatedResponse;
            }
            catch (Exception ex)
            {
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