namespace GameGuild.Identity.Authentication;

/// <summary>
///     Persistence for delegated account-recovery requests and their approval factors
///     (issue #209). Tokens and codes are matched by SHA-256 hex digest; live requests
///     are those in PendingApprovals or ResetUnlocked.
/// </summary>
public interface IAccountRecoveryStore
{
    /// <summary>Atomically persists a new recovery request together with its approval factors.</summary>
    Task AddWithApprovalsAsync(AccountRecoveryRequest request, IReadOnlyList<RecoveryApproval> approvals, CancellationToken cancellationToken);

    /// <summary>Returns the owner's live (PendingApprovals/ResetUnlocked) request, if any.</summary>
    Task<AccountRecoveryRequest?> FindLiveByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>Returns the request by id, if it exists.</summary>
    Task<AccountRecoveryRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns the ResetUnlocked request whose reset-token digest matches, if any.</summary>
    Task<AccountRecoveryRequest?> FindResetUnlockedByResetTokenHashAsync(string resetTokenHash, CancellationToken cancellationToken);

    /// <summary>Returns the unredeemed TrustedContact approval whose code digest matches, if any.</summary>
    Task<RecoveryApproval?> FindRedeemableByApprovalCodeHashAsync(string approvalCodeHash, CancellationToken cancellationToken);

    /// <summary>Returns every approval factor issued for a request (audit/status view).</summary>
    Task<IReadOnlyList<RecoveryApproval>> GetByRequestAsync(Guid recoveryRequestId, CancellationToken cancellationToken);

    /// <summary>
    ///     Adds one passkey approval; returns false when this credential already approved
    ///     the request (replay) or the request is no longer collecting approvals.
    /// </summary>
    Task<bool> TryAddPasskeyApprovalAsync(RecoveryApproval approval, CancellationToken cancellationToken);

    /// <summary>
    ///     Counts distinct approvals toward the threshold: confirmed-contact redemptions
    ///     (one per contact) plus at most one for passkey assertions (any number of
    ///     surviving credentials proves the same single fact, so they never stack).
    /// </summary>
    Task<int> CountDistinctApprovalsAsync(Guid recoveryRequestId, CancellationToken cancellationToken);

    /// <summary>
    ///     Marks elapsed requests terminal: PendingApprovals past the approval window and
    ///     ResetUnlocked past the reset-token expiry. Returns the number of rows transitioned.
    /// </summary>
    Task<int> ExpireElapsedAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Persistently stores all tracked changes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
