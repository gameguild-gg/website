namespace GameGuild.API.Eventing;

public interface IOutboxDispatcher
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default);
}

public interface IInboxStore
{
    Task<InboxReceipt?> TryLockAsync(
        Guid eventId,
        string consumerName,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(InboxReceipt receipt, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<InboxReceipt> RecordFailureAsync(
        Guid eventId,
        string consumerName,
        Exception exception,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public interface IEventReplayService
{
    Task<EventTransportStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeadLetterEvent>> GetDeadLettersAsync(CancellationToken cancellationToken = default);
    Task<bool> ReplayAsync(Guid eventId, string? consumerName = null, CancellationToken cancellationToken = default);
}

public sealed record EventTransportStatus(long Pending, long Completed, long DeadLettered, long IncompleteConsumers);

public sealed record DeadLetterEvent(
    Guid EventId,
    string EventName,
    string ConsumerName,
    int AttemptCount,
    string? LastError,
    DateTimeOffset DeadLetteredAtUtc);
