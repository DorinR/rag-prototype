namespace rag_experiment.Models.Auth
{
    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public UserDto? User { get; set; }
        public string? RefreshToken { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}