namespace GameGuild.Identity.Authorization.Caching;

/// <summary>
///     A bulk permission-check decision as stored in the L1/L2 authorization caches.
/// </summary>
/// <param name="IsGranted">
///     The evaluated decision at evaluation time.
/// </param>
/// <param name="EffectiveUntilUtc">
///     The earliest grant expiration that participated in the decision, or <c>null</c> when the decision
///     is not time-bound. Time-based grant expiration performs no mutation and advances no security
///     version, so cached copies carry this boundary with them: entries are written with a TTL clamped
///     to it (never above the configured cache TTLs), and every read re-checks it so an entry whose
///     shortest remaining grant lifetime has passed is treated as a miss (fail closed) and re-evaluated
///     from the authoritative grants instead of being served.
/// </param>
public readonly record struct CachedPermissionDecision(bool IsGranted, DateTime? EffectiveUntilUtc);
