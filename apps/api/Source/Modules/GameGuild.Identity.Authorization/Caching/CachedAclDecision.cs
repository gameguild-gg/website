namespace GameGuild.Identity.Authorization.Caching;

/// <summary>
///     An ACL access decision as stored in the L1/L2 authorization caches.
/// </summary>
/// <param name="AccessLevel">The effective access level at evaluation time.</param>
/// <param name="EffectiveUntilUtc">
///     The earliest effective grant expiration that participated in the decision, or <c>null</c> when
///     the decision is not time-bound. Time-based grant expiration performs no mutation and advances no
///     security version, so cached copies carry this boundary with them: entries are written with a TTL
///     clamped to it, and every serve re-checks it so an expired decision is never returned (this also
///     covers entries promoted from L2 to L1 and entries written before this boundary was tracked).
/// </param>
public readonly record struct CachedAclDecision(AccessLevel AccessLevel, DateTime? EffectiveUntilUtc);
