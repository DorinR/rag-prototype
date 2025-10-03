using rag_experiment.Models;

namespace rag_experiment.Repositories.Conversations
{
    /// <summary>
    /// Interface for conversation repository operations
    /// </summary>
    public interface IConversationRepository
    {
        /// <summary>
        /// Retrieves all messages for a specific conversation
        /// </summary>
        /// <param name="conversationId">The ID of the conversation</param>
        /// <returns>List of messages ordered by timestamp, or empty list if conversation not found</returns>
        Task<List<Message>> GetMessagesAsync(int conversationId);

        /// <summary>
        /// Adds a new message to a conversation
        /// </summary>
        /// <param name="message">The message to add</param>
        /// <returns>The added message with its generated ID</returns>
        Task<Message> AddMessageAsync(Message message);
    }
}
