using System.ComponentModel.DataAnnotations;

namespace rag_experiment.Models
{
    public class Conversation
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // User association
        public int UserId { get; set; }
        public User User { get; set; }

        // Navigation properties
        public List<Document> Documents { get; set; } = new();
        public List<Message> Messages { get; set; } = new();
    }
}