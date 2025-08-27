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

            // Always return tokens in response body (no more cookies!)
            var responseWithTokens = new
            {
                response.Success,
                response.Message,
                response.User,
                AccessToken = jwtToken,
                RefreshToken = refreshToken.Token
            };

            return Ok(responseWithTokens);
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

            // Always return tokens in response body (no more cookies!)
            var responseWithTokens = new
            {
                response.Success,
                response.Message,
                response.User,
                AccessToken = jwtToken,
                RefreshToken = refreshToken.Token
            };

            return Ok(responseWithTokens);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrEmpty(request?.RefreshToken))
            {
                return BadRequest(new AuthResponse { Success = false, Message = "No refresh token provided" });
            }

            var response = await _authService.RefreshTokenAsync(request.RefreshToken);
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

            // Return new tokens in response body
            var responseWithTokens = new
            {
                response.Success,
                response.Message,
                response.User,
                AccessToken = jwtToken,
                RefreshToken = response.RefreshToken
            };

            return Ok(responseWithTokens);
        }

        [Authorize]
        [HttpPost("revoke-token")]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrEmpty(request?.RefreshToken))
            {
                return BadRequest(new { message = "Refresh token is required" });
            }

            var success = await _authService.RevokeTokenAsync(request.RefreshToken);
            if (!success)
            {
                return BadRequest(new { message = "Token revocation failed" });
            }

            return Ok(new { message = "Token revoked successfully" });
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


    }
}