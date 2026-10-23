namespace GameGuild.Identity.Authentication;

/// <summary>
///     One delegated account-recovery request opened by (or on behalf of) an account
///     owner (issue #209). Collects K-of-N approvals inside a bounded window; reaching
///     the threshold mints a single-use forced-reset token. All tokens are stored as
///     SHA-256 hex digests only.
/// </summary>
public sealed class AccountRecoveryRequest
{
    public Guid Id { get; set; }

    /// <summary>The account being recovered.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>Tenant of the recovered account (isolation and audit).</summary>
    public Guid TenantId { get; set; }

    public AccountRecoveryStatus Status { get; set; } = AccountRecoveryStatus.PendingApprovals;

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Approvals are rejected before this time (anti-impulse cooling-off window).</summary>
    public DateTimeOffset CooldownEndsAt { get; set; }

    /// <summary>Approvals after this time are rejected and the request expires.</summary>
    public DateTimeOffset ApprovalWindowEndsAt { get; set; }

    /// <summary>Approval threshold (K) snapshotted at request time from configuration.</summary>
    public int RequiredApprovals { get; set; }

    /// <summary>Hex digest of the forced-reset token; null until the threshold is reached.</summary>
    public string? ResetTokenHash { get; set; }

    public DateTimeOffset? ResetTokenExpiresAt { get; set; }

    public DateTimeOffset? ResetUnlockedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>True while the request can still collect approvals or be reset.</summary>
    public bool IsLive => Status is AccountRecoveryStatus.PendingApprovals or AccountRecoveryStatus.ResetUnlocked;
}
