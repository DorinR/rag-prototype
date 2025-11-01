using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;
using rag_experiment.Services;
using rag_experiment.Services.Auth;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace rag_experiment.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/conversations/{conversationId}/[controller]")]
    public class MessageController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserContext _userContext;

        public MessageController(AppDbContext dbContext, IUserContext userContext)
        {
            _dbContext = dbContext;
            _userContext = userContext;
        }

        [HttpPost]
        public async Task<IActionResult> AddMessage(int conversationId, [FromBody] AddMessageRequest request)
        {
            try
            {
                // Debug: Log the raw request body
                Request.EnableBuffering();
                Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body);
                var rawBody = await reader.ReadToEndAsync();
                Request.Body.Position = 0;

                var logger = HttpContext.RequestServices.GetRequiredService<ILogger<MessageController>>();
                logger.LogInformation("Raw request body: {RawBody}", rawBody);
                logger.LogInformation("Parsed request - Role: {Role}, Content: {Content}, Metadata: {Metadata}",
                    request?.Role, request?.Content, request?.Metadata);

                // Debug: Check ModelState
                if (!ModelState.IsValid)
                {
                    logger.LogWarning("ModelState is invalid:");
                    foreach (var state in ModelState)
                    {
                        foreach (var error in state.Value.Errors)
                        {
                            logger.LogWarning("Field: {Field}, Error: {Error}", state.Key, error.ErrorMessage);
                        }
                    }
                    return BadRequest(ModelState);
                }

                var userId = _userContext.GetCurrentUserId();

                // Verify conversation exists and belongs to user
                var conversation = await _dbContext.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);

                if (conversation == null)
                    return NotFound("Conversation not found");

                var message = new Message
                {
                    ConversationId = conversationId,
                    Role = request.Role,
                    Content = request.Content,
                    Metadata = request.Metadata
                };

                _dbContext.Messages.Add(message);

                // Update conversation's UpdatedAt timestamp
                conversation.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    message.Id,
                    message.Role,
                    message.Content,
                    message.Timestamp,
                    message.Metadata,
                    message.ConversationId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while adding the message: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(int conversationId)
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                // Verify conversation exists and belongs to user
                var conversationExists = await _dbContext.Conversations
                    .AnyAsync(c => c.Id == conversationId && c.UserId == userId);

                if (!conversationExists)
                    return NotFound("Conversation not found");

                var messages = await _dbContext.Messages
                    .Include(m => m.Sources)
                        .ThenInclude(s => s.Document)
                    .Where(m => m.ConversationId == conversationId)
                    .OrderBy(m => m.Timestamp)
                    .Select(m => new
                    {
                        m.Id,
                        m.Role,
                        m.Content,
                        m.Timestamp,
                        m.Metadata,
                    Sources = m.Sources.OrderBy(s => s.Order).Select(s => new
                    {
                        s.DocumentId,
                        DocumentTitle = s.Document.Title ?? s.Document.OriginalFileName,
                        DocumentLink = s.Document.DocumentLink,
                        FileName = s.Document.FileName,
                        s.RelevanceScore,
                        s.ChunksUsed
                    })
                    })
                    .ToListAsync();

                return Ok(messages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving messages: {ex.Message}");
            }
        }

        [HttpDelete("{messageId}")]
        public async Task<IActionResult> DeleteMessage(int conversationId, int messageId)
        {
            try
            {
                var userId = _userContext.GetCurrentUserId();

                // Verify conversation exists and belongs to user, and message belongs to conversation
                var message = await _dbContext.Messages
                    .Include(m => m.Conversation)
                    .FirstOrDefaultAsync(m => m.Id == messageId &&
                                           m.ConversationId == conversationId &&
                                           m.Conversation.UserId == userId);

                if (message == null)
                    return NotFound("Message not found");

                _dbContext.Messages.Remove(message);

                // Update conversation's UpdatedAt timestamp
                message.Conversation.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Message deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while deleting the message: {ex.Message}");
            }
        }
    }

    public class AddMessageRequest
    {
        [Required(ErrorMessage = "Role is required")]
        public MessageRole Role { get; set; }

        [Required(ErrorMessage = "Content is required")]
        public string Content { get; set; }

        public string? Metadata { get; set; }
    }
}