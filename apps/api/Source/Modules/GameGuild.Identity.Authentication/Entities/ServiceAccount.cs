using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Represents a service account for machine-to-machine (M2M) authentication.
///     Service accounts use the OAuth2 client_credentials grant type.
/// </summary>
public class ServiceAccount
{
    /// <summary>
    ///     Unique identifier for the service account.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     The client ID used for authentication (public identifier).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    ///     The hashed client secret (never store plaintext).
    ///     Uses the same hashing as refresh tokens (SHA-256).
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ClientSecretHash { get; set; } = string.Empty;

    /// <summary>
    ///     Human-readable name for the service account.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Optional description of what this service account is used for.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    ///     The tenant this service account belongs to (null for global/system service accounts).
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     Comma-separated list of OAuth scopes granted to this service account.
    ///     Example: "read:users,write:projects,admin:tenants"
    /// </summary>
    [MaxLength(2000)]
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    ///     Whether this service account is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Optional expiration date for the service account.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    ///     When the service account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>
    ///     Who created this service account (user ID or "system").
    /// </summary>
    [MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    ///     When the service account was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>
    ///     When the client secret was last rotated.
    /// </summary>
    public DateTime? SecretRotatedAt { get; set; }

    /// <summary>
    ///     Number of times the secret has been rotated.
    /// </summary>
    public int SecretRotationCount { get; set; }

    /// <summary>
    ///     Last successful authentication timestamp.
    /// </summary>
    public DateTime? LastAuthenticatedAt { get; set; }

    /// <summary>
    ///     IP address of the last successful authentication.
    /// </summary>
    [MaxLength(45)]
    public string? LastAuthenticatedFromIp { get; set; }

    /// <summary>
    ///     Total number of successful authentications.
    /// </summary>
    public long AuthenticationCount { get; set; }

    /// <summary>
    ///     Number of failed authentication attempts since last success.
    /// </summary>
    public int FailedAuthenticationAttempts { get; set; }

    /// <summary>
    ///     Whether the service account is locked due to too many failed attempts.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    ///     When the service account was locked (if applicable).
    /// </summary>
    public DateTime? LockedAt { get; set; }

    /// <summary>
    ///     Allowed IP addresses (comma-separated CIDR notation). Empty means all IPs allowed.
    /// </summary>
    [MaxLength(2000)]
    public string? AllowedIpAddresses { get; set; }

    /// <summary>
    ///     SHA-1 thumbprint (uppercase hex, no separators) of the X.509 client certificate
    ///     bound to this service account. Null when the account authenticates by secret only.
    /// </summary>
    [MaxLength(40)]
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    ///     SHA-256 hex hash of the bound certificate's Subject Public Key Info (key pin).
    ///     Null when only the full-certificate thumbprint is bound.
    /// </summary>
    [MaxLength(64)]
    public string? CertificateSpkiSha256 { get; set; }

    /// <summary>
    ///     Gets whether the service account can authenticate.
    /// </summary>
    public bool CanAuthenticate
    {
        get
        {
            if (!IsActive || IsLocked) { return false; }

            return !ExpiresAt.HasValue || SystemClock.UtcNow < ExpiresAt.Value;
        }
    }

    /// <summary>
    ///     Gets the scopes as a set.
    /// </summary>
    public IReadOnlySet<string> GetScopesSet()
    {
        if (string.IsNullOrWhiteSpace(Scopes))
            return new HashSet<string>();

        return Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Records a successful authentication.
    /// </summary>
    public void RecordSuccessfulAuthentication(string? ipAddress)
    {
        LastAuthenticatedAt = SystemClock.UtcNow;
        LastAuthenticatedFromIp = ipAddress;
        AuthenticationCount++;
        FailedAuthenticationAttempts = 0;
    }

    /// <summary>
    ///     Records a failed authentication attempt.
    /// </summary>
    /// <param name="lockThreshold">Number of failed attempts before locking.</param>
    public void RecordFailedAuthentication(int lockThreshold = 10)
    {
        FailedAuthenticationAttempts++;
        if (FailedAuthenticationAttempts >= lockThreshold)
        {
            IsLocked = true;
            LockedAt = SystemClock.UtcNow;
        }
    }

    /// <summary>
    ///     Unlocks the service account.
    /// </summary>
    public void Unlock()
    {
        IsLocked = false;
        LockedAt = null;
        FailedAuthenticationAttempts = 0;
    }

    /// <summary>
    ///     Locks the service account with a reason.
    /// </summary>
    /// <param name="reason">The reason for locking (logged for audit purposes)</param>
    public void Lock(string reason)
    {
        IsLocked = true;
        LockedAt = SystemClock.UtcNow;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Rotates the client secret.
    /// </summary>
    public void RotateSecret(string newSecretHash)
    {
        ClientSecretHash = newSecretHash;
        SecretRotatedAt = SystemClock.UtcNow;
        SecretRotationCount++;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Binds an X.509 client certificate (thumbprint and optional SPKI key pin) to this
    ///     service account, allowing certificate authentication in lieu of the client secret.
    /// </summary>
    public void BindCertificate(string thumbprint, string? spkiSha256 = null)
    {
        CertificateThumbprint = thumbprint;
        CertificateSpkiSha256 = spkiSha256;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Removes the bound X.509 client certificate; the account authenticates by secret only afterwards.
    /// </summary>
    public void UnbindCertificate()
    {
        CertificateThumbprint = null;
        CertificateSpkiSha256 = null;
        UpdatedAt = SystemClock.UtcNow;
    }
}
