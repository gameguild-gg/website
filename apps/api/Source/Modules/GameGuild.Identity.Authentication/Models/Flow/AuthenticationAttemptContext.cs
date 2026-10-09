namespace GameGuild.Identity.Authentication;

/// <summary>
///     Context information for an authentication attempt.
/// </summary>
public class AuthenticationAttemptContext
{
    /// <summary>
    ///     User ID if known (for session validation, anomaly detection).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    ///     User identifier (email, username, or wallet address) being authenticated.
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    ///     Authentication method used (Local, OAuth, Web3, etc.).
    /// </summary>
    public string AuthenticationMethod { get; set; } = string.Empty;

    /// <summary>
    ///     IP address of the attempt.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    ///     User agent string.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    ///     Device information.
    /// </summary>
    public DeviceInfo? Device { get; set; }

    /// <summary>
    ///     Alias for Device property to match AuthService usage.
    /// </summary>
    public DeviceInfo? DeviceInfo { get => Device; set => Device = value; }

    /// <summary>
    ///     Location information.
    /// </summary>
    public LocationInfo? Location { get; set; }

    /// <summary>
    ///     Alias for Location property to match AuthService usage.
    /// </summary>
    public LocationInfo? LocationInfo { get => Location; set => Location = value; }

    /// <summary>
    ///     Device fingerprint.
    /// </summary>
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    ///     SHA-256 hex digest of the candidate password submitted during this attempt,
    ///     computed by the caller before verification. Used exclusively for
    ///     breached-password (credential-stuffing) threat-intelligence matching; the
    ///     plaintext password must never be stored on the context, and this digest must
    ///     never be persisted or logged.
    /// </summary>
    public string? PasswordSha256Hex { get; set; }

    /// <summary>
    ///     Tenant context.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     Alias for tenant context to match AuthService usage.
    /// </summary>
    public TenantInfo? TenantInfo { get; set; }

    /// <summary>
    ///     When the attempt occurred.
    /// </summary>
    public DateTime AttemptedAt { get; set; }

    /// <summary>
    ///     Alias for AttemptedAt to match test usage.
    /// </summary>
    public DateTime Timestamp { get => AttemptedAt; set => AttemptedAt = value; }

    /// <summary>
    ///     Time of day (for behavioral analysis).
    /// </summary>
    public TimeSpan TimeOfDay { get => AttemptedAt.TimeOfDay; }

    /// <summary>
    ///     Day of week (for behavioral analysis).
    /// </summary>
    public DayOfWeek DayOfWeek { get => AttemptedAt.DayOfWeek; }

    /// <summary>
    ///     Whether this is a weekend.
    /// </summary>
    public bool IsWeekend { get => DayOfWeek == DayOfWeek.Saturday || DayOfWeek == DayOfWeek.Sunday; }

    /// <summary>
    ///     Additional context metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
