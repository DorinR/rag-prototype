using System.ComponentModel.DataAnnotations;

namespace rag_experiment.Models
{
    /// <summary>
    /// Represents a document source that contributed to a message response.
    /// Tracks which documents were used to generate each assistant message for citation purposes.
    /// </summary>
    public class MessageSource
    {
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the message that cited this document
        /// </summary>
        [Required]
        public int MessageId { get; set; }

        /// <summary>
        /// Foreign key to the document that was cited
        /// </summary>
        [Required]
        public int DocumentId { get; set; }

        /// <summary>
        /// The similarity/relevance score when this document was retrieved (0.0 to 1.0)
        /// </summary>
        public float RelevanceScore { get; set; }

        /// <summary>
        /// Number of chunks from this document that were used in the context
        /// </summary>
        public int ChunksUsed { get; set; }

        /// <summary>
        /// Display order for source citations (most relevant first)
        /// </summary>
        public int Order { get; set; }

        // Navigation properties
        public Message Message { get; set; }
        public Document Document { get; set; }
    }
}


