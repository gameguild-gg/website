using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request DTO for local sign-in
/// </summary>
public class LocalSignInRequest
{
    public string? Username { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    ///     Optional tenant ID to use for the sign-in. If not provided, will use the first available tenant for the user
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     Device fingerprint for trusted device tracking
    /// </summary>
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    ///     When true, the session is persistent ("remember me"): the refresh token uses the
    ///     configurable persistent lifetime (<c>Jwt:PersistentRefreshTokenExpirationDays</c>)
    ///     instead of the standard one. Null or false keeps the standard lifetime.
    /// </summary>
    public bool? RememberMe { get; set; }

    // Server-only account resolution. These internal properties cannot be bound from JSON or advertised in OpenAPI.
    internal bool CredentialResolutionFailed { get; init; }
    internal Guid? ResolvedUserId { get; init; }

    /// <summary>
    ///     Server-owned timing window already opened by an entry point that resolved account
    ///     candidates before delegating here (polymorphic sign-in). When present, compensation
    ///     is measured from this earlier origin so candidate resolution stays inside the
    ///     compensated window. Internal: cannot be supplied by a request.
    /// </summary>
    internal AuthenticationTimingScope? TimingWindow { get; init; }

    /// <summary>
    ///     Alias for Email to support polymorphic sign-in scenarios
    /// </summary>
    public string EmailOrUsername { get => Email; }
}
