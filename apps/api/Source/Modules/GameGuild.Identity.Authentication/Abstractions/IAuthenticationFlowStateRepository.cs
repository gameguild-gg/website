namespace GameGuild.Identity.Authentication;

/// <summary>
///     Persistence for multi-step authentication flow states.
/// </summary>
public interface IAuthenticationFlowStateRepository
{
    /// <summary>
    ///     Persists a new authentication flow state.
    /// </summary>
    Task<AuthenticationFlowStateRecord> CreateAsync(AuthenticationFlowStateRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a flow state by its flow ID.
    /// </summary>
    Task<AuthenticationFlowStateRecord?> GetByFlowIdAsync(Guid flowId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing flow state.
    /// </summary>
    Task<AuthenticationFlowStateRecord> UpdateAsync(AuthenticationFlowStateRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks a flow state as abandoned at the given time. Missing flows are ignored.
    /// </summary>
    Task AbandonAsync(Guid flowId, DateTime abandonedAt, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes flow states that expired before the given instant (cleanup task).
    /// </summary>
    Task DeleteExpiredAsync(DateTime expiredBefore, CancellationToken cancellationToken = default);
}
