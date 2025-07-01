using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using rag_experiment.Models;

namespace rag_experiment.Services
{
    public class OpenAiEmbeddingGenerationService : IEmbeddingGenerationService
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAISettings _settings;
        private static readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private static int _tokensUsedInLastMinute = 0;
        private static int _requestsInLastMinute = 0;

        public OpenAiEmbeddingGenerationService(
            IHttpClientFactory httpClientFactory,
            IOptions<OpenAISettings> settings)
        {
            _settings = settings.Value;
            _httpClient = httpClientFactory.CreateClient("OpenAI");
        }

        public async Task<Dictionary<string, float[]>> GenerateEmbeddingsAsync(IEnumerable<string> chunks)
        {
            var result = new Dictionary<string, float[]>();
            var chunksList = chunks.ToList();

            // Process in batches
            for (var i = 0; i < chunksList.Count; i += _settings.MaxBatchSize)
            {
                var batch = chunksList.Skip(i).Take(_settings.MaxBatchSize).ToList();
                await WaitForRateLimit(batch);

                var request = new
                {
                    model = _settings.ModelName,
                    input = batch
                };

                var response = await _httpClient.PostAsync(
                    "embeddings",
                    new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
                );

                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
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
                model = _settings.ModelName,
                input = new[] { text }
            };

            var response = await _httpClient.PostAsync(
                "embeddings",
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
            );

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseContent);

            if (embeddingResponse?.Data == null || embeddingResponse.Usage == null)
            {
                throw new InvalidOperationException($"Invalid response from OpenAI API: {responseContent}");
            }

            return embeddingResponse.Data[0].Embedding;
        }

        private async Task WaitForRateLimit(List<string> batch)
        {
            if (!_settings.EnableRateLimiting) return;

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
                if (_tokensUsedInLastMinute + estimatedTokens > _settings.TpmLimit || _requestsInLastMinute > _settings.RpmLimit)
                {
                    var timeToWait = 60 - (now - _lastRequestTime).TotalSeconds;
                    if (timeToWait > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(timeToWait));
                        _lastRequestTime = DateTime.UtcNow;
                        _tokensUsedInLastMinute = 0;
                        _requestsInLastMinute = 0;
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
            if (!_settings.EnableRateLimiting) return;

            await _rateLimitSemaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                if ((now - _lastRequestTime).TotalMinutes >= 1)
                {
                    _tokensUsedInLastMinute = 0;
                    _requestsInLastMinute = 0;
                    _lastRequestTime = now;
                }
                _tokensUsedInLastMinute += tokensUsed;
                _requestsInLastMinute++;
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