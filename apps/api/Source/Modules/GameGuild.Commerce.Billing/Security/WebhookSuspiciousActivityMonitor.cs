using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     In-process threshold monitor for suspicious webhook sources. Tracks security failures per
///     source in a sliding window and temporarily blocks sources that exceed the configured
///     threshold. Fail open: when disabled (default) or on internal error, nothing is blocked.
/// </summary>
public interface IWebhookSuspiciousActivityMonitor
{
    /// <summary>Whether the source is currently blocked.</summary>
    bool IsBlocked(string sourceKey, DateTime utcNow);

    /// <summary>
    ///     Records a security failure. Returns true when this failure crossed the threshold and
    ///     the source became blocked (callers should publish a SourceBlocked event).
    /// </summary>
    bool RegisterFailure(string sourceKey, DateTime utcNow);

    /// <summary>Snapshots the currently blocked sources (for the monitoring surface).</summary>
    IReadOnlyList<WebhookSourceBlockState> GetBlockedSources(DateTime utcNow);

    /// <summary>Removes expired tracking state. Safe to call opportunistically.</summary>
    void Prune(DateTime utcNow);
}

/// <summary>A currently blocked webhook source.</summary>
public sealed record WebhookSourceBlockState(string SourceKey, DateTime BlockedUntilUtc, int FailureCount);

public sealed class WebhookSuspiciousActivityMonitor(
    IOptions<BillingConfiguration> billingConfiguration,
    ILogger<WebhookSuspiciousActivityMonitor> logger) : IWebhookSuspiciousActivityMonitor
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<DateTime>> _failures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WebhookSourceBlockState> _blocks = new(StringComparer.Ordinal);
    private readonly WebhookSuspiciousActivitySettings _settings =
        billingConfiguration.Value.Webhook.Security.SuspiciousActivity;

    public bool IsBlocked(string sourceKey, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);

        if (!_settings.Enabled)
        {
            return false;
        }

        try
        {
            lock (_gate)
            {
                if (!_blocks.TryGetValue(sourceKey, out var block))
                {
                    return false;
                }

                if (block.BlockedUntilUtc > utcNow)
                {
                    return true;
                }

                _blocks.Remove(sourceKey);
                _failures.Remove(sourceKey);
                return false;
            }
        }
        catch (Exception exception)
        {
            // Fail open: a monitor malfunction must not block legitimate provider traffic.
            logger.LogError(exception, "Webhook suspicious-activity monitor failed while checking {SourceKey}", sourceKey);
            return false;
        }
    }

    public bool RegisterFailure(string sourceKey, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);

        if (!_settings.Enabled)
        {
            return false;
        }

        try
        {
            lock (_gate)
            {
                var windowStart = utcNow.AddSeconds(-Math.Max(1, _settings.WindowSeconds));
                if (!_failures.TryGetValue(sourceKey, out var timestamps))
                {
                    timestamps = [];
                    _failures[sourceKey] = timestamps;
                }

                timestamps.RemoveAll(timestamp => timestamp < windowStart);
                timestamps.Add(utcNow);

                if (timestamps.Count < Math.Max(1, _settings.FailureThreshold))
                {
                    return false;
                }

                if (_blocks.TryGetValue(sourceKey, out var existing) && existing.BlockedUntilUtc > utcNow)
                {
                    return false;
                }

                var blockedUntil = utcNow.AddSeconds(Math.Max(1, _settings.BlockDurationSeconds));
                _blocks[sourceKey] = new WebhookSourceBlockState(sourceKey, blockedUntil, timestamps.Count);
                return true;
            }
        }
        catch (Exception exception)
        {
            // Fail open: never turn a monitoring error into a block decision.
            logger.LogError(exception, "Webhook suspicious-activity monitor failed while registering a failure for {SourceKey}", sourceKey);
            return false;
        }
    }

    public IReadOnlyList<WebhookSourceBlockState> GetBlockedSources(DateTime utcNow)
    {
        if (!_settings.Enabled)
        {
            return [];
        }

        lock (_gate)
        {
            return _blocks.Values
                .Where(block => block.BlockedUntilUtc > utcNow)
                .OrderByDescending(block => block.BlockedUntilUtc)
                .ToList();
        }
    }

    public void Prune(DateTime utcNow)
    {
        lock (_gate)
        {
            var expiredBlocks = _blocks
                .Where(pair => pair.Value.BlockedUntilUtc <= utcNow)
                .Select(pair => pair.Key)
                .ToList();
            foreach (var key in expiredBlocks)
            {
                _blocks.Remove(key);
                _failures.Remove(key);
            }

            var staleWindows = _failures
                .Where(pair => pair.Value.All(timestamp => timestamp < utcNow.AddSeconds(-Math.Max(1, _settings.WindowSeconds))))
                .Select(pair => pair.Key)
                .ToList();
            foreach (var key in staleWindows)
            {
                _failures.Remove(key);
            }
        }
    }
}
