using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;
using rag_experiment.Services;
using rag_experiment.Services.Auth;

namespace rag_experiment.Repositories.Conversations
{
    /// <summary>
    /// Repository implementation for conversation operations using Entity Framework
    /// </summary>
    public class ConversationRepository : IConversationRepository
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserContext _userContext;

        public ConversationRepository(AppDbContext dbContext, IUserContext userContext)
        {
            _dbContext = dbContext;
            _userContext = userContext;
        }

        /// <summary>
        /// Retrieves all messages for a specific conversation
        /// </summary>
        /// <param name="conversationId">The ID of the conversation</param>
        /// <returns>List of messages ordered by timestamp, or empty list if conversation not found</returns>
        public async Task<List<Message>> GetMessagesAsync(int conversationId)
        {
            var userId = _userContext.GetCurrentUserId();

            // First verify that the conversation exists and belongs to the current user
            var conversationExists = await _dbContext.Conversations
                .AnyAsync(c => c.Id == conversationId && c.UserId == userId);

            if (!conversationExists)
                return new List<Message>();

            // Get all messages for the conversation ordered by timestamp
            var messages = await _dbContext.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            return messages;
        }
    }
}
