using Microsoft.AspNetCore.Mvc;
using rag_experiment.Services;
using rag_experiment.Models;
using Microsoft.Extensions.Options;
using System.Text;
using rag_experiment.Services.Ingestion.VectorStorage;
using rag_experiment.Repositories.Documents;

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

        public QueryController(
            EmbeddingRepository embeddingRepository,
            IEmbeddingGenerationService openAiEmbeddingGenerationService,
            IQueryPreprocessor queryPreprocessor,
            ILlmService llmService,
            ITextProcessor textProcessor,
            AppDbContext dbContext,
            IDocumentRepository documentRepository)
        {
            _embeddingRepository = embeddingRepository;
            _openAiEmbeddingGenerationService = openAiEmbeddingGenerationService;
            _queryPreprocessor = queryPreprocessor;
            _llmService = llmService;
            _documentRepository = documentRepository;
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
                // Pre-process the query
                string processedQuery = await _queryPreprocessor.ProcessQueryAsync(request.Query);

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

                // Combine the top chunks into a single context string
                var contextBuilder = new StringBuilder();
                foreach (var doc in retrievedResults)
                {
                    contextBuilder.AppendLine($"--- {doc.documentTitle} ---");
                    contextBuilder.AppendLine(doc.text);
                    contextBuilder.AppendLine();
                }
                string combinedContext = contextBuilder.ToString();

                // Generate LLM response using the combined context
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
                // Log initial request
                Console.WriteLine($"[QueryKnowledgeBase] STEP 1 - Request received: ConversationId={request.ConversationId}, Query='{request.Query}', Limit={request.Limit}");

                // Pre-process the query
                string processedQuery = await _queryPreprocessor.ProcessQueryAsync(request.Query);
                Console.WriteLine($"[QueryKnowledgeBase] STEP 2 - Query processed: Original='{request.Query}' -> Processed='{processedQuery}'");

                // Generate embedding for the processed query
                var queryEmbedding = await _openAiEmbeddingGenerationService.GenerateEmbeddingAsync(processedQuery);
                Console.WriteLine($"[QueryKnowledgeBase] STEP 3 - Embedding generated: Success={queryEmbedding != null}, Dimensions={queryEmbedding?.Length ?? 0}");

                if (queryEmbedding == null)
                {
                    Console.WriteLine($"[QueryKnowledgeBase] ERROR - Failed to generate embedding for query");
                    return StatusCode(500, "Failed to generate embedding for query");
                }

                // get the k most similar documents
                var limit = request.Limit > 0 ? request.Limit : 10;
                var topKSimilarEmbeddings = await _embeddingRepository.FindSimilarEmbeddingsAsync(queryEmbedding, limit);
                Console.WriteLine($"[QueryKnowledgeBase] STEP 4 - Similar embeddings found: Count={topKSimilarEmbeddings?.Count() ?? 0}, RequestedLimit={limit}");

                if (topKSimilarEmbeddings == null || !topKSimilarEmbeddings.Any())
                {
                    Console.WriteLine($"[QueryKnowledgeBase] WARNING - No similar embeddings found");
                    return Ok(new
                    {
                        originalQuery = request.Query,
                        processedQuery = processedQuery,
                        conversationId = request.ConversationId,
                        llmResponse = "No relevant documents found for your query.",
                        retrievedChunks = new List<object>()
                    });
                }

                var similarities = topKSimilarEmbeddings.Select(e => e.Similarity).ToList();
                Console.WriteLine($"[QueryKnowledgeBase] STEP 4a - Similarity scores: Min={similarities.Min():F4}, Max={similarities.Max():F4}, Avg={similarities.Average():F4}");

                // Get the IDs of all of the documents from the top-K embeddings.
                var relatedDocumentsIds = topKSimilarEmbeddings.Select(doc => doc.DocumentId).Distinct().ToList();
                Console.WriteLine($"[QueryKnowledgeBase] STEP 5 - Document IDs extracted: UniqueDocuments={relatedDocumentsIds.Count}, IDs=[{string.Join(", ", relatedDocumentsIds)}]");

                // get the documents
                var relatedDocuments = await _documentRepository.GetByIdsAsync(relatedDocumentsIds.Select(id => int.Parse(id)));
                Console.WriteLine($"[QueryKnowledgeBase] STEP 6 - Documents retrieved: RequestedCount={relatedDocumentsIds.Count}, ActualCount={relatedDocuments?.Count() ?? 0}");

                if (relatedDocuments?.Any() == true)
                {
                    var docLengths = relatedDocuments.Select(d => d.DocumentText?.Length ?? 0).ToList();
                    Console.WriteLine($"[QueryKnowledgeBase] STEP 6a - Document lengths: Min={docLengths.Min()}, Max={docLengths.Max()}, Avg={docLengths.Average():F0}");
                }

                // Format the retrieved passages
                var retrievedResults = topKSimilarEmbeddings.Select(doc => new
                {
                    fullDocumentText = relatedDocuments?.FirstOrDefault(d => d.Id == int.Parse(doc.DocumentId))?.DocumentText,
                    documentId = doc.DocumentId,
                    documentTitle = doc.DocumentTitle,
                    similarity = doc.Similarity
                }).ToList();
                Console.WriteLine($"[QueryKnowledgeBase] STEP 7 - Results formatted: Count={retrievedResults.Count}");

                var nullDocuments = retrievedResults.Count(r => r.fullDocumentText == null);
                if (nullDocuments > 0)
                {
                    Console.WriteLine($"[QueryKnowledgeBase] STEP 7a - WARNING: {nullDocuments} documents have null fullDocumentText");
                }

                // Combine the top chunks into a single context string
                var contextBuilder = new StringBuilder();
                foreach (var doc in retrievedResults)
                {
                    contextBuilder.AppendLine($"--- {doc.documentTitle} ---");
                    contextBuilder.AppendLine(doc.fullDocumentText ?? "");
                    contextBuilder.AppendLine();
                }
                string combinedContext = contextBuilder.ToString();
                Console.WriteLine($"[QueryKnowledgeBase] STEP 8 - Context built: TotalLength={combinedContext.Length}, DocumentSections={retrievedResults.Count}");

                // Generate LLM response using the combined context
                string llmResponse = await _llmService.GenerateResponseAsync(request.Query, combinedContext);
                Console.WriteLine($"[QueryKnowledgeBase] STEP 9 - LLM response generated: Success={!string.IsNullOrEmpty(llmResponse)}, ResponseLength={llmResponse?.Length ?? 0}");

                // Return the formatted response with both retrieved chunks and LLM answer
                var response = new
                {
                    originalQuery = request.Query,
                    processedQuery = processedQuery,
                    conversationId = request.ConversationId,
                    llmResponse = llmResponse,
                    retrievedChunks = retrievedResults
                };
                Console.WriteLine($"[QueryKnowledgeBase] STEP 10 - Response prepared: ConversationId={request.ConversationId}, ChunksCount={retrievedResults.Count}, HasLlmResponse={!string.IsNullOrEmpty(llmResponse)}");

                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QueryKnowledgeBase] ERROR - ConversationId={request.ConversationId}, Exception={ex.GetType().Name}, Message={ex.Message}");
                Console.WriteLine($"[QueryKnowledgeBase] ERROR - StackTrace={ex.StackTrace}");
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