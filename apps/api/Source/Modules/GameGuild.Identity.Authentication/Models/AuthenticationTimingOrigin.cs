using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authentication;

/// <summary>Server-created timing context. Elapsed time uses one monotonic provider throughout the attempt.</summary>
public sealed class AuthenticationTimingOrigin
{
    private static readonly object RequestOriginKey = new();
    private readonly TimeProvider clock;
    private readonly long started;
    private readonly TimeSpan elapsedBeforeOrigin;

    public AuthenticationTimingOrigin() : this(TimeProvider.System, TimeSpan.Zero) { }

    private AuthenticationTimingOrigin(TimeProvider clock, TimeSpan elapsedBeforeOrigin)
    {
        this.clock = clock;
        this.elapsedBeforeOrigin = elapsedBeforeOrigin;
        started = clock.GetTimestamp();
        StartedAtUtc = clock.GetUtcNow().UtcDateTime;
    }

    public DateTime StartedAtUtc { get; }

    public TimeSpan Elapsed => elapsedBeforeOrigin + clock.GetElapsedTime(started);

    internal TimeProvider Clock => clock;

    public static AuthenticationTimingOrigin Start() => Start(TimeProvider.System);

    public static AuthenticationTimingOrigin Start(TimeProvider? clock) => new(clock ?? TimeProvider.System, TimeSpan.Zero);

    /// <summary>Reuse the server-created request origin across admission filters and command dispatch.</summary>
    public static AuthenticationTimingOrigin GetOrStartForRequest(HttpContext? context) =>
        GetOrStartForRequest(context, TimeProvider.System);

    public static AuthenticationTimingOrigin GetOrStartForRequest(HttpContext? context, TimeProvider? clock)
    {
        if (context is null) { return Start(clock); }
        if (context.Items.TryGetValue(RequestOriginKey, out var existing) && existing is AuthenticationTimingOrigin origin)
        {
            return origin;
        }
        var created = Start(clock);
        context.Items[RequestOriginKey] = created;
        return created;
    }

    // Legacy callers have only UTC. The active host path uses Start, so wall-clock adjustments cannot change its budget.
    internal static AuthenticationTimingOrigin FromLegacyWallClock(DateTime startTime)
    {
        var elapsed = SystemClock.UtcNow - startTime;
        return new AuthenticationTimingOrigin(TimeProvider.System, elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero);
    }
}
