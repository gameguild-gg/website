using System.Diagnostics.Metrics;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Session analytics metrics. Metric names (documented in docs/security/session-management-metrics.md):
///     <list type="bullet">
///         <item><c>authentication.sessions.created</c> — counter of persisted session creations.</item>
///         <item><c>authentication.sessions.terminated</c> — counter, tag <c>reason</c> (lowercased <see cref="SessionTerminationReason"/>).</item>
///         <item><c>authentication.sessions.active</c> — observable gauge tracking the in-process balance of created minus terminated sessions.</item>
///     </list>
///     Process counters with bounded tags; database evidence remains the authoritative history.
///     The active gauge is per-process: horizontal replicas each report their own balance, so
///     the fleet-wide active count is the sum across instances. A failing in-process telemetry
///     listener must never change session lifecycle behavior — every emission is fail-open.
/// </summary>
public static class SessionAnalyticsMetrics
{
    /// <summary>The meter name registered with the OpenTelemetry runtime.</summary>
    public static string MeterName => "GameGuild.Identity.Authentication.Sessions";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Creations = Meter.CreateCounter<long>(
        "authentication.sessions.created", "{session}", "Sessions created and persisted.");
    private static readonly Counter<long> Terminations = Meter.CreateCounter<long>(
        "authentication.sessions.terminated", "{session}", "Sessions terminated, tagged by termination reason.");
    private static long _activeBalance;

    static SessionAnalyticsMetrics()
    {
        Meter.CreateObservableGauge(
            "authentication.sessions.active",
            observeValue: () => Volatile.Read(ref _activeBalance),
            unit: "{session}",
            description: "In-process balance of created minus terminated sessions (sum across replicas for the fleet total).");
    }

    /// <summary>Records a persisted session creation.</summary>
    public static void RecordCreated()
    {
        try
        {
            Creations.Add(1);
            Interlocked.Increment(ref _activeBalance);
        }
        catch (Exception)
        {
            // Fail open: telemetry must never affect session lifecycle.
        }
    }

    /// <summary>Records a session termination (single or bulk) by reason.</summary>
    public static void RecordTerminated(SessionTerminationReason reason, int count = 1)
    {
        if (count <= 0 || !Enum.IsDefined(reason))
        {
            return;
        }

        try
        {
            Terminations.Add(count, new KeyValuePair<string, object?>("reason", reason.ToString().ToLowerInvariant()));
            Interlocked.Add(ref _activeBalance, -count);
        }
        catch (Exception)
        {
            // Fail open: telemetry must never affect session lifecycle.
        }
    }
}
