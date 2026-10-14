using System.Security.Cryptography;
using System.Text;
using GameGuild.CQRS;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     SMS OTP factor lifecycle (issue #244). Mirrors <see cref="EmailCodeService"/> security
///     semantics: crypto-random six-digit codes, SHA-256 digest-only storage, constant-time
///     comparison (including a dummy comparison on the miss path so timings stay uniform),
///     single-use consumption, a five-attempt verify ceiling and a sixty-second resend throttle.
///     Codes are queued for delivery through the Notifications SMS channel.
/// </summary>
public sealed class SmsMfaService(
    ILogger<SmsMfaService> logger,
    IMemoryCache memoryCache,
    IUserMfaConfigurationRepository mfaConfigRepository,
    IPublisher publisher) : ISmsMfaService
{
    /// <summary>How long an issued code can be verified.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Minimum interval between two code requests for the same user.</summary>
    public static readonly TimeSpan ResendThrottleWindow = TimeSpan.FromSeconds(60);

    /// <summary>Wrong-code attempts tolerated before the code is invalidated.</summary>
    public const int MaxVerificationAttempts = 5;

    private const string EntryKeyPrefix = "smsmfa:entry:";
    private const string ThrottleKeyPrefix = "smsmfa:throttle:";

    private static readonly byte[] DummyDigest = SHA256.HashData(Encoding.UTF8.GetBytes("sms-mfa-code-timing-equalizer"));

    /// <inheritdoc />
    public async Task<SmsMfaChallengeResult> StartEnrollmentAsync(Guid userId, string phoneNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        var normalized = NormalizePhoneNumber(phoneNumber);
        if (normalized is null)
        {
            return Failure("Phone number must be in international E.164 format (for example +15551234567).");
        }

        var code = await IssueCodeAsync(userId, normalized, isEnrollment: true, cancellationToken).ConfigureAwait(false);
        return code is null
            ? Failure("A verification code was sent recently. Wait before requesting another.")
            : new SmsMfaChallengeResult
            {
                Success = true,
                PhoneNumberMasked = MaskPhoneNumber(normalized),
                ExpiresInSeconds = (int)CodeLifetime.TotalSeconds
            };
    }

    /// <inheritdoc />
    public async Task<MfaVerificationResult> CompleteEnrollmentAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var entry = ConsumeCode(userId, code);
        if (entry is null)
        {
            MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Sms, "verify-failed");
            return new MfaVerificationResult { Success = false, Message = "Invalid or expired SMS code" };
        }

        var configuration = await mfaConfigRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Sms, "verify-failed");
            return new MfaVerificationResult { Success = false, Message = "No MFA configuration found for user" };
        }

        configuration.SmsPhoneNumber = entry.PhoneNumber;
        configuration.IsEnabled = true;
        configuration.UpdatedAt = SystemClock.UtcNow;
        await mfaConfigRepository.UpdateAsync(configuration, cancellationToken).ConfigureAwait(false);

        MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Sms, "enrolled");
        logger.LogInformation("SMS MFA enrolled for user {UserId}", LogRedaction.RedactId(userId, "uid"));
        return new MfaVerificationResult { Success = true, Message = "SMS MFA enrolled successfully" };
    }

    /// <inheritdoc />
    public async Task<SmsMfaChallengeResult> SendChallengeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var configuration = await mfaConfigRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (configuration?.SmsPhoneNumber is not { } phoneNumber)
        {
            return Failure("SMS MFA is not enrolled for this account.");
        }

        var code = await IssueCodeAsync(userId, phoneNumber, isEnrollment: false, cancellationToken).ConfigureAwait(false);
        return code is null
            ? Failure("A verification code was sent recently. Wait before requesting another.")
            : new SmsMfaChallengeResult
            {
                Success = true,
                PhoneNumberMasked = MaskPhoneNumber(phoneNumber),
                ExpiresInSeconds = (int)CodeLifetime.TotalSeconds
            };
    }

    /// <inheritdoc />
    public Task<bool> VerifyCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ConsumeCode(userId, code) is not null);
    }

    private async Task<string?> IssueCodeAsync(Guid userId, string phoneNumber, bool isEnrollment, CancellationToken cancellationToken)
    {
        var throttleKey = ThrottleKeyPrefix + userId.ToString("N");
        if (memoryCache.TryGetValue(throttleKey, out DateTime lastIssued) &&
            SystemClock.UtcNow - lastIssued < ResendThrottleWindow)
        {
            logger.LogWarning(
                "SMS MFA code request throttled for user {UserId}; a code was issued {Seconds:F0}s ago",
                LogRedaction.RedactId(userId, "uid"),
                (SystemClock.UtcNow - lastIssued).TotalSeconds);
            return null;
        }

        // Crypto-random six-digit code (uniform via the rejection-free Int32 overload).
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var entry = new SmsCodeEntry
        {
            PhoneNumber = phoneNumber,
            CodeDigest = SHA256.HashData(Encoding.UTF8.GetBytes(code)),
            ExpiresAt = SystemClock.UtcNow.Add(CodeLifetime)
        };

        memoryCache.Set(EntryKey(userId), entry, new MemoryCacheEntryOptions
        {
            // Cache eviction is only a safety net; the logical TTL lives in entry.ExpiresAt and is
            // enforced against the domain clock in ConsumeCodeAsync (see EmailCodeService for the
            // rationale against stamping domain-clock-derived absolute expirations here).
            AbsoluteExpirationRelativeToNow = CodeLifetime + EmailCodeService.EvictionSlack
        }.SetSize(1));

        memoryCache.Set(throttleKey, SystemClock.UtcNow, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ResendThrottleWindow
        }.SetSize(1));

        try
        {
            await publisher.Publish(new SmsMfaCodeRequestedNotification
            {
                UserId = userId,
                PhoneNumber = phoneNumber,
                Code = code,
                IsEnrollment = isEnrollment
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Delivery failure must not leave a live code the user never receives; drop it.
            memoryCache.Remove(EntryKey(userId));
            logger.LogError(exception, "Failed to dispatch SMS MFA code notification for user {UserId}", LogRedaction.RedactId(userId, "uid"));
            throw;
        }

        logger.LogInformation("SMS MFA code generated for user {UserId} ({State})",
            LogRedaction.RedactId(userId, "uid"), isEnrollment ? "enrollment" : "challenge");
        return code;
    }

    /// <summary>Consumes a submitted code; returns the consumed entry (with phone) on success, null otherwise.</summary>
    private SmsCodeEntry? ConsumeCode(Guid userId, string code)
    {
        var entryKey = EntryKey(userId);
        var suppliedDigest = string.IsNullOrWhiteSpace(code)
            ? SHA256.HashData(Encoding.UTF8.GetBytes(string.Empty))
            : SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));

        // Constant-time comparison on every path, including missing entries, so the digest match
        // itself never becomes a timing oracle.
        var entry = memoryCache.TryGetValue(entryKey, out SmsCodeEntry? stored) ? stored : null;
        var storedDigest = entry?.CodeDigest ?? DummyDigest;
        var matches = CryptographicOperations.FixedTimeEquals(storedDigest, suppliedDigest);

        if (entry is null)
        {
            logger.LogWarning("SMS MFA verification attempted with no pending code for user {UserId}", LogRedaction.RedactId(userId, "uid"));
            return null;
        }

        if (entry.ExpiresAt < SystemClock.UtcNow)
        {
            memoryCache.Remove(entryKey);
            logger.LogWarning("Expired SMS MFA code used for user {UserId}", LogRedaction.RedactId(userId, "uid"));
            return null;
        }

        if (!matches)
        {
            entry.FailedAttempts++;
            if (entry.FailedAttempts >= MaxVerificationAttempts)
            {
                memoryCache.Remove(entryKey);
                logger.LogWarning(
                    "SMS MFA code exhausted after {Attempts} failed attempts for user {UserId}",
                    entry.FailedAttempts,
                    LogRedaction.RedactId(userId, "uid"));
            }

            return null;
        }

        memoryCache.Remove(entryKey);
        logger.LogInformation("SMS MFA code consumed successfully for user {UserId}", LogRedaction.RedactId(userId, "uid"));
        return entry;
    }

    private static string EntryKey(Guid userId) => EntryKeyPrefix + userId.ToString("N");

    /// <summary>Normalizes to E.164: optional leading +, then 8–15 digits.</summary>
    internal static string? NormalizePhoneNumber(string phoneNumber)
    {
        var trimmed = phoneNumber.Trim();
        if (trimmed.Length < 9 || trimmed.Length > 16)
        {
            return null;
        }

        var span = trimmed.StartsWith('+') ? trimmed[1..] : trimmed;
        if (span.Length < 8 || span.Length > 15 || !span.All(char.IsDigit))
        {
            return null;
        }

        return "+" + span;
    }

    internal static string MaskPhoneNumber(string phoneNumber) =>
        phoneNumber.Length < 5 ? "****" : $"***-***-{phoneNumber[^4..]}";

    private static SmsMfaChallengeResult Failure(string message) =>
        new() { Success = false, PhoneNumberMasked = string.Empty, ExpiresInSeconds = 0, Message = message };

    private sealed class SmsCodeEntry
    {
        public string? PhoneNumber { get; init; }
        public byte[] CodeDigest { get; init; } = [];
        public DateTime ExpiresAt { get; init; }
        public int FailedAttempts { get; set; }
    }
}
