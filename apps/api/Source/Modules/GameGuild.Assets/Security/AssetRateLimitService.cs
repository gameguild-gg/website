using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.Security;

/// <summary>
/// Configuration for asset rate limiting.
/// Mitigates: Hotlinking/Bandwidth Abuse (#1), Path Token Brute Force (#3)
/// </summary>
public class AssetRateLimitOptions
{
    public const string SectionName = "Assets:RateLimiting";

    /// <summary>
    /// Maximum access requests per asset per hour.
    /// Feature flag: asset:hotlink:limit:per:hour
    /// </summary>
    public int MaxAccessPerAssetPerHour { get; set; } = 1000;

    /// <summary>
    /// Maximum 403 responses per IP per hour (brute force protection).
    /// </summary>
    public int Max403PerIpPerHour { get; set; } = 50;

    /// <summary>
    /// Block duration in minutes after exceeding rate limit.
    /// </summary>
    public int BlockDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Sliding window size in seconds.
    /// </summary>
    public int WindowSizeSeconds { get; set; } = 3600; // 1 hour
}

/// <summary>
/// Result of a rate limit check.
/// </summary>
public sealed record RateLimitResult(
    bool IsAllowed,
    int CurrentCount,
    int Limit,
    TimeSpan? RetryAfter = null,
    string? Reason = null);

/// <summary>
/// Interface for asset rate limiting.
/// </summary>
public interface IAssetRateLimitService
{
    /// <summary>
    /// Checks and increments the access count for an asset.
    /// Returns whether the request is allowed.
    /// </summary>
    Task<RateLimitResult> CheckAssetAccessRateAsync(
        Guid assetReferenceId,
        CancellationToken ct = default);

    /// <summary>
    /// Records a 403 response for an IP address.
    /// Returns whether the IP should be blocked.
    /// </summary>
    Task<RateLimitResult> Record403ResponseAsync(
        string ipAddress,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if an IP address is currently blocked.
    /// </summary>
    Task<bool> IsIpBlockedAsync(string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Gets current access stats for an asset.
    /// </summary>
    Task<AssetAccessStats> GetAccessStatsAsync(
        Guid assetReferenceId,
        CancellationToken ct = default);
}

/// <summary>
/// Access statistics for an asset.
/// </summary>
public record AssetAccessStats(
    Guid AssetReferenceId,
    long CurrentHourCount,
    long TotalCount,
    DateTime? LastAccessTime);

/// <summary>
/// Implementation of asset rate limiting using distributed cache.
/// </summary>
public class AssetRateLimitService : IAssetRateLimitService
{
    private readonly IDistributedCache _cache;
    private readonly AssetRateLimitOptions _options;
    private readonly ILogger<AssetRateLimitService> _logger;

    private const string AssetAccessKeyPrefix = "asset:access:";
    private const string Ip403KeyPrefix = "asset:403:";
    private const string IpBlockKeyPrefix = "asset:block:";

    public AssetRateLimitService(
        IDistributedCache cache,
        IOptions<AssetRateLimitOptions> options,
        ILogger<AssetRateLimitService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RateLimitResult> CheckAssetAccessRateAsync(
        Guid assetReferenceId,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return new RateLimitResult(true, 0, _options.MaxAccessPerAssetPerHour);
        }

        var windowKey = GetWindowKey();
        var key = $"{AssetAccessKeyPrefix}{assetReferenceId}:{windowKey}";

        var currentCountStr = await _cache.GetStringAsync(key, ct).ConfigureAwait(false);
        var currentCount = string.IsNullOrEmpty(currentCountStr) ? 0 : int.Parse(currentCountStr);

        if (currentCount >= _options.MaxAccessPerAssetPerHour)
        {
            var retryAfter = TimeSpan.FromSeconds(_options.WindowSizeSeconds - GetSecondsIntoWindow());
            _logger.LogWarning(
                "Asset {AssetId} rate limit exceeded: {Count}/{Limit} per hour",
                assetReferenceId, currentCount, _options.MaxAccessPerAssetPerHour);

            return new RateLimitResult(
                false,
                currentCount,
                _options.MaxAccessPerAssetPerHour,
                retryAfter,
                "Asset access rate limit exceeded");
        }

        // Increment counter
        var newCount = currentCount + 1;
        await _cache.SetStringAsync(
            key,
            newCount.ToString(),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.WindowSizeSeconds)
            },
            ct);

        return new RateLimitResult(true, newCount, _options.MaxAccessPerAssetPerHour);
    }

    public async Task<RateLimitResult> Record403ResponseAsync(
        string ipAddress,
        CancellationToken ct = default)
    {
        if (!_options.Enabled || string.IsNullOrEmpty(ipAddress))
        {
            return new RateLimitResult(true, 0, _options.Max403PerIpPerHour);
        }

        var windowKey = GetWindowKey();
        var key = $"{Ip403KeyPrefix}{ipAddress}:{windowKey}";

        var currentCountStr = await _cache.GetStringAsync(key, ct).ConfigureAwait(false);
        var currentCount = string.IsNullOrEmpty(currentCountStr) ? 0 : int.Parse(currentCountStr);

        var newCount = currentCount + 1;
        await _cache.SetStringAsync(
            key,
            newCount.ToString(),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.WindowSizeSeconds)
            },
            ct);

        if (newCount >= _options.Max403PerIpPerHour)
        {
            // Block the IP
            var blockKey = $"{IpBlockKeyPrefix}{ipAddress}";
            await _cache.SetStringAsync(
                blockKey,
                SystemClock.UtcNow.ToString("O"),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.BlockDurationMinutes)
                },
                ct);

            _logger.LogWarning(
                "IP {IpAddress} blocked due to excessive 403 responses: {Count}/{Limit}",
                LogRedaction.Sanitize(ipAddress), newCount, _options.Max403PerIpPerHour);

            return new RateLimitResult(
                false,
                newCount,
                _options.Max403PerIpPerHour,
                TimeSpan.FromMinutes(_options.BlockDurationMinutes),
                "IP blocked due to excessive failed access attempts");
        }

        return new RateLimitResult(true, newCount, _options.Max403PerIpPerHour);
    }

    public async Task<bool> IsIpBlockedAsync(string ipAddress, CancellationToken ct = default)
    {
        if (!_options.Enabled || string.IsNullOrEmpty(ipAddress))
        {
            return false;
        }

        var blockKey = $"{IpBlockKeyPrefix}{ipAddress}";
        var blockedAt = await _cache.GetStringAsync(blockKey, ct).ConfigureAwait(false);
        return !string.IsNullOrEmpty(blockedAt);
    }

    public async Task<AssetAccessStats> GetAccessStatsAsync(
        Guid assetReferenceId,
        CancellationToken ct = default)
    {
        var windowKey = GetWindowKey();
        var key = $"{AssetAccessKeyPrefix}{assetReferenceId}:{windowKey}";

        var currentCountStr = await _cache.GetStringAsync(key, ct).ConfigureAwait(false);
        var currentCount = string.IsNullOrEmpty(currentCountStr) ? 0 : long.Parse(currentCountStr);

        return new AssetAccessStats(
            assetReferenceId,
            currentCount,
            currentCount, // Would need separate total counter for accurate total
            currentCount > 0 ? SystemClock.UtcNow : null);
    }

    private string GetWindowKey()
    {
        // Round to window start time
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.ToUnixTimeSeconds() / _options.WindowSizeSeconds * _options.WindowSizeSeconds;
        return windowStart.ToString();
    }

    private int GetSecondsIntoWindow()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return (int)(now % _options.WindowSizeSeconds);
    }
}
