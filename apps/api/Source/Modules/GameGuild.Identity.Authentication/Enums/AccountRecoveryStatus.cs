namespace GameGuild.Identity.Authentication;

/// <summary>Lifecycle of a delegated account-recovery request (issue #209).</summary>
public enum AccountRecoveryStatus
{
    /// <summary>Opened; collecting K-of-N approvals inside the approval window.</summary>
    PendingApprovals = 0,

    /// <summary>The approval threshold was reached; the single-use reset token is live.</summary>
    ResetUnlocked = 1,

    /// <summary>The reset token was consumed and the forced credential reset completed.</summary>
    Completed = 2,

    /// <summary>The owner cancelled the request during the cooling-off window.</summary>
    Cancelled = 3,

    /// <summary>The approval window or reset token elapsed; terminal.</summary>
    Expired = 4
}
