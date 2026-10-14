namespace GameGuild.Identity.Authentication;

/// <summary>
///     Persistence for lost-MFA-device recovery requests. Tokens are matched by their
///     SHA-256 hex digest; live rows are those in Pending or AwaitingConfirmation.
/// </summary>
public interface IMfaRecoveryRequestStore
{
    /// <summary>Adds a new recovery request and persists it.</summary>
    Task AddAsync(MfaRecoveryRequest request, CancellationToken cancellationToken);

    /// <summary>Returns the user's live (Pending/AwaitingConfirmation) request, if any.</summary>
    Task<MfaRecoveryRequest?> FindLiveByUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Returns the live request whose request-token digest matches, if any.</summary>
    Task<MfaRecoveryRequest?> FindLiveByRequestTokenHashAsync(string requestTokenHash, CancellationToken cancellationToken);

    /// <summary>Returns the AwaitingConfirmation request whose confirmation-token digest matches, if any.</summary>
    Task<MfaRecoveryRequest?> FindAwaitingByConfirmationTokenHashAsync(string confirmationTokenHash, CancellationToken cancellationToken);

    /// <summary>
    ///     Marks live requests older than the cutoff (from RequestedAt) as Expired.
    ///     Returns the number of rows transitioned.
    /// </summary>
    Task<int> ExpireOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>
    ///     Marks the user's live (Pending/AwaitingConfirmation) request as Cancelled.
    ///     Returns the cancelled request, or null when no live request exists.
    /// </summary>
    Task<MfaRecoveryRequest?> CancelLiveByUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Persistently stores all tracked changes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);}
