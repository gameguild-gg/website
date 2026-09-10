using GameGuild.Finance.Contracts;

namespace GameGuild.Resources;

public sealed class CostAccountingEventHandler(
    IApplicationDbContext context,
    InternalCostValuationService valuationService) :
    IIntegrationEventHandler<UseCaseOperationOccurredV1>,
    IIntegrationEventHandler<UserCreatedEvent>,
    IIntegrationEventHandler<UserDeletedEvent>,
    IIntegrationEventHandler<AssetReferenceCreatedEvent>,
    IIntegrationEventHandler<AssetReferenceRemovedEvent>,
    IIntegrationEventHandler<AssetObjectStoredEvent>,
    IIntegrationEventHandler<AssetObjectDeletedEvent>,
    IIntegrationEventHandler<AssetTransformedEvent>,
    IIntegrationEventHandler<AssetServedEvent>,
    IIntegrationEventHandler<EconomyPostingAcceptedEventV1>,
    IIntegrationEventHandler<ApiRequestMeasuredEventV1>
{
    public Task HandleAsync(UseCaseOperationOccurredV1 @event, CancellationToken cancellationToken = default) =>
        RecordAsync(@event, [(InternalCostMetrics.UseCaseOperation, 1, "operation", "shared")], cancellationToken);

    public Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default) =>
        RecordAsync(@event, [(InternalCostMetrics.UserLifecycle, 1, "create", "shared")], cancellationToken);

    public Task HandleAsync(UserDeletedEvent @event, CancellationToken cancellationToken = default) =>
        RecordAsync(@event, [(InternalCostMetrics.UserLifecycle, 1, "delete", "shared")], cancellationToken);

    public Task HandleAsync(AssetReferenceCreatedEvent @event, CancellationToken cancellationToken = default) =>
        RecordAsync(@event, [(InternalCostMetrics.AssetReference, 1, "create", "shared")], cancellationToken);

    public Task HandleAsync(AssetReferenceRemovedEvent @event, CancellationToken cancellationToken = default) =>
        RecordAsync(@event, [(InternalCostMetrics.AssetReference, 1, "remove", "shared")], cancellationToken);

    public Task HandleAsync(AssetObjectStoredEvent @event, CancellationToken cancellationToken = default) =>
        RecordAsync(@event,
            [
                (InternalCostMetrics.S3PutRequest, 1, "request", "aws"),
                (InternalCostMetrics.S3StorageByteMonth, @event.ByteCount, "byte-month", "aws")
            ], cancellationToken);

    public Task HandleAsync(AssetObjectDeletedEvent @event, CancellationToken cancellationToken = default) =>
        RecordAsync(@event, [(InternalCostMetrics.S3DeleteRequest, 1, "request", "aws")], cancellationToken);

    public Task HandleAsync(AssetTransformedEvent @event, CancellationToken cancellationToken = default) =>
        RecordAsync(@event, [(InternalCostMetrics.AssetTransformation, 1, "transformation", "shared")], cancellationToken);

    public Task HandleAsync(AssetServedEvent @event, CancellationToken cancellationToken = default) =>
        RecordAsync(@event,
            [
                (InternalCostMetrics.S3GetRequest, 1, "request", "aws"),
                (InternalCostMetrics.DataTransferOutByte, @event.ByteCount, "byte", "aws")
            ], cancellationToken);

    public Task HandleAsync(EconomyPostingAcceptedEventV1 @event, CancellationToken cancellationToken = default) =>
        RecordAsync(@event,
            [
                (InternalCostMetrics.EconomyPosting, 1, "posting", "shared"),
                (InternalCostMetrics.EconomyJournalLine, @event.Lines.Count, "line", "shared")
            ], cancellationToken);

    public async Task HandleAsync(ApiRequestMeasuredEventV1 @event, CancellationToken cancellationToken = default)
    {
        var usage = await RecordAsync(
                @event,
                [(InternalCostMetrics.ApiRequest, 1, "request", "shared")],
                cancellationToken,
                saveChanges: false)
            .ConfigureAwait(false);
        context.Set<SharedCostAllocationEntry>().Add(new SharedCostAllocationEntry
        {
            TenantId = @event.TenantId,
            RequestEventId = @event.EventId,
            UsageLedgerEntryId = usage[0].Id,
            DurationMilliseconds = @event.DurationMilliseconds,
            DatabaseMilliseconds = @event.DatabaseMilliseconds,
            CacheOperations = @event.CacheOperations,
            TransferredBytes = @event.TransferredBytes,
            StatusCode = @event.StatusCode,
            Method = @event.Method,
            RouteTemplate = @event.RouteTemplate,
            OccurredAtUtc = @event.OccurredAt
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<InternalUsageLedgerEntry>> RecordAsync(
        IDurableIntegrationEvent @event,
        IReadOnlyList<(string MetricCode, decimal Quantity, string Unit, string Provider)> measurements,
        CancellationToken cancellationToken,
        bool saveChanges = true)
    {
        var entries = new List<InternalUsageLedgerEntry>(measurements.Count);
        foreach (var measurement in measurements)
        {
            var entry = new InternalUsageLedgerEntry
            {
                TenantId = @event.TenantId,
                EventId = @event.EventId,
                EventName = @event.EventName,
                MetricCode = measurement.MetricCode,
                Quantity = measurement.Quantity,
                Unit = measurement.Unit,
                Provider = measurement.Provider,
                Region = "global",
                CorrelationId = @event.CorrelationId,
                AggregateType = @event.AggregateType,
                AggregateId = @event.AggregateId,
                OccurredAtUtc = @event.OccurredAt,
                ValuationStatus = InternalCostStatuses.Pending
            };
            context.Set<InternalUsageLedgerEntry>().Add(entry);
            await valuationService.TryValueAsync(entry, cancellationToken).ConfigureAwait(false);
            entries.Add(entry);
        }

        if (saveChanges)
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return entries;
    }
}
