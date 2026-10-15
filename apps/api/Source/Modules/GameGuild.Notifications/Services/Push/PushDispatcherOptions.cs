namespace GameGuild.Notifications.Services.Push;

/// <summary>
/// Push dispatcher tuning. Bound to configuration section "Notifications:PushDispatcher".
/// </summary>
public sealed class PushDispatcherOptions
{
    /// <summary>Delay between sweep passes.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Total delivery attempts before a row is deadlettered.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Retry backoff per failed attempt; index is the pre-increment AttemptCount, last entry repeats.</summary>
    public TimeSpan[] BackoffSchedule { get; set; } = [TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)];

    /// <summary>Transactional push older than this is deadlettered as stale instead of sent (challenges expire first).</summary>
    public TimeSpan TransactionalStalenessTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Maximum rows processed per sweep.</summary>
    public int SweepBatchSize { get; set; } = 50;
}
