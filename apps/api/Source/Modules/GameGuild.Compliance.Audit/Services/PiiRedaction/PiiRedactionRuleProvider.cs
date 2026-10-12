using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Thrown when the redaction rule set cannot be loaded and no previously loaded snapshot exists to
/// fall back on. Callers must treat this as a fail-closed signal and redact protectable fields
/// wholesale instead of persisting potentially unredacted PII.
/// </summary>
public sealed class PiiRedactionUnavailableException : Exception
{
    public PiiRedactionUnavailableException(Exception inner)
        : base("The PII redaction rule set is unavailable and no cached snapshot exists; the pipeline must fail closed.", inner)
    {
    }
}

/// <summary>
/// A loaded, ordered rule set: tenant rules first (field-name before global, ascending priority),
/// then platform-global rules in the same order. The order is the application order consumed by
/// <see cref="PiiRedactionEngine.ApplyToValue"/>.
/// </summary>
public sealed record PiiRuleSet
{
    public required IReadOnlyList<CompiledPiiRule> Rules { get; init; }

    public required IReadOnlyList<Guid> RuleIds { get; init; }

    public required DateTime LoadedAt { get; init; }
}

/// <summary>
/// Loads and caches compiled PII redaction rules. Singleton: snapshots are cached per scope
/// (tenant or global) with a short TTL, refreshed in the background by
/// <see cref="PiiRedactionRuleRefreshService"/>, and served stale when a refresh fails — the pipeline
/// only fails closed when no snapshot was ever loaded for the scope.
/// </summary>
public interface IPiiRedactionRuleProvider
{
    /// <summary>Rule set for a tenant: its own rules plus the platform-global rules.</summary>
    Task<PiiRuleSet> GetTenantRuleSetAsync(Guid? tenantId, CancellationToken cancellationToken);

    /// <summary>Platform-global rules only; used by the structured-log sink where tenant context is absent.</summary>
    Task<PiiRuleSet> GetGlobalRuleSetAsync(CancellationToken cancellationToken);

    /// <summary>Synchronously cached global rule set, or null when nothing has been loaded yet. Never throws.</summary>
    PiiRuleSet? TryGetGlobalSnapshot();

    /// <summary>Cached state summary for the administration surface.</summary>
    PiiRedactionCacheStatus GetCacheStatus();

    /// <summary>Drops cached snapshots so the next load observes rule mutations immediately.</summary>
    void Invalidate(Guid? tenantId);
}

public sealed record PiiRedactionCacheStatus(
    bool GlobalSnapshotLoaded,
    DateTime? GlobalLoadedAt,
    int CachedTenantScopes,
    int GlobalRuleCount,
    DateTime? LastRefreshAttemptAt,
    string? LastRefreshError);

/// <summary>Default provider backed by the audit DbContext through a scope factory.</summary>
public sealed class PiiRedactionRuleProvider(
    IServiceScopeFactory scopeFactory,
    ILogger<PiiRedactionRuleProvider> logger) : IPiiRedactionRuleProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    private readonly SemaphoreSlim _globalLoadGate = new(1, 1);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _tenantLoadGates = new(StringComparer.Ordinal);

    private DateTime? _lastRefreshAttemptAt;

    private string? _lastRefreshError;

    public async Task<PiiRuleSet> GetTenantRuleSetAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var scopeKey = ScopeKey(tenantId);
        if (_cache.TryGetValue(scopeKey, out var entry) && !entry.IsExpired)
        {
            return entry.RuleSet;
        }

        var gate = _tenantLoadGates.GetOrAdd(scopeKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(scopeKey, out entry) && !entry.IsExpired)
            {
                return entry.RuleSet;
            }

            var ruleSet = await LoadAsync(tenantId, includeGlobal: true, cancellationToken).ConfigureAwait(false);
            _cache[scopeKey] = new CacheEntry(ruleSet);
            return ruleSet;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<PiiRuleSet> GetGlobalRuleSetAsync(CancellationToken cancellationToken)
    {
        const string globalKey = "global";
        if (_cache.TryGetValue(globalKey, out var entry) && !entry.IsExpired)
        {
            return entry.RuleSet;
        }

        await _globalLoadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(globalKey, out entry) && !entry.IsExpired)
            {
                return entry.RuleSet;
            }

            var ruleSet = await LoadAsync(null, includeGlobal: false, cancellationToken).ConfigureAwait(false);
            _cache[globalKey] = new CacheEntry(ruleSet);
            return ruleSet;
        }
        finally
        {
            _globalLoadGate.Release();
        }
    }

    public PiiRuleSet? TryGetGlobalSnapshot() =>
        _cache.TryGetValue("global", out var entry) ? entry.RuleSet : null;

    public PiiRedactionCacheStatus GetCacheStatus()
    {
        var globalLoaded = _cache.TryGetValue("global", out var entry);
        return new PiiRedactionCacheStatus(
            globalLoaded,
            globalLoaded ? entry!.RuleSet.LoadedAt : null,
            _cache.Count,
            globalLoaded ? entry!.RuleSet.Rules.Count : 0,
            _lastRefreshAttemptAt,
            _lastRefreshError);
    }

    /// <summary>
    /// Drops cached snapshots so the next load observes rule mutations immediately. Tenant snapshots
    /// embed platform-global rules, so any mutation clears every scope.
    /// </summary>
    public void Invalidate(Guid? tenantId)
    {
        _cache.Clear();
    }

    private async Task<PiiRuleSet> LoadAsync(Guid? tenantId, bool includeGlobal, CancellationToken cancellationToken)
    {
        _lastRefreshAttemptAt = SystemClock.UtcNow;
        try
        {
            List<PiiRedactionRule> persisted;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var tenant = tenantId;
                if (includeGlobal && tenant is not null)
                {
                    persisted = await context.Set<PiiRedactionRule>()
                        .AsNoTracking()
                        .Where(rule => rule.IsEnabled && (rule.TenantId == tenant || rule.TenantId == null))
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    persisted = await context.Set<PiiRedactionRule>()
                        .AsNoTracking()
                        .Where(rule => rule.IsEnabled && rule.TenantId == null)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            _lastRefreshError = null;
            return Compile(persisted);
        }
        catch (Exception exception)
        {
            _lastRefreshError = exception.Message;

            // Stale-if-error: keep serving the previous snapshot; only fail closed when none exists.
            if (_cache.TryGetValue(ScopeKey(tenantId), out var stale))
            {
                logger.LogWarning(
                    exception,
                    "PII redaction rule refresh failed for scope {Scope}; serving stale snapshot loaded at {LoadedAt}",
                    ScopeKey(tenantId),
                    stale.RuleSet.LoadedAt);
                return stale.RuleSet;
            }

            throw new PiiRedactionUnavailableException(exception);
        }
    }

    public static PiiRuleSet Compile(IEnumerable<PiiRedactionRule> rules)
    {
        var ordered = rules
            .Select(PiiRedactionEngine.Compile)
            .OrderBy(rule => rule.Scope == PiiRuleScope.Global ? 1 : 0)
            .ThenBy(rule => rule.Priority)
            .ThenBy(rule => rule.RuleId)
            .ToList();

        return new PiiRuleSet
        {
            Rules = ordered,
            RuleIds = ordered.Select(rule => rule.RuleId).ToList(),
            LoadedAt = SystemClock.UtcNow
        };
    }

    internal static string ScopeKey(Guid? tenantId) => tenantId?.ToString("N") ?? "global";

    private sealed record CacheEntry(PiiRuleSet RuleSet)
    {
        public bool IsExpired => SystemClock.UtcNow - RuleSet.LoadedAt > CacheTtl;
    }
}

/// <summary>
/// Keeps the global rule snapshot warm for the sync structured-log sink and refreshes tenant scopes.
/// A refresh failure only logs — the provider serves stale snapshots until one succeeds.
/// </summary>
public sealed class PiiRedactionRuleRefreshService(
    IPiiRedactionRuleProvider provider,
    ILogger<PiiRedactionRuleRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await provider.GetGlobalRuleSetAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Periodic PII redaction rule refresh failed; will retry next cycle");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
