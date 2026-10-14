using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Push MFA challenge lifecycle (issue #259, v1 contract): issue → push → approve/deny within
///     the TTL → verify, with timeout falling back to TOTP. Challenge state is JSON in
///     <see cref="IDistributedCache"/> (Redis path in production, mirroring the token revocation
///     store) keyed by challenge id with a user-index pointer for verification. Only a pending,
///     unexpired challenge accepts a response; every other state fails closed.
/// </summary>
public sealed class PushMfaService(
    ILogger<PushMfaService> logger,
    IDistributedCache cache,
    IUserMfaConfigurationRepository mfaConfigRepository,
    IPublisher publisher) : IPushMfaService
{
    /// <summary>How long a challenge stays decidable before it times out (fallback to TOTP).</summary>
    public static readonly TimeSpan ChallengeTtl = TimeSpan.FromSeconds(120);

    private const string ChallengeKeyPrefix = "auth:push-mfa:challenge:";
    private const string UserKeyPrefix = "auth:push-mfa:user:";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<bool> RegisterDeviceAsync(Guid userId, string deviceToken, CancellationToken cancellationToken = default)
    {
        // Malformed tokens (empty, too short, too long) are a rejection, not an exception: the
        // caller surface treats registration as idempotent-or-refused.
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            return false;
        }

        var trimmed = deviceToken.Trim();
        if (trimmed.Length < 16 || trimmed.Length > 512)
        {
            return false;
        }

        var configuration = await mfaConfigRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            logger.LogWarning("Push device registration attempted with no MFA configuration for user {UserId}", LogRedaction.RedactId(userId, "uid"));
            return false;
        }

        configuration.PushDeviceToken = trimmed;
        configuration.UpdatedAt = SystemClock.UtcNow;
        await mfaConfigRepository.UpdateAsync(configuration, cancellationToken).ConfigureAwait(false);

        MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Push, "device-registered");
        logger.LogInformation("Push MFA device registered for user {UserId}", LogRedaction.RedactId(userId, "uid"));
        return true;
    }

    /// <inheritdoc />
    public async Task<PushMfaChallengeResult> IssueChallengeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var configuration = await mfaConfigRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (configuration?.PushDeviceToken is not { } deviceToken)
        {
            return new PushMfaChallengeResult
            {
                Success = false,
                ChallengeId = string.Empty,
                ExpiresInSeconds = 0,
                Message = "No push MFA device is enrolled for this account."
            };
        }

        var challengeId = Guid.NewGuid();
        var expiresAt = SystemClock.UtcNow.Add(ChallengeTtl);
        var state = new PushMfaChallengeState
        {
            UserId = userId,
            CreatedAt = SystemClock.UtcNow,
            ExpiresAt = expiresAt,
            Status = PushMfaChallengeStatus.Pending
        };

        var options = new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt.AddMinutes(5) };
        await cache.SetStringAsync(
            ChallengeKey(challengeId),
            JsonSerializer.Serialize(state, JsonOptions),
            options,
            cancellationToken).ConfigureAwait(false);
        await cache.SetStringAsync(
            UserKey(userId),
            challengeId.ToString("N"),
            options,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await publisher.Publish(new PushMfaChallengeNotification
            {
                UserId = userId,
                DeviceToken = deviceToken,
                ChallengeId = challengeId
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Push delivery failure must not leave an open challenge; drop it.
            await cache.RemoveAsync(ChallengeKey(challengeId), cancellationToken).ConfigureAwait(false);
            await cache.RemoveAsync(UserKey(userId), cancellationToken).ConfigureAwait(false);
            logger.LogError(exception, "Failed to dispatch push MFA challenge notification for user {UserId}", LogRedaction.RedactId(userId, "uid"));
            throw;
        }

        MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: false, outcome: "challenge-issued");
        logger.LogInformation("Push MFA challenge issued for user {UserId}", LogRedaction.RedactId(userId, "uid"));
        return new PushMfaChallengeResult
        {
            Success = true,
            ChallengeId = challengeId.ToString("N"),
            ExpiresInSeconds = (int)ChallengeTtl.TotalSeconds
        };
    }

    /// <inheritdoc />
    public async Task<MfaVerificationResult> RespondAsync(Guid challengeId, bool approved, CancellationToken cancellationToken = default)
    {
        var (state, key) = await LoadAsync(challengeId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: false, outcome: "respond-unknown");
            return new MfaVerificationResult { Success = false, Message = "Challenge not found or expired" };
        }

        if (state.ExpiresAt < SystemClock.UtcNow)
        {
            MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: false, outcome: "respond-expired");
            return new MfaVerificationResult { Success = false, Message = "Challenge expired" };
        }

        if (state.Status != PushMfaChallengeStatus.Pending)
        {
            MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: false, outcome: "respond-already-decided");
            return new MfaVerificationResult { Success = false, Message = "Challenge was already answered" };
        }

        state.Status = approved ? PushMfaChallengeStatus.Approved : PushMfaChallengeStatus.Denied;
        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(state, JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpiration = state.ExpiresAt.AddMinutes(5) },
            cancellationToken).ConfigureAwait(false);

        MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: false, outcome: approved ? "approved" : "denied");
        logger.LogInformation("Push MFA challenge {Decision} for user {UserId}",
            approved ? "approved" : "denied", LogRedaction.RedactId(state.UserId, "uid"));
        return new MfaVerificationResult
        {
            Success = true,
            Message = approved ? "Challenge approved" : "Challenge denied"
        };
    }

    /// <inheritdoc />
    public async Task<MfaVerificationResult> VerifyAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var pointer = await cache.GetStringAsync(UserKey(userId), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(pointer) || !Guid.TryParseExact(pointer, "N", out var challengeId))
        {
            MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: false, outcome: "no-challenge");
            return new MfaVerificationResult
            {
                Success = false,
                RequiresAdditionalVerification = true,
                Message = "No push challenge is pending. Use your authenticator app (TOTP) instead."
            };
        }

        var (state, key) = await LoadAsync(challengeId, cancellationToken).ConfigureAwait(false);
        if (state?.UserId != userId)
        {
            MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: false, outcome: "challenge-mismatch");
            return new MfaVerificationResult
            {
                Success = false,
                RequiresAdditionalVerification = true,
                Message = "No push challenge is pending. Use your authenticator app (TOTP) instead."
            };
        }

        if (state.ExpiresAt < SystemClock.UtcNow || state.Status == PushMfaChallengeStatus.Pending)
        {
            // Timeout (or still waiting): fail closed with an explicit fallback signal.
            MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: false, outcome: "timeout");
            return new MfaVerificationResult
            {
                Success = false,
                RequiresAdditionalVerification = true,
                Message = state.Status == PushMfaChallengeStatus.Pending
                    ? "Push challenge still pending. Approve it or fall back to your authenticator app (TOTP)."
                    : "Push challenge timed out. Fall back to your authenticator app (TOTP)."
            };
        }

        if (state.Status == PushMfaChallengeStatus.Denied)
        {
            MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: false, outcome: "denied");
            await cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return new MfaVerificationResult { Success = false, Message = "Push challenge was denied" };
        }

        // Approved and unexpired: consume the challenge so it cannot be replayed.
        await cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        await cache.RemoveAsync(UserKey(userId), cancellationToken).ConfigureAwait(false);
        MfaEnrollmentMetrics.RecordVerification(MfaMethod.Push, succeeded: true);
        logger.LogInformation("Push MFA verification succeeded for user {UserId}", LogRedaction.RedactId(userId, "uid"));
        return new MfaVerificationResult { Success = true, Message = "Push challenge verified" };
    }

    private async Task<(PushMfaChallengeState? State, string Key)> LoadAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        var key = ChallengeKey(challengeId);
        var payload = await cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return (null, key);
        }

        try
        {
            return (JsonSerializer.Deserialize<PushMfaChallengeState>(payload, JsonOptions), key);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Unreadable push MFA challenge payload for {ChallengeId}", challengeId);
            return (null, key);
        }
    }

    private static string ChallengeKey(Guid challengeId) => ChallengeKeyPrefix + challengeId.ToString("N");

    private static string UserKey(Guid userId) => UserKeyPrefix + userId.ToString("N");

    private enum PushMfaChallengeStatus
    {
        Pending = 0,
        Approved = 1,
        Denied = 2
    }

    private sealed class PushMfaChallengeState
    {
        public Guid UserId { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime ExpiresAt { get; init; }
        public PushMfaChallengeStatus Status { get; set; }
    }
}
