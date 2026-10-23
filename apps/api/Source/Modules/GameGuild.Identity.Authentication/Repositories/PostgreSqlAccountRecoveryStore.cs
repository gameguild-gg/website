using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     PostgreSQL-backed persistence for delegated account-recovery requests and their
///     approval factors, following the PostgreSqlMfaRecoveryRequestStore pattern
///     (shared IApplicationDbContext).
/// </summary>
public sealed class PostgreSqlAccountRecoveryStore(IApplicationDbContext context) : IAccountRecoveryStore
{
    private DbSet<AccountRecoveryRequest> Requests => context.Set<AccountRecoveryRequest>();
    private DbSet<RecoveryApproval> Approvals => context.Set<RecoveryApproval>();
    private DbSet<TrustedContact> Contacts => context.Set<TrustedContact>();

    public async Task AddWithApprovalsAsync(AccountRecoveryRequest request, IReadOnlyList<RecoveryApproval> approvals, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(approvals);
        await Requests.AddAsync(request, cancellationToken).ConfigureAwait(false);
        if (approvals.Count > 0)
        {
            await Approvals.AddRangeAsync(approvals, cancellationToken).ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<AccountRecoveryRequest?> FindLiveByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        return Requests.SingleOrDefaultAsync(request => request.OwnerUserId == ownerUserId &&
            (request.Status == AccountRecoveryStatus.PendingApprovals ||
             request.Status == AccountRecoveryStatus.ResetUnlocked), cancellationToken);
    }

    public Task<AccountRecoveryRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Requests.SingleOrDefaultAsync(request => request.Id == id, cancellationToken);
    }

    public Task<AccountRecoveryRequest?> FindResetUnlockedByResetTokenHashAsync(string resetTokenHash, CancellationToken cancellationToken)
    {
        RequireDigest(resetTokenHash);
        return Requests.SingleOrDefaultAsync(request => request.ResetTokenHash == resetTokenHash &&
            request.Status == AccountRecoveryStatus.ResetUnlocked, cancellationToken);
    }

    public Task<RecoveryApproval?> FindRedeemableByApprovalCodeHashAsync(string approvalCodeHash, CancellationToken cancellationToken)
    {
        RequireDigest(approvalCodeHash);
        return Approvals.SingleOrDefaultAsync(approval => approval.ApprovalCodeHash == approvalCodeHash &&
            approval.Factor == RecoveryApprovalFactor.TrustedContact && approval.ApprovedAt == null, cancellationToken);
    }

    public async Task<IReadOnlyList<RecoveryApproval>> GetByRequestAsync(Guid recoveryRequestId, CancellationToken cancellationToken)
    {
        var approvals = await Approvals.Where(approval => approval.RecoveryRequestId == recoveryRequestId)
            .OrderBy(approval => approval.IssuedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
        return approvals;
    }

    public async Task<bool> TryAddPasskeyApprovalAsync(RecoveryApproval approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (approval.Factor != RecoveryApprovalFactor.Passkey || approval.WebAuthnCredentialId is not { } credentialId)
        {
            throw new ArgumentException("A passkey approval requires a WebAuthn credential id.", nameof(approval));
        }

        var requestLive = await Requests.AnyAsync(request => request.Id == approval.RecoveryRequestId &&
            (request.Status == AccountRecoveryStatus.PendingApprovals ||
             request.Status == AccountRecoveryStatus.ResetUnlocked), cancellationToken).ConfigureAwait(false);
        if (!requestLive)
        {
            return false;
        }

        // One approval per credential per request: a repeated assertion from the same
        // surviving device is a replay and must not stack toward the threshold.
        var alreadyApproved = await Approvals.AnyAsync(existing => existing.RecoveryRequestId == approval.RecoveryRequestId
            && existing.Factor == RecoveryApprovalFactor.Passkey
            && existing.WebAuthnCredentialId == credentialId, cancellationToken).ConfigureAwait(false);
        if (alreadyApproved)
        {
            return false;
        }

        await Approvals.AddAsync(approval, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> CountDistinctApprovalsAsync(Guid recoveryRequestId, CancellationToken cancellationToken)
    {
        var redeemed = await Approvals.AsNoTracking()
            .Where(approval => approval.RecoveryRequestId == recoveryRequestId && approval.ApprovedAt != null)
            .Select(approval => new { approval.Factor, approval.TrustedContactId, approval.WebAuthnCredentialId })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Fail closed on revoked/deactivated contacts: an approval from a contact that is
        // no longer Confirmed stops counting immediately.
        var redeemedContactIds = redeemed
            .Where(approval => approval.Factor == RecoveryApprovalFactor.TrustedContact)
            .Select(approval => approval.TrustedContactId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var confirmedContactIds = redeemedContactIds.Count == 0
            ? []
            : await Contacts.AsNoTracking()
                .Where(contact => redeemedContactIds.Contains(contact.Id) && contact.Status == TrustedContactStatus.Confirmed)
                .Select(contact => contact.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Every passkey assertion proves the same single fact (the owner still controls a
        // surviving enrolled credential), so passkeys contribute at most one approval.
        var passkeyApproval = redeemed.Any(approval => approval.Factor == RecoveryApprovalFactor.Passkey) ? 1 : 0;
        return confirmedContactIds.Count + passkeyApproval;
    }

    public Task<int> ExpireElapsedAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        return Requests.Where(request =>
                (request.Status == AccountRecoveryStatus.PendingApprovals && request.ApprovalWindowEndsAt < now)
                || (request.Status == AccountRecoveryStatus.ResetUnlocked
                    && request.ResetTokenExpiresAt != null && request.ResetTokenExpiresAt < now))
            .ExecuteUpdateAsync(setters => setters.SetProperty(request => request.Status, AccountRecoveryStatus.Expired),
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void RequireDigest(string tokenHash)
    {
        if (tokenHash is not { Length: 64 } || !tokenHash.All(IsAsciiDigitLowerHex))
        {
            throw new ArgumentException("A lowercase 64-character hex digest is required.", nameof(tokenHash));
        }
    }

    private static bool IsAsciiDigitLowerHex(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'f');
}
