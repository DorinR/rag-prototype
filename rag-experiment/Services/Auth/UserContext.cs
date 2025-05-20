using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace rag_experiment.Services.Auth
{
    public interface IUserContext
    {
        int GetCurrentUserId();
    }

    public class UserContext : IUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated or user ID is invalid");
            }

            return userId;
        }
    }
}