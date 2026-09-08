namespace GameGuild.Resources;

public sealed class QuotaLedgerEntry : EntityBase
{
    public Guid EventId { get; init; }
    public ResourceUsageType ResourceType { get; init; }
    public long Delta { get; init; }
    public required string OperationCode { get; init; }
    public Guid CorrelationId { get; init; }
    public required string AggregateType { get; init; }
    public required string AggregateId { get; init; }
    public DateTime TimestampUtc { get; init; }
    public bool IsReservationFinalization { get; init; }
}
