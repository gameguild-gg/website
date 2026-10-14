using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Tracks authentication attempts for anomaly detection and security monitoring
/// </summary>
public class AuthenticationAttempt
{
    public Guid Id { get; set; }

    /// <summary>
    ///     Email address used in the login attempt (normalized)
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    ///     User ID if the user exists (null for non-existent users to prevent enumeration)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    ///     IP address of the login attempt
    /// </summary>
    [Required]
    [MaxLength(45)] // IPv6 max length
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    ///     User agent string from the request
    /// </summary>
    [MaxLength(1000)]
    public string? UserAgent { get; set; }

    /// <summary>
    ///     Whether the login attempt was successful
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    ///     Type of failure if unsuccessful
    /// </summary>
    [MaxLength(50)]
    public string? FailureReason { get; set; }

    /// <summary>
    ///     Timestamp of the attempt
    /// </summary>
    public DateTime AttemptedAt { get; set; }

    /// <summary>
    ///     Time taken to process the authentication (for timing analysis)
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    ///     Geographic location derived from IP (city, country)
    /// </summary>
    [MaxLength(200)]
    public string? Location { get; set; }

    /// <summary>
    ///     ISO country code resolved from the attempt IP by the geolocation provider (issue #242);
    ///     null when no provider resolved the address (Unknown).
    /// </summary>
    [MaxLength(8)]
    public string? Country { get; set; }

    /// <summary>
    ///     City resolved from the attempt IP by the geolocation provider (issue #242);
    ///     null when no provider resolved the address (Unknown).
    /// </summary>
    [MaxLength(100)]
    public string? City { get; set; }

    /// <summary>
    ///     Device fingerprint hash for device tracking
    /// </summary>
    [MaxLength(64)]
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    ///     Session ID if available
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    ///     Tenant ID if provided in the request
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     Whether this attempt was flagged as suspicious
    /// </summary>
    public bool IsSuspicious { get; set; }

    /// <summary>
    ///     Risk score assigned to this attempt (0-100)
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    ///     Additional metadata about the attempt (JSON)
    /// </summary>
    [MaxLength(2000)]
    public string? Metadata { get; set; }

    /// <summary>
    ///     Correlation ID for request tracking
    /// </summary>
    [MaxLength(64)]
    public string? CorrelationId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
