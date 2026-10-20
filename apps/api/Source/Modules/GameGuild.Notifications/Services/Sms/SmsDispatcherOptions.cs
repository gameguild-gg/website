namespace GameGuild.Notifications.Services.Sms;

/// <summary>
/// SMS dispatcher tuning. Bound to configuration section "Notifications:SmsDispatcher".
/// </summary>
public sealed class SmsDispatcherOptions
{
    /// <summary>Delay between sweep passes.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Total delivery attempts before a row is deadlettered.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Retry backoff per failed attempt; index is the pre-increment AttemptCount, last entry repeats.</summary>
    public TimeSpan[] BackoffSchedule { get; set; } = [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)];

    /// <summary>Transactional SMS older than this is deadlettered as stale instead of sent (MFA codes expire first).</summary>
    public TimeSpan TransactionalStalenessTtl { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Maximum rows processed per sweep.</summary>
    public int SweepBatchSize { get; set; } = 50;
}
