using Microsoft.AspNetCore.Mvc;
using rag_experiment.Services;
using rag_experiment.Models;
using rag_experiment.Services.Ingestion.VectorStorage;
using System.Security.Cryptography;
using System.Text;

namespace rag_experiment.Controllers
{
    /// <summary>
    /// Controller responsible for training operations including document processing and embedding generation
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TrainingController : ControllerBase
    {
        private readonly EmbeddingRepository _embeddingRepository;
        private readonly IEmbeddingGenerationService _openAiEmbeddingGenerationService;
        private readonly ITextProcessor _textProcessor;
        private readonly AppDbContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the TrainingController
        /// </summary>
        /// <param name="embeddingRepository">Repository for managing embeddings</param>
        /// <param name="openAiEmbeddingGenerationService">Service for generating embeddings</param>
        /// <param name="textProcessor">Service for text processing operations</param>
        /// <param name="dbContext">Database context for data operations</param>
        public TrainingController(
            EmbeddingRepository embeddingRepository,
            IEmbeddingGenerationService openAiEmbeddingGenerationService,
            ITextProcessor textProcessor,
            AppDbContext dbContext)
        {
            _embeddingRepository = embeddingRepository;
            _openAiEmbeddingGenerationService = openAiEmbeddingGenerationService;
            _textProcessor = textProcessor;
            _dbContext = dbContext;
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

                // Use null for system training data (no user or conversation association)

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
                            ConversationId = null,
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

                            // Generate hash of chunk text for change detection
                            var chunkHash = GenerateChunkHash(chunk.value);

                            // Store as SystemKnowledgeBase embedding with real document ID
                            _embeddingRepository.AddEmbedding(
                                text: chunk.value,
                                embeddingData: embedding,
                                documentId: document.Id.ToString(), // Use real document ID
                                userId: null, // No user association for system training data
                                conversationId: null, // No conversation association for system training data
                                documentTitle: fileName,
                                owner: EmbeddingOwner.SystemKnowledgeBase,
                                chunkIndex: chunk.index,
                                chunkHash: chunkHash
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
        /// <param name="text">The text to split into chunks</param>
        /// <param name="chunkSize">Maximum size of each chunk in characters</param>
        /// <param name="overlap">Number of characters to overlap between chunks</param>
        /// <returns>List of text chunks</returns>
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

        /// <summary>
        /// Generates a SHA256 hash of the chunk text for change detection
        /// </summary>
        /// <param name="chunkText">The text content to hash</param>
        /// <returns>SHA256 hash as byte array</returns>
        private byte[] GenerateChunkHash(string chunkText)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(chunkText));
            }
        }
    }

    /// <summary>
    /// Request model for training operations
    /// </summary>
    public class TrainRequest
    {
        /// <summary>
        /// The folder name containing training documents
        /// </summary>
        public required string FolderName { get; set; }
    }
}
