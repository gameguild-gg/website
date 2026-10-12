using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Refresh token entity for managing user sessions
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>The preceding token's persisted ID; null for a root or unknown legacy lineage.</summary>
    public Guid? ParentTokenId { get; set; }

    /// <summary>The persisted authentication session; null only when historical binding is unknown.</summary>
    public Guid? SessionId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    [MaxLength(45)]
    public string? RevokedByIp { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(500)]
    public string? ReplacedByToken { get; set; }

    [Required]
    [MaxLength(45)]
    public string CreatedByIp { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsExpired { get => SystemClock.UtcNow >= ExpiresAt; }

    public bool IsActive { get => !IsRevoked && !IsExpired; }
}
