using GameGuild.Configuration.PresentationLayer.RateLimiting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameGuild.Resources;

/// <summary>
///     Redis-backed distributed rate limiter using sliding window algorithm
/// </summary>
public class RedisDistributedRateLimiter(
    IConnectionMultiplexer redis,
    ILogger<RedisDistributedRateLimiter> logger,
    RateLimitingOptions? options = null) : IDistributedRateLimiter
{
    private const string FixedWindowAdmissionScript = """
        local maxRequests = tonumber(ARGV[1])
        local ttlMilliseconds = tonumber(ARGV[2])
        local currentCount = tonumber(redis.call('GET', KEYS[1]) or '0')
        local remainingMilliseconds = redis.call('PTTL', KEYS[1])

        if currentCount >= maxRequests then
            return { 0, currentCount, math.max(0, remainingMilliseconds) }
        end

        currentCount = redis.call('INCR', KEYS[1])
        if currentCount == 1 or remainingMilliseconds < 0 then
            redis.call('PEXPIRE', KEYS[1], ttlMilliseconds)
        end

        return { 1, currentCount, math.max(0, redis.call('PTTL', KEYS[1])) }
        """;

    private const string TokenBucketAdmissionScript = """
        local capacity = tonumber(ARGV[1])
        local refillTokens = tonumber(ARGV[2])
        local refillPeriodMilliseconds = tonumber(ARGV[3])
        local retentionMilliseconds = tonumber(ARGV[4])
        local redisTime = redis.call('TIME')
        local now = tonumber(redisTime[1]) * 1000 + math.floor(tonumber(redisTime[2]) / 1000)
        local state = redis.call('HMGET', KEYS[1], 'tokens', 'lastRefill')
        local tokens = tonumber(state[1])
        local lastRefill = tonumber(state[2])

        if not tokens or not lastRefill then
            tokens = capacity
            lastRefill = now
        end

        local elapsed = math.max(0, now - lastRefill)
        local completedPeriods = math.floor(elapsed / refillPeriodMilliseconds)
        if completedPeriods > 0 then
            tokens = math.min(capacity, tokens + completedPeriods * refillTokens)
            lastRefill = lastRefill + completedPeriods * refillPeriodMilliseconds
        end

        local allowed = 0
        local retryAfterMilliseconds = 0
        if tokens >= 1 then
            tokens = tokens - 1
            allowed = 1
        else
            retryAfterMilliseconds = math.max(1, refillPeriodMilliseconds - (now - lastRefill))
        end

        redis.call('HSET', KEYS[1], 'tokens', tokens, 'lastRefill', lastRefill)
        redis.call('PEXPIRE', KEYS[1], retentionMilliseconds)
        return { allowed, retryAfterMilliseconds, tokens }
        """;

    private const string ConcurrencyLeaseAdmissionScript = """
        local nowTime = redis.call('TIME')
        local now = tonumber(nowTime[1]) * 1000 + math.floor(tonumber(nowTime[2]) / 1000)
        local maxConcurrent = tonumber(ARGV[1])
        local leaseMilliseconds = tonumber(ARGV[2])
        local leaseId = ARGV[3]

        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', now)
        local active = redis.call('ZCARD', KEYS[1])
        if active >= maxConcurrent then
            local oldest = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
            local retryAfterMilliseconds = 1
            if oldest[2] then
                retryAfterMilliseconds = math.max(1, tonumber(oldest[2]) - now)
            end
            return { 0, retryAfterMilliseconds }
        end

        redis.call('ZADD', KEYS[1], now + leaseMilliseconds, leaseId)
        redis.call('PEXPIRE', KEYS[1], leaseMilliseconds + 60000)
        return { 1, 0 }
        """;

    private const string ConcurrencyLeaseRenewalScript = """
        local nowTime = redis.call('TIME')
        local now = tonumber(nowTime[1]) * 1000 + math.floor(tonumber(nowTime[2]) / 1000)
        local leaseMilliseconds = tonumber(ARGV[1])
        local leaseId = ARGV[2]
        local expiresAt = redis.call('ZSCORE', KEYS[1], leaseId)

        if not expiresAt or tonumber(expiresAt) <= now then
            redis.call('ZREM', KEYS[1], leaseId)
            return 0
        end

        redis.call('ZADD', KEYS[1], 'XX', now + leaseMilliseconds, leaseId)
        redis.call('PEXPIRE', KEYS[1], leaseMilliseconds + 60000)
        return 1
        """;

    private const string ConcurrencyLeaseReleaseScript = """
        return redis.call('ZREM', KEYS[1], ARGV[1])
        """;

    private const string ActivePenaltyScript = """
        local remaining = redis.call('PTTL', KEYS[1])
        if remaining > 0 then
            return remaining
        end
        return 0
        """;

    private const string RecordPenaltyViolationScript = """
        local threshold = tonumber(ARGV[1])
        local decayMilliseconds = tonumber(ARGV[2])
        local basePenaltyMilliseconds = tonumber(ARGV[3])
        local maxPenaltyMilliseconds = tonumber(ARGV[4])
        local violations = redis.call('INCR', KEYS[1])

        if violations == 1 then
            redis.call('PEXPIRE', KEYS[1], decayMilliseconds)
        end

        if violations < threshold then
            return 0
        end

        local exponent = math.min(violations - threshold, 30)
        local penaltyMilliseconds = math.min(maxPenaltyMilliseconds, basePenaltyMilliseconds * (2 ^ exponent))
        redis.call('SET', KEYS[2], '1', 'PX', penaltyMilliseconds)
        return penaltyMilliseconds
        """;

    private const string AllowRequestScript = """
        local now = tonumber(ARGV[1])
        local windowMilliseconds = tonumber(ARGV[2])
        local maxRequests = tonumber(ARGV[3])
        local requestId = ARGV[4]
        local windowStart = now - windowMilliseconds

        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', windowStart)
        local currentCount = redis.call('ZCARD', KEYS[1])

        if currentCount >= maxRequests then
            local oldest = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
            local retryAfterMilliseconds = 0
            if oldest[2] then
                retryAfterMilliseconds = math.max(0, tonumber(oldest[2]) + windowMilliseconds - now)
            end
            return { 0, currentCount, retryAfterMilliseconds }
        end

        redis.call('ZADD', KEYS[1], now, requestId)
        redis.call('PEXPIRE', KEYS[1], windowMilliseconds + 60000)
        return { 1, currentCount + 1, 0 }
        """;

    private readonly IConnectionMultiplexer _redis = redis;
    private readonly RedisRateLimitFailureMode _failureMode = options?.RedisFailureMode ?? RedisRateLimitFailureMode.FailOpen;
    private const string KeyPrefix = "ratelimit:";

    public Task<bool> IsAllowedFixedWindowAsync(string key, int maxRequests, TimeSpan window)
        => IsAllowedFixedWindowAsync(key, maxRequests, window, CancellationToken.None);

    public async Task<bool> IsAllowedFixedWindowAsync(
        string key,
        int maxRequests,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        ValidateRequest(key, maxRequests, window);
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowMilliseconds = checked((long)Math.Ceiling(window.TotalMilliseconds));
        var windowStart = now / windowMilliseconds * windowMilliseconds;
        var ttlMilliseconds = windowMilliseconds - now % windowMilliseconds;
        var redisKey = GetRedisKey($"fixed:{key}:{windowStart}");

        try
        {
            var result = (RedisResult[]?)await _redis.GetDatabase().ScriptEvaluateAsync(
                FixedWindowAdmissionScript,
                [redisKey],
                [maxRequests, ttlMilliseconds]).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Redis returned no fixed-window result for the rate-limit script.");
            if (result.Length != 3)
            {
                throw new InvalidOperationException("Redis returned an invalid fixed-window result for the rate-limit script.");
            }

            var allowed = result[0].ToString() == "1";
            var currentCount = long.Parse(result[1].ToString()!, System.Globalization.CultureInfo.InvariantCulture);
            if (!allowed)
            {
                var retryAfterMilliseconds = long.Parse(result[2].ToString()!, System.Globalization.CultureInfo.InvariantCulture);
                logger.LogWarning(
                    "Fixed-window rate limit exceeded for key {Key}: {CurrentCount}/{MaxRequests} in {Window}; retry after {RetryAfterMilliseconds}ms",
                    LogRedaction.Sanitize(key), currentCount, maxRequests, window, retryAfterMilliseconds);
                return false;
            }

            logger.LogDebug("Fixed-window rate limit passed for key {Key}: {CurrentCount}/{MaxRequests}",
                LogRedaction.Sanitize(key), currentCount, maxRequests);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            HandleRedisFailure(ex, "fixed-window admission", key);
            return true;
        }
    }

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
        var replenishmentSteps = ((long)tokenLimit + tokensPerPeriod - 1) / tokensPerPeriod;
        var retentionMilliseconds = checked(periodMilliseconds * (replenishmentSteps + 1));
        var redisKey = GetRedisKey($"token-bucket:{key}");

        try
        {
            var result = (RedisResult[]?)await _redis.GetDatabase().ScriptEvaluateAsync(
                TokenBucketAdmissionScript,
                [redisKey],
                [tokenLimit, tokensPerPeriod, periodMilliseconds, retentionMilliseconds]).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Redis returned no token-bucket result for the rate-limit script.");
            if (result.Length != 3)
            {
                throw new InvalidOperationException("Redis returned an invalid token-bucket result for the rate-limit script.");
            }

            var allowed = result[0].ToString() == "1";
            var retryAfterMilliseconds = long.Parse(result[1].ToString()!, System.Globalization.CultureInfo.InvariantCulture);
            var decision = new RateLimitDecision(allowed, TimeSpan.FromMilliseconds(Math.Max(0, retryAfterMilliseconds)));
            if (!allowed)
            {
                logger.LogWarning(
                    "Token-bucket rate limit exceeded for key {Key}; retry after {RetryAfterMilliseconds}ms",
                    LogRedaction.Sanitize(key),
                    retryAfterMilliseconds);
                return decision;
            }

            logger.LogDebug("Token-bucket rate limit passed for key {Key}", LogRedaction.Sanitize(key));
            return decision;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            HandleRedisFailure(ex, "token-bucket admission", key);
            return new RateLimitDecision(true, TimeSpan.Zero);
        }
    }

    public Task<ConcurrencyLimitDecision> TryAcquireConcurrencyLeaseAsync(
        string key,
        int maxConcurrent,
        TimeSpan leaseDuration)
        => TryAcquireConcurrencyLeaseAsync(key, maxConcurrent, leaseDuration, CancellationToken.None);

    public async Task<ConcurrencyLimitDecision> TryAcquireConcurrencyLeaseAsync(
        string key,
        int maxConcurrent,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ValidateConcurrencyLease(key, maxConcurrent, leaseDuration);
        cancellationToken.ThrowIfCancellationRequested();

        var leaseMilliseconds = checked((long)Math.Ceiling(leaseDuration.TotalMilliseconds));
        var redisKey = GetRedisKey($"concurrency:{key}");
        var leaseId = Guid.NewGuid().ToString("N");

        try
        {
            var result = (RedisResult[]?)await _redis.GetDatabase().ScriptEvaluateAsync(
                ConcurrencyLeaseAdmissionScript,
                [redisKey],
                [maxConcurrent, leaseMilliseconds, leaseId]).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Redis returned no concurrency-lease result.");
            if (result.Length != 2)
            {
                throw new InvalidOperationException("Redis returned an invalid concurrency-lease result.");
            }

            var allowed = result[0].ToString() == "1";
            var retryAfterMilliseconds = long.Parse(result[1].ToString()!, System.Globalization.CultureInfo.InvariantCulture);
            if (!allowed)
            {
                logger.LogWarning(
                    "Distributed concurrency limit exceeded for key {Key}: maximum {MaxConcurrent}; retry after {RetryAfterMilliseconds}ms",
                    LogRedaction.Sanitize(key),
                    maxConcurrent,
                    retryAfterMilliseconds);
                return new ConcurrencyLimitDecision(false, TimeSpan.FromMilliseconds(retryAfterMilliseconds));
            }

            var lease = new RedisConcurrencyLease(_redis, redisKey, leaseId, leaseDuration, logger);
            return new ConcurrencyLimitDecision(true, TimeSpan.Zero, lease);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            HandleRedisFailure(ex, "concurrency admission", key);
            return new ConcurrencyLimitDecision(true, TimeSpan.Zero);
        }
    }

    public Task<TimeSpan?> GetActivePenaltyAsync(string key)
        => GetActivePenaltyAsync(key, CancellationToken.None);

    public async Task<TimeSpan?> GetActivePenaltyAsync(
        string key,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var remaining = await _redis.GetDatabase().ScriptEvaluateAsync(
                ActivePenaltyScript,
                [GetRedisKey($"penalty:block:{key}")]).ConfigureAwait(false);
            var remainingMilliseconds = long.Parse(remaining.ToString()!, System.Globalization.CultureInfo.InvariantCulture);
            return remainingMilliseconds > 0 ? TimeSpan.FromMilliseconds(remainingMilliseconds) : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            HandleRedisFailure(ex, "penalty lookup", key);
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
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = await _redis.GetDatabase().ScriptEvaluateAsync(
                RecordPenaltyViolationScript,
                [GetRedisKey($"penalty:violations:{key}"), GetRedisKey($"penalty:block:{key}")],
                [
                    violationThreshold,
                    checked((long)Math.Ceiling(decayWindow.TotalMilliseconds)),
                    checked((long)Math.Ceiling(basePenalty.TotalMilliseconds)),
                    checked((long)Math.Ceiling(maxPenalty.TotalMilliseconds))
                ]).ConfigureAwait(false);
            var penaltyMilliseconds = long.Parse(result.ToString()!, System.Globalization.CultureInfo.InvariantCulture);
            return penaltyMilliseconds > 0 ? TimeSpan.FromMilliseconds(penaltyMilliseconds) : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            HandleRedisFailure(ex, "penalty update", key);
            return null;
        }
    }

    public async Task<bool> IsAllowedAsync(string key, int maxRequests, TimeSpan window, CancellationToken cancellationToken = default)
    {
        ValidateRequest(key, maxRequests, window);
        cancellationToken.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var redisKey = GetRedisKey(key);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowMilliseconds = checked((long)Math.Ceiling(window.TotalMilliseconds));

        try
        {
            // Keep prune, count, admission and insert in one server-side operation so
            // concurrent API instances cannot admit more requests than the limit.
            var result = (RedisResult[]?)await db.ScriptEvaluateAsync(
                AllowRequestScript,
                [redisKey],
                [now, windowMilliseconds, maxRequests, Guid.NewGuid().ToString("N")]).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Redis returned no admission result for the rate-limit script.");
            if (result.Length != 3)
            {
                throw new InvalidOperationException("Redis returned an invalid admission result for the rate-limit script.");
            }

            var allowed = result[0].ToString() == "1";
            var currentCount = long.Parse(result[1].ToString()!, System.Globalization.CultureInfo.InvariantCulture);

            if (!allowed)
            {
                var retryAfterMilliseconds = long.Parse(result[2].ToString()!, System.Globalization.CultureInfo.InvariantCulture);
                logger.LogWarning(
                    "Rate limit exceeded for key {Key}: {CurrentCount}/{MaxRequests} in {Window}; retry after {RetryAfterMilliseconds}ms",
                    LogRedaction.Sanitize(key), currentCount, maxRequests, window, retryAfterMilliseconds);
                return false;
            }

            logger.LogDebug("Rate limit check passed for key {Key}: {CurrentCount}/{MaxRequests}",
                LogRedaction.Sanitize(key), currentCount, maxRequests);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            HandleRedisFailure(ex, "sliding-window admission", key);
            return true;
        }
    }

    public async Task<int> GetCurrentCountAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var redisKey = GetRedisKey(key);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)window.TotalMilliseconds;

        try
        {
            // Remove expired entries first
            await db.SortedSetRemoveRangeByScoreAsync(redisKey, 0, windowStart).ConfigureAwait(false);

            // Count current requests
            var count = await db.SortedSetLengthAsync(redisKey).ConfigureAwait(false);
            return (int)count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current count for key {Key}", LogRedaction.Sanitize(key));
            return 0;
        }
    }

    public async Task<TimeSpan?> GetTimeUntilResetAsync(string key, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var redisKey = GetRedisKey(key);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)window.TotalMilliseconds;

        try
        {
            // Get oldest entry in current window
            var oldestEntries = await db.SortedSetRangeByScoreWithScoresAsync(
                redisKey,
                start: windowStart,
                stop: double.PositiveInfinity,
                take: 1).ConfigureAwait(false);

            if (oldestEntries.Length == 0)
            {
                return null;
            }

            var oldestTimestamp = (long)oldestEntries[0].Score;
            var resetTime = oldestTimestamp + (long)window.TotalMilliseconds;
            var timeUntilReset = resetTime - now;

            return timeUntilReset > 0 ? TimeSpan.FromMilliseconds(timeUntilReset) : TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting reset time for key {Key}", LogRedaction.Sanitize(key));
            return null;
        }
    }

    public async Task ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var redisKey = GetRedisKey(key);

        try
        {
            await db.KeyDeleteAsync(redisKey).ConfigureAwait(false);
            await db.KeyDeleteAsync(GetRedisKey($"token-bucket:{key}")).ConfigureAwait(false);
            await db.KeyDeleteAsync(GetRedisKey($"concurrency:{key}")).ConfigureAwait(false);
            logger.LogInformation("Rate limit reset for key {Key}", LogRedaction.Sanitize(key));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting rate limit for key {Key}", LogRedaction.Sanitize(key));
            throw;
        }
    }

    private static string GetRedisKey(string key) => $"{KeyPrefix}{key}";

    private void HandleRedisFailure(Exception exception, string operation, string key)
    {
        logger.LogError(
            exception,
            "Redis rate-limit {Operation} failed for key {Key}; configured failure mode is {FailureMode}.",
            operation,
            LogRedaction.Sanitize(key),
            _failureMode);

        if (_failureMode == RedisRateLimitFailureMode.FailClosed)
        {
            throw new RateLimitBackendUnavailableException(operation, exception);
        }
    }

    private static void ValidateRequest(string key, int maxRequests, TimeSpan window)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (maxRequests <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRequests), "The request limit must be greater than zero.");
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "The rate-limit window must be greater than zero.");
        }

        if (window.TotalMilliseconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "The rate-limit window must be at least one millisecond.");
        }
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

    private static void ValidateConcurrencyLease(string key, int maxConcurrent, TimeSpan leaseDuration)
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

    private sealed class RedisConcurrencyLease : IAsyncDisposable
    {
        private readonly CancellationTokenSource _stopRenewal = new();
        private readonly IConnectionMultiplexer _redis;
        private readonly string _redisKey;
        private readonly string _leaseId;
        private readonly ILogger _logger;
        private readonly Task _renewalTask;
        private int _disposed;

        public RedisConcurrencyLease(
            IConnectionMultiplexer redis,
            string redisKey,
            string leaseId,
            TimeSpan leaseDuration,
            ILogger logger)
        {
            _redis = redis;
            _redisKey = redisKey;
            _leaseId = leaseId;
            _logger = logger;
            _renewalTask = RenewAsync(redis, redisKey, leaseId, leaseDuration, logger, _stopRenewal.Token);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _stopRenewal.Cancel();
            try
            {
                await _renewalTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stopRenewal.IsCancellationRequested)
            {
                _logger.LogDebug("Redis concurrency lease renewal stopped during disposal for {LeaseId}.", _leaseId);
            }

            try
            {
                await _redis.GetDatabase().ScriptEvaluateAsync(
                    ConcurrencyLeaseReleaseScript,
                    [_redisKey],
                    [_leaseId]).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release Redis concurrency lease {LeaseId}; it will expire automatically.", _leaseId);
            }
            finally
            {
                _stopRenewal.Dispose();
            }
        }

        private static async Task RenewAsync(
            IConnectionMultiplexer redis,
            string redisKey,
            string leaseId,
            TimeSpan leaseDuration,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var intervalMilliseconds = Math.Clamp((long)Math.Floor(leaseDuration.TotalMilliseconds / 3), 1, 30_000);
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMilliseconds));
            var leaseMilliseconds = checked((long)Math.Ceiling(leaseDuration.TotalMilliseconds));

            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        var renewed = await redis.GetDatabase().ScriptEvaluateAsync(
                            ConcurrencyLeaseRenewalScript,
                            [redisKey],
                            [leaseMilliseconds, leaseId]).ConfigureAwait(false);
                        if (renewed.ToString() != "1")
                        {
                            logger.LogWarning("Redis concurrency lease {LeaseId} expired before renewal.", leaseId);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not renew Redis concurrency lease {LeaseId}; retrying before its expiry.", leaseId);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug("Redis concurrency lease renewal stopped for {LeaseId}.", leaseId);
            }
        }
    }
}
