using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace GameGuild.Identity.Authorization.Caching;

/// <summary>Tracks authorization cache keys so invalidation can remove entries from process-wide L1.</summary>
public interface IPermissionCacheKeyTracker
{
    /// <summary>Gets a process-local snapshot of the tracked L1 cache entries.</summary>
    PermissionCacheKeyTrackerStatistics GetStatistics();

    /// <summary>Tracks a key and removes its registration when the L1 entry expires or is evicted.</summary>
    void Track(string key, string cacheType);

    /// <summary>Tracks a key and removes its registration when the L1 entry expires or is evicted.</summary>
    void Track(string key, string cacheType, MemoryCacheEntryOptions? entryOptions);

    /// <summary>
    ///     Records an L1 read hit for a tracked key so capacity eviction removes the
    ///     least-recently-used entries instead of the oldest-inserted ones.
    /// </summary>
    void Touch(string key);

    /// <summary>Removes a key from the index without changing the L1 cache.</summary>
    void Forget(string key);

    /// <summary>Removes all matching keys from L1 and the index.</summary>
    int Invalidate(Func<string, bool> predicate, string fallbackCacheType, string reason);

    /// <summary>Removes keys matching a glob pattern. An asterisk matches any sequence of characters.</summary>
    int InvalidatePattern(string pattern, string cacheType, string reason);

    /// <summary>
    ///     Lists tracked keys (most recently used first), optionally narrowed to keys containing
    ///     <paramref name="searchTerm"/> and belonging to <paramref name="cacheType"/>.
    /// </summary>
    /// <param name="searchTerm">Case-insensitive substring filter; <c>null</c> matches every key.</param>
    /// <param name="cacheType">Exact cache-type filter (for example <c>acl</c>); <c>null</c> matches every type.</param>
    /// <param name="skip">Number of filtered entries to skip.</param>
    /// <param name="take">Maximum number of entries to return.</param>
    IReadOnlyList<PermissionCacheTrackedKeyInfo> SearchTrackedKeys(string? searchTerm, string? cacheType, int skip, int take);

    /// <summary>Gets tracking metadata for one key, or <c>null</c> when the key is not tracked.</summary>
    PermissionCacheTrackedKeyInfo? GetTrackedKey(string key);
}

/// <summary>Process-wide index of entries stored in the shared authorization L1 cache.</summary>
/// <remarks>
///     <para>
///         Capacity eviction is least-recently-used: every tracked L1 read hit advances the key's
///         last-access sequence through <see cref="Touch"/>, and when the tracked-entry cap is
///         exceeded the least recently used entry (oldest insertion as the tie-break) is removed
///         from both the index and L1. TTL behavior is unchanged: expiry still removes entries
///         through the registered post-eviction callbacks.
///     </para>
/// </remarks>
public sealed class PermissionCacheKeyTracker : IPermissionCacheKeyTracker
{
    private readonly IMemoryCache _memoryCache;
    private readonly ICacheMetricsService _metrics;
    private readonly int _maxTrackedEntries;
    private readonly ConcurrentDictionary<string, CacheKeyRegistration> _keys = new(StringComparer.Ordinal);
    private long _nextSequence;

    public PermissionCacheKeyTracker(
        IMemoryCache memoryCache,
        ICacheMetricsService metrics)
        : this(memoryCache, metrics, 5000)
    {
    }

    public PermissionCacheKeyTracker(
        IMemoryCache memoryCache,
        ICacheMetricsService metrics,
        int maxTrackedEntries)
    {
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(metrics);
        if (maxTrackedEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTrackedEntries), "The cache entry limit must be positive.");
        }

        _memoryCache = memoryCache;
        _metrics = metrics;
        _maxTrackedEntries = maxTrackedEntries;
    }

    /// <inheritdoc />
    public PermissionCacheKeyTrackerStatistics GetStatistics()
    {
        var entries = _keys.ToArray();
        var users = entries
            .Select(entry => TryGetUserId(entry.Key))
            .Where(userId => userId.HasValue)
            .Select(userId => userId!.Value)
            .Distinct()
            .Count();

        return new PermissionCacheKeyTrackerStatistics(
            entries.LongLength,
            users,
            CountEntries(entries, "permission"),
            CountEntries(entries, "acl"),
            CountEntries(entries, "policy"));
    }

    /// <inheritdoc />
    public void Track(string key, string cacheType)
    {
        Track(key, cacheType, null);
    }

    /// <inheritdoc />
    public void Track(string key, string cacheType, MemoryCacheEntryOptions? entryOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheType);

        var registration = new CacheKeyRegistration(cacheType, Interlocked.Increment(ref _nextSequence));
        _keys[key] = registration;

        entryOptions?.RegisterPostEvictionCallback(
            static (evictedKey, _, _, state) =>
            {
                if (evictedKey is string key && state is EvictionState evictionState)
                {
                    evictionState.Tracker.Forget(key, evictionState.RegistrationId);
                }
            },
            new EvictionState(this, registration.RegistrationId));

        EvictOverCapacity(registration.RegistrationId);
    }

    /// <inheritdoc />
    public void Touch(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_keys.TryGetValue(key, out var registration))
        {
            registration.RecordAccess(Interlocked.Increment(ref _nextSequence));
        }
    }

    /// <inheritdoc />
    public void Forget(string key) => _keys.TryRemove(key, out _);

    /// <inheritdoc />
    public int Invalidate(Func<string, bool> predicate, string fallbackCacheType, string reason)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var removed = 0;
        foreach (var entry in _keys)
        {
            if (!predicate(entry.Key) || !RemoveRegistration(entry.Key, entry.Value))
            {
                continue;
            }

            _memoryCache.Remove(entry.Key);
            var cacheType = entry.Value.CacheType == "all" ? fallbackCacheType : entry.Value.CacheType;
            _metrics.RecordEviction(CacheLevel.L1, cacheType, reason);
            removed++;
        }

        return removed;
    }

    /// <inheritdoc />
    public int InvalidatePattern(string pattern, string cacheType, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        return Invalidate(key => MatchesPattern(key, pattern), cacheType, reason);
    }

    /// <inheritdoc />
    public IReadOnlyList<PermissionCacheTrackedKeyInfo> SearchTrackedKeys(string? searchTerm, string? cacheType, int skip, int take)
    {
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), skip, "The skip count cannot be negative.");
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, "The page size must be positive.");
        }

        return _keys.ToArray()
            .Where(entry => string.IsNullOrEmpty(searchTerm) ||
                            entry.Key.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.IsNullOrEmpty(cacheType) ||
                            string.Equals(entry.Value.CacheType, cacheType, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Value.LastAccessSequence)
            .ThenByDescending(entry => entry.Value.RegistrationId)
            .Skip(skip)
            .Take(take)
            .Select(entry => entry.Value.ToInfo(entry.Key, _memoryCache))
            .ToArray();
    }

    /// <inheritdoc />
    public PermissionCacheTrackedKeyInfo? GetTrackedKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _keys.TryGetValue(key, out var registration) ? registration.ToInfo(key, _memoryCache) : null;
    }

    private void Forget(string key, long registrationId)
    {
        if (_keys.TryGetValue(key, out var current) && current.RegistrationId == registrationId)
        {
            RemoveRegistration(key, current);
        }
    }

    private void EvictOverCapacity(long protectedRegistrationId)
    {
        while (_keys.Count > _maxTrackedEntries)
        {
            var leastRecentlyUsed = _keys
                .Where(entry => entry.Value.RegistrationId != protectedRegistrationId)
                .MinBy(entry => (entry.Value.LastAccessSequence, entry.Value.RegistrationId));
            if (leastRecentlyUsed.Key is null)
            {
                return;
            }

            if (!RemoveRegistration(leastRecentlyUsed.Key, leastRecentlyUsed.Value))
            {
                continue;
            }

            _memoryCache.Remove(leastRecentlyUsed.Key);
            var cacheType = leastRecentlyUsed.Value.CacheType == "all" ? "permission" : leastRecentlyUsed.Value.CacheType;
            _metrics.RecordEviction(CacheLevel.L1, cacheType, "capacity");
        }
    }

    private bool RemoveRegistration(string key, CacheKeyRegistration registration)
    {
        return ((ICollection<KeyValuePair<string, CacheKeyRegistration>>)_keys)
            .Remove(new KeyValuePair<string, CacheKeyRegistration>(key, registration));
    }

    private static long CountEntries(KeyValuePair<string, CacheKeyRegistration>[] entries, string cacheType) =>
        entries.LongCount(entry => string.Equals(entry.Value.CacheType, cacheType, StringComparison.OrdinalIgnoreCase));

    private static Guid? TryGetUserId(string key)
    {
        var segments = key.Split(new[] { ':', '|' }, StringSplitOptions.RemoveEmptyEntries);
        var userSegment = segments.Length >= 4 &&
                          string.Equals(segments[0], "acl", StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(segments[1], "subj", StringComparison.OrdinalIgnoreCase)
            ? 3
            : segments.Length >= 3 &&
              (string.Equals(segments[0], "acl", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segments[0], "perm", StringComparison.OrdinalIgnoreCase))
            ? 2
                : -1;

        return userSegment >= 0 && Guid.TryParse(segments[userSegment], out var userId) ? userId : null;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        var searchFrom = 0;
        foreach (var segment in pattern.Split('*'))
        {
            if (segment.Length == 0)
            {
                continue;
            }

            var matchAt = value.IndexOf(segment, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (matchAt < 0)
            {
                return false;
            }

            searchFrom = matchAt + segment.Length;
        }

        return true;
    }

    private sealed class CacheKeyRegistration
    {
        private long _lastAccessSequence;

        public CacheKeyRegistration(string cacheType, long sequence)
        {
            CacheType = cacheType;
            RegistrationId = sequence;
            FirstTrackedUtc = DateTime.UtcNow;
            _lastAccessSequence = sequence;
            LastAccessUtc = FirstTrackedUtc;
        }

        public string CacheType { get; }

        public long RegistrationId { get; }

        public DateTime FirstTrackedUtc { get; }

        public DateTime LastAccessUtc { get; private set; }

        public long LastAccessSequence => Volatile.Read(ref _lastAccessSequence);

        public void RecordAccess(long sequence)
        {
            Interlocked.Exchange(ref _lastAccessSequence, sequence);
            LastAccessUtc = DateTime.UtcNow;
        }

        public PermissionCacheTrackedKeyInfo ToInfo(string key, IMemoryCache memoryCache) => new(
            key,
            CacheType,
            FirstTrackedUtc,
            LastAccessUtc,
            memoryCache.TryGetValue(key, out _));
    }

    private sealed record EvictionState(PermissionCacheKeyTracker Tracker, long RegistrationId);
}

/// <summary>Tracking metadata for a single authorization L1 cache key.</summary>
/// <param name="Key">The tracked cache key.</param>
/// <param name="CacheType">The cache type the entry was tracked under (for example <c>acl</c> or <c>permission</c>).</param>
/// <param name="FirstTrackedUtc">UTC time the key was (re)registered in the tracker.</param>
/// <param name="LastAccessUtc">UTC time of the tracked insertion or the most recent L1 read hit.</param>
/// <param name="PresentInL1">Whether the entry is currently readable from the process-local L1 cache.</param>
public sealed record PermissionCacheTrackedKeyInfo(
    string Key,
    string CacheType,
    DateTime FirstTrackedUtc,
    DateTime LastAccessUtc,
    bool PresentInL1);

/// <summary>Process-local counts for currently tracked authorization L1 cache entries.</summary>
public sealed record PermissionCacheKeyTrackerStatistics(
    long TotalEntries,
    int UniqueUsers,
    long PermissionEntries,
    long AclEntries,
    long PolicyEntries);
