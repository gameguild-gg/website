using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Read-through cache in front of FX rate lookups on the conversion path (issue #412
///     spec B: "Redis cache (1h TTL) in front of lookups"). Backed by the platform's
///     <see cref="IDistributedCache"/> (Redis in production, memory in single-node/dev) whenever
///     one is registered; falls back to a process-local TTL map otherwise (unit tests, focused
///     hosts). Misses are never cached — a missing quotation must keep hitting the source so a
///     freshly synced rate becomes visible without a cache flush.
/// </summary>
public interface IFxRateCache
{
    /// <summary>Gets a cached quotation for the pair, if present and unexpired.</summary>
    Task<ExchangeRate?> GetAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default);

    /// <summary>Stores a quotation for the pair for the configured TTL.</summary>
    Task SetAsync(ExchangeRate rate, CancellationToken cancellationToken = default);

    /// <summary>Drops the cached entry for a pair (used when an override changes the answer).</summary>
    Task InvalidateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IFxRateCache"/> implementation (distributed-first, in-memory fallback).</summary>
public sealed class FxRateCache(
    IDistributedCache? distributed,
    IOptions<FxSyncOptions> options,
    ILogger<FxRateCache> logger) : IFxRateCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, (ExchangeRate Rate, DateTime ExpiresAtUtc)> _local = new(StringComparer.Ordinal);

    private TimeSpan Ttl { get => TimeSpan.FromMinutes(Math.Max(0, options.Value.CacheTtlMinutes)); }

    /// <inheritdoc />
    public async Task<ExchangeRate?> GetAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(baseCurrency, quoteCurrency);

        if (distributed is not null)
        {
            try
            {
                var json = await distributed.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    return Deserialize(json);
                }
            }
            catch (Exception exception)
            {
                // Cache trouble must never break conversion: log and fall through to the source.
                logger.LogWarning(exception, "FX distributed cache read failed for {Key}.", key);
            }
        }

        if (_local.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAtUtc > SystemClock.UtcNow)
            {
                return entry.Rate;
            }

            _local.TryRemove(key, out _);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task SetAsync(ExchangeRate rate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rate);

        var key = CacheKey(rate.BaseCurrency, rate.QuoteCurrency);
        var payload = JsonSerializer.Serialize(
            new CachedRate(rate.BaseCurrency, rate.QuoteCurrency, rate.Rate, rate.AsOfUtc, rate.Source),
            JsonOptions);

        if (distributed is not null)
        {
            try
            {
                await distributed.SetStringAsync(
                    key,
                    payload,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl },
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "FX distributed cache write failed for {Key}.", key);
            }
        }

        _local[key] = (rate, SystemClock.UtcNow.Add(Ttl));
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(string baseCurrency, string quoteCurrency, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(baseCurrency, quoteCurrency);
        _local.TryRemove(key, out _);

        if (distributed is not null)
        {
            try
            {
                await distributed.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "FX distributed cache invalidation failed for {Key}.", key);
            }
        }
    }

    private static string CacheKey(string baseCurrency, string quoteCurrency)
    {
        return $"fx:rate:{baseCurrency.Trim().ToUpperInvariant()}:{quoteCurrency.Trim().ToUpperInvariant()}";
    }

    private static ExchangeRate? Deserialize(string json)
    {
        try
        {
            var cached = JsonSerializer.Deserialize<CachedRate>(json, JsonOptions);
            return cached is null
                ? null
                : new ExchangeRate(cached.BaseCurrency, cached.QuoteCurrency, cached.Rate, cached.AsOfUtc, cached.Source);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record CachedRate(string BaseCurrency, string QuoteCurrency, decimal Rate, DateTimeOffset AsOfUtc, string Source);
}
