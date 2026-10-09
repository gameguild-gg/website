using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Evaluation-layer rate limiting / enumeration protection (issue #358). Tracks
///     denied permission evaluations per (user, tenant) pair in a sliding window and,
///     once the configured deny limit is exceeded, fails further evaluations closed for
///     that pair until the throttle duration elapses.
/// </summary>
/// <remarks>
///     <para>
///         This is deliberately distinct from endpoint rate limiting: it never blocks
///         requests, it only makes <c>HasPermission*</c> style evaluation outcomes deny
///         (fail-closed) while a user+tenant pair is throttled. Legitimate allow checks
///         for a throttled pair also deny — an attacker probing the permission space
///         cannot distinguish probed denies from throttle denies, which is the point.
///     </para>
///     <para>
///         The default implementation is in-memory (per-process). Counters are trimmed
///         lazily on access; the tracker does not grow unbounded.
///     </para>
/// </remarks>
public interface IEvaluationDenialThrottleService
{
    /// <summary>
    ///     True when the (user, tenant) pair is currently throttled: evaluations must
    ///     fail closed without consulting the permission stores.
    /// </summary>
    bool IsThrottled(Guid userId, Guid tenantId);

    /// <summary>
    ///     Records one denied evaluation outcome. When the recorded deny count crosses
    ///     the configured window limit, the pair becomes throttled.
    /// </summary>
    /// <param name="userId">The evaluated user.</param>
    /// <param name="tenantId">The tenant scope of the evaluation.</param>
    /// <param name="evaluatedAt">Timestamp of the evaluation (defaults to now).</param>
    void RecordDenial(Guid userId, Guid tenantId, DateTime? evaluatedAt = null);
}

/// <summary>
///     In-memory sliding-window implementation of <see cref="IEvaluationDenialThrottleService"/>.
///     Thread-safe; configured through <see cref="EvaluationThrottleOptions"/>.
/// </summary>
public sealed class EvaluationDenialThrottleService(
    IOptions<PermissionEngineOptions> engineOptions,
    ILogger<EvaluationDenialThrottleService> logger) : IEvaluationDenialThrottleService
{
    private readonly object _sync = new();
    private readonly Dictionary<(Guid UserId, Guid TenantId), Queue<DateTime>> _denials = new();
    private readonly Dictionary<(Guid UserId, Guid TenantId), DateTime> _throttledUntil = new();

    private EvaluationThrottleOptions Options => engineOptions.Value.EvaluationThrottle;

    /// <inheritdoc />
    public bool IsThrottled(Guid userId, Guid tenantId)
    {
        if (!Options.Enabled)
        {
            return false;
        }

        lock (_sync)
        {
            if (!_throttledUntil.TryGetValue((userId, tenantId), out var until))
            {
                return false;
            }

            if (until > SystemClock.UtcNow)
            {
                return true;
            }

            // Throttle window elapsed: clear the state so the pair starts fresh.
            _throttledUntil.Remove((userId, tenantId));
            _denials.Remove((userId, tenantId));
            return false;
        }
    }

    /// <inheritdoc />
    public void RecordDenial(Guid userId, Guid tenantId, DateTime? evaluatedAt = null)
    {
        if (!Options.Enabled)
        {
            return;
        }

        var now = evaluatedAt ?? SystemClock.UtcNow;
        var windowStart = now.AddSeconds(-Options.WindowSeconds);
        var key = (userId, tenantId);

        lock (_sync)
        {
            if (!_denials.TryGetValue(key, out var window))
            {
                window = new Queue<DateTime>();
                _denials[key] = window;
            }

            window.Enqueue(now);
            while (window.Count > 0 && window.Peek() < windowStart)
            {
                window.Dequeue();
            }

            if (window.Count > Options.MaxDeniedEvaluationsPerWindow
                && !_throttledUntil.ContainsKey(key))
            {
                _throttledUntil[key] = now.AddSeconds(Options.ThrottleDurationSeconds);
                logger.LogWarning(
                    "Evaluation throttle tripped for user {UserId} in tenant {TenantId} after {DenyCount} denied evaluations in {WindowSeconds}s - failing evaluations closed for {ThrottleSeconds}s (enumeration protection).",
                    userId,
                    tenantId,
                    window.Count,
                    Options.WindowSeconds,
                    Options.ThrottleDurationSeconds);
            }

            TrimState();
        }
    }

    /// <summary>
    ///     Bounds memory: drops expired throttle entries and empty deny queues.
    ///     Called opportunistically (no background timer).
    /// </summary>
    private void TrimState()
    {
        var now = SystemClock.UtcNow;

        if (_throttledUntil.Count > 0)
        {
            var expired = _throttledUntil.Where(pair => pair.Value <= now)
                .Select(pair => pair.Key)
                .ToList();
            foreach (var key in expired)
            {
                _throttledUntil.Remove(key);
                _denials.Remove(key);
            }
        }

        if (_denials.Count > 0)
        {
            var windowStart = now.AddSeconds(-Options.WindowSeconds);
            var stale = _denials.Where(pair => pair.Value.Count == 0 || pair.Value.Peek() < windowStart)
                .Select(pair => pair.Key)
                .ToList();
            foreach (var key in stale)
            {
                if (!_throttledUntil.ContainsKey(key))
                {
                    _denials.Remove(key);
                }
            }
        }
    }
}
