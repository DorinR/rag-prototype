using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using rag_experiment.Models.Auth;
using rag_experiment.Services.Auth;
using rag_experiment.Services;

namespace rag_experiment.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AuthController(
            IAuthService authService,
            IConfiguration configuration,
            ILogger<AuthController> logger,
            AppDbContext context,
            IWebHostEnvironment environment)
        {
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
            _context = context;
            _environment = environment;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var response = await _authService.RegisterAsync(request);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            // Generate tokens
            var user = await _authService.GetUserByIdAsync(response.User.Id);
            if (user == null)
            {
                return StatusCode(500, new AuthResponse { Success = false, Message = "Error retrieving user after registration" });
            }

            var jwtToken = ((AuthService)_authService).GenerateJwtToken(user);
            var refreshToken = ((AuthService)_authService).GenerateRefreshToken();
            user.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            // Set cookies
            SetTokenCookies(jwtToken, refreshToken.Token);

            return Ok(response);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            // Generate tokens
            var user = await _authService.GetUserByIdAsync(response.User.Id);
            if (user == null)
            {
                return StatusCode(500, new AuthResponse { Success = false, Message = "Error retrieving user" });
            }

            var jwtToken = ((AuthService)_authService).GenerateJwtToken(user);
            var refreshToken = ((AuthService)_authService).GenerateRefreshToken();
            user.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            // Set cookies
            SetTokenCookies(jwtToken, refreshToken.Token);

            return Ok(response);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
            {
                return BadRequest(new AuthResponse { Success = false, Message = "No refresh token provided" });
            }

            var response = await _authService.RefreshTokenAsync(refreshToken);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            // Generate new JWT token
            var user = await _authService.GetUserByIdAsync(response.User.Id);
            if (user == null)
            {
                return StatusCode(500, new AuthResponse { Success = false, Message = "Error retrieving user" });
            }

            var jwtToken = ((AuthService)_authService).GenerateJwtToken(user);

            // Make sure we have a new refresh token
            if (string.IsNullOrEmpty(response.RefreshToken))
            {
                return StatusCode(500, new AuthResponse { Success = false, Message = "No refresh token generated" });
            }

            // Set new cookies
            SetTokenCookies(jwtToken, response.RefreshToken);

            return Ok(response);
        }

        [Authorize]
        [HttpPost("revoke-token")]
        public async Task<IActionResult> RevokeToken()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
            {
                return BadRequest(new { message = "Token is required" });
            }

            var success = await _authService.RevokeTokenAsync(refreshToken);
            if (!success)
            {
                return BadRequest(new { message = "Token revocation failed" });
            }

            // Remove cookies
            Response.Cookies.Delete("token");
            Response.Cookies.Delete("refreshToken");

            return Ok(new { message = "Token revoked" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var response = await _authService.ResetPasswordAsync(request);
            return Ok(response);
        }

        [HttpPost("reset-password-confirm")]
        public async Task<IActionResult> ResetPasswordConfirm([FromBody] ConfirmResetPasswordRequest request)
        {
            var response = await _authService.ConfirmResetPasswordAsync(request);
            if (!response.Success)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        private void SetTokenCookies(string jwtToken, string refreshToken)
        {
            // For cross-origin cookies, we need SameSite=None and Secure=true
            // In development, we might need to handle localhost scenarios
            var isProduction = !_environment.IsDevelopment();

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = isProduction, // Required for SameSite=None in production
                SameSite = SameSiteMode.None, // Required for cross-origin requests
                Expires = DateTime.UtcNow.AddDays(7)
            };

            Response.Cookies.Append("token", jwtToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = isProduction, // Required for SameSite=None in production
                SameSite = SameSiteMode.None, // Required for cross-origin requests
                Expires = DateTime.UtcNow.AddMinutes(15) // Match JWT token expiry
            });

            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }
    }
}