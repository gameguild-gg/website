using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Issues and verifies one-time email sign-in codes backed by the in-process
///     memory cache. Codes are six cryptographically random digits, live for ten
///     minutes, accept at most five verification attempts, are single-use, and are
///     stored only as SHA-256 digests. Code comparison is constant-time, including
///     a dummy comparison on the unknown-email path so timings stay uniform.
/// </summary>
public class EmailCodeService(
    ILogger<EmailCodeService> logger,
    IMemoryCache memoryCache) : IEmailCodeService
{
    private const string EntryKeyPrefix = "emailcode:entry:";
    private const string ThrottleKeyPrefix = "emailcode:throttle:";

    /// <summary>How long an issued code can be verified.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Minimum interval between two code requests for the same address.</summary>
    public static readonly TimeSpan ResendThrottleWindow = TimeSpan.FromSeconds(60);

    /// <summary>Wrong-code attempts tolerated before the code is invalidated.</summary>
    public const int MaxVerificationAttempts = 5;

    /// <summary>
    ///     Extra retention beyond <see cref="CodeLifetime"/> for the cache-level safety-net
    ///     eviction, so an entry is never physically dropped by the cache before its logical
    ///     lifetime has been judged by the domain clock.
    /// </summary>
    public static readonly TimeSpan EvictionSlack = TimeSpan.FromMinutes(5);

    private static readonly byte[] DummyDigest = SHA256.HashData(Encoding.UTF8.GetBytes("email-code-timing-equalizer"));

    public Task<string?> GenerateEmailCodeAsync(Guid userId, string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var emailDigest = EmailDigest(normalizedEmail);
        var throttleKey = ThrottleKeyPrefix + emailDigest;

        if (memoryCache.TryGetValue(throttleKey, out DateTime lastIssued) &&
            SystemClock.UtcNow - lastIssued < ResendThrottleWindow)
        {
            logger.LogWarning(
                "Email-code request throttled for user {UserId}; a code was issued {Seconds:F0}s ago",
                LogRedaction.RedactId(userId, "uid"),
                (SystemClock.UtcNow - lastIssued).TotalSeconds);
            return Task.FromResult<string?>(null);
        }

        // Crypto-random six-digit code (000000–999999, uniform via the RNG rejection-free Int32 overload).
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var entry = new EmailCodeInfo
        {
            UserId = userId,
            Email = normalizedEmail,
            CodeDigest = SHA256.HashData(Encoding.UTF8.GetBytes(code)),
            ExpiresAt = SystemClock.UtcNow.Add(CodeLifetime)
        };

        memoryCache.Set(EntryKeyPrefix + emailDigest, entry, new MemoryCacheEntryOptions
        {
            // Cache-level eviction is only a safety net for abandoned entries, so it must
            // be relative to the cache's own clock. The logical ten-minute TTL lives in
            // entry.ExpiresAt and is enforced against the domain clock (SystemClock) in
            // VerifyEmailCodeAsync — stamping an absolute, domain-clock-derived expiration
            // here would let the cache's real-time scanner evict live entries whenever the
            // domain clock (e.g. a fake provider in tests) disagrees with wall time.
            AbsoluteExpirationRelativeToNow = CodeLifetime + EvictionSlack
        }.SetSize(1));

        memoryCache.Set(throttleKey, SystemClock.UtcNow, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ResendThrottleWindow
        }.SetSize(1));

        logger.LogInformation("Generated one-time email sign-in code for user {UserId}", LogRedaction.RedactId(userId, "uid"));
        return Task.FromResult<string?>(code);
    }

    public Task<TokenValidationResult> VerifyEmailCodeAsync(string email, string code)
    {
        var normalizedEmail = NormalizeEmail(email);
        var entryKey = EntryKeyPrefix + EmailDigest(normalizedEmail);
        var suppliedDigest = string.IsNullOrWhiteSpace(code)
            ? SHA256.HashData(Encoding.UTF8.GetBytes(string.Empty))
            : SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));

        // Constant-time comparison on every path, including unknown emails, so the
        // digest match itself never becomes a timing oracle.
        var entry = memoryCache.TryGetValue(entryKey, out EmailCodeInfo? stored) ? stored : null;
        var storedDigest = entry?.CodeDigest ?? DummyDigest;
        var matches = CryptographicOperations.FixedTimeEquals(storedDigest, suppliedDigest);

        if (entry is null)
        {
            logger.LogWarning("Verification attempted with no pending email sign-in code for {Email}", LogRedaction.MaskEmail(normalizedEmail));
            return Task.FromResult(TokenValidationResult.Failed("Invalid code"));
        }

        if (entry.ExpiresAt < SystemClock.UtcNow)
        {
            memoryCache.Remove(entryKey);
            logger.LogWarning("Expired email sign-in code used for user {UserId}", LogRedaction.RedactId(entry.UserId, "uid"));
            return Task.FromResult(TokenValidationResult.Failed("Expired code"));
        }

        if (!matches)
        {
            entry.FailedAttempts++;
            if (entry.FailedAttempts >= MaxVerificationAttempts)
            {
                memoryCache.Remove(entryKey);
                logger.LogWarning(
                    "Email sign-in code exhausted after {Attempts} failed attempts for user {UserId}",
                    entry.FailedAttempts,
                    LogRedaction.RedactId(entry.UserId, "uid"));
            }
            else
            {
                logger.LogWarning(
                    "Wrong email sign-in code submitted for user {UserId}; {Remaining} attempts left",
                    LogRedaction.RedactId(entry.UserId, "uid"),
                    MaxVerificationAttempts - entry.FailedAttempts);
            }

            return Task.FromResult(TokenValidationResult.Failed("Invalid code"));
        }

        if (!entry.TryConsume())
        {
            logger.LogWarning("Already consumed email sign-in code used for user {UserId}", LogRedaction.RedactId(entry.UserId, "uid"));
            return Task.FromResult(TokenValidationResult.Failed("Invalid code"));
        }

        memoryCache.Remove(entryKey);
        logger.LogInformation("Email sign-in code consumed successfully for user {UserId}", LogRedaction.RedactId(entry.UserId, "uid"));
        return Task.FromResult(new TokenValidationResult(true, entry.UserId, entry.Email));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string EmailDigest(string normalizedEmail) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)));
}
