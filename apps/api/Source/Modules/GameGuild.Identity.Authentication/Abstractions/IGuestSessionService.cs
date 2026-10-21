namespace GameGuild.Identity.Authentication;

/// <summary>Why a guest session issuance or upgrade was refused (fail closed).</summary>
public enum GuestSessionFailureReason
{
    /// <summary>Guest access is disabled by configuration.</summary>
    GuestAccessDisabled,

    /// <summary>The presented credential is not a valid guest token.</summary>
    InvalidGuestToken,

    /// <summary>The guest session is unknown, expired or already upgraded.</summary>
    GuestSessionUnavailable,

    /// <summary>The upgrade caller did not present an authenticated session.</summary>
    AuthenticatedSessionRequired
}

/// <summary>Outcome of issuing a guest session.</summary>
public sealed record GuestSessionResult
{
    public bool Success { get; init; }

    public GuestSessionFailureReason? FailureReason { get; init; }

    /// <summary>The guest session id — its own namespace, never a user id.</summary>
    public Guid GuestSessionId { get; init; }

    /// <summary>The short-lived guest bearer token.</summary>
    public string AccessToken { get; init; } = string.Empty;

    public DateTime ExpiresAt { get; init; }

    /// <summary>The enumerated permissions this guest session holds.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}

/// <summary>Context captured when a guest session is issued.</summary>
public sealed record GuestSessionRequest
{
    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? DeviceFingerprint { get; init; }
}

/// <summary>Outcome of upgrading a guest session to an authenticated session.</summary>
public sealed record GuestSessionUpgradeResult
{
    public bool Success { get; init; }

    public GuestSessionFailureReason? FailureReason { get; init; }

    /// <summary>The upgraded guest session id (present on success).</summary>
    public Guid GuestSessionId { get; init; }
}

/// <summary>
///     Guest session lifecycle (issue #213): issuance, validation and one-time upgrade to an
///     authenticated session. Every issuance, rejection and upgrade is forwarded to the
///     security-event pipeline through <see cref="IAuthenticationAuditEventSink"/>.
/// </summary>
public interface IGuestSessionService
{
    /// <summary>
    ///     Issues a short-lived anonymous guest session. Fails closed (audited) when guest
    ///     access is disabled.
    /// </summary>
    Task<GuestSessionResult> IssueAsync(GuestSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validates a presented guest credential and returns the live snapshot, or null when
    ///     the token is invalid or the session is unknown/expired/already upgraded.
    /// </summary>
    Task<GuestSessionSnapshot?> ValidateAsync(string? guestToken, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Migrates a guest session into a freshly issued authenticated session (session
    ///     continuity at sign-in/sign-up). The guest session is marked upgraded exactly once;
    ///     a replayed or invalid guest credential is rejected and audited without ever
    ///     mapping the guest onto the user identity.
    /// </summary>
    Task<GuestSessionUpgradeResult> UpgradeAsync(
        string? guestToken,
        Guid userId,
        Guid authenticatedSessionId,
        CancellationToken cancellationToken = default);
}
