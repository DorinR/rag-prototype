using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;
using rag_experiment.Services;
using rag_experiment.Services.Events;
using rag_experiment.Services.Auth;
using rag_experiment.Services.BackgroundJobs;
using Hangfire;
using Microsoft.Extensions.Options;

namespace rag_experiment.Controllers
{
    [ApiController]
    [Authorize] // Require authentication for all endpoints
    [Route("api/[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;
        private readonly IUserContext _userContext;
        private readonly IConfiguration _configuration;
        private readonly ITextProcessor _textProcessor;
        private readonly ITextChunker _textChunker;
        private readonly ChunkingSettings _chunkingSettings;

        public DocumentController(
            AppDbContext dbContext,
            IWebHostEnvironment environment,
            IUserContext userContext,
            IConfiguration configuration,
            ITextProcessor textProcessor,
            ITextChunker textChunker,
            IOptions<ChunkingSettings> chunkingSettings)
        {
            _dbContext = dbContext;
            _environment = environment;
            _userContext = userContext;
            _configuration = configuration;
            _textProcessor = textProcessor;
            _textChunker = textChunker;
            _chunkingSettings = chunkingSettings.Value;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument(IFormFile file, [FromForm] int conversationId, [FromForm] string description = "")
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                var userId = _userContext.GetCurrentUserId();

                // Verify conversation exists and belongs to user
                var conversation = await _dbContext.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);

                if (conversation == null)
                    return NotFound("Conversation not found or you don't have access to it");

                // Create uploads directory if it doesn't exist
                var uploadPath = _configuration["DocumentStorage:UploadPath"] ?? "Uploads";
                var uploadsDirectory = Path.Combine(_environment.ContentRootPath, uploadPath);
                if (!Directory.Exists(uploadsDirectory))
                    Directory.CreateDirectory(uploadsDirectory);

                // Generate a unique filename
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadsDirectory, fileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Create document record
                var document = new Document
                {
                    FileName = fileName,
                    OriginalFileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    FilePath = filePath,
                    Description = description,
                    ConversationId = conversationId
                };

                // Save to database
                _dbContext.Documents.Add(document);

                // Update conversation's UpdatedAt timestamp
                conversation.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Enqueue background job for document processing
                var jobId = BackgroundJob.Enqueue<DocumentProcessingJobService>(
                    service => service.StartProcessing(document.Id.ToString(), document.FilePath, userId.ToString(),
                        conversationId.ToString()
                    ));

                return Ok(new
                {
                    documentId = document.Id,
                    fileName = document.OriginalFileName,
                    fileSize = document.FileSize,
                    conversationId = conversationId,
                    jobId = jobId,
                    message = "Document uploaded successfully and processing job queued"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while uploading the document: {ex.Message}");
            }
        }

        [HttpGet("conversation/{conversationId}")]
        public async Task<IActionResult> GetDocumentsByConversation(int conversationId)
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                // Verify conversation exists and belongs to user
                var conversationExists = await _dbContext.Conversations
                    .AnyAsync(c => c.Id == conversationId && c.UserId == userId);

                if (!conversationExists)
                    return NotFound("Conversation not found or you don't have access to it");

                var documents = await _dbContext.Documents
                    .Where(d => d.ConversationId == conversationId)
                    .Select(d => new
                    {
                        d.Id,
                        d.OriginalFileName,
                        d.ContentType,
                        d.FileSize,
                        d.UploadedAt,
                        d.Description
                    })
                    .ToListAsync();

                return Ok(documents);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving documents: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocument(int id)
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                var document = await _dbContext.Documents
                    .Include(d => d.Conversation)
                    .FirstOrDefaultAsync(d => d.Id == id && d.Conversation.UserId == userId);

                if (document == null)
                    return NotFound("Document not found or you don't have access to it");

                return Ok(new
                {
                    document.Id,
                    document.OriginalFileName,
                    document.ContentType,
                    document.FileSize,
                    document.UploadedAt,
                    document.Description,
                    document.ConversationId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving the document: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                // Find the document with conversation relationship
                var document = await _dbContext.Documents
                    .Include(d => d.Conversation)
                    .FirstOrDefaultAsync(d => d.Id == id && d.Conversation.UserId == userId);

                if (document == null)
                    return NotFound("Document not found or you don't have access to it");

                // Delete the physical file
                if (System.IO.File.Exists(document.FilePath))
                {
                    System.IO.File.Delete(document.FilePath);
                }

                // Delete the document record
                _dbContext.Documents.Remove(document);

                // Update conversation's UpdatedAt timestamp
                document.Conversation.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Publish document deleted event
                EventBus.Publish(new DocumentDeletedEvent(id));

                return Ok(new { message = "Document and associated embeddings deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while deleting the document: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllDocuments()
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                var documents = await _dbContext.Documents
                    .Include(d => d.Conversation)
                    .Where(d => d.Conversation.UserId == userId)
                    .Select(d => new
                    {
                        d.Id,
                        d.OriginalFileName,
                        d.ContentType,
                        d.FileSize,
                        d.UploadedAt,
                        d.Description,
                        d.ConversationId,
                        ConversationTitle = d.Conversation.Title
                    })
                    .ToListAsync();

                return Ok(documents);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving documents: {ex.Message}");
            }
        }

        /// <summary>
        /// Estimates the total number of tokens for all .txt files in the token-estimation directory.
        /// This endpoint processes files through the same pipeline used for document ingestion
        /// (text processing, chunking) and provides token count estimation for cost calculation.
        /// </summary>
        /// <returns>Token estimation results including total tokens and per-file breakdown</returns>
        [HttpPost("estimate-tokens")]
        [AllowAnonymous] // Allow access without authentication for token estimation
        public async Task<IActionResult> EstimateTokensForDirectory()
        {
            try
            {
                // Define the fixed directory path
                var tokenEstimationPath = Path.Combine(_environment.ContentRootPath, "Test Data", "token-estimation");

                if (!Directory.Exists(tokenEstimationPath))
                {
                    return BadRequest($"Token estimation directory does not exist: {tokenEstimationPath}");
                }

                // Get all .txt files in the directory
                var txtFiles = Directory.GetFiles(tokenEstimationPath, "*.txt", SearchOption.TopDirectoryOnly);

                if (!txtFiles.Any())
                {
                    return Ok(new
                    {
                        message = "No .txt files found in the token estimation directory",
                        directoryPath = tokenEstimationPath,
                        totalTokens = 0,
                        totalFiles = 0,
                        files = new object[0]
                    });
                }

                var fileResults = new List<object>();
                var totalTokens = 0;
                var totalChunks = 0;
                var totalCharacters = 0;

                foreach (var filePath in txtFiles)
                {
                    try
                    {
                        // Read the file content
                        var rawText = await System.IO.File.ReadAllTextAsync(filePath);
                        var originalLength = rawText.Length;

                        // Process the text through the same pipeline used in document ingestion
                        var processedText = _textProcessor.ProcessText(rawText);
                        var processedLength = processedText.Length;

                        // Chunk the text using the same settings as document ingestion
                        var chunks = _textChunker.ChunkText(
                            processedText,
                            _chunkingSettings.ChunkSize,
                            _chunkingSettings.ChunkOverlap
                        );

                        // Estimate tokens using the same logic as OpenAI embedding service
                        // (1 token ≈ 4 characters)
                        var fileTokens = chunks.Sum(chunk => chunk.Length / 4);
                        var fileChunks = chunks.Count;

                        totalTokens += fileTokens;
                        totalChunks += fileChunks;
                        totalCharacters += processedLength;

                        fileResults.Add(new
                        {
                            fileName = Path.GetFileName(filePath),
                            originalCharacters = originalLength,
                            processedCharacters = processedLength,
                            charactersRemoved = originalLength - processedLength,
                            estimatedTokens = fileTokens,
                            chunkCount = fileChunks,
                            averageChunkSize = fileChunks > 0 ? chunks.Average(c => c.Length) : 0,
                            chunkSizes = chunks.Select(c => c.Length).ToArray()
                        });
                    }
                    catch (Exception fileEx)
                    {
                        fileResults.Add(new
                        {
                            fileName = Path.GetFileName(filePath),
                            error = $"Failed to process file: {fileEx.Message}",
                            estimatedTokens = 0,
                            chunkCount = 0
                        });
                    }
                }

                // Calculate cost estimations for both sync and batch APIs
                // Sync API: $0.065 per 1M tokens
                // Batch API: $0.00013 per 1M tokens
                var syncApiCostUsd = (totalTokens / 1000000.0) * 0.065;
                var batchApiCostUsd = (totalTokens / 1000000.0) * 0.00013;

                // Calculate estimates for full corpus (12,000 files) based on average tokens per file
                var averageTokensPerFile = txtFiles.Length > 0 ? totalTokens / txtFiles.Length : 0;
                var fullCorpusSize = 12000;
                var fullCorpusTotalTokens = averageTokensPerFile * fullCorpusSize;
                var fullCorpusSyncCostUsd = (fullCorpusTotalTokens / 1000000.0) * 0.065;
                var fullCorpusBatchCostUsd = (fullCorpusTotalTokens / 1000000.0) * 0.00013;

                return Ok(new
                {
                    message = "Token estimation completed successfully",
                    directoryPath = tokenEstimationPath,
                    summary = new
                    {
                        totalFiles = txtFiles.Length,
                        totalCharacters = totalCharacters,
                        totalTokens = totalTokens,
                        totalChunks = totalChunks,
                        averageTokensPerFile = txtFiles.Length > 0 ? totalTokens / txtFiles.Length : 0,
                        averageChunksPerFile = txtFiles.Length > 0 ? (double)totalChunks / txtFiles.Length : 0,
                        pricing = new
                        {
                            sampleFiles = new
                            {
                                syncApiCostUsd = Math.Round(syncApiCostUsd, 6),
                                batchApiCostUsd = Math.Round(batchApiCostUsd, 6),
                                syncApiRate = "$0.065 per 1M tokens",
                                batchApiRate = "$0.00013 per 1M tokens",
                                potentialSavings = Math.Round(syncApiCostUsd - batchApiCostUsd, 6),
                                savingsPercentage = syncApiCostUsd > 0 ? Math.Round(((syncApiCostUsd - batchApiCostUsd) / syncApiCostUsd) * 100, 2) : 0
                            },
                            fullCorpusEstimate = new
                            {
                                totalFiles = fullCorpusSize,
                                averageTokensPerFile = averageTokensPerFile,
                                estimatedTotalTokens = fullCorpusTotalTokens,
                                syncApiCostUsd = Math.Round(fullCorpusSyncCostUsd, 2),
                                batchApiCostUsd = Math.Round(fullCorpusBatchCostUsd, 2),
                                potentialSavings = Math.Round(fullCorpusSyncCostUsd - fullCorpusBatchCostUsd, 2),
                                savingsPercentage = fullCorpusSyncCostUsd > 0 ? Math.Round(((fullCorpusSyncCostUsd - fullCorpusBatchCostUsd) / fullCorpusSyncCostUsd) * 100, 2) : 0,
                                note = "Costs estimated based on average tokens per file from sample"
                            }
                        },
                        processingSettings = new
                        {
                            chunkSize = _chunkingSettings.ChunkSize,
                            chunkOverlap = _chunkingSettings.ChunkOverlap,
                            tokenEstimationMethod = "1 token ≈ 4 characters (OpenAI standard)"
                        }
                    },
                    files = fileResults
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while estimating tokens: {ex.Message}");
            }
        }
    }
}