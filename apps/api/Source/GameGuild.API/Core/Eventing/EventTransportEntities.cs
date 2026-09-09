namespace GameGuild.API.Eventing;

public sealed class OutboxMessage
{
    public Guid EventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public required string EventName { get; set; }
    public required string EventType { get; set; }
    public required string SourceModule { get; set; }
    public required string AggregateType { get; set; }
    public required string AggregateId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public int SchemaVersion { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ClaimedUntilUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? DeadLetteredAtUtc { get; set; }
}

public sealed class InboxReceipt
{
    public Guid EventId { get; set; }
    public required string ConsumerName { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? DeadLetteredAtUtc { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
