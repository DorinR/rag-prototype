using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace rag_experiment.Services
{
    public class OpenAiEmbeddingGenerationService : IEmbeddingGenerationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly bool _enableRateLimiting;
        private const int MaxBatchSize = 100;
        private const int TokensPerMinuteLimit = 150000;
        private static readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private static int _tokensUsedInLastMinute = 0;

        public OpenAiEmbeddingGenerationService(IConfiguration configuration, HttpClient httpClient)
        {
            _apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new ArgumentException("OpenAI API key not found in configuration");
            _enableRateLimiting = configuration.GetValue<bool>("OpenAI:EnableRateLimiting", false);

            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<Dictionary<string, float[]>> GenerateEmbeddingsAsync(IEnumerable<string> chunks)
        {
            var result = new Dictionary<string, float[]>();
            var chunksList = chunks.ToList();

            // Process in batches
            for (var i = 0; i < chunksList.Count; i += MaxBatchSize)
            {
                var batch = chunksList.Skip(i).Take(MaxBatchSize).ToList();
                await WaitForRateLimit(batch);

                var request = new
                {
                    model = "text-embedding-3-small",
                    input = batch
                };

                var response = await _httpClient.PostAsync(
                    "embeddings",
                    new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
                );

                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Raw JSON Response: {responseContent}");
                var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseContent);

                if (embeddingResponse?.Data == null || embeddingResponse.Usage == null)
                {
                    throw new InvalidOperationException($"Invalid response from OpenAI API: {responseContent}");
                }

                // Update rate limiting stats
                await UpdateRateLimitStats(embeddingResponse.Usage.TotalTokens);

                // Add results to dictionary
                for (var j = 0; j < batch.Count; j++)
                {
                    result[batch[j]] = embeddingResponse.Data[j].Embedding;
                }
            }

            return result;
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            var request = new
            {
                model = "text-embedding-3-small",
                input = new[] { text }
            };

            var response = await _httpClient.PostAsync(
                "embeddings",
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
            );

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Raw JSON Response: {responseContent}");
            var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseContent);

            if (embeddingResponse?.Data == null || embeddingResponse.Usage == null)
            {
                throw new InvalidOperationException($"Invalid response from OpenAI API: {responseContent}");
            }

            return embeddingResponse.Data[0].Embedding;
        }

        private async Task WaitForRateLimit(List<string> batch)
        {
            if (!_enableRateLimiting) return;

            await _rateLimitSemaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;

                // Reset counters if it's been more than a minute
                if ((now - _lastRequestTime).TotalMinutes >= 1)
                {
                    _tokensUsedInLastMinute = 0;
                    _lastRequestTime = now;
                }

                // Estimate tokens in batch (rough estimate: 1 token ≈ 4 characters)
                var estimatedTokens = batch.Sum(text => text.Length / 4);

                // If this batch would exceed our limit, wait until the minute is up
                if (_tokensUsedInLastMinute + estimatedTokens > TokensPerMinuteLimit)
                {
                    var timeToWait = 60 - (now - _lastRequestTime).TotalSeconds;
                    if (timeToWait > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(timeToWait));
                        _tokensUsedInLastMinute = 0;
                        _lastRequestTime = DateTime.UtcNow;
                    }
                }
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }

        private async Task UpdateRateLimitStats(int tokensUsed)
        {
            if (!_enableRateLimiting) return;

            await _rateLimitSemaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                if ((now - _lastRequestTime).TotalMinutes >= 1)
                {
                    _tokensUsedInLastMinute = 0;
                    _lastRequestTime = now;
                }
                _tokensUsedInLastMinute += tokensUsed;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }

        private class EmbeddingResponse
        {
            [JsonPropertyName("data")]
            public List<EmbeddingData> Data { get; set; } = new();

            [JsonPropertyName("usage")]
            public UsageInfo Usage { get; set; } = new();
        }

        private class EmbeddingData
        {
            [JsonPropertyName("embedding")]
            public float[] Embedding { get; set; } = Array.Empty<float>();

            [JsonPropertyName("index")]
            public int Index { get; set; }

            [JsonPropertyName("object")]
            public string Object { get; set; }
        }

        private class UsageInfo
        {
            public int TotalTokens { get; set; }
        }
    }
}