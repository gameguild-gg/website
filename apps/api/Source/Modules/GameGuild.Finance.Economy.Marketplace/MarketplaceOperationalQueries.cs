using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Operations;

namespace GameGuild.Finance.Economy.Marketplace;

public sealed record MarketplaceSettlementOperationalSummary(
    Guid Id,
    Guid TenantId,
    Guid OrderId,
    Guid OrderLineItemId,
    Guid ProductId,
    int Quantity,
    int RefundedQuantity,
    Guid BuyerId,
    Guid SellerId,
    long PolicyVersion,
    ProductCurrencyMode CurrencyMode,
    MarketplaceSettlementStatus Status,
    Guid EntitlementId,
    MarketplaceEntitlementStatus EntitlementStatus,
    long JournalSequence,
    long ReserveVersion,
    string JurisdictionCode,
    DateTimeOffset RefundHoldUntil,
    DateTimeOffset SettledAt,
    DateTimeOffset UpdatedAt);

public sealed record MarketplaceSettlementLegOperationalStatus(
    CurrencyCode Currency,
    long Units,
    long SellerUnits,
    long PlatformFeeUnits,
    long RefundedUnits);

public sealed record MarketplaceEventOperationalStatus(
    Guid Id,
    long Sequence,
    string Kind,
    string EvidenceHash,
    DateTimeOffset OccurredAt);

public sealed record MarketplaceRefundOperationalStatus(
    Guid Id,
    Guid TenantId,
    Guid SettlementId,
    Guid BuyerId,
    bool IsFullRefund,
    bool EntitlementRevoked,
    string ReasonCode,
    int Quantity,
    int RefundedQuantity,
    long FirstJournalSequence,
    DateTimeOffset RefundedAt);

public sealed record MarketplaceOutboxOperationalStatus(
    Guid Id,
    Guid TenantId,
    Guid SettlementId,
    string MessageType,
    string PayloadHash,
    DateTimeOffset OccurredAt,
    DateTimeOffset? PublishedAt,
    int AttemptCount,
    DateTimeOffset? LeaseExpiresAt,
    bool HasLastError);

public sealed record MarketplaceSettlementOperationalDetails(
    MarketplaceSettlementOperationalSummary Summary,
    IReadOnlyList<MarketplaceSettlementLegOperationalStatus> Legs,
    IReadOnlyList<MarketplaceEventOperationalStatus> Events,
    IReadOnlyList<MarketplaceRefundOperationalStatus> Refunds,
    IReadOnlyList<MarketplaceOutboxOperationalStatus> Outbox);

public interface IMarketplaceOperationalQueryReader
{
    ValueTask<EconomyOperationalPage<MarketplaceSettlementOperationalSummary>> ListSettlementsAsync(
        Guid tenantId, MarketplaceSettlementStatus? status, int limit, string? cursor,
        CancellationToken cancellationToken);

    ValueTask<MarketplaceSettlementOperationalDetails?> FindSettlementAsync(
        Guid tenantId, Guid settlementId, CancellationToken cancellationToken);

    ValueTask<EconomyOperationalPage<MarketplaceRefundOperationalStatus>> ListRefundsAsync(
        Guid tenantId, int limit, string? cursor, CancellationToken cancellationToken);

    ValueTask<MarketplaceRefundOperationalStatus?> FindRefundAsync(
        Guid tenantId, Guid refundId, CancellationToken cancellationToken);

    ValueTask<EconomyOperationalPage<MarketplaceOutboxOperationalStatus>> ListOutboxAsync(
        Guid tenantId, bool? published, int limit, string? cursor, CancellationToken cancellationToken);
}
