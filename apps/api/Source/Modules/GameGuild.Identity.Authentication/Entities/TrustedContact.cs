namespace GameGuild.Identity.Authentication;

/// <summary>
///     A delegated trusted contact designated by an account owner for account recovery
///     (issue #209). A contact becomes approval-eligible only after redeeming the
///     single-use invite token; the token is stored as a SHA-256 hex digest only.
/// </summary>
public sealed class TrustedContact
{
    public Guid Id { get; set; }

    /// <summary>The account owner who designated this contact; approvals act on their account.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>Tenant of the owning account (isolation and audit).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Normalized (lowercase) contact email; recovery codes are delivered here.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Free-form relation to the owner (for example "spouse"); purely informational.</summary>
    public string? Relation { get; set; }

    public TrustedContactStatus Status { get; set; } = TrustedContactStatus.Invited;

    /// <summary>Hex digest of the outstanding invite token; null once consumed or revoked.</summary>
    public string? InviteTokenHash { get; set; }

    public DateTimeOffset? InviteTokenExpiresAt { get; set; }

    public DateTimeOffset InvitedAt { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>Terminal revocation timestamp; a revoked contact can never approve recovery.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>True while this contact can receive and redeem recovery codes.</summary>
    public bool IsEligible => Status == TrustedContactStatus.Confirmed;
}
