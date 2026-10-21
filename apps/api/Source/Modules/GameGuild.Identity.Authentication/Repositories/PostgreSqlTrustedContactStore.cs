using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     PostgreSQL-backed persistence for delegated trusted contacts, following the
///     PostgreSqlMfaRecoveryRequestStore pattern (shared IApplicationDbContext).
/// </summary>
public sealed class PostgreSqlTrustedContactStore(IApplicationDbContext context) : ITrustedContactStore
{
    private DbSet<TrustedContact> Contacts => context.Set<TrustedContact>();

    public async Task AddAsync(TrustedContact contact, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(contact);
        await Contacts.AddAsync(contact, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<TrustedContact?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Contacts.SingleOrDefaultAsync(contact => contact.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TrustedContact>> GetActiveByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var contacts = await QueryActive(ownerUserId).OrderBy(contact => contact.InvitedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return contacts;
    }

    public Task<int> CountActiveByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        return QueryActive(ownerUserId).CountAsync(cancellationToken);
    }

    public Task<TrustedContact?> FindActiveByOwnerAndEmailAsync(Guid ownerUserId, string normalizedEmail, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        return QueryActive(ownerUserId).SingleOrDefaultAsync(contact => contact.Email == normalizedEmail, cancellationToken);
    }

    public Task<TrustedContact?> FindInvitedByInviteTokenHashAsync(string inviteTokenHash, CancellationToken cancellationToken)
    {
        RequireDigest(inviteTokenHash);
        return Contacts.SingleOrDefaultAsync(contact =>
            contact.InviteTokenHash == inviteTokenHash && contact.Status == TrustedContactStatus.Invited, cancellationToken);
    }

    public Task<int> RevokeExpiredInvitesBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        return Contacts.Where(contact => contact.Status == TrustedContactStatus.Invited
                && contact.InviteTokenExpiresAt != null && contact.InviteTokenExpiresAt < cutoff)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(contact => contact.Status, TrustedContactStatus.Revoked)
                .SetProperty(contact => contact.RevokedAt, cutoff), cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private IQueryable<TrustedContact> QueryActive(Guid ownerUserId) =>
        Contacts.Where(contact => contact.OwnerUserId == ownerUserId
            && (contact.Status == TrustedContactStatus.Invited || contact.Status == TrustedContactStatus.Confirmed));

    private static void RequireDigest(string tokenHash)
    {
        if (tokenHash is not { Length: 64 } || !tokenHash.All(IsAsciiDigitLowerHex))
        {
            throw new ArgumentException("A lowercase 64-character hex digest is required.", nameof(tokenHash));
        }
    }

    private static bool IsAsciiDigitLowerHex(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'f');
}
