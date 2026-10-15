namespace GameGuild.Identity.Authentication;

/// <summary>Result of starting an SMS MFA enrollment or challenge.</summary>
public sealed class SmsMfaChallengeResult
{
    /// <summary>Whether a code was generated and queued for delivery.</summary>
    public required bool Success { get; init; }

    /// <summary>Masked destination phone (last four digits) for user confirmation.</summary>
    public required string PhoneNumberMasked { get; init; }

    /// <summary>Seconds until the issued code expires.</summary>
    public required int ExpiresInSeconds { get; init; }

    /// <summary>Failure message when <see cref="Success"/> is false.</summary>
    public string? Message { get; init; }
}

/// <summary>
///     Issues and verifies one-time SMS MFA codes (issue #244). Codes are six cryptographically
///     random digits, live for ten minutes, accept at most five verification attempts, are
///     single-use, throttled to one request per minute per user, and stored only as SHA-256
///     digests. Delivery rides the Notifications infrastructure (SMS channel).
/// </summary>
public interface ISmsMfaService
{
    /// <summary>Starts enrollment: sends a verification code to the given phone for the authenticated user.</summary>
    Task<SmsMfaChallengeResult> StartEnrollmentAsync(Guid userId, string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>Completes enrollment by verifying the code sent to the pending phone number.</summary>
    Task<MfaVerificationResult> CompleteEnrollmentAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    /// <summary>Sends a fresh sign-in challenge to the user's enrolled phone (throttled).</summary>
    Task<SmsMfaChallengeResult> SendChallengeAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Verifies a submitted SMS code (constant-time, single-use).</summary>
    Task<bool> VerifyCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default);
}
