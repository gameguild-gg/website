namespace GameGuild.Identity.Authentication;

/// <summary>Result of issuing a push MFA challenge.</summary>
public sealed class PushMfaChallengeResult
{
    /// <summary>Whether a challenge was created and the push was queued for delivery.</summary>
    public required bool Success { get; init; }

    /// <summary>Opaque challenge identifier (round-trips through the device response).</summary>
    public required string ChallengeId { get; init; }

    /// <summary>Seconds until the challenge expires; on timeout the client falls back to TOTP.</summary>
    public required int ExpiresInSeconds { get; init; }

    /// <summary>Failure message when <see cref="Success"/> is false.</summary>
    public string? Message { get; init; }
}

/// <summary>
///     Push-based MFA v1 (issue #259): an approval challenge is issued, delivered as a push
///     notification through the Notifications infrastructure, and may be approved or denied on
///     the enrolled device within a short TTL. Expired or unanswered challenges fail closed with
///     an explicit fallback-to-TOTP signal so sign-in flows can downgrade to another factor.
///     Challenge state lives in <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
///     so approval and verification can land on different instances.
/// </summary>
public interface IPushMfaService
{
    /// <summary>Registers (or replaces) the mobile device token for push MFA.</summary>
    Task<bool> RegisterDeviceAsync(Guid userId, string deviceToken, CancellationToken cancellationToken = default);

    /// <summary>Issues a new approval challenge and queues its push notification.</summary>
    Task<PushMfaChallengeResult> IssueChallengeAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Records the device's decision for a pending, unexpired challenge.</summary>
    Task<MfaVerificationResult> RespondAsync(Guid challengeId, bool approved, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies the user's outstanding challenge: success only when approved and unexpired.
    ///     Timeout/denial outcomes carry an explicit fallback message (client should use TOTP).
    /// </summary>
    Task<MfaVerificationResult> VerifyAsync(Guid userId, CancellationToken cancellationToken = default);
}
