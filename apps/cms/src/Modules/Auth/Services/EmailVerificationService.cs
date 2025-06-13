using System.Security.Cryptography;
using System.Text;
using GameGuild.Data;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.User.Models;
using Microsoft.EntityFrameworkCore;
using UserModel = GameGuild.Modules.User.Models.User;
using CredentialModel = GameGuild.Modules.User.Models.Credential;

namespace GameGuild.Modules.Auth.Services
{
    public interface IEmailVerificationService
    {
        Task<EmailOperationResponseDto> SendEmailVerificationAsync(string email);

        Task<EmailOperationResponseDto> VerifyEmailAsync(string token);

        Task<EmailOperationResponseDto> SendPasswordResetAsync(string email);

        Task<EmailOperationResponseDto> ResetPasswordAsync(string token, string newPassword);

        Task<string> GenerateEmailVerificationTokenAsync(Guid userId);

        Task<string> GeneratePasswordResetTokenAsync(Guid userId);
    }

    public class EmailVerificationService : IEmailVerificationService
    {
        private readonly ApplicationDbContext _context;

        private readonly ILogger<EmailVerificationService> _logger;

        private readonly IConfiguration _configuration;

        // In production, use Redis or database for token storage
        private readonly Dictionary<string, TokenInfo> _tokens = new();

        public EmailVerificationService(ApplicationDbContext context, ILogger<EmailVerificationService> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<EmailOperationResponseDto> SendEmailVerificationAsync(string email)
        {
            try
            {
                User.Models.User? user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    return new EmailOperationResponseDto
                    {
                        Success = false, Message = "User not found"
                    };
                }

                string token = await GenerateEmailVerificationTokenAsync(user.Id);

                // TODO: Send actual email using an email service (SendGrid, SMTP, etc.)
                // For now, just log the token (in production, never log sensitive tokens)
                _logger.LogInformation("Email verification token for {Email}: {Token}", email, token);

                return new EmailOperationResponseDto
                {
                    Success = true, Message = "Verification email sent successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email verification to {Email}", email);

                return new EmailOperationResponseDto
                {
                    Success = false, Message = "Failed to send verification email"
                };
            }
        }

        public async Task<EmailOperationResponseDto> VerifyEmailAsync(string token)
        {
            try
            {
                if (!_tokens.TryGetValue(token, out TokenInfo? tokenInfo))
                {
                    return new EmailOperationResponseDto
                    {
                        Success = false, Message = "Invalid or expired token"
                    };
                }

                if (tokenInfo.ExpiresAt < DateTime.UtcNow)
                {
                    _tokens.Remove(token);

                    return new EmailOperationResponseDto
                    {
                        Success = false, Message = "Token has expired"
                    };
                }

                if (tokenInfo.Type != "email_verification")
                {
                    return new EmailOperationResponseDto
                    {
                        Success = false, Message = "Invalid token type"
                    };
                }

                User.Models.User? user = await _context.Users.FindAsync(tokenInfo.UserId);
                if (user == null)
                {
                    return new EmailOperationResponseDto
                    {
                        Success = false, Message = "User not found"
                    };
                }

                // Mark email as verified (you might want to add an EmailVerified field to User model)
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Remove used token
                _tokens.Remove(token);

                return new EmailOperationResponseDto
                {
                    Success = true, Message = "Email verified successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email with token {Token}", token);

                return new EmailOperationResponseDto
                {
                    Success = false, Message = "Email verification failed"
                };
            }
        }

        public async Task<EmailOperationResponseDto> SendPasswordResetAsync(string email)
        {
            try
            {
                User.Models.User? user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    // Don't reveal if user exists or not for security
                    return new EmailOperationResponseDto
                    {
                        Success = true, Message = "If an account with that email exists, a password reset link has been sent"
                    };
                }

                string token = await GeneratePasswordResetTokenAsync(user.Id);

                // TODO: Send actual email using an email service
                // For now, just log the token (in production, never log sensitive tokens)
                _logger.LogInformation("Password reset token for {Email}: {Token}", email, token);

                return new EmailOperationResponseDto
                {
                    Success = true, Message = "If an account with that email exists, a password reset link has been sent"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset to {Email}", email);

                return new EmailOperationResponseDto
                {
                    Success = false, Message = "Failed to send password reset email"
                };
            }
        }

        public async Task<EmailOperationResponseDto> ResetPasswordAsync(string token, string newPassword)
        {
            try
            {
                if (!_tokens.TryGetValue(token, out TokenInfo? tokenInfo))
                {
                    return new EmailOperationResponseDto
                    {
                        Success = false, Message = "Invalid or expired token"
                    };
                }

                if (tokenInfo.ExpiresAt < DateTime.UtcNow)
                {
                    _tokens.Remove(token);

                    return new EmailOperationResponseDto
                    {
                        Success = false, Message = "Token has expired"
                    };
                }

                if (tokenInfo.Type != "password_reset")
                {
                    return new EmailOperationResponseDto
                    {
                        Success = false, Message = "Invalid token type"
                    };
                }

                User.Models.User? user = await _context.Users.Include(u => u.Credentials).FirstOrDefaultAsync(u => u.Id == tokenInfo.UserId);
                if (user == null)
                {
                    return new EmailOperationResponseDto
                    {
                        Success = false, Message = "User not found"
                    };
                }

                // Find and update password credential
                Credential? passwordCredential = user.Credentials?.FirstOrDefault(c => c.Type == "password");
                if (passwordCredential != null)
                {
                    passwordCredential.Value = HashPassword(newPassword);
                    passwordCredential.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new password credential
                    passwordCredential = new Credential
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        Type = "password",
                        Value = HashPassword(newPassword),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Credentials.Add(passwordCredential);
                }

                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Remove used token
                _tokens.Remove(token);

                return new EmailOperationResponseDto
                {
                    Success = true, Message = "Password reset successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password with token {Token}", token);

                return new EmailOperationResponseDto
                {
                    Success = false, Message = "Password reset failed"
                };
            }
        }

        public Task<string> GenerateEmailVerificationTokenAsync(Guid userId)
        {
            string token = GenerateSecureToken();
            var tokenInfo = new TokenInfo
            {
                UserId = userId, Type = "email_verification", ExpiresAt = DateTime.UtcNow.AddHours(24) // 24-hour expiration
            };

            _tokens[token] = tokenInfo;

            // Clean up expired tokens
            CleanupExpiredTokens();

            return Task.FromResult(token);
        }

        public Task<string> GeneratePasswordResetTokenAsync(Guid userId)
        {
            string token = GenerateSecureToken();
            var tokenInfo = new TokenInfo
            {
                UserId = userId, Type = "password_reset", ExpiresAt = DateTime.UtcNow.AddHours(1) // 1-hour expiration
            };

            _tokens[token] = tokenInfo;

            // Clean up expired tokens
            CleanupExpiredTokens();

            return Task.FromResult(token);
        }

        private string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);

            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private string HashPassword(string password)
        {
            // Use the same hashing logic as in AuthService
            using var sha256 = SHA256.Create();
            byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));

            return Convert.ToBase64String(hashedBytes);
        }

        private void CleanupExpiredTokens()
        {
            var expiredKeys = _tokens.Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow).Select(kvp => kvp.Key).ToList();
            foreach (string key in expiredKeys)
            {
                _tokens.Remove(key);
            }
        }

        private class TokenInfo
        {
            public Guid UserId
            {
                get;
                set;
            }

            public string Type
            {
                get;
                set;
            } = string.Empty;

            public DateTime ExpiresAt
            {
                get;
                set;
            }
        }
    }
}
