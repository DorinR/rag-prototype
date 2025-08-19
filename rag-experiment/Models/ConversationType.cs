namespace rag_experiment.Models
{
    /// <summary>
    /// Defines the type of conversation to distinguish between different query contexts
    /// </summary>
    public enum ConversationType
    {
        /// <summary>
        /// Conversation for asking questions about specific uploaded documents
        /// </summary>
        DocumentQuery = 0,

        /// <summary>
        /// Conversation for asking questions about the general system knowledge base
        /// </summary>
        GeneralKnowledge = 1
    }
}
