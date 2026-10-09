namespace GameGuild.Identity.Authentication;

/// <summary>
///     Session policy configuration.
/// </summary>
public abstract class SessionPolicy
{
    /// <summary>
    ///     Access token expiration in minutes.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    /// <summary>
    ///     Refresh token expiration in days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 30;

    /// <summary>
    ///     Refresh token expiration in days for persistent ("remember me") sessions.
    ///     Mirrors <c>Jwt:PersistentRefreshTokenExpirationDays</c> for tenant-scoped
    ///     session policy consumers.
    /// </summary>
    public int PersistentRefreshTokenExpirationDays { get; set; } = 30;

    /// <summary>
    ///     Whether to allow multiple concurrent sessions.
    /// </summary>
    public bool AllowMultipleSessions { get; set; } = true;

    /// <summary>
    ///     Maximum number of concurrent sessions per user.
    /// </summary>
    public int? MaxConcurrentSessions { get; set; }

    /// <summary>
    ///     Session idle timeout in minutes.
    /// </summary>
    public int? IdleTimeoutMinutes { get; set; }

    /// <summary>
    ///     Whether to extend session on activity.
    /// </summary>
    public bool ExtendOnActivity { get; set; } = true;

    /// <summary>
    ///     Whether to require session binding to IP address.
    /// </summary>
    public bool BindToIpAddress { get; set; }

    /// <summary>
    ///     Whether to require session binding to device fingerprint.
    /// </summary>
    public bool BindToDeviceFingerprint { get; set; } = true;
}
