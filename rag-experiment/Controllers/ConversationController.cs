using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;
using rag_experiment.Services;
using rag_experiment.Services.Auth;

namespace rag_experiment.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ConversationController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserContext _userContext;

        public ConversationController(AppDbContext dbContext, IUserContext userContext)
        {
            _dbContext = dbContext;
            _userContext = userContext;
        }

        // Disable regular conversations for now.
        // [HttpPost]
        // public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
        // {
        //     try
        //     {
        //         var userId = _userContext.GetCurrentUserId();

        //         var conversation = new Conversation
        //         {
        //             Title = request.Title,
        //             UserId = userId,
        //             Type = ConversationType.DocumentQuery
        //         };

        //         _dbContext.Conversations.Add(conversation);
        //         await _dbContext.SaveChangesAsync();

        //         return Ok(new
        //         {
        //             id = conversation.Id,
        //             title = conversation.Title,
        //             type = conversation.Type.ToString(),
        //             createdAt = conversation.CreatedAt,
        //             updatedAt = conversation.UpdatedAt
        //         });
        //     }
        //     catch (Exception ex)
        //     {
        //         return StatusCode(500, $"An error occurred while creating the conversation: {ex.Message}");
        //     }
        // }

        /// <summary>
        /// Creates a new conversation for querying the general knowledge base
        /// </summary>
        /// <param name="request">Request containing the conversation title</param>
        /// <returns>Created conversation details</returns>
        [HttpPost("general-knowledge")]
        public async Task<IActionResult> CreateGeneralKnowledgeConversation([FromBody] CreateConversationRequest request)
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                var conversation = new Conversation
                {
                    Title = request.Title,
                    UserId = userId,
                    Type = ConversationType.GeneralKnowledge
                };

                _dbContext.Conversations.Add(conversation);
                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    id = conversation.Id,
                    title = conversation.Title,
                    type = conversation.Type.ToString(),
                    createdAt = conversation.CreatedAt,
                    updatedAt = conversation.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while creating the general knowledge conversation: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllConversations()
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                var conversations = await _dbContext.Conversations
                    .Where(c => c.UserId == userId)
                    .OrderByDescending(c => c.UpdatedAt)
                    .Select(c => new
                    {
                        c.Id,
                        c.Title,
                        Type = c.Type.ToString(),
                        c.CreatedAt,
                        c.UpdatedAt,
                        DocumentCount = c.Documents.Count,
                        MessageCount = c.Messages.Count
                    })
                    .ToListAsync();

                return Ok(conversations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving conversations: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetConversation(int id)
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                var conversation = await _dbContext.Conversations
                    .Include(c => c.Documents)
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

                if (conversation == null)
                    return NotFound("Conversation not found");

                return Ok(new
                {
                    conversation.Id,
                    conversation.Title,
                    Type = conversation.Type.ToString(),
                    conversation.CreatedAt,
                    conversation.UpdatedAt,
                    Documents = conversation.Documents.Select(d => new
                    {
                        d.Id,
                        d.OriginalFileName,
                        d.ContentType,
                        d.FileSize,
                        d.UploadedAt,
                        d.Description
                    }),
                    Messages = conversation.Messages.OrderBy(m => m.Timestamp).Select(m => new
                    {
                        m.Id,
                        m.Role,
                        m.Content,
                        m.Timestamp,
                        m.Metadata
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving the conversation: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateConversation(int id, [FromBody] UpdateConversationRequest request)
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                var conversation = await _dbContext.Conversations
                    .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

                if (conversation == null)
                    return NotFound("Conversation not found");

                conversation.Title = request.Title;
                conversation.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    conversation.Id,
                    conversation.Title,
                    conversation.CreatedAt,
                    conversation.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while updating the conversation: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteConversation(int id)
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                var conversation = await _dbContext.Conversations
                    .Include(c => c.Documents)
                    .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

                if (conversation == null)
                    return NotFound("Conversation not found");

                // Delete physical files
                foreach (var document in conversation.Documents)
                {
                    if (System.IO.File.Exists(document.FilePath))
                    {
                        System.IO.File.Delete(document.FilePath);
                    }
                }

                // EF Core will handle cascade deletes for Documents, Messages, and Embeddings
                _dbContext.Conversations.Remove(conversation);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Conversation and all associated data deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while deleting the conversation: {ex.Message}");
            }
        }
    }

    public class CreateConversationRequest
    {
        public string Title { get; set; }
    }

    public class UpdateConversationRequest
    {
        public string Title { get; set; }
    }
}