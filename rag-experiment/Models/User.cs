using System.ComponentModel.DataAnnotations;

namespace rag_experiment.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // Navigation properties
        public List<RefreshToken> RefreshTokens { get; set; } = new();
        public List<Conversation> Conversations { get; set; } = new();
    }
}