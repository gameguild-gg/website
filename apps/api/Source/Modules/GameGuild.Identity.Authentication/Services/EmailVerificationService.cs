using System.Security.Cryptography;
using System.Text;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles verification/reset token generation, validation, and email dispatch coordination.
/// </summary>
public class EmailVerificationService(
    ILogger<EmailVerificationService> logger,
    IMemoryCache memoryCache,
    IPublisher publisher,
    IUserRepository? userRepository = null) : IEmailVerificationService
{
    private const string TokenKeyPrefix = "emailverify:token:";
    private const string VerifiedKeyPrefix = "emailverify:verified:";
    private const string RateLimitKeyPrefix = "emailverify:ratelimit:";
    private const string EmailVerificationTokenType = "email_verification";
    private const string PasswordResetTokenType = "password_reset";
    private const string MagicLinkTokenType = "magic_link";

    public Task<string> GenerateVerificationTokenAsync(Guid userId, string email)
    {
        return GenerateTokenAsync(userId, email, EmailVerificationTokenType, TimeSpan.FromHours(24));
    }

    public Task<string> GeneratePasswordResetTokenAsync(Guid userId, string email)
    {
        return GenerateTokenAsync(userId, email, PasswordResetTokenType, TimeSpan.FromHours(1));
    }

    public Task<string> GenerateMagicLinkTokenAsync(Guid userId, string email)
    {
        return GenerateTokenAsync(userId, email, MagicLinkTokenType, TimeSpan.FromMinutes(15));
    }

    private Task<string> GenerateTokenAsync(Guid userId, string email, string tokenType, TimeSpan lifetime)
    {
        try
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            var tokenInfo = new TokenInfo
            {
                UserId = userId,
                Email = email.ToLowerInvariant(),
                Type = tokenType,
                ExpiresAt = SystemClock.UtcNow.Add(lifetime)
            };

            memoryCache.Set(GetTokenCacheKey(token), tokenInfo, new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = tokenInfo.ExpiresAt
            }.SetSize(1));

            logger.LogInformation("Generated {TokenType} token for user {UserId}", tokenType, LogRedaction.RedactId(userId, "uid"));
            return Task.FromResult(token);
        }
        catch (Exception ex)
        {
            logger.LogError("Error generating {TokenType} token for user {UserId}: {ErrorType}",
                tokenType, LogRedaction.RedactId(userId, "uid"), ex.GetType().FullName);
            throw;
        }
    }

    public async Task SendVerificationEmailAsync(string email, string token, string? userName = null)
    {
        try
        {
            await publisher.Publish(
                new EmailVerificationRequestedNotification
                {
                    Email = email,
                    Token = token,
                    UserName = userName
                }).ConfigureAwait(false);

            logger.LogInformation("Verification email queued for {Email}", LogRedaction.MaskEmail(email));
        }
        catch (Exception ex)
        {
            logger.LogError("Error sending verification email to {Email}: {ErrorType}",
                LogRedaction.MaskEmail(email), ex.GetType().FullName);
            throw;
        }
    }

    public async Task<bool> VerifyEmailTokenAsync(Guid userId, string token)
    {
        var result = await ValidateAndConsumeTokenAsync(
            token,
            EmailVerificationTokenType,
            userId,
            markEmailVerified: true).ConfigureAwait(false);

        return result.Success;
    }

    public Task<TokenValidationResult> VerifyEmailTokenAsync(string token)
    {
        return ValidateAndConsumeTokenAsync(
            token,
            EmailVerificationTokenType,
            expectedUserId: null,
            markEmailVerified: true);
    }

    public Task<TokenValidationResult> VerifyPasswordResetTokenAsync(string token)
    {
        return ValidateAndConsumeTokenAsync(
            token,
            PasswordResetTokenType,
            expectedUserId: null,
            markEmailVerified: false);
    }

    public Task<TokenValidationResult> VerifyMagicLinkTokenAsync(string token)
    {
        return ValidateAndConsumeTokenAsync(
            token,
            MagicLinkTokenType,
            expectedUserId: null,
            markEmailVerified: false);
    }

    private Task<TokenValidationResult> ValidateAndConsumeTokenAsync(
        string token,
        string expectedType,
        Guid? expectedUserId,
        bool markEmailVerified)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogWarning("Empty {TokenType} token used", expectedType);
                return Task.FromResult(TokenValidationResult.Failed("Token is required"));
            }

            var tokenKey = GetTokenCacheKey(token);
            if (!memoryCache.TryGetValue(tokenKey, out TokenInfo? tokenInfo) || tokenInfo == null)
            {
                logger.LogWarning("Invalid {TokenType} token used", expectedType);
                return Task.FromResult(TokenValidationResult.Failed("Invalid token"));
            }

            if (expectedUserId.HasValue && tokenInfo.UserId != expectedUserId.Value)
            {
                logger.LogWarning(
                    "Token user ID mismatch. Expected {ExpectedUserId}, got {ActualUserId}",
                    LogRedaction.RedactId(expectedUserId, "uid"),
                    LogRedaction.RedactId(tokenInfo.UserId, "uid"));

                return Task.FromResult(TokenValidationResult.Failed("Token does not belong to the requested user"));
            }

            if (tokenInfo.ExpiresAt < SystemClock.UtcNow)
            {
                memoryCache.Remove(tokenKey);
                logger.LogWarning("Expired {TokenType} token used for user {UserId}", expectedType, LogRedaction.RedactId(tokenInfo.UserId, "uid"));
                return Task.FromResult(TokenValidationResult.Failed("Expired token"));
            }

            if (tokenInfo.Type != expectedType)
            {
                logger.LogWarning(
                    "Invalid token type {ActualTokenType}; expected {ExpectedTokenType}",
                    tokenInfo.Type,
                    expectedType);

                return Task.FromResult(TokenValidationResult.Failed("Invalid token type"));
            }

            if (!tokenInfo.TryConsume())
            {
                logger.LogWarning("Already consumed {TokenType} token used", expectedType);
                return Task.FromResult(TokenValidationResult.Failed("Invalid token"));
            }

            if (markEmailVerified)
            {
                memoryCache.Set(VerifiedKeyPrefix + tokenInfo.UserId, true, new MemoryCacheEntryOptions().SetSize(1));
            }

            memoryCache.Remove(tokenKey);

            logger.LogInformation("{TokenType} token consumed successfully for user {UserId}", expectedType, LogRedaction.RedactId(tokenInfo.UserId, "uid"));
            return Task.FromResult(new TokenValidationResult(true, tokenInfo.UserId, tokenInfo.Email));
        }
        catch (Exception ex)
        {
            logger.LogError("Error verifying {TokenType} token: {ErrorType}", expectedType, ex.GetType().FullName);
            return Task.FromResult(TokenValidationResult.Failed("Token verification failed"));
        }
    }

    public async Task<bool> IsEmailVerifiedAsync(Guid userId)
    {
        try
        {
            if (userRepository is not null)
            {
                var user = await userRepository.GetByIdAsync(userId).ConfigureAwait(false);
                if (user is not null)
                {
                    return user.IsEmailVerified;
                }
            }

            return memoryCache.TryGetValue(VerifiedKeyPrefix + userId, out bool verified) && verified;
        }
        catch (Exception ex)
        {
            logger.LogError("Error checking email verification status for user {UserId}: {ErrorType}",
                LogRedaction.RedactId(userId, "uid"), ex.GetType().FullName);
            return false;
        }
    }

    public async Task<bool> ResendVerificationEmailAsync(Guid userId, string email)
    {
        try
        {
            // Cache identity needs a distinct key per address; log redaction deliberately emits a constant marker.
            // Normalize before hashing so case variants of the same address share one rate-limit bucket,
            // and use the complete digest to avoid truncation collisions and keep raw emails out of cache keys.
            var emailDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));
            var rateLimitKey = RateLimitKeyPrefix + $"{userId}:{emailDigest}";

            if (memoryCache.TryGetValue(rateLimitKey, out DateTime lastSent))
            {
                var timeSinceLastSent = SystemClock.UtcNow - lastSent;

                if (timeSinceLastSent < TimeSpan.FromMinutes(2))
                {
                    logger.LogWarning(
                        "Rate limit exceeded for resending verification email to user {UserId}. Last sent {Seconds} seconds ago",
                        LogRedaction.RedactId(userId, "uid"),
                        timeSinceLastSent.TotalSeconds);

                    return false;
                }
            }

            var token = await GenerateVerificationTokenAsync(userId, email).ConfigureAwait(false);
            await SendVerificationEmailAsync(email, token).ConfigureAwait(false);

            memoryCache.Set(rateLimitKey, SystemClock.UtcNow, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            }.SetSize(1));

            logger.LogInformation("Resent verification email to user {UserId}", LogRedaction.RedactId(userId, "uid"));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Error resending verification email for user {UserId}: {ErrorType}",
                LogRedaction.RedactId(userId, "uid"), ex.GetType().FullName);
            return false;
        }
    }

    public Task<bool> IsTokenValidAsync(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token) ||
                !memoryCache.TryGetValue(GetTokenCacheKey(token), out TokenInfo? tokenInfo) || tokenInfo == null)
            {
                return Task.FromResult(false);
            }

            var isValid = !tokenInfo.IsConsumed && tokenInfo.ExpiresAt >= SystemClock.UtcNow &&
                (tokenInfo.Type == EmailVerificationTokenType ||
                 tokenInfo.Type == PasswordResetTokenType ||
                 tokenInfo.Type == MagicLinkTokenType);

            return Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            logger.LogError("Error checking token validity: {ErrorType}", ex.GetType().FullName);
            return Task.FromResult(false);
        }
    }

    private static string GetTokenCacheKey(string token) =>
        TokenKeyPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
