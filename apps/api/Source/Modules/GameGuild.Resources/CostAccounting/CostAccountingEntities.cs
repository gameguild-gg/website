namespace GameGuild.Resources;

public static class InternalCostMetrics
{
    public const string ApiRequest = "saas.api.request";
    public const string UseCaseOperation = "saas.use-case.operation";
    public const string UserLifecycle = "saas.user.lifecycle";
    public const string PropertyLifecycle = "saas.property.lifecycle";
    public const string PropertyMedia = "saas.property.media";
    public const string AssetReference = "saas.asset.reference";
    public const string S3PutRequest = "aws.s3.put-request";
    public const string S3DeleteRequest = "aws.s3.delete-request";
    public const string S3GetRequest = "aws.s3.get-request";
    public const string S3StorageByteMonth = "aws.s3.storage-byte-month";
    public const string DataTransferOutByte = "aws.data-transfer.out-byte";
    public const string AssetTransformation = "saas.asset.transformation";
}

public static class InternalCostStatuses
{
    public const string Pending = "pending";
    public const string Valued = "valued";
}

public sealed class InternalUsageLedgerEntry : EntityBase
{
    public Guid EventId { get; init; }
    public required string EventName { get; init; }
    public required string MetricCode { get; init; }
    public decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required string Provider { get; init; }
    public required string Region { get; init; }
    public Guid CorrelationId { get; init; }
    public required string AggregateType { get; init; }
    public required string AggregateId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public required string ValuationStatus { get; set; }
}

public sealed class CloudPriceRate : EntityBase
{
    public required string Provider { get; init; }
    public required string ServiceCode { get; init; }
    public required string MetricCode { get; init; }
    public required string Region { get; init; }
    public required string Unit { get; init; }
    public required string SourceCurrency { get; init; }
    public decimal SourceUnitPrice { get; init; }
    public decimal UsdExchangeRate { get; init; }
    public required string ProviderRateId { get; init; }
    public DateTime EffectiveAtUtc { get; init; }
    public DateTime RetrievedAtUtc { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
}

public sealed class InternalCostValuation : EntityBase
{
    public Guid UsageLedgerEntryId { get; init; }
    public Guid CloudPriceRateId { get; init; }
    public required string SourceCurrency { get; init; }
    public decimal SourceUnitPrice { get; init; }
    public decimal SourceCost { get; init; }
    public decimal UsdExchangeRate { get; init; }
    public decimal UsdCost { get; init; }
    public bool IsStaleEstimate { get; init; }
    public DateTime ValuedAtUtc { get; init; }
}

public sealed class ActualCloudCostEntry : EntityBase
{
    public required string Provider { get; init; }
    public required string Source { get; init; }
    public required string ExternalLineId { get; init; }
    public required string ServiceCode { get; init; }
    public required string UsageType { get; init; }
    public string? ResourceId { get; init; }
    public DateTime BillingPeriodStartUtc { get; init; }
    public DateTime BillingPeriodEndUtc { get; init; }
    public required string SourceCurrency { get; init; }
    public decimal SourceCost { get; init; }
    public decimal UsdCost { get; init; }
    public bool IsUsdNormalizationPending { get; init; }
    public bool IsForecast { get; init; }
    public DateTime ImportedAtUtc { get; init; }
}

public sealed class SharedCostAllocationEntry : EntityBase
{
    public Guid RequestEventId { get; init; }
    public Guid UsageLedgerEntryId { get; init; }
    public decimal DurationMilliseconds { get; init; }
    public decimal DatabaseMilliseconds { get; init; }
    public long CacheOperations { get; init; }
    public long TransferredBytes { get; init; }
    public int StatusCode { get; init; }
    public required string Method { get; init; }
    public required string RouteTemplate { get; init; }
    public DateTime OccurredAtUtc { get; init; }
}
