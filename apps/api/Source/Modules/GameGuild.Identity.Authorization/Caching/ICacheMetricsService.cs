using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace GameGuild.Identity.Authorization.Caching;

/// <summary>
///     Service for collecting and reporting cache metrics.
/// </summary>
public interface ICacheMetricsService
{
    /// <summary>
    ///     Records a cache hit.
    /// </summary>
    /// <param name="cacheLevel">The cache level (L1 or L2).</param>
    /// <param name="cacheType">The type of cache (policy, permission, acl).</param>
    void RecordHit(CacheLevel cacheLevel, string cacheType);

    /// <summary>
    ///     Records a cache miss.
    /// </summary>
    /// <param name="cacheType">The type of cache (policy, permission, acl).</param>
    void RecordMiss(string cacheType);

    /// <summary>
    ///     Records the elapsed time spent looking up one cache entry across enabled cache levels.
    /// </summary>
    /// <param name="duration">Elapsed cache lookup time.</param>
    /// <param name="cacheType">The cache type for metrics.</param>
    void RecordLookupDuration(TimeSpan duration, string cacheType);

    /// <summary>
    ///     Records a cache eviction.
    /// </summary>
    /// <param name="cacheLevel">The cache level (L1 or L2).</param>
    /// <param name="cacheType">The type of cache (policy, permission, acl).</param>
    /// <param name="reason">The reason for eviction (optional, defaults to "explicit").</param>
    void RecordEviction(CacheLevel cacheLevel, string cacheType, string reason = "explicit");

    /// <summary>
    ///     Gets the current cache statistics.
    /// </summary>
    CacheStatistics GetStatistics();
}

/// <summary>
///     Cache level identifier.
/// </summary>
public enum CacheLevel
{
    /// <summary>
    ///     Level 1: In-memory cache (fast, per-instance).
    /// </summary>
    L1,

    /// <summary>
    ///     Level 2: Distributed cache (shared across instances).
    /// </summary>
    L2
}

/// <summary>
///     Cache statistics for observability.
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>
    ///     Total number of L1 cache hits.
    /// </summary>
    public long L1Hits { get; set; }

    /// <summary>
    ///     Total number of L2 cache hits.
    /// </summary>
    public long L2Hits { get; set; }

    /// <summary>
    ///     Total number of cache misses.
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    ///     Total number of cache evictions.
    /// </summary>
    public long Evictions { get; set; }

    /// <summary>
    ///     L1 hit rate (0-1).
    /// </summary>
    public double L1HitRate => TotalRequests > 0 ? (double)L1Hits / TotalRequests : 0;

    /// <summary>
    ///     L2 hit rate (0-1).
    /// </summary>
    public double L2HitRate => TotalRequests > 0 ? (double)L2Hits / TotalRequests : 0;

    /// <summary>
    ///     Overall hit rate (0-1).
    /// </summary>
    public double OverallHitRate => TotalRequests > 0 ? (double)(L1Hits + L2Hits) / TotalRequests : 0;

    /// <summary>
    ///     Total number of cache requests.
    /// </summary>
    public long TotalRequests => L1Hits + L2Hits + Misses;

    /// <summary>
    ///     Statistics by cache type.
    /// </summary>
    public Dictionary<string, CacheTypeStatistics> ByType { get; set; } = new();

    /// <summary>
    ///     Process-local lookup-duration aggregates by cache type.
    /// </summary>
    public Dictionary<string, CacheLookupStatistics> LookupDurationByType { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
///     Bounded aggregate for cache lookup durations; raw samples are not retained.
/// </summary>
public sealed record CacheLookupStatistics(long Count, double AverageMilliseconds);

/// <summary>
///     Statistics for a specific cache type.
/// </summary>
public sealed class CacheTypeStatistics
{
    /// <summary>
    ///     Cache type name.
    /// </summary>
    public string CacheType { get; set; } = string.Empty;

    /// <summary>
    ///     L1 hits for this cache type.
    /// </summary>
    public long L1Hits { get; set; }

    /// <summary>
    ///     L2 hits for this cache type.
    /// </summary>
    public long L2Hits { get; set; }

    /// <summary>
    ///     Misses for this cache type.
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    ///     Hit rate for this cache type.
    /// </summary>
    public double HitRate => (L1Hits + L2Hits + Misses) > 0 
        ? (double)(L1Hits + L2Hits) / (L1Hits + L2Hits + Misses) 
        : 0;
}

/// <summary>
///     Default implementation of <see cref="ICacheMetricsService"/> using System.Diagnostics.Metrics.
/// </summary>
public sealed class CacheMetricsService : ICacheMetricsService
{
    private static readonly Meter Meter = new("GameGuild.Identity.Authorization.Cache", "1.0.0");

    private readonly Counter<long> _hitsCounter;
    private readonly Counter<long> _missesCounter;
    private readonly Counter<long> _evictionsCounter;
    private readonly Histogram<double> _lookupDurationHistogram;
    private readonly bool _enabled;

    private long _l1Hits;
    private long _l2Hits;
    private long _misses;
    private long _evictions;

    private readonly Dictionary<string, CacheTypeStatistics> _typeStats = new();
    private readonly ConcurrentDictionary<string, LookupDurationAccumulator> _lookupDurations = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    /// <summary>
    ///     Initializes a new instance of <see cref="CacheMetricsService"/>.
    /// </summary>
    public CacheMetricsService(bool enabled = true)
    {
        _enabled = enabled;
        _hitsCounter = Meter.CreateCounter<long>(
            "authorization_cache_hits",
            "hits",
            "Number of cache hits");

        _missesCounter = Meter.CreateCounter<long>(
            "authorization_cache_misses",
            "misses",
            "Number of cache misses");

        _evictionsCounter = Meter.CreateCounter<long>(
            "authorization_cache_evictions",
            "evictions",
            "Number of cache evictions");

        _lookupDurationHistogram = Meter.CreateHistogram<double>(
            "authorization_cache_lookup_duration",
            "ms",
            "Time spent reading an authorization cache entry across enabled cache levels");
    }

    /// <inheritdoc />
    public void RecordHit(CacheLevel cacheLevel, string cacheType)
    {
        if (!_enabled)
        {
            return;
        }

        var tags = new TagList(
        [
            new KeyValuePair<string, object?>("cache_level", cacheLevel.ToString()),
            new KeyValuePair<string, object?>("cache_type", cacheType)
        ]);
        _hitsCounter.Add(1, tags);

        if (cacheLevel == CacheLevel.L1)
        {
            Interlocked.Increment(ref _l1Hits);
        }
        else
        {
            Interlocked.Increment(ref _l2Hits);
        }

        UpdateTypeStats(cacheType, cacheLevel, isHit: true);
    }

    /// <inheritdoc />
    public void RecordMiss(string cacheType)
    {
        if (!_enabled)
        {
            return;
        }

        var tags = new TagList(
        [
            new KeyValuePair<string, object?>("cache_type", cacheType)
        ]);
        _missesCounter.Add(1, tags);

        Interlocked.Increment(ref _misses);
        UpdateTypeStats(cacheType, null, isHit: false);
    }

    /// <inheritdoc />
    public void RecordLookupDuration(TimeSpan duration, string cacheType)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Cache lookup duration cannot be negative.");
        }

        if (!_enabled)
        {
            return;
        }

        var tags = new TagList(
        [
            new KeyValuePair<string, object?>("cache_type", cacheType)
        ]);
        _lookupDurationHistogram.Record(duration.TotalMilliseconds, tags);

        _lookupDurations.GetOrAdd(cacheType, static _ => new LookupDurationAccumulator()).Record(duration.Ticks);
    }

    /// <inheritdoc />
    public void RecordEviction(CacheLevel cacheLevel, string cacheType, string reason = "explicit")
    {
        if (!_enabled)
        {
            return;
        }

        var tags = new TagList(
        [
            new KeyValuePair<string, object?>("cache_level", cacheLevel.ToString()),
            new KeyValuePair<string, object?>("cache_type", cacheType),
            new KeyValuePair<string, object?>("reason", reason)
        ]);
        _evictionsCounter.Add(1, tags);

        Interlocked.Increment(ref _evictions);
    }

    /// <inheritdoc />
    public CacheStatistics GetStatistics()
    {
        lock (_lock)
        {
            return new CacheStatistics
            {
                L1Hits = _l1Hits,
                L2Hits = _l2Hits,
                Misses = _misses,
                Evictions = _evictions,
                ByType = _typeStats.ToDictionary(
                    pair => pair.Key,
                    pair => new CacheTypeStatistics
                    {
                        CacheType = pair.Value.CacheType,
                        L1Hits = pair.Value.L1Hits,
                        L2Hits = pair.Value.L2Hits,
                        Misses = pair.Value.Misses
                    },
                    StringComparer.Ordinal),
                LookupDurationByType = _lookupDurations.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.GetSnapshot(),
                    StringComparer.Ordinal)
            };
        }
    }

    private sealed class LookupDurationAccumulator
    {
        private long _count;
        private long _totalDurationTicks;

        public void Record(long durationTicks)
        {
            Interlocked.Add(ref _totalDurationTicks, durationTicks);
            Interlocked.Increment(ref _count);
        }

        public CacheLookupStatistics GetSnapshot()
        {
            var count = Interlocked.Read(ref _count);
            var totalDurationTicks = Interlocked.Read(ref _totalDurationTicks);
            var averageMilliseconds = count == 0
                ? 0
                : totalDurationTicks / (double)count / TimeSpan.TicksPerMillisecond;

            return new CacheLookupStatistics(count, averageMilliseconds);
        }
    }

    private void UpdateTypeStats(string cacheType, CacheLevel? cacheLevel, bool isHit)
    {
        lock (_lock)
        {
            if (!_typeStats.TryGetValue(cacheType, out var stats))
            {
                stats = new CacheTypeStatistics { CacheType = cacheType };
                _typeStats[cacheType] = stats;
            }

            if (isHit)
            {
                if (cacheLevel == CacheLevel.L1)
                {
                    stats.L1Hits++;
                }
                else
                {
                    stats.L2Hits++;
                }
            }
            else
            {
                stats.Misses++;
            }
        }
    }
}
