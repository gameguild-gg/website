using System.Diagnostics;
using System.Text.Json;
using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization.Caching;

/// <summary>
///     Abstraction for a hybrid (L1 + L2) permission cache.
/// </summary>
public interface IHybridPermissionCache
{
    /// <summary>Gets multiple value-type entries with bounded concurrency.</summary>
    /// <remarks>At most 500 keys may be submitted; duplicate keys are read once. Reads are not transactional.</remarks>
    Task<IReadOnlyDictionary<string, CacheResult<T>>> GetManyValuesAsync<T>(
        IReadOnlyCollection<string> keys,
        string cacheType) where T : struct;

    Task<IReadOnlyDictionary<string, CacheResult<T>>> GetManyValuesAsync<T>(
        IReadOnlyCollection<string> keys,
        string cacheType,
        CancellationToken cancellationToken) where T : struct;

    /// <summary>Sets multiple value-type entries with bounded concurrency.</summary>
    /// <remarks>At most 500 entries are accepted. Cache writes are not transactional.</remarks>
    Task SetManyValuesAsync<T>(
        IReadOnlyDictionary<string, T> values,
        string cacheType) where T : struct;

    Task SetManyValuesAsync<T>(
        IReadOnlyDictionary<string, T> values,
        string cacheType,
        CancellationToken cancellationToken) where T : struct;

    /// <summary>
    ///     Gets a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cacheType">The cache type for metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached value, or default if not found.</returns>
    Task<T?> GetAsync<T>(string key, string cacheType, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    ///     Gets a value from the cache (value types).
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cacheType">The cache type for metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached value wrapped in a result, or null if not found.</returns>
    Task<CacheResult<T>> GetValueAsync<T>(string key, string cacheType, CancellationToken cancellationToken = default) where T : struct;

    /// <summary>
    ///     Sets a value in the cache.
    /// </summary>
    /// <typeparam name="T">The type of value to store.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="cacheType">The cache type for metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, string cacheType, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sets a value in the cache with custom TTL.
    /// </summary>
    /// <typeparam name="T">The type of value to store.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="cacheType">The cache type for metrics.</param>
    /// <param name="ttlSeconds">TTL override in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, string cacheType, int ttlSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sets a value type in the cache.
    /// </summary>
    /// <typeparam name="T">The type of value to store (must be a value type).</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="cacheType">The cache type for metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetValueAsync<T>(string key, T value, string cacheType, CancellationToken cancellationToken = default) where T : struct;

    /// <summary>
    ///     Sets a value type in the cache with an explicit TTL that caps both the L1 and the L2 lifetime.
    /// </summary>
    /// <typeparam name="T">The type of value to store (must be a value type).</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="cacheType">The cache type for metrics.</param>
    /// <param name="ttlSeconds">
    ///     TTL override in seconds; it never extends the configured TTLs, it only shortens them.
    ///     Used to clamp cached ACL decisions to their earliest effective grant expiration.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetValueAsync<T>(string key, T value, string cacheType, int ttlSeconds, CancellationToken cancellationToken = default) where T : struct;

    /// <summary>
    ///     Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cacheType">The cache type for metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(string key, string cacheType, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates all cache entries matching a pattern.
    /// </summary>
    /// <param name="pattern">The key pattern to match.</param>
    /// <param name="cacheType">The cache type for metrics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidatePatternAsync(string pattern, string cacheType, CancellationToken cancellationToken = default);
}

/// <summary>
///     Result of a cache lookup for value types.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public readonly struct CacheResult<T> where T : struct
{
    /// <summary>
    ///     Whether the value was found in the cache.
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    ///     The cached value.
    /// </summary>
    public T Value { get; init; }

    /// <summary>
    ///     Creates a found result.
    /// </summary>
    public static CacheResult<T> Hit(T value) => new() { Found = true, Value = value };

    /// <summary>
    ///     Creates a miss result.
    /// </summary>
    public static CacheResult<T> Miss() => new() { Found = false, Value = default };
}

/// <summary>
///     Hybrid cache implementation with L1 (in-memory) and optional L2 (distributed/Redis) cache.
/// </summary>
/// <remarks>
///     <para>
///         <b>Cache Levels:</b>
///         <list type="bullet">
///             <item>L1 (IMemoryCache): Fast, per-instance, short TTL</item>
///             <item>L2 (IDistributedCache): Shared across instances, longer TTL</item>
///         </list>
///     </para>
///     <para>
///         <b>Read Flow:</b> L1 → L2 → Database
///         <b>Write Flow:</b> Database → Invalidate L1 + L2
///     </para>
/// </remarks>
public sealed class HybridPermissionCache : IHybridPermissionCache
{
    private const int MaxBulkOperationSize = 500;
    private const int MaxBulkOperationConcurrency = 16;

    private readonly IMemoryCache _l1Cache;
    private readonly IDistributedCache? _l2Cache;
    private readonly ICacheMetricsService _metrics;
    private readonly AuthorizationCacheOptions _options;
    private readonly ILogger<HybridPermissionCache> _logger;
    private readonly IPermissionCacheKeyTracker _keyTracker;
    private readonly bool _useL2;

    /// <summary>
    ///     Initializes a new instance of <see cref="HybridPermissionCache"/>.
    /// </summary>
    public HybridPermissionCache(
        IMemoryCache l1Cache,
        IOptions<AuthorizationCacheOptions> options,
        ICacheMetricsService metrics,
        ILogger<HybridPermissionCache> logger)
        : this(l1Cache, options, metrics, logger, null, new PermissionCacheKeyTracker(l1Cache, metrics))
    {
    }

    public HybridPermissionCache(
        IMemoryCache l1Cache,
        IOptions<AuthorizationCacheOptions> options,
        ICacheMetricsService metrics,
        ILogger<HybridPermissionCache> logger,
        IDistributedCache? l2Cache)
        : this(l1Cache, options, metrics, logger, l2Cache, new PermissionCacheKeyTracker(l1Cache, metrics))
    {
    }

    public HybridPermissionCache(
        IMemoryCache l1Cache,
        IOptions<AuthorizationCacheOptions> options,
        ICacheMetricsService metrics,
        ILogger<HybridPermissionCache> logger,
        IPermissionCacheKeyTracker keyTracker)
        : this(l1Cache, options, metrics, logger, null, keyTracker)
    {
    }

    public HybridPermissionCache(
        IMemoryCache l1Cache,
        IOptions<AuthorizationCacheOptions> options,
        ICacheMetricsService metrics,
        ILogger<HybridPermissionCache> logger,
        IDistributedCache? l2Cache,
        IPermissionCacheKeyTracker keyTracker)
    {
        _l1Cache = l1Cache;
        _l2Cache = l2Cache;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
        _keyTracker = keyTracker;
        _useL2 = _options.UseDistributedCache && _l2Cache != null;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, string cacheType, CancellationToken cancellationToken = default) where T : class
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            // Try L1 first
            T? l1Value = default;
            var foundInL1 = false;
            try
            {
                foundInL1 = _l1Cache.TryGetValue(key, out l1Value) && l1Value != null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "L1 cache read failed for key {Key}, falling back to L2 or database", key);
            }

            if (foundInL1)
            {
                _keyTracker.Touch(key);
                _metrics.RecordHit(CacheLevel.L1, cacheType);
                return l1Value;
            }

            // Try L2 if enabled
            if (_useL2)
            {
                try
                {
                    var l2Bytes = await _l2Cache!.GetAsync(key, cancellationToken).ConfigureAwait(false);
                    if (l2Bytes != null && l2Bytes.Length > 0 && TryDecodeL2Payload(key, l2Bytes, out var plain))
                    {
                        var l2Value = JsonSerializer.Deserialize<T>(plain);
                        if (l2Value != null)
                        {
                            _metrics.RecordHit(CacheLevel.L2, cacheType);

                            // Promote to L1
                            TryPromoteToL1(key, l2Value, cacheType);

                            return l2Value;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Treat an unavailable or unreadable L2 entry as a cache miss so the caller
                    // can resolve the authoritative value from its backing store.
                    _logger.LogWarning(ex, "L2 cache read failed for key {Key}, falling back to database", key);
                }
            }

            _metrics.RecordMiss(cacheType);
            return null;

        }
        finally
        {
            _metrics.RecordLookupDuration(Stopwatch.GetElapsedTime(startedAt), cacheType);
        }
    }

    /// <inheritdoc />
    public Task<CacheResult<T>> GetValueAsync<T>(string key, string cacheType, CancellationToken cancellationToken = default) where T : struct =>
        GetValueAsyncCore<T>(key, cacheType, cancellationToken, skipL1: false, startedAtTimestamp: null);

    private async Task<CacheResult<T>> GetValueAsyncCore<T>(
        string key,
        string cacheType,
        CancellationToken cancellationToken,
        bool skipL1,
        long? startedAtTimestamp) where T : struct
    {
        var startedAt = startedAtTimestamp ?? Stopwatch.GetTimestamp();
        try
        {
            if (!skipL1)
            {
                T l1Value = default;
                var foundInL1 = false;
                try
                {
                    foundInL1 = _l1Cache.TryGetValue(key, out l1Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "L1 cache read failed for key {Key}, falling back to L2 or database", key);
                }

                if (foundInL1)
                {
                    _keyTracker.Touch(key);
                    _metrics.RecordHit(CacheLevel.L1, cacheType);
                    return CacheResult<T>.Hit(l1Value);
                }
            }

            // Try L2 if enabled
            if (_useL2)
            {
                try
                {
                    var l2Bytes = await _l2Cache!.GetAsync(key, cancellationToken).ConfigureAwait(false);
                    if (l2Bytes != null && l2Bytes.Length > 0 && TryDecodeL2Payload(key, l2Bytes, out var plain))
                    {
                        var l2Value = JsonSerializer.Deserialize<T>(plain);
                        _metrics.RecordHit(CacheLevel.L2, cacheType);

                        // Promote to L1
                        TryPromoteToL1(key, l2Value, cacheType);

                        return CacheResult<T>.Hit(l2Value);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "L2 cache read failed for key {Key}, falling back to database", key);
                }
            }

            _metrics.RecordMiss(cacheType);
            return CacheResult<T>.Miss();

        }
        finally
        {
            _metrics.RecordLookupDuration(Stopwatch.GetElapsedTime(startedAt), cacheType);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, CacheResult<T>>> GetManyValuesAsync<T>(
        IReadOnlyCollection<string> keys,
        string cacheType) where T : struct =>
        GetManyValuesAsync<T>(keys, cacheType, CancellationToken.None);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, CacheResult<T>>> GetManyValuesAsync<T>(
        IReadOnlyCollection<string> keys,
        string cacheType,
        CancellationToken cancellationToken) where T : struct
    {
        ArgumentNullException.ThrowIfNull(keys);
        cancellationToken.ThrowIfCancellationRequested();

        var distinctKeys = NormalizeBulkKeys(keys);
        if (distinctKeys.Length == 0)
        {
            return new Dictionary<string, CacheResult<T>>(StringComparer.Ordinal);
        }

        var results = new CacheResult<T>[distinctKeys.Length];
        var l2Candidates = new List<int>(distinctKeys.Length);
        var lookupStartTimes = new long[distinctKeys.Length];
        for (var index = 0; index < distinctKeys.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startedAt = Stopwatch.GetTimestamp();
            lookupStartTimes[index] = startedAt;
            T l1Value = default;
            var foundInL1 = false;
            try
            {
                foundInL1 = _l1Cache.TryGetValue(distinctKeys[index], out l1Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "L1 cache read failed for key {Key}, falling back to L2 or database", distinctKeys[index]);
            }

            if (foundInL1)
            {
                _keyTracker.Touch(distinctKeys[index]);
                _metrics.RecordHit(CacheLevel.L1, cacheType);
                results[index] = CacheResult<T>.Hit(l1Value);
                _metrics.RecordLookupDuration(Stopwatch.GetElapsedTime(startedAt), cacheType);
            }
            else
            {
                l2Candidates.Add(index);
            }
        }

        if (l2Candidates.Count > 0)
        {
            await Parallel.ForEachAsync(
                l2Candidates,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Math.Min(l2Candidates.Count, MaxBulkOperationConcurrency)
                },
                async (index, token) =>
                {
                    results[index] = await GetValueAsyncCore<T>(
                        distinctKeys[index],
                        cacheType,
                        token,
                        skipL1: true,
                        startedAtTimestamp: lookupStartTimes[index]).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }

        var resultMap = new Dictionary<string, CacheResult<T>>(distinctKeys.Length, StringComparer.Ordinal);
        for (var index = 0; index < distinctKeys.Length; index++)
        {
            resultMap.Add(distinctKeys[index], results[index]);
        }

        return resultMap;
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, string cacheType, CancellationToken cancellationToken = default)
    {
        return SetAsyncCore(key, value, cacheType, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, string cacheType, int ttlSeconds, CancellationToken cancellationToken = default)
    {
        return SetAsyncCore(key, value, cacheType, ttlSeconds, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetValueAsync<T>(string key, T value, string cacheType, CancellationToken cancellationToken = default) where T : struct
    {
        return SetAsyncCore(key, value, cacheType, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetValueAsync<T>(string key, T value, string cacheType, int ttlSeconds, CancellationToken cancellationToken = default) where T : struct
    {
        return SetAsyncCore(key, value, cacheType, ttlSeconds, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetManyValuesAsync<T>(IReadOnlyDictionary<string, T> values, string cacheType) where T : struct =>
        SetManyValuesAsync(values, cacheType, CancellationToken.None);

    /// <inheritdoc />
    public async Task SetManyValuesAsync<T>(
        IReadOnlyDictionary<string, T> values,
        string cacheType,
        CancellationToken cancellationToken) where T : struct
    {
        ArgumentNullException.ThrowIfNull(values);
        cancellationToken.ThrowIfCancellationRequested();
        if (values.Count > MaxBulkOperationSize)
        {
            throw new ArgumentOutOfRangeException(nameof(values), $"At most {MaxBulkOperationSize} cache entries can be written at once.");
        }

        var entries = values.ToArray();
        foreach (var entry in entries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Key);
        }

        if (entries.Length == 0)
        {
            return;
        }

        await Parallel.ForEachAsync(
            Enumerable.Range(0, entries.Length),
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Min(entries.Length, MaxBulkOperationConcurrency)
            },
            async (index, token) =>
            {
                var entry = entries[index];
                await SetValueAsync(entry.Key, entry.Value, cacheType, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    private static string[] NormalizeBulkKeys(IReadOnlyCollection<string> keys)
    {
        if (keys.Count > MaxBulkOperationSize)
        {
            throw new ArgumentOutOfRangeException(nameof(keys), $"At most {MaxBulkOperationSize} cache entries can be read at once.");
        }

        var uniqueKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            uniqueKeys.Add(key);
        }

        return uniqueKeys.ToArray();
    }

    private async Task SetAsyncCore<T>(string key, T value, string cacheType, int? ttlSeconds, CancellationToken cancellationToken)
    {
        var l1Ttl = TimeSpan.FromSeconds(ttlSeconds ?? GetL1TtlSeconds(cacheType));

        // An explicit TTL only shortens the configured L2 lifetime; it never extends it. This lets
        // callers clamp time-bound entries (for example ACL decisions bounded by grant expiration)
        // so neither cache level can outlive the data the entry was derived from.
        var l2Ttl = TimeSpan.FromSeconds(
            ttlSeconds is { } explicitTtl
                ? Math.Min(explicitTtl, _options.DistributedCacheTtlSeconds)
                : _options.DistributedCacheTtlSeconds);

        // Set in L1
        SetL1(key, value, cacheType, l1Ttl);

        // Set in L2 if enabled
        if (_useL2)
        {
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
                bytes = EncodeL2Payload(key, bytes);
                var distributedOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = l2Ttl
                };
                await _l2Cache!.SetAsync(key, bytes, distributedOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "L2 cache write failed for key {Key}", key);
            }
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, string cacheType, CancellationToken cancellationToken = default)
    {
        // Remove from L1
        _l1Cache.Remove(key);
        _keyTracker.Forget(key);
        _metrics.RecordEviction(CacheLevel.L1, cacheType, "explicit");

        // Remove from L2 if enabled
        if (_useL2)
        {
            try
            {
                await _l2Cache!.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
                _metrics.RecordEviction(CacheLevel.L2, cacheType, "explicit");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "L2 cache remove failed for key {Key}", key);
            }
        }
    }

    /// <inheritdoc />
    public Task InvalidatePatternAsync(string pattern, string cacheType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var removed = _keyTracker.InvalidatePattern(pattern, cacheType, "pattern_invalidation");
        _logger.LogDebug("Invalidated {EntryCount} local cache entries matching {Pattern}; L2 remains versioned.", removed, pattern);
        return Task.CompletedTask;
    }

    private void SetL1<T>(string key, T value, string cacheType, TimeSpan? ttl = null)
    {
        var absoluteTtl = ttl ?? TimeSpan.FromSeconds(GetL1TtlSeconds(cacheType));
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(absoluteTtl)
            .SetSize(1);

        if (absoluteTtl >= TimeSpan.FromSeconds(2))
        {
            cacheOptions.SetSlidingExpiration(TimeSpan.FromTicks(absoluteTtl.Ticks / 2));
        }

        _keyTracker.Track(key, cacheType, cacheOptions);
        _l1Cache.Set(key, value, cacheOptions);
    }

    private void TryPromoteToL1<T>(string key, T value, string cacheType)
    {
        try
        {
            SetL1(key, value, cacheType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "L1 cache promotion failed for key {Key}; returning the L2 value", key);
        }
    }

    private int GetL1TtlSeconds(string cacheType)
    {
        if (string.Equals(cacheType, "policy", StringComparison.OrdinalIgnoreCase))
        {
            return _options.PolicyTtlSeconds;
        }

        if (string.Equals(cacheType, "acl", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(cacheType, "access-control-list", StringComparison.OrdinalIgnoreCase))
        {
            return _options.AccessControlListTtlSeconds;
        }

        if (string.Equals(cacheType, "ruleset", StringComparison.OrdinalIgnoreCase))
        {
            return _options.RulesetTtlSeconds;
        }

        return _options.PermissionTtlSeconds;
    }

    /// <summary>
    ///     Compresses an L2 payload when compression is enabled, the algorithm is set, and the
    ///     serialized value meets the configured threshold. Values below the threshold (and all
    ///     values when compression is off) are stored as raw JSON, which stays readable across
    ///     mixed-version fleets through <see cref="TryDecodeL2Payload"/>.
    /// </summary>
    private byte[] EncodeL2Payload(string key, byte[] serialized)
    {
        if (!_options.L2CompressionEnabled ||
            _options.L2CompressionAlgorithm == L2CompressionAlgorithm.None ||
            serialized.Length < _options.L2CompressionThresholdBytes)
        {
            return serialized;
        }

        try
        {
            var wrapped = PermissionCacheL2Payload.Wrap(serialized, _options.L2CompressionAlgorithm);
            _logger.LogDebug(
                "Compressed L2 cache payload for key {Key} from {RawBytes} to {StoredBytes} bytes using {Algorithm}",
                key,
                serialized.Length,
                wrapped.Length,
                _options.L2CompressionAlgorithm);
            return wrapped;
        }
        catch (Exception ex)
        {
            // Compression is an optimization: fall back to the raw payload rather than failing the write.
            _logger.LogWarning(ex, "L2 cache payload compression failed for key {Key}; storing the value uncompressed", key);
            return serialized;
        }
    }

    /// <summary>
    ///     Decodes an L2 payload, transparently handling both compressed envelopes and raw JSON
    ///     written by deployments predating compression.
    /// </summary>
    /// <returns><c>false</c> when the payload is enveloped but undecodable; the caller then treats the entry as a miss.</returns>
    private bool TryDecodeL2Payload(string key, byte[] stored, out byte[] plain)
    {
        if (PermissionCacheL2Payload.TryUnwrap(stored, out plain))
        {
            return true;
        }

        _logger.LogWarning(
            "L2 cache entry for key {Key} carries an unsupported or corrupted compression envelope; treating it as a miss",
            key);
        return false;
    }
}
