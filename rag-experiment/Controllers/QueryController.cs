using Microsoft.AspNetCore.Mvc;
using rag_experiment.Services;
using rag_experiment.Models;
using Microsoft.Extensions.Options;
using System.Text;
using rag_experiment.Services.Ingestion.VectorStorage;
using rag_experiment.Repositories.Documents;
using rag_experiment.Repositories.Conversations;
using rag_experiment.Services.Query;

namespace rag_experiment.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueryController : ControllerBase
    {
        private readonly EmbeddingRepository _embeddingRepository;
        private readonly IEmbeddingGenerationService _openAiEmbeddingGenerationService;
        private readonly IQueryPreprocessor _queryPreprocessor;
        private readonly ILlmService _llmService;
        private readonly IDocumentRepository _documentRepository;
        private readonly IConversationRepository _conversationRepository;
        private readonly IQueryIntentClassifier _queryIntentClassifier;
        private readonly IAdaptiveRetrievalStrategy _adaptiveRetrievalStrategy;
        private readonly ILogger<QueryController> _logger;

        public QueryController(
            EmbeddingRepository embeddingRepository,
            IEmbeddingGenerationService openAiEmbeddingGenerationService,
            IQueryPreprocessor queryPreprocessor,
            ILlmService llmService,
            ITextProcessor textProcessor,
            AppDbContext dbContext,
            IDocumentRepository documentRepository,
            IConversationRepository conversationRepository,
            IQueryIntentClassifier queryIntentClassifier,
            IAdaptiveRetrievalStrategy adaptiveRetrievalStrategy,
            ILogger<QueryController> logger)
        {
            _embeddingRepository = embeddingRepository;
            _openAiEmbeddingGenerationService = openAiEmbeddingGenerationService;
            _queryPreprocessor = queryPreprocessor;
            _llmService = llmService;
            _documentRepository = documentRepository;
            _conversationRepository = conversationRepository;
            _queryIntentClassifier = queryIntentClassifier;
            _adaptiveRetrievalStrategy = adaptiveRetrievalStrategy;
            _logger = logger;
        }

        /// <summary>
        /// Formats conversation messages into a readable context string for the LLM
        /// </summary>
        /// <param name="messages">List of conversation messages</param>
        /// <returns>Formatted conversation history string</returns>
        private string FormatConversationHistory(List<Message> messages)
        {
            if (!messages.Any())
                return string.Empty;

            var conversationBuilder = new StringBuilder();
            conversationBuilder.AppendLine("=== CONVERSATION HISTORY ===");

            foreach (var message in messages)
            {
                string roleLabel = message.Role switch
                {
                    MessageRole.User => "USER",
                    MessageRole.Assistant => "ASSISTANT",
                    MessageRole.System => "SYSTEM",
                    _ => "UNKNOWN"
                };

                conversationBuilder.AppendLine($"[{roleLabel}]: {message.Content}");
                conversationBuilder.AppendLine();
            }

            conversationBuilder.AppendLine("=== END CONVERSATION HISTORY ===");
            conversationBuilder.AppendLine();

            return conversationBuilder.ToString();
        }

        [HttpPost("query")]
        public async Task<IActionResult> Query([FromBody] QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.Query))
            {
                return BadRequest("Query is required");
            }

            if (request.ConversationId <= 0)
            {
                return BadRequest("ConversationId is required");
            }

            try
            {
                // Get conversation history
                var conversationMessages = await _conversationRepository.GetMessagesAsync(request.ConversationId);
                var conversationHistory = FormatConversationHistory(conversationMessages);

                // Pre-process the query with conversation context
                string processedQuery = string.IsNullOrEmpty(conversationHistory)
                    ? await _queryPreprocessor.ProcessQueryAsync(request.Query)
                    : await _queryPreprocessor.ProcessQueryAsync(request.Query, conversationHistory);

                // Generate embedding for the processed query
                var queryEmbedding = await _openAiEmbeddingGenerationService.GenerateEmbeddingAsync(processedQuery);

                // Find similar documents within the specified conversation
                var limit = request.Limit > 0 ? request.Limit : 10;
                var similarDocuments = _embeddingRepository.FindSimilarEmbeddingsFromUsersDocuments(queryEmbedding, request.ConversationId, limit);

                // Format the retrieved passages
                var retrievedResults = similarDocuments.Select(doc => new
                {
                    text = doc.Text,
                    documentId = doc.DocumentId,
                    documentTitle = doc.DocumentTitle,
                    similarity = doc.Similarity
                }).ToList();

                // Combine conversation history and document chunks into a single context string
                var contextBuilder = new StringBuilder();

                // Add conversation history first
                if (!string.IsNullOrEmpty(conversationHistory))
                {
                    contextBuilder.AppendLine(conversationHistory);
                }

                // Add retrieved document chunks
                contextBuilder.AppendLine("=== RELEVANT DOCUMENTS ===");
                foreach (var doc in retrievedResults)
                {
                    contextBuilder.AppendLine($"--- {doc.documentTitle} ---");
                    contextBuilder.AppendLine(doc.text);
                    contextBuilder.AppendLine();
                }
                string combinedContext = contextBuilder.ToString();

                // Generate LLM response using the combined context (conversation + documents)
                string llmResponse = await _llmService.GenerateResponseAsync(request.Query, combinedContext);

                // Return the formatted response with both retrieved chunks and LLM answer
                return Ok(new
                {
                    originalQuery = request.Query,
                    processedQuery,
                    conversationId = request.ConversationId,
                    llmResponse,
                    retrievedChunks = retrievedResults
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred processing the query: {ex.Message}");
            }
        }

        [HttpPost("query-knowledge-base")]
        public async Task<IActionResult> QueryKnowledgeBase([FromBody] QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.Query))
            {
                return BadRequest("Query is required");
            }

            if (request.ConversationId <= 0)
            {
                return BadRequest("ConversationId is required");
            }

            try
            {
                // Get conversation history
                var conversationMessages = await _conversationRepository.GetMessagesAsync(request.ConversationId);
                var conversationHistory = FormatConversationHistory(conversationMessages);

                // 1. Classify query intent for adaptive retrieval
                var intentResult = await _queryIntentClassifier.ClassifyQueryAsync(request.Query);
                _logger.LogInformation("Query intent classified as {Intent}: {Reasoning}",
                    intentResult.Intent, intentResult.Reasoning);

                // 2. Get retrieval configuration based on intent
                var retrievalConfig = _adaptiveRetrievalStrategy.GetConfigForIntent(intentResult.Intent, request.Query);

                // Allow manual override via request.Limit if provided
                var maxK = retrievalConfig.MaxK;
                var minSimilarity = retrievalConfig.MinSimilarity;

                // Pre-process the query with conversation context
                string processedQuery = string.IsNullOrEmpty(conversationHistory)
                    ? await _queryPreprocessor.ProcessQueryAsync(request.Query)
                    : await _queryPreprocessor.ProcessQueryAsync(request.Query, conversationHistory);

                // Generate embedding for the processed query
                var queryEmbedding = await _openAiEmbeddingGenerationService.GenerateEmbeddingAsync(processedQuery);

                // 3. Adaptive retrieval with threshold
                var topKSimilarEmbeddings = await _embeddingRepository.FindSimilarEmbeddingsAdaptiveAsync(
                    queryEmbedding,
                    maxK,
                    minSimilarity);

                _logger.LogInformation("Retrieved {Count} embeddings with threshold {Threshold}",
                    topKSimilarEmbeddings.Count, minSimilarity);

                // 4. Aggregate by document for source tracking
                var documentContributions = topKSimilarEmbeddings
                    .GroupBy(doc => doc.DocumentId)
                    .Select(g => new
                    {
                        DocumentId = int.Parse(g.Key),
                        ChunksUsed = g.Count(),
                        AvgSimilarity = g.Average(d => d.Similarity),
                        MaxSimilarity = g.Max(d => d.Similarity),
                        DocumentTitle = g.First().DocumentTitle,
                        Chunks = g.ToList()
                    })
                    .OrderByDescending(d => d.MaxSimilarity)
                    .ToList();

                // Get the IDs of all of the documents from the top-K embeddings
                var relatedDocumentsIds = documentContributions.Select(d => d.DocumentId).ToList();

                // Get the documents
                var relatedDocuments = await _documentRepository.GetByIdsAsync(relatedDocumentsIds);

                // Format the retrieved passages
                var retrievedResults = topKSimilarEmbeddings.Select(doc => new
                {
                    fullDocumentText = doc.Text,
                    documentId = doc.DocumentId,
                    documentTitle = doc.DocumentTitle,
                    similarity = doc.Similarity
                }).ToList();

                // Combine conversation history and document chunks into a single context string
                var contextBuilder = new StringBuilder();

                // Add conversation history first
                if (!string.IsNullOrEmpty(conversationHistory))
                {
                    contextBuilder.AppendLine(conversationHistory);
                }

                // Add retrieved document chunks
                contextBuilder.AppendLine("=== KNOWLEDGE BASE DOCUMENTS ===");
                foreach (var doc in retrievedResults)
                {
                    contextBuilder.AppendLine($"--- {doc.documentTitle} ---");
                    contextBuilder.AppendLine(doc.fullDocumentText ?? "");
                    contextBuilder.AppendLine();
                }
                string combinedContext = contextBuilder.ToString();

                _logger.LogInformation("Combined context: {Length} chars, Estimated tokens: {Tokens}",
                    combinedContext.Length, combinedContext.Length / 4);

                // Generate LLM response using the combined context (conversation + knowledge base)
                string llmResponse = await _llmService.GenerateResponseAsync(request.Query, combinedContext);

                // 5. Save assistant message WITH source citations
                var assistantMessage = new Message
                {
                    Role = MessageRole.Assistant,
                    Content = llmResponse,
                    ConversationId = request.ConversationId,
                    Timestamp = DateTime.UtcNow
                };

                // Add source citations
                int order = 0;
                foreach (var docContribution in documentContributions)
                {
                    assistantMessage.Sources.Add(new MessageSource
                    {
                        DocumentId = docContribution.DocumentId,
                        RelevanceScore = docContribution.MaxSimilarity,
                        ChunksUsed = docContribution.ChunksUsed,
                        Order = order++
                    });
                }

                // Save the message with sources
                await _conversationRepository.AddMessageAsync(assistantMessage);

                _logger.LogInformation("Saved assistant message with {SourceCount} source citations",
                    assistantMessage.Sources.Count);

                // 6. Return the formatted response with sources
                return Ok(new
                {
                    originalQuery = request.Query,
                    processedQuery = processedQuery,
                    conversationId = request.ConversationId,
                    intent = intentResult.Intent.ToString(),
                    intentReasoning = intentResult.Reasoning,
                    retrievalConfig = new
                    {
                        maxK = maxK,
                        minSimilarity = minSimilarity,
                        description = retrievalConfig.Description
                    },
                    llmResponse = llmResponse,
                    sources = documentContributions.Select(d => new
                    {
                        documentId = d.DocumentId,
                        documentTitle = d.DocumentTitle,
                        fileName = relatedDocuments.FirstOrDefault(rd => rd.Id == d.DocumentId)?.FileName,
                        relevanceScore = d.MaxSimilarity,
                        chunksUsed = d.ChunksUsed
                    }).ToList(),
                    retrievedChunks = retrievedResults,
                    totalChunks = topKSimilarEmbeddings.Count,
                    uniqueDocuments = documentContributions.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing query");
                return StatusCode(500, $"An error occurred processing the query: {ex.Message}");
            }
        }
    }

    public class QueryRequest
    {
        public required string Query { get; set; }
        public int ConversationId { get; set; }
        public int Limit { get; set; } = 10;
    }

    public class QueryAllConversationsRequest
    {
        public required string Query { get; set; }
        public int Limit { get; set; } = 10;
    }
}