namespace GameGuild.Identity.Authorization;

/// <summary>
///     An ACL evaluation outcome together with the point in time at which the outcome can change
///     purely through the passage of time (grant expiration), without any mutation occurring.
/// </summary>
/// <param name="AccessLevel">The effective access level for the evaluated subject and resource.</param>
/// <param name="EarliestEffectiveExpirationUtc">
///     The earliest <c>ExpiresAt</c> among the effective entries that produced the decision, or
///     <c>null</c> when none of them is time-bound. A cached copy of this decision must not be
///     served beyond this timestamp.
/// </param>
public readonly record struct TimeBoundAccessEvaluation(AccessLevel AccessLevel, DateTime? EarliestEffectiveExpirationUtc);

/// <summary>
///     Optional capability that <see cref="IAccessControlListService"/> implementations can expose when
///     they can report when an evaluated access decision may change through time-based grant expiration.
/// </summary>
/// <remarks>
///     <para>
///         Time-based expiration performs no mutation and advances no security version, so the shared
///         version-based cache invalidation cannot react to it. The cached ACL wrapper uses this
///     capability to clamp the cache TTL of each decision to its earliest effective grant expiration,
///     preventing a cached allow from outliving the grant that produced it.
///     </para>
/// </remarks>
public interface ITimeBoundAccessControlListEvaluation
{
    /// <summary>
    ///     Evaluates access for a subject on a resource and reports the earliest effective grant
    ///     expiration that participates in the decision.
    /// </summary>
    Task<TimeBoundAccessEvaluation> EvaluateAccessTimeBoundAsync(
        AclSubject subject,
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default);
}
