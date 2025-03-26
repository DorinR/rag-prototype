using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Models;

namespace Services
{
    public class OpenAIEmbeddingService
    {
        private readonly OpenAIClient _client;
        private const int MaxBatchSize = 100;
        private const int TokensPerMinuteLimit = 150000;
        private static readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private static int _tokensUsedInLastMinute = 0;

        public OpenAIEmbeddingService(IConfiguration configuration)
        {
            var apiKey = configuration["OpenAI:ApiKey"] 
                ?? throw new ArgumentException("OpenAI API key not found in configuration");
            _client = new OpenAIClient(apiKey);
        }

        // ... rest of the existing code ...
    }
} 