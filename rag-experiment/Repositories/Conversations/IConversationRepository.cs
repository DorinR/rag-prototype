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
    }
}
