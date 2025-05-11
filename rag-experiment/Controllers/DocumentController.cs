using Microsoft.AspNetCore.Mvc;
using rag_experiment.Models;
using rag_experiment.Services;
using rag_experiment.Services.Events;

namespace rag_experiment.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;

        public DocumentController(AppDbContext dbContext, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _environment = environment;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument(IFormFile file, [FromForm] string description = "")
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
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
                    Description = description
                };

                // Save to database
                _dbContext.Documents.Add(document);
                await _dbContext.SaveChangesAsync();

                // Publish event for document processing
                EventBus.Publish(new DocumentUploadedEvent(document.Id));

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
            var documents = _dbContext.Documents
                .Select(d => new
                {
                    d.Id,
                    d.OriginalFileName,
                    d.ContentType,
                    d.FileSize,
                    d.UploadedAt,
                    d.Description
                })
                .ToList();

            return Ok(documents);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocument(int id)
        {
            var document = await _dbContext.Documents.FindAsync(id);

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
                // Find the document
                var document = await _dbContext.Documents.FindAsync(id);
                if (document == null)
                    return NotFound("Document not found");

                // Delete associated embeddings
                var documentIdString = $"file://{Path.GetFullPath(document.FilePath)}";
                var embeddingsToDelete = _dbContext.Embeddings
                    .Where(e => e.DocumentId == documentIdString)
                    .ToList();

                _dbContext.Embeddings.RemoveRange(embeddingsToDelete);

                // Delete the physical file
                if (System.IO.File.Exists(document.FilePath))
                {
                    System.IO.File.Delete(document.FilePath);
                }

                // Delete the document record
                _dbContext.Documents.Remove(document);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Document and associated embeddings deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while deleting the document: {ex.Message}");
            }
        }
    }
}