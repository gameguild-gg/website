using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     API key for programmatic access with scoped permissions
/// </summary>
[Table("api_keys")]
[Index(nameof(KeyHash), IsUnique = true)]
[Index(nameof(UserId))]
[Index(nameof(TenantId))]
[Index(nameof(IsActive))]
[Index(nameof(ExpiresAt))]
public class ApiKey : EntityBase
{
    /// <summary>
    ///     User who owns this API key
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    ///     Display name for the key
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     SHA-256 hash of the API key (never store plaintext)
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    ///     Prefix of the key for identification (first 8 chars, e.g., "gg_live_")
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    ///     Scopes granted to this key (comma-separated)
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    ///     Whether this key is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     When this key expires (null = never expires)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    ///     Last time this key was used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    ///     Number of times this key has been used
    /// </summary>
    public long UsageCount { get; set; }

    /// <summary>
    ///     IP address restriction (null = no restriction)
    /// </summary>
    [MaxLength(100)]
    public string? IpWhitelist { get; set; }

    /// <summary>
    ///     When this key was revoked (if applicable)
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    ///     Reason for revocation
    /// </summary>
    [MaxLength(200)]
    public string? RevocationReason { get; set; }

    /// <summary>
    ///     Key that this key replaces (set when this key was issued by a rotation)
    /// </summary>
    public Guid? ReplacesKeyId { get; set; }

    /// <summary>
    ///     When this key stops being honored because it was rotated (overlap/grace window end).
    ///     Until this moment the rotated (old) key remains valid alongside its replacement.
    /// </summary>
    public DateTime? RotationGraceEndsAt { get; set; }

    /// <summary>
    ///     Create a new API key
    /// </summary>
    public static (ApiKey key, string plaintext) Create(
        Guid userId,
        Guid tenantId,
        string name,
        string[] scopes,
        DateTime? expiresAt = null,
        string? ipWhitelist = null)
    {
        // Generate secure random key: gg_live_<32 random chars>
        // Use 48 bytes (64 Base64 chars) to guarantee at least 32 alphanumeric chars after stripping
        var randomBytes = new byte[48];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 32);

        var plaintext = $"gg_live_{randomPart}";
        var keyHash = ComputeHash(plaintext);

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Name = name,
            KeyHash = keyHash,
            KeyPrefix = "gg_live_",
            Scopes = string.Join(",", scopes),
            IsActive = true,
            ExpiresAt = expiresAt,
            IpWhitelist = ipWhitelist
        };

        return (apiKey, plaintext);
    }

    /// <summary>
    ///     Validate a plaintext key against this entity
    /// </summary>
    public bool ValidateKey(string plaintext)
    {
        var hash = ComputeHash(plaintext);
        return hash == KeyHash;
    }

    /// <summary>
    ///     Check if key is currently valid
    /// </summary>
    public bool IsValid()
    {
        if (!IsActive)
        {
            return false;
        }

        if (RevokedAt.HasValue)
        {
            return false;
        }

        if (IsRotationGraceExpired())
        {
            return false;
        }

        if (ExpiresAt.HasValue && ExpiresAt.Value < SystemClock.UtcNow)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Whether the rotation overlap window for this key has already closed
    /// </summary>
    public bool IsRotationGraceExpired()
        => RotationGraceEndsAt.HasValue && RotationGraceEndsAt.Value <= SystemClock.UtcNow;

    /// <summary>
    ///     Check if key has a specific scope
    /// </summary>
    public bool HasScope(string scope)
    {
        var scopes = Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return scopes.Contains(scope, StringComparer.OrdinalIgnoreCase) ||
               scopes.Contains("*"); // Wildcard scope
    }

    /// <summary>
    ///     Record usage of this key
    /// </summary>
    public void RecordUsage()
    {
        LastUsedAt = SystemClock.UtcNow;
        UsageCount++;
        Touch();
    }

    /// <summary>
    ///     Revoke this key
    /// </summary>
    public void Revoke(string reason)
    {
        IsActive = false;
        RevokedAt = SystemClock.UtcNow;
        RevocationReason = reason;
        Touch();
    }

    /// <summary>
    ///     Start the rotation overlap window: this (old) key stays valid until
    ///     <paramref name="graceEndsAt"/>, after which <see cref="IsValid"/> fails closed.
    /// </summary>
    public void BeginRotationGrace(DateTime graceEndsAt)
    {
        RotationGraceEndsAt = graceEndsAt;
        Touch();
    }

    /// <summary>
    ///     Lazily record the revocation of a rotated key once its overlap window has closed.
    ///     Returns true when the entity transitioned (caller must persist), false when
    ///     there is nothing to finalize.
    /// </summary>
    public bool FinalizeRotationRevocation()
    {
        if (!IsRotationGraceExpired() || RevokedAt.HasValue || !IsActive)
        {
            return false;
        }

        Revoke("Rotated: superseded by a replacement key after the overlap window closed");
        return true;
    }

    /// <summary>
    ///     Compute SHA-256 hash of plaintext key
    /// </summary>
    private static string ComputeHash(string plaintext)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    ///     Get list of scopes as array
    /// </summary>
    public string[] GetScopes()
    {
        return Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries);
    }
}
