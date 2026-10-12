using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Tenant-scoped bearer token used by an external identity provider to drive the
///     SCIM 2.0 provisioning surface. Mirrors <c>ApiKey</c>: only a SHA-256 hash of the
///     token is stored, the plaintext is returned exactly once at issuance, and rotation
///     keeps the old token valid for an overlap (grace) window.
/// </summary>
[Table("scim_provisioning_tokens")]
[Index(nameof(KeyHash), IsUnique = true)]
[Index(nameof(TenantId))]
[Index(nameof(IsActive))]
[Index(nameof(ExpiresAt))]
public class ScimProvisioningToken : EntityBase
{
    /// <summary>
    ///     Prefix shared by every issued provisioning token.
    /// </summary>
    public const string TokenPrefix = "gg_scim_";

    /// <summary>
    ///     Tenant whose resources this token may provision. The SCIM surface derives the
    ///     tenant exclusively from this value; route or header tenants are never trusted.
    /// </summary>
    [Required]
    public new Guid TenantId { get; set; }

    /// <summary>
    ///     Administrator who issued the token (audit trail).
    /// </summary>
    [Required]
    public Guid CreatedByUserId { get; set; }

    /// <summary>
    ///     Display name for the token (usually the identity provider it belongs to).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     SHA-256 hash of the token (never store plaintext).
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    ///     Constant <c>gg_scim_</c> prefix marker for identification.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string KeyPrefix { get; set; } = TokenPrefix;

    /// <summary>
    ///     Comma-separated SCIM scopes granted to this token.
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Scopes { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     When this token expires (null = never expires).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public long UsageCount { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(200)]
    public string? RevocationReason { get; set; }

    /// <summary>
    ///     Token replaced by this token (set when issued by a rotation).
    /// </summary>
    public Guid? ReplacesTokenId { get; set; }

    /// <summary>
    ///     When this token stops being honored because it was rotated. Until this moment
    ///     the rotated (old) token remains valid alongside its replacement.
    /// </summary>
    public DateTime? RotationGraceEndsAt { get; set; }

    /// <summary>
    ///     Issue a new provisioning token. Returns the entity plus the plaintext token,
    ///     which is shown to the caller exactly once.
    /// </summary>
    public static (ScimProvisioningToken Token, string Plaintext) Create(
        Guid tenantId,
        Guid createdByUserId,
        string name,
        string[] scopes,
        DateTime? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var randomBytes = new byte[48];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "", StringComparison.Ordinal)
            .Replace("/", "", StringComparison.Ordinal)
            .Replace("=", "", StringComparison.Ordinal)
            .Substring(0, 32);

        var plaintext = $"{TokenPrefix}{randomPart}";
        var token = new ScimProvisioningToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedByUserId = createdByUserId,
            Name = name,
            KeyHash = ComputeHash(plaintext),
            KeyPrefix = TokenPrefix,
            Scopes = string.Join(",", scopes),
            IsActive = true,
            ExpiresAt = expiresAt
        };

        return (token, plaintext);
    }

    public bool IsValid()
    {
        if (!IsActive) return false;
        if (RevokedAt.HasValue) return false;
        if (IsRotationGraceExpired()) return false;
        if (ExpiresAt.HasValue && ExpiresAt.Value < SystemClock.UtcNow) return false;
        return true;
    }

    public bool IsRotationGraceExpired()
        => RotationGraceEndsAt.HasValue && RotationGraceEndsAt.Value <= SystemClock.UtcNow;

    public bool HasScope(string scope)
    {
        var scopes = Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return scopes.Contains(scope, StringComparer.OrdinalIgnoreCase) || scopes.Contains("*");
    }

    public string[] GetScopes()
        => Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries);

    public void RecordUsage()
    {
        LastUsedAt = SystemClock.UtcNow;
        UsageCount++;
        Touch();
    }

    public void Revoke(string reason)
    {
        IsActive = false;
        RevokedAt = SystemClock.UtcNow;
        RevocationReason = reason;
        Touch();
    }

    /// <summary>
    ///     Start the rotation overlap window: this (old) token stays valid until
    ///     <paramref name="graceEndsAt"/>, after which <see cref="IsValid"/> fails closed.
    /// </summary>
    public void BeginRotationGrace(DateTime graceEndsAt)
    {
        RotationGraceEndsAt = graceEndsAt;
        Touch();
    }

    /// <summary>
    ///     Lazily record the revocation of a rotated token once its overlap window closed.
    ///     Returns true when the entity transitioned (caller must persist).
    /// </summary>
    public bool FinalizeRotationRevocation()
    {
        if (!IsRotationGraceExpired() || RevokedAt.HasValue || !IsActive)
        {
            return false;
        }

        Revoke("Rotated: superseded by a replacement token after the overlap window closed");
        return true;
    }

    private static string ComputeHash(string plaintext)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        return Convert.ToHexString(sha256.ComputeHash(bytes)).ToLowerInvariant();
    }
}
