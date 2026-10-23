namespace GameGuild.Identity.Authentication;

/// <summary>
///     Persistence for delegated trusted contacts (issue #209). Invite tokens are
///     matched by SHA-256 hex digest; active rows are those in Invited or Confirmed.
/// </summary>
public interface ITrustedContactStore
{
    /// <summary>Adds a new contact and persists it.</summary>
    Task AddAsync(TrustedContact contact, CancellationToken cancellationToken);

    /// <summary>Returns the contact by id, if it exists.</summary>
    Task<TrustedContact?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns the owner's active (Invited/Confirmed) contacts.</summary>
    Task<IReadOnlyList<TrustedContact>> GetActiveByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>Counts the owner's active (Invited/Confirmed) contacts.</summary>
    Task<int> CountActiveByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>Returns the owner's active contact for a normalized email, if any.</summary>
    Task<TrustedContact?> FindActiveByOwnerAndEmailAsync(Guid ownerUserId, string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>Returns the Invited contact whose invite-token digest matches, if any.</summary>
    Task<TrustedContact?> FindInvitedByInviteTokenHashAsync(string inviteTokenHash, CancellationToken cancellationToken);

    /// <summary>
    ///     Marks Invited contacts whose invite token expired before the cutoff as Revoked.
    ///     Returns the number of rows transitioned (frees the unique owner+email slot).
    /// </summary>
    Task<int> RevokeExpiredInvitesBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>Persistently stores all tracked changes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
