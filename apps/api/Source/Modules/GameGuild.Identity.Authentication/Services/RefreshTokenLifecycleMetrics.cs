using System.Diagnostics.Metrics;

namespace GameGuild.Identity.Authentication;

/// <summary>Process counters with bounded tags. Database evidence remains the authoritative history.</summary>
public static class RefreshTokenLifecycleMetrics
{
    public static string MeterName => "GameGuild.Identity.Authentication.RefreshTokens";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Attempts = Meter.CreateCounter<long>(
        "authentication.refresh_token.attempts", "{attempt}", "Refresh token operations attempted before persistence.");
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>(
        "authentication.refresh_token.operations", "{operation}", "Persisted refresh token lifecycle outcomes, emitted after commit.");
    private static readonly Counter<long> CleanupRuns = Meter.CreateCounter<long>(
        "authentication.refresh_token.cleanup_runs", "{run}", "Bounded credential cleanup outcomes.");
    private static readonly Counter<long> CleanupRows = Meter.CreateCounter<long>(
        "authentication.refresh_token.cleanup_rows", "{row}", "Credential rows deleted by committed cleanup cycles.");

    public static void RecordCleanupCommitted(int tokens, int sessions)
    {
        if (tokens < 0) { throw new ArgumentOutOfRangeException(nameof(tokens)); }
        if (sessions < 0) { throw new ArgumentOutOfRangeException(nameof(sessions)); }
        try
        {
            CleanupRuns.Add(1, new KeyValuePair<string, object?>("outcome", "committed"));
            CleanupRows.Add(tokens, new KeyValuePair<string, object?>("resource", "refresh_token"));
            CleanupRows.Add(sessions, new KeyValuePair<string, object?>("resource", "session"));
        }
        catch (Exception)
        {
            return;
        }
    }

    public static void RecordCleanupFailure(bool timedOut)
    {
        try { CleanupRuns.Add(1, new KeyValuePair<string, object?>("outcome", timedOut ? "timeout" : "failed")); }
        catch (Exception)
        {
            return;
        }
    }

    public static void RecordAttempt(RefreshTokenLifecycleOperation operation)
    {
        if (!Enum.IsDefined(operation)) { throw new ArgumentOutOfRangeException(nameof(operation)); }
        // A failing in-process telemetry listener must not change authentication or undo a commit.
        try { Attempts.Add(1, new KeyValuePair<string, object?>("operation", operation.ToString().ToLowerInvariant())); }
        catch (Exception)
        {
            return;
        }
    }

    public static void RecordPersisted(RefreshTokenLifecycleEvent lifecycleEvent)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);
        if (!Enum.IsDefined(lifecycleEvent.Operation)) { throw new ArgumentOutOfRangeException(nameof(lifecycleEvent)); }
        if (!Enum.IsDefined(lifecycleEvent.Reason)) { throw new ArgumentOutOfRangeException(nameof(lifecycleEvent)); }
        var outcome = lifecycleEvent.Operation switch
        {
            RefreshTokenLifecycleOperation.Rejected => "rejected",
            RefreshTokenLifecycleOperation.ReplayContained => "contained",
            _ => "committed"
        };
        try
        {
            Operations.Add(1,
                new KeyValuePair<string, object?>("operation", lifecycleEvent.Operation.ToString().ToLowerInvariant()),
                new KeyValuePair<string, object?>("outcome", outcome),
                new KeyValuePair<string, object?>("reason", lifecycleEvent.Reason.ToString().ToLowerInvariant()));
        }
        catch (Exception)
        {
            return;
        }
    }
}
