using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     User session with device information for session management
/// </summary>
public class UserSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    ///     JWT refresh token
    /// </summary>
    [Required]
    [MaxLength(512)]
    [AuditSensitive]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    ///     Access token hash (for tracking, not storage)
    /// </summary>
    [MaxLength(128)]
    [AuditSensitive]
    public string? AccessTokenHash { get; set; }

    /// <summary>
    ///     IP address when session was created
    /// </summary>
    [Required]
    [MaxLength(45)] // IPv6 max length
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    ///     User agent string
    /// </summary>
    [MaxLength(1000)]
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    ///     Device fingerprint for identification
    /// </summary>
    [MaxLength(64)]
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    ///     Parsed device information (JSON)
    /// </summary>
    [MaxLength(2000)]
    public string? DeviceInfo { get; set; }

    /// <summary>
    ///     Geographic location (JSON)
    /// </summary>
    [MaxLength(500)]
    public string? Location { get; set; }

    /// <summary>
    ///     Session expiration time
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    ///     Last time this session was used
    /// </summary>
    public DateTime LastUsedAt { get; set; }

    /// <summary>
    ///     Whether session is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Reason for session termination
    /// </summary>
    [MaxLength(100)]
    public string? TerminationReason { get; set; }

    /// <summary>
    ///     When session was terminated
    /// </summary>
    public DateTime? TerminatedAt { get; set; }

    /// <summary>
    ///     Whether this is a trusted device
    /// </summary>
    public bool IsTrustedDevice { get; set; }

    /// <summary>
    ///     When device was marked as trusted
    /// </summary>
    public DateTime? TrustedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsExpired { get => SystemClock.UtcNow >= ExpiresAt; }

    public bool IsValid { get => IsActive && !IsExpired; }
}
