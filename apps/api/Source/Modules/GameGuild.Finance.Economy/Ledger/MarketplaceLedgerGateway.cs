using System.Data.Common;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public sealed record MarketplaceFifoReservationRequest(
    Guid OperationId,
    WalletId WalletId,
    IReadOnlyList<CoinAmount> Legs,
    DateTimeOffset ReservedAt);

public interface IMarketplaceFifoReservationGateway
{
    IReadOnlyList<PersistedFragmentReservation> Reserve(MarketplaceFifoReservationRequest request);
}

public sealed record PersistedMarketplacePriceLeg(
    CurrencyCode Currency,
    long Units,
    long SellerUnits,
    long PlatformFeeUnits);

public sealed record PersistedMarketplaceOrderSnapshot(
    Guid OrderId,
    Guid OrderLineItemId,
    Guid ProductId,
    Guid ProductPricingVersionId,
    int PriceVersion,
    int Quantity,
    decimal UnitPrice,
    string FiatCurrency,
    string SnapshotHash);

public sealed record PersistedMarketplaceSettlementRequest(
    RegisteredPostingAuthority Authority,
    CapabilityAuthorizationReceipt CapabilityReceipt,
    Guid SettlementId,
    PostingId PostingId,
    IdempotencyKey IdempotencyKey,
    PersistedMarketplaceOrderSnapshot Order,
    Guid BuyerId,
    WalletId BuyerWalletId,
    Guid SellerId,
    WalletId SellerWalletId,
    WalletId PlatformFeeWalletId,
    long MarketplacePolicyVersion,
    int CurrencyMode,
    IReadOnlyList<PersistedMarketplacePriceLeg> Legs,
    IReadOnlyList<Guid> ReservationIds,
    Guid EntitlementId,
    DateTimeOffset RefundHoldUntil,
    DateTimeOffset SettledAt);

public interface IMarketplaceSettlementLedgerGateway
{
    RegisteredPostingReceipt Settle(PersistedMarketplaceSettlementRequest request);
}

public sealed record PersistedMarketplaceRefundLeg(CurrencyCode Currency, long Units);

public sealed record PersistedMarketplaceRefundRequest(
    RegisteredPostingAuthority Authority,
    CapabilityAuthorizationReceipt CapabilityReceipt,
    Guid RefundId,
    Guid SettlementId,
    PostingId PostingId,
    IdempotencyKey IdempotencyKey,
    Guid BuyerId,
    long MarketplacePolicyVersion,
    int Quantity,
    int CumulativeRefundedQuantity,
    IReadOnlyList<PersistedMarketplaceRefundLeg> Legs,
    string ReasonCode,
    string ReasonHash,
    DateTimeOffset RefundedAt);

public interface IMarketplaceRefundLedgerGateway
{
    RegisteredPostingReceipt Refund(PersistedMarketplaceRefundRequest request);
}

public sealed class PostgreSqlMarketplaceLedgerGateway :
    IMarketplaceFifoReservationGateway,
    IMarketplaceSettlementLedgerGateway,
    IMarketplaceRefundLedgerGateway
{
    private readonly DbContext _db;

    public PostgreSqlMarketplaceLedgerGateway(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Persistent Marketplace ledger operations require the application's relational DbContext.");
    }

    public IReadOnlyList<PersistedFragmentReservation> Reserve(MarketplaceFifoReservationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OperationId == Guid.Empty)
            throw new ArgumentException("Operation ID is required.", nameof(request));
        ArgumentNullException.ThrowIfNull(request.Legs);
        if (request.Legs.Count == 0 || request.Legs.Any(leg => leg.Units <= 0) ||
            request.Legs.Select(leg => leg.Currency).Distinct().Count() != request.Legs.Count)
            throw new ArgumentException("Marketplace reservation legs must be unique and positive.", nameof(request));
        var legs = JsonSerializer.Serialize(request.Legs.Select(leg => new
        {
            currency = (int)leg.Currency,
            units = leg.Units
        }));
        try
        {
            return _db.Set<MarketplaceFifoReservationReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM economy_private.reserve_marketplace_fifo_fragments_v1(
                        {request.OperationId},
                        {request.WalletId.Value},
                        CAST({legs} AS jsonb),
                        {(int)PersistedFragmentReservationPurpose.MarketplaceSettlement},
                        {request.ReservedAt})
                    """)
                .AsNoTracking()
                .AsEnumerable()
                .Select(row => new PersistedFragmentReservation(
                    row.ReservationId,
                    request.OperationId,
                    new CreditLotId(row.ParentLotId),
                    new SourceStampId(row.RootSourceStampId),
                    row.ReversalEpoch,
                    new RootTraceRange(
                        new SourceStampId(row.RootSourceStampId),
                        row.StartInclusive,
                        checked(row.EndExclusive - row.StartInclusive),
                        row.ReversalEpoch),
                    new CoinAmount(row.Currency, row.AmountUnits)))
                .ToArray();
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new RegisteredPostingRejectedException(
                "The persistent Marketplace FIFO writer rejected the reservation.", exception);
        }
    }

    public RegisteredPostingReceipt Settle(PersistedMarketplaceSettlementRequest request)
    {
        ValidateSettlement(request);
        var order = JsonSerializer.Serialize(new
        {
            order_id = request.Order.OrderId,
            line_item_id = request.Order.OrderLineItemId,
            product_id = request.Order.ProductId,
            pricing_version_id = request.Order.ProductPricingVersionId,
            price_version = request.Order.PriceVersion,
            quantity = request.Order.Quantity,
            unit_price = request.Order.UnitPrice,
            fiat_currency = request.Order.FiatCurrency,
            snapshot_hash = request.Order.SnapshotHash
        });
        var legs = JsonSerializer.Serialize(request.Legs.Select(leg => new
        {
            currency = (int)leg.Currency,
            units = leg.Units,
            seller_units = leg.SellerUnits,
            platform_fee_units = leg.PlatformFeeUnits
        }));
        var reservationIds = JsonSerializer.Serialize(request.ReservationIds);
        var receiptEvidence = JsonSerializer.Serialize(request.CapabilityReceipt.EvidenceHashes);
        try
        {
            var row = _db.Set<RegisteredPostingReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM economy_private.post_marketplace_settlement_v1(
                        {request.Authority.CapabilityId},
                        {request.Authority.ActorId},
                        {request.Authority.TenantId},
                        {request.SettlementId},
                        {request.PostingId.Value},
                        {request.IdempotencyKey.Value},
                        {request.CapabilityReceipt.PolicyVersion},
                        {request.CapabilityReceipt.ReserveVersion},
                        {request.Authority.RiskDecisionId},
                        {request.Authority.RiskOperationFingerprint},
                        {request.Authority.ExpectedCounterVersion},
                        {request.BuyerId},
                        {request.BuyerWalletId.Value},
                        {request.SellerId},
                        {request.SellerWalletId.Value},
                        {request.PlatformFeeWalletId.Value},
                        {request.MarketplacePolicyVersion},
                        {request.CurrencyMode},
                        CAST({order} AS jsonb),
                        CAST({legs} AS jsonb),
                        CAST({reservationIds} AS jsonb),
                        {request.EntitlementId},
                        {request.RefundHoldUntil},
                        {request.SettledAt},
                        {request.CapabilityReceipt.Id},
                        {request.CapabilityReceipt.ReceiptHash},
                        {request.CapabilityReceipt.KillSwitchEpoch},
                        {request.CapabilityReceipt.JurisdictionCode},
                        CAST({receiptEvidence} AS jsonb))
                    """)
                .AsNoTracking()
                .Single();
            return new RegisteredPostingReceipt(
                new PostingId(row.PostingId), row.JournalSequence, row.JournalHash, row.Duplicate);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new RegisteredPostingRejectedException(
                "The persistent Marketplace writer rejected the settlement.", exception);
        }
    }

    public RegisteredPostingReceipt Refund(PersistedMarketplaceRefundRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Authority);
        ArgumentNullException.ThrowIfNull(request.CapabilityReceipt);
        ArgumentNullException.ThrowIfNull(request.Legs);
        if (request.RefundId == Guid.Empty || request.SettlementId == Guid.Empty ||
            request.BuyerId == Guid.Empty || request.MarketplacePolicyVersion <= 0 ||
            request.Quantity <= 0 || request.CumulativeRefundedQuantity < request.Quantity ||
            request.Legs.Count == 0 || request.Legs.Any(leg => leg.Units <= 0) ||
            request.Legs.Select(leg => leg.Currency).Distinct().Count() != request.Legs.Count)
            throw new ArgumentException("Marketplace refund inputs are invalid.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ReasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ReasonHash);
        var legs = JsonSerializer.Serialize(request.Legs.Select(leg => new
        {
            currency = (int)leg.Currency,
            units = leg.Units
        }));
        var receiptEvidence = JsonSerializer.Serialize(request.CapabilityReceipt.EvidenceHashes);
        try
        {
            var row = _db.Set<RegisteredPostingReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM economy_private.post_marketplace_refund_v1(
                        {request.Authority.CapabilityId},
                        {request.Authority.ActorId},
                        {request.Authority.TenantId},
                        {request.RefundId},
                        {request.SettlementId},
                        {request.PostingId.Value},
                        {request.IdempotencyKey.Value},
                        {request.CapabilityReceipt.PolicyVersion},
                        {request.CapabilityReceipt.ReserveVersion},
                        {request.Authority.RiskDecisionId},
                        {request.Authority.RiskOperationFingerprint},
                        {request.Authority.ExpectedCounterVersion},
                        {request.BuyerId},
                        {request.MarketplacePolicyVersion},
                        {request.Quantity},
                        {request.CumulativeRefundedQuantity},
                        CAST({legs} AS jsonb),
                        {request.ReasonCode.Trim()},
                        {request.ReasonHash},
                        {request.RefundedAt},
                        {request.CapabilityReceipt.Id},
                        {request.CapabilityReceipt.ReceiptHash},
                        {request.CapabilityReceipt.KillSwitchEpoch},
                        {request.CapabilityReceipt.JurisdictionCode},
                        CAST({receiptEvidence} AS jsonb))
                    """)
                .AsNoTracking()
                .Single();
            return new RegisteredPostingReceipt(
                new PostingId(row.PostingId), row.JournalSequence, row.JournalHash, row.Duplicate);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new RegisteredPostingRejectedException(
                "The persistent Marketplace writer rejected the refund.", exception);
        }
    }

    private static void ValidateSettlement(PersistedMarketplaceSettlementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Authority);
        ArgumentNullException.ThrowIfNull(request.CapabilityReceipt);
        ArgumentNullException.ThrowIfNull(request.Order);
        ArgumentNullException.ThrowIfNull(request.Legs);
        ArgumentNullException.ThrowIfNull(request.ReservationIds);
        if (request.SettlementId == Guid.Empty || request.BuyerId == Guid.Empty ||
            request.SellerId == Guid.Empty || request.EntitlementId == Guid.Empty)
            throw new ArgumentException("Settlement identities are required.", nameof(request));
        if (request.BuyerId == request.SellerId || request.Legs.Count == 0 ||
            request.Legs.Any(leg => leg.Units <= 0 || leg.SellerUnits < 0 ||
                                    leg.PlatformFeeUnits < 0 ||
                                    checked(leg.SellerUnits + leg.PlatformFeeUnits) != leg.Units) ||
            request.ReservationIds.Count == 0 || request.ReservationIds.Any(id => id == Guid.Empty) ||
            request.ReservationIds.Distinct().Count() != request.ReservationIds.Count ||
            request.RefundHoldUntil <= request.SettledAt)
            throw new ArgumentException("Marketplace settlement inputs are invalid.", nameof(request));
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException or DbUpdateException or InvalidOperationException ||
        exception.GetBaseException() is DbException;
}
