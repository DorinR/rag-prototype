using System.ComponentModel.DataAnnotations;

namespace rag_experiment.Models
{
    public enum MessageRole
    {
        User,
        Assistant,
        System
    }

    public class Message
    {
        public int Id { get; set; }

        [Required]
        public MessageRole Role { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // JSON field for additional context (can store metadata like tokens, processing time, etc.)
        public string? Metadata { get; set; }

        // Conversation association
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; }

        // Source documents that contributed to this message (only populated for Assistant messages)
        public List<MessageSource> Sources { get; set; } = new();
    }
}