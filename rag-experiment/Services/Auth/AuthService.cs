using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using rag_experiment.Models;
using rag_experiment.Models.Auth;
using BC = BCrypt.Net.BCrypt;

namespace rag_experiment.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(AppDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                {
                    return new AuthResponse { Success = false, Message = "Email already registered" };
                }

                var user = new User
                {
                    Email = request.Email,
                    PasswordHash = BC.HashPassword(request.Password),
                    FirstName = request.FirstName,
                    LastName = request.LastName
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return new AuthResponse
                {
                    Success = true,
                    Message = "Registration successful",
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return new AuthResponse { Success = false, Message = "Registration failed" };
            }
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.RefreshTokens)
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null || !BC.Verify(request.Password, user.PasswordHash))
                {
                    return new AuthResponse { Success = false, Message = "Invalid email or password" };
                }

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return new AuthResponse { Success = false, Message = "Login failed" };
            }
        }

        public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                var token = await _context.RefreshTokens
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.Token == refreshToken);

                if (token == null)
                {
                    return new AuthResponse { Success = false, Message = "Invalid refresh token" };
                }

                if (!token.IsActive)
                {
                    return new AuthResponse { Success = false, Message = "Inactive refresh token" };
                }

                // Revoke the current refresh token
                token.RevokedAt = DateTime.UtcNow;
                token.ReasonRevoked = "Refresh token used";

                // Generate a new refresh token
                var newRefreshToken = GenerateRefreshToken();
                token.ReplacedByToken = newRefreshToken.Token;

                token.User.RefreshTokens.Add(newRefreshToken);
                await _context.SaveChangesAsync();

                return new AuthResponse
                {
                    Success = true,
                    Message = "Token refreshed successfully",
                    User = new UserDto
                    {
                        Id = token.User.Id,
                        Email = token.User.Email,
                        FirstName = token.User.FirstName,
                        LastName = token.User.LastName
                    },
                    RefreshToken = newRefreshToken.Token
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return new AuthResponse { Success = false, Message = "Token refresh failed" };
            }
        }

        public async Task<bool> RevokeTokenAsync(string refreshToken)
        {
            try
            {
                var token = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);

                if (token == null)
                {
                    return false;
                }

                if (!token.IsActive)
                {
                    return false;
                }

                token.RevokedAt = DateTime.UtcNow;
                token.ReasonRevoked = "Revoked by user";
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking refresh token");
                return false;
            }
        }

        public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null)
                {
                    // For security reasons, we return success even if the email doesn't exist
                    return new AuthResponse { Success = true, Message = "If your email is registered, you will receive a password reset link" };
                }

                // TODO: Implement email sending logic here
                // For now, we'll just return a success response
                return new AuthResponse { Success = true, Message = "If your email is registered, you will receive a password reset link" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset request");
                return new AuthResponse { Success = false, Message = "Password reset request failed" };
            }
        }

        public async Task<AuthResponse> ConfirmResetPasswordAsync(ConfirmResetPasswordRequest request)
        {
            try
            {
                // TODO: Implement actual token validation logic
                // For now, we'll return a not implemented response
                return new AuthResponse { Success = false, Message = "Password reset confirmation not implemented" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset confirmation");
                return new AuthResponse { Success = false, Message = "Password reset confirmation failed" };
            }
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public RefreshToken GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            return new RefreshToken
            {
                Token = Convert.ToBase64String(randomBytes),
                ExpiresAt = DateTime.UtcNow.AddDays(7), // 7 days expiry
                CreatedAt = DateTime.UtcNow
            };
        }

        public string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured"));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email)
                }),
                Expires = DateTime.UtcNow.AddMinutes(15), // 15 minutes expiry
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}