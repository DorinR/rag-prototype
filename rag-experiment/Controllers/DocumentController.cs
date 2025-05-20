using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;
using rag_experiment.Services;
using rag_experiment.Services.Events;
using rag_experiment.Services.Auth;

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

        public DocumentController(
            AppDbContext dbContext,
            IWebHostEnvironment environment,
            IUserContext userContext)
        {
            _dbContext = dbContext;
            _environment = environment;
            _userContext = userContext;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument(IFormFile file, [FromForm] string description = "")
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                var userId = _userContext.GetCurrentUserId();

                // Create uploads directory if it doesn't exist
                var uploadsDirectory = Path.Combine(_environment.ContentRootPath, "Uploads");
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
                    UserId = userId
                };

                // Save to database
                _dbContext.Documents.Add(document);
                await _dbContext.SaveChangesAsync();

                // Publish event for document processing
                EventBus.Publish(new DocumentUploadedEvent(document.Id, userId));

                return Ok(new
                {
                    documentId = document.Id,
                    fileName = document.OriginalFileName,
                    fileSize = document.FileSize,
                    message = "Document uploaded successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while uploading the document: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllDocuments()
        {
            var userId = _userContext.GetCurrentUserId();

            var documents = await _dbContext.Documents
                .Where(d => d.UserId == userId)
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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocument(int id)
        {
            var userId = _userContext.GetCurrentUserId();

            var document = await _dbContext.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (document == null)
                return NotFound("Document not found");

            return Ok(new
            {
                document.Id,
                document.OriginalFileName,
                document.ContentType,
                document.FileSize,
                document.UploadedAt,
                document.Description
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                // Find the document
                var document = await _dbContext.Documents
                    .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

                if (document == null)
                    return NotFound("Document not found");

                // Delete the physical file
                if (System.IO.File.Exists(document.FilePath))
                {
                    System.IO.File.Delete(document.FilePath);
                }

                // Delete the document record
                _dbContext.Documents.Remove(document);
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
    }
}