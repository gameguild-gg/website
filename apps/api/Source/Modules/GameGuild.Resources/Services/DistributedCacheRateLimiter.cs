using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace GameGuild.Resources;

/// <summary>
///     Portable distributed-cache rate limiter used when a raw Redis connection is not configured.
/// </summary>
public sealed class DistributedCacheRateLimiter(
    IDistributedCache cache,
    ILogger<DistributedCacheRateLimiter> logger) : IDistributedRateLimiter
{
    private const string KeyPrefix = "ratelimit:";
    private static readonly object ConcurrencyLock = new();
    private static readonly Dictionary<string, Dictionary<string, DateTimeOffset>> ConcurrencyLeases = new(StringComparer.Ordinal);

    public Task<bool> IsAllowedFixedWindowAsync(string key, int maxRequests, TimeSpan window)
        => IsAllowedFixedWindowAsync(key, maxRequests, window, CancellationToken.None);

    public Task<bool> IsAllowedFixedWindowAsync(
        string key,
        int maxRequests,
        TimeSpan window,
        CancellationToken cancellationToken)
        => IsAllowedAsync(key, maxRequests, window, cancellationToken);

    public Task<RateLimitDecision> TryAcquireTokenBucketAsync(
        string key,
        int tokenLimit,
        int tokensPerPeriod,
        TimeSpan replenishmentPeriod)
        => TryAcquireTokenBucketAsync(key, tokenLimit, tokensPerPeriod, replenishmentPeriod, CancellationToken.None);

    public async Task<RateLimitDecision> TryAcquireTokenBucketAsync(
        string key,
        int tokenLimit,
        int tokensPerPeriod,
        TimeSpan replenishmentPeriod,
        CancellationToken cancellationToken)
    {
        ValidateTokenBucket(key, tokenLimit, tokensPerPeriod, replenishmentPeriod);
        cancellationToken.ThrowIfCancellationRequested();

        var periodMilliseconds = checked((long)Math.Ceiling(replenishmentPeriod.TotalMilliseconds));
        var stepsToFill = ((long)tokenLimit + tokensPerPeriod - 1) / tokensPerPeriod;
        var retentionMilliseconds = checked(periodMilliseconds * (stepsToFill + 1));
        var cacheKey = $"{KeyPrefix}token-bucket:{key}:{tokenLimit}:{tokensPerPeriod}:{periodMilliseconds}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        try
        {
            var state = await cache.GetStringAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            var tokens = tokenLimit;
            var lastRefill = now;
            if (!string.IsNullOrWhiteSpace(state))
            {
                var parts = state.Split('|', 2);
                if (parts.Length == 2 && int.TryParse(parts[0], out var cachedTokens) && long.TryParse(parts[1], out var cachedRefill))
                {
                    tokens = Math.Clamp(cachedTokens, 0, tokenLimit);
                    lastRefill = Math.Min(cachedRefill, now);
                }
                else
                {
                    logger.LogWarning("Ignoring malformed token-bucket state for key {Key}", LogRedaction.Sanitize(key));
                }
            }

            var elapsedMilliseconds = Math.Max(0, now - lastRefill);
            var completedPeriods = elapsedMilliseconds / periodMilliseconds;
            if (completedPeriods > 0)
            {
                tokens = (int)Math.Min(tokenLimit, (long)tokens + completedPeriods * tokensPerPeriod);
                lastRefill += completedPeriods * periodMilliseconds;
            }

            var allowed = tokens > 0;
            if (allowed)
            {
                tokens--;
            }

            var retryAfter = allowed
                ? TimeSpan.Zero
                : TimeSpan.FromMilliseconds(Math.Max(1, periodMilliseconds - (now - lastRefill)));
            await cache.SetStringAsync(
                cacheKey,
                $"{tokens}|{lastRefill}",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(retentionMilliseconds)
                },
                cancellationToken).ConfigureAwait(false);

            return new RateLimitDecision(allowed, retryAfter);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Distributed cache token-bucket error for key {Key}. Allowing request (fail-open for availability).", LogRedaction.Sanitize(key));
            return new RateLimitDecision(true, TimeSpan.Zero);
        }
    }

    public Task<ConcurrencyLimitDecision> TryAcquireConcurrencyLeaseAsync(
        string key,
        int maxConcurrent,
        TimeSpan leaseDuration)
        => TryAcquireConcurrencyLeaseAsync(key, maxConcurrent, leaseDuration, CancellationToken.None);

    public Task<ConcurrencyLimitDecision> TryAcquireConcurrencyLeaseAsync(
        string key,
        int maxConcurrent,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (maxConcurrent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrent), "The concurrency limit must be greater than zero.");
        }

        if (leaseDuration.TotalMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "The concurrency lease duration must be at least one millisecond.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (ConcurrencyLock)
        {
            var now = DateTimeOffset.UtcNow;
            if (!ConcurrencyLeases.TryGetValue(key, out var leases))
            {
                leases = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
                ConcurrencyLeases.Add(key, leases);
            }

            foreach (var expiredLease in leases.Where(lease => lease.Value <= now).Select(lease => lease.Key).ToArray())
            {
                leases.Remove(expiredLease);
            }

            if (leases.Count >= maxConcurrent)
            {
                var nextExpiry = leases.Values.Min();
                return Task.FromResult(new ConcurrencyLimitDecision(false, nextExpiry - now));
            }

            var leaseId = Guid.NewGuid().ToString("N");
            leases[leaseId] = now + leaseDuration;
            var lease = new LocalConcurrencyLease(key, leaseId, leaseDuration, logger);
            return Task.FromResult(new ConcurrencyLimitDecision(true, TimeSpan.Zero, lease));
        }
    }

    public Task<TimeSpan?> GetActivePenaltyAsync(string key)
        => GetActivePenaltyAsync(key, CancellationToken.None);

    public async Task<TimeSpan?> GetActivePenaltyAsync(
        string key,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        try
        {
            var expiration = await cache.GetStringAsync($"{KeyPrefix}penalty:block:{key}", cancellationToken).ConfigureAwait(false);
            if (!long.TryParse(expiration, out var expirationMilliseconds))
            {
                return null;
            }

            var remainingMilliseconds = expirationMilliseconds - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return remainingMilliseconds > 0 ? TimeSpan.FromMilliseconds(remainingMilliseconds) : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Distributed cache rate-limit penalty lookup failed for key {Key}; allowing request (fail-open for availability).", key);
            return null;
        }
    }

    public Task<TimeSpan?> RecordRateLimitViolationAsync(
        string key,
        int violationThreshold,
        TimeSpan decayWindow,
        TimeSpan basePenalty,
        TimeSpan maxPenalty)
        => RecordRateLimitViolationAsync(key, violationThreshold, decayWindow, basePenalty, maxPenalty, CancellationToken.None);

    public async Task<TimeSpan?> RecordRateLimitViolationAsync(
        string key,
        int violationThreshold,
        TimeSpan decayWindow,
        TimeSpan basePenalty,
        TimeSpan maxPenalty,
        CancellationToken cancellationToken)
    {
        ValidatePenalty(key, violationThreshold, decayWindow, basePenalty, maxPenalty);
        try
        {
            var violationKey = $"{KeyPrefix}penalty:violations:{key}";
            var currentValue = await cache.GetStringAsync(violationKey, cancellationToken).ConfigureAwait(false);
            var violations = int.TryParse(currentValue, out var currentViolations) ? currentViolations + 1 : 1;
            await cache.SetStringAsync(
                violationKey,
                violations.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = decayWindow },
                cancellationToken).ConfigureAwait(false);

            if (violations < violationThreshold)
            {
                return null;
            }

            var exponent = Math.Min(violations - violationThreshold, 30);
            var penaltyMilliseconds = Math.Min(maxPenalty.TotalMilliseconds, basePenalty.TotalMilliseconds * Math.Pow(2, exponent));
            var penalty = TimeSpan.FromMilliseconds(penaltyMilliseconds);
            var expiration = DateTimeOffset.UtcNow.Add(penalty).ToUnixTimeMilliseconds();
            await cache.SetStringAsync(
                $"{KeyPrefix}penalty:block:{key}",
                expiration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = penalty },
                cancellationToken).ConfigureAwait(false);
            return penalty;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Distributed cache rate-limit penalty update failed for key {Key}; keeping the base rate limit only (fail-open for availability).", LogRedaction.Sanitize(key));
            return null;
        }
    }

    public async Task<bool> IsAllowedAsync(string key, int maxRequests, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetWindowKey(key, window);
        var count = await GetCountAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (count >= maxRequests)
        {
            logger.LogWarning("Rate limit exceeded for key {Key}: {CurrentCount}/{MaxRequests} in {Window}", LogRedaction.Sanitize(key), count, maxRequests, window);
            return false;
        }

        await SetCountAsync(cacheKey, count + 1, window, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> GetCurrentCountAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        return await GetCountAsync(GetWindowKey(key, window), cancellationToken).ConfigureAwait(false);
    }

    public Task<TimeSpan?> GetTimeUntilResetAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowMs = (long)window.TotalMilliseconds;
        var windowStart = now / windowMs * windowMs;
        var resetAt = windowStart + windowMs;
        return Task.FromResult<TimeSpan?>(TimeSpan.FromMilliseconds(Math.Max(0, resetAt - now)));
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        var wildcardKey = $"{KeyPrefix}{key}:";
        logger.LogInformation("Distributed cache limiter reset requested for {KeyPrefix}; specific rolling-window keys expire automatically.", LogRedaction.Sanitize(wildcardKey));
        return Task.CompletedTask;
    }

    private async Task<int> GetCountAsync(string key, CancellationToken cancellationToken)
    {
        var value = await cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
        return int.TryParse(value, out var count) ? count : 0;
    }

    private Task SetCountAsync(string key, int count, TimeSpan window, CancellationToken cancellationToken)
    {
        return cache.SetStringAsync(
            key,
            count.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = window.Add(TimeSpan.FromMinutes(1)) },
            cancellationToken);
    }

    private static string GetWindowKey(string key, TimeSpan window)
    {
        var windowMs = (long)window.TotalMilliseconds;
        var windowStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / windowMs * windowMs;
        return $"{KeyPrefix}{key}:{windowStart}";
    }

    private static void ValidateTokenBucket(string key, int tokenLimit, int tokensPerPeriod, TimeSpan replenishmentPeriod)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (tokenLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenLimit), "The token limit must be greater than zero.");
        }

        if (tokensPerPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokensPerPeriod), "The replenishment token count must be greater than zero.");
        }

        if (replenishmentPeriod.TotalMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(replenishmentPeriod), "The replenishment period must be at least one millisecond.");
        }
    }

    private static void ValidatePenalty(
        string key,
        int violationThreshold,
        TimeSpan decayWindow,
        TimeSpan basePenalty,
        TimeSpan maxPenalty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (violationThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(violationThreshold), "The violation threshold must be greater than zero.");
        }

        if (decayWindow.TotalMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(decayWindow), "The penalty decay window must be at least one millisecond.");
        }

        if (basePenalty.TotalMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(basePenalty), "The base penalty must be at least one millisecond.");
        }

        if (maxPenalty < basePenalty)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPenalty), "The maximum penalty must not be smaller than the base penalty.");
        }
    }

    private sealed class LocalConcurrencyLease(
        string key,
        string leaseId,
        TimeSpan duration,
        ILogger logger) : IAsyncDisposable
    {
        private readonly Timer _renewalTimer = new(
            _ => Renew(key, leaseId, duration, logger),
            null,
            GetRenewalInterval(duration),
            GetRenewalInterval(duration));
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _renewalTimer.Dispose();
                lock (ConcurrencyLock)
                {
                    if (ConcurrencyLeases.TryGetValue(key, out var leases))
                    {
                        leases.Remove(leaseId);
                        if (leases.Count == 0)
                        {
                            ConcurrencyLeases.Remove(key);
                        }
                    }
                }
            }

            return ValueTask.CompletedTask;
        }

        private static void Renew(string leaseKey, string id, TimeSpan leaseDuration, ILogger leaseLogger)
        {
            lock (ConcurrencyLock)
            {
                if (ConcurrencyLeases.TryGetValue(leaseKey, out var leases) && leases.ContainsKey(id))
                {
                    leases[id] = DateTimeOffset.UtcNow + leaseDuration;
                }
                else
                {
                    leaseLogger.LogWarning("Local concurrency lease {LeaseId} expired before renewal.", id);
                }
            }
        }

        private static TimeSpan GetRenewalInterval(TimeSpan duration)
        {
            var milliseconds = Math.Clamp((long)Math.Floor(duration.TotalMilliseconds / 3), 1, 30_000);
            return TimeSpan.FromMilliseconds(milliseconds);
        }
    }
}
