using Microsoft.AspNetCore.Mvc;
using rag_experiment.Services;
using rag_experiment.Models;
using Microsoft.Extensions.Options;
using System.Text;
using rag_experiment.Services.Ingestion.VectorStorage;
using rag_experiment.Services.Ingestion.TextExtraction;

namespace rag_experiment.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RagController : ControllerBase
    {
        private readonly EmbeddingRepository _embeddingRepository;
        private readonly IEmbeddingGenerationService _openAiEmbeddingGenerationService;
        private readonly IQueryPreprocessor _queryPreprocessor;
        private readonly ILlmService _llmService;
        private readonly ITextProcessor _textProcessor;
        private readonly AppDbContext _dbContext;

        public RagController(
            EmbeddingRepository embeddingRepository,
            IEmbeddingGenerationService openAiEmbeddingGenerationService,
            IQueryPreprocessor queryPreprocessor,
            ILlmService llmService,
            ITextProcessor textProcessor,
            AppDbContext dbContext)
        {
            _embeddingRepository = embeddingRepository;
            _openAiEmbeddingGenerationService = openAiEmbeddingGenerationService;
            _queryPreprocessor = queryPreprocessor;
            _llmService = llmService;
            _textProcessor = textProcessor;
            _dbContext = dbContext;
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
                    processedQuery = processedQuery,
                    conversationId = request.ConversationId,
                    llmResponse = llmResponse,
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
                // Pre-process the query
                string processedQuery = await _queryPreprocessor.ProcessQueryAsync(request.Query);

                // Generate embedding for the processed query
                var queryEmbedding = await _openAiEmbeddingGenerationService.GenerateEmbeddingAsync(processedQuery);

                // Find similar documents across the entire knowledge base (all users and conversations)
                var limit = request.Limit > 0 ? request.Limit : 10;
                var similarDocuments = await _embeddingRepository.FindSimilarEmbeddingsAsync(queryEmbedding, limit);

                // realted documents
                var relatedDocumentsIds = similarDocuments.Select(doc => doc.DocumentId).Distinct().ToList();

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
                    processedQuery = processedQuery,
                    conversationId = request.ConversationId,
                    llmResponse = llmResponse,
                    retrievedChunks = retrievedResults
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred processing the query: {ex.Message}");
            }
        }

        /// <summary>
        /// Trains the system by processing all TXT files in the specified training folder
        /// and creating SystemKnowledgeBase embeddings for them. Also creates Document records with full text content.
        /// </summary>
        /// <param name="request">Training request containing the folder name</param>
        /// <returns>Training results including number of documents processed</returns>
        [HttpPost("train")]
        public async Task<IActionResult> Train([FromBody] TrainRequest request)
        {
            if (string.IsNullOrEmpty(request.FolderName))
            {
                return BadRequest("FolderName is required");
            }

            try
            {
                // Get the training folder path
                string trainingFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "Training", request.FolderName);

                if (!Directory.Exists(trainingFolderPath))
                {
                    return NotFound($"Training folder '{request.FolderName}' not found");
                }

                // Get only TXT files in the training folder
                var txtFiles = Directory.GetFiles(trainingFolderPath, "*.txt", SearchOption.AllDirectories);

                if (txtFiles.Length == 0)
                {
                    return Ok(new
                    {
                        message = "No TXT files found in the training folder",
                        folderName = request.FolderName,
                        documentsProcessed = 0
                    });
                }

                int documentsProcessed = 0;
                int totalChunks = 0;
                var processedFiles = new List<string>();

                // System constants for training data
                const int SYSTEM_USER_ID = -1; // Special user ID for system training data
                const int SYSTEM_CONVERSATION_ID = -1; // Special conversation ID for system training data

                foreach (string filePath in txtFiles)
                {
                    string fileName = Path.GetFileName(filePath);

                    try
                    {
                        // Read text from TXT file
                        string text = await System.IO.File.ReadAllTextAsync(filePath);

                        if (string.IsNullOrWhiteSpace(text))
                        {
                            Console.WriteLine($"Warning: No text found in {fileName}");
                            continue;
                        }

                        // Create Document record in database
                        var document = new Document
                        {
                            FileName = $"training_{request.FolderName}_{fileName}",
                            OriginalFileName = fileName,
                            ContentType = "text/plain",
                            FileSize = new System.IO.FileInfo(filePath).Length,
                            FilePath = filePath,
                            Description = $"Training document from {request.FolderName} folder",
                            DocumentText = text, // Store the full extracted text
                            ConversationId = SYSTEM_CONVERSATION_ID,
                            UploadedAt = DateTime.UtcNow
                        };

                        _dbContext.Documents.Add(document);
                        await _dbContext.SaveChangesAsync(); // Save to get the document ID

                        // Process the text using the same pipeline as document ingestion
                        var processedText = _textProcessor.ProcessText(text);

                        // Split into chunks (using default chunking settings)
                        var chunks = SplitTextIntoChunks(processedText, 1000, 200); // 1000 char chunks with 200 char overlap

                        // Generate embeddings for each chunk
                        foreach (var chunk in chunks.Select((value, index) => new { value, index }))
                        {
                            var embedding = await _openAiEmbeddingGenerationService.GenerateEmbeddingAsync(chunk.value);

                            // Store as SystemKnowledgeBase embedding with real document ID
                            _embeddingRepository.AddEmbedding(
                                text: chunk.value,
                                embeddingData: embedding,
                                documentId: document.Id.ToString(), // Use real document ID
                                userId: SYSTEM_USER_ID,
                                conversationId: SYSTEM_CONVERSATION_ID,
                                documentTitle: fileName,
                                owner: EmbeddingOwner.SystemKnowledgeBase
                            );

                            totalChunks++;
                        }

                        documentsProcessed++;
                        processedFiles.Add(fileName);

                        Console.WriteLine($"Processed training document: {fileName} ({chunks.Count} chunks) - Document ID: {document.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing {fileName}: {ex.Message}");
                        // Continue with other files
                    }
                }

                return Ok(new
                {
                    message = "Training completed successfully",
                    folderName = request.FolderName,
                    documentsProcessed = documentsProcessed,
                    totalChunks = totalChunks,
                    processedFiles = processedFiles,
                    note = "Document records created with full text content stored in DocumentText column"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred during training: {ex.Message}");
            }
        }

        /// <summary>
        /// Splits text into chunks with overlap for training purposes.
        /// </summary>
        private List<string> SplitTextIntoChunks(string text, int chunkSize, int overlap)
        {
            var chunks = new List<string>();

            if (string.IsNullOrEmpty(text))
                return chunks;

            int start = 0;
            while (start < text.Length)
            {
                int end = Math.Min(start + chunkSize, text.Length);
                string chunk = text.Substring(start, end - start);

                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    chunks.Add(chunk.Trim());
                }

                if (end >= text.Length)
                    break;

                start += chunkSize - overlap;
            }

            return chunks;
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

    public class TrainRequest
    {
        public required string FolderName { get; set; }
    }
}