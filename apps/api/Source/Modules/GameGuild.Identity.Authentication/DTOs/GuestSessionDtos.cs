namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request body for guest session issuance. Carries no credentials: the caller only
///     proves possession of nothing, so issuance is strictly rate limited per IP and audited.
/// </summary>
public sealed class GuestSessionIssueRequest
{
    /// <summary>Optional client device fingerprint, stored as operational metadata.</summary>
    public string? DeviceFingerprint { get; set; }
}

/// <summary>
///     Response for a successfully issued guest session. The token is a short-lived bearer
///     credential valid ONLY on the guest scheme; there is no refresh token by design.
/// </summary>
public sealed class GuestSessionResponse
{
    /// <summary>The guest session id — its own namespace, never a user id.</summary>
    public Guid GuestSessionId { get; set; }

    /// <summary>Always <c>Bearer</c>.</summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>The short-lived guest access token (guest audience).</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>When the guest session expires.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Lifetime in seconds.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>The enumerated permissions this guest session holds (the full allowlist).</summary>
    public IReadOnlyList<string> Permissions { get; set; } = [];
}

/// <summary>
///     Introspection of the caller's current guest session (served on the guest scheme).
/// </summary>
public sealed class GuestSessionStatus
{
    /// <summary>The guest session id.</summary>
    public Guid GuestSessionId { get; set; }

    /// <summary>The guest role value (own namespace).</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The enumerated permissions held.</summary>
    public IReadOnlyList<string> Permissions { get; set; } = [];
}
