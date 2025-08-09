using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;
using rag_experiment.Services;
using rag_experiment.Services.Events;
using rag_experiment.Services.Auth;
using rag_experiment.Services.BackgroundJobs;
using Hangfire;

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

        public DocumentController(
            AppDbContext dbContext,
            IWebHostEnvironment environment,
            IUserContext userContext,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _environment = environment;
            _userContext = userContext;
            _configuration = configuration;
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
    }
}