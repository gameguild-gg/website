namespace GameGuild.Identity.Authentication;

/// <summary>
///     A persisted guest session snapshot (issue #213). Guest sessions live in their own
///     namespace: the subject is a random guest session id, never a user id, and the record
///     carries no tenant membership. The upgrade columns record the one-time migration of a
///     guest session into an authenticated session.
/// </summary>
public sealed record GuestSessionSnapshot
{
    /// <summary>The guest session id (the token's guest_id claim and audit correlation id).</summary>
    public required Guid GuestSessionId { get; init; }

    /// <summary>When the guest session was issued.</summary>
    public required DateTime IssuedAt { get; init; }

    /// <summary>When the guest session expires; expiry always ends the session.</summary>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>Client IP at issuance (operational metadata only).</summary>
    public string? IpAddress { get; init; }

    /// <summary>User agent at issuance (operational metadata only).</summary>
    public string? UserAgent { get; init; }

    /// <summary>Optional client-supplied device fingerprint at issuance.</summary>
    public string? DeviceFingerprint { get; init; }

    /// <summary>Set once the guest session has been upgraded to an authenticated session.</summary>
    public Guid? UpgradedToUserId { get; init; }

    /// <summary>The authenticated session created by the upgrade.</summary>
    public Guid? UpgradedToSessionId { get; init; }

    /// <summary>When the upgrade happened.</summary>
    public DateTime? UpgradedAt { get; init; }

    /// <summary>Logical expiry against the domain clock (cache TTLs are only a safety net).</summary>
    public bool IsExpired(DateTime now) => ExpiresAt <= now;
}

/// <summary>
///     Guest session persistence (issue #213). Backed by the host distributed cache with a
///     TTL matching the session lifetime — guest sessions are deliberately ephemeral and are
///     not part of the durable user-session table.
/// </summary>
public interface IGuestSessionStore
{
    /// <summary>Persists a new guest session snapshot with its lifetime as the eviction TTL.</summary>
    Task SaveAsync(GuestSessionSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Returns the live snapshot, or null when unknown, expired or unreadable.</summary>
    Task<GuestSessionSnapshot?> FindAsync(Guid guestSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks the session upgraded to an authenticated session exactly once. Returns false
    ///     when the session is unknown, expired, or has already been upgraded (fail closed).
    /// </summary>
    Task<bool> TryMarkUpgradedAsync(
        Guid guestSessionId,
        Guid userId,
        Guid authenticatedSessionId,
        DateTime upgradedAt,
        CancellationToken cancellationToken = default);
}
