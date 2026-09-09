using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Marketplace;

public readonly record struct MarketplaceSettlementId
{
    public MarketplaceSettlementId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Settlement ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static MarketplaceSettlementId New() => new(Guid.NewGuid());
}

public enum MarketplaceSettlementStatus
{
    Settled = 1,
    PartiallyRefunded = 2,
    Refunded = 3
}

public enum MarketplaceCreditPurpose
{
    SellerProceeds = 1,
    PlatformFee = 2
}

public enum MarketplaceEntitlementStatus
{
    PendingGrant = 1,
    Granted = 2,
    Revoked = 3
}

public sealed class MarketplaceFundingFragment
{
    public MarketplaceFundingFragment(CreditLot parentLot, FragmentSelection selection)
    {
        ArgumentNullException.ThrowIfNull(parentLot);
        ArgumentNullException.ThrowIfNull(selection);
        if (parentLot.Id != selection.ParentLotId ||
            parentLot.Amount.Currency != selection.Amount.Currency ||
            parentLot.TraceUnitsPerCoinUnit != selection.TraceUnitsPerCoinUnit)
            throw new ArgumentException("Funding selection is not bound to its parent lot.", nameof(selection));

        ParentLot = parentLot;
        Selection = selection;
    }

    public CreditLot ParentLot { get; }
    public FragmentSelection Selection { get; }
    public CoinAmount Amount => Selection.Amount;
    public IReadOnlyList<RootTraceRange> SelectedRanges => Selection.SelectedRanges;
}

public sealed record MarketplaceParentLineage(
    CreditLot ParentLot,
    CoinAmount Amount,
    IReadOnlyList<RootTraceRange> Ranges);

public sealed class MarketplaceSettlementCredit
{
    internal MarketplaceSettlementCredit(
        MarketplaceCreditPurpose purpose,
        SourceStampContract? source,
        CreditLot lot,
        IReadOnlyList<MarketplaceParentLineage> parentLineage,
        HoldContract refundHold,
        DateTimeOffset refundHoldUntil)
    {
        Purpose = purpose;
        Source = source;
        Lot = lot;
        ParentLineage = Array.AsReadOnly(parentLineage.ToArray());
        RefundHold = refundHold;
        RefundHoldUntil = refundHoldUntil;
    }

    public MarketplaceCreditPurpose Purpose { get; }
    public SourceStampContract? Source { get; }
    public CreditLot Lot { get; }
    public IReadOnlyList<MarketplaceParentLineage> ParentLineage { get; }
    public HoldContract RefundHold { get; }
    public DateTimeOffset RefundHoldUntil { get; }
}

public sealed record MarketplaceEntitlementGrantRequest(
    MarketplaceSettlementId SettlementId,
    Guid OrderId,
    Guid ProductId,
    Guid BuyerId,
    DateTimeOffset GrantedAt);

public sealed record MarketplaceEntitlementReceipt(
    Guid Id,
    MarketplaceSettlementId SettlementId,
    Guid OrderId,
    Guid ProductId,
    Guid BuyerId,
    DateTimeOffset GrantedAt);

public interface IMarketplaceEntitlementGateway
{
    MarketplaceEntitlementReceipt Grant(MarketplaceEntitlementGrantRequest request);
    void Revoke(MarketplaceEntitlementReceipt receipt, DateTimeOffset revokedAt);
}

public sealed record MarketplaceRiskApproval(
    RiskDecisionSnapshot Decision,
    ProtectedOperationContext Context,
    EntityRiskCluster EntityCluster,
    IReadOnlyList<AggregateRiskLimit> Limits,
    IReadOnlyList<Guid> CounterReservationIds);

public sealed record SettleMarketplaceOrderCommand(
    MarketplaceSettlementId Id,
    Guid OrderId,
    Guid ProductId,
    Guid BuyerId,
    WalletId BuyerWalletId,
    Guid SellerId,
    WalletId SellerWalletId,
    WalletId PlatformFeeWalletId,
    MarketplaceQuoteSnapshot Quote,
    IReadOnlyList<CreditLot> AvailableBuyerLots,
    MarketplaceRiskApproval Risk,
    DateTimeOffset SettledAt,
    DateTimeOffset RefundHoldUntil,
    long FirstJournalSequence,
    IdempotencyKey IdempotencyKey);

public sealed record RefundMarketplaceOrderCommand(
    MarketplaceSettlementId SettlementId,
    Guid BuyerId,
    WalletId BuyerWalletId,
    IReadOnlyList<CoinAmount> RefundLegs,
    long FirstJournalSequence,
    DateTimeOffset RefundedAt,
    IdempotencyKey IdempotencyKey);

public sealed class MarketplaceSettlementResult
{
    private readonly Dictionary<CurrencyCode, long> _refundedUnits = [];

    internal MarketplaceSettlementResult(
        MarketplaceSettlementId id,
        Guid orderId,
        Guid productId,
        Guid buyerId,
        Guid sellerId,
        MarketplaceQuoteSnapshot quote,
        IReadOnlyList<MarketplaceFundingFragment> fundingFragments,
        IReadOnlyList<MarketplaceSettlementCredit> credits,
        MarketplaceEntitlementReceipt entitlement,
        DateTimeOffset settledAt)
    {
        Id = id;
        OrderId = orderId;
        ProductId = productId;
        BuyerId = buyerId;
        SellerId = sellerId;
        Quote = quote;
        FundingFragments = Array.AsReadOnly(fundingFragments.ToArray());
        Credits = Array.AsReadOnly(credits.ToArray());
        Entitlement = entitlement;
        SettledAt = settledAt;
    }

    public MarketplaceSettlementId Id { get; }
    public Guid OrderId { get; }
    public Guid ProductId { get; }
    public Guid BuyerId { get; }
    public Guid SellerId { get; }
    public MarketplaceQuoteSnapshot Quote { get; }
    public IReadOnlyList<MarketplaceFundingFragment> FundingFragments { get; }
    public IReadOnlyList<MarketplaceSettlementCredit> Credits { get; }
    public MarketplaceEntitlementReceipt Entitlement { get; }
    public DateTimeOffset SettledAt { get; }
    public MarketplaceSettlementStatus Status { get; private set; } = MarketplaceSettlementStatus.Settled;
    public IReadOnlyList<CoinAmount> RefundedLegs =>
        _refundedUnits.OrderBy(pair => pair.Key)
            .Select(pair => new CoinAmount(pair.Key, pair.Value)).ToArray();

    internal void ApplyRefund(IReadOnlyList<CoinAmount> legs, bool isFull)
    {
        foreach (var leg in legs)
            _refundedUnits[leg.Currency] =
                checked(_refundedUnits.GetValueOrDefault(leg.Currency) + leg.Units);
        Status = isFull
            ? MarketplaceSettlementStatus.Refunded
            : MarketplaceSettlementStatus.PartiallyRefunded;
    }
}

public sealed record MarketplaceRefundResult(
    MarketplaceSettlementId SettlementId,
    IReadOnlyList<CoinAmount> RefundedLegs,
    IReadOnlyList<CreditLot> RestoredBuyerLots,
    bool IsFullRefund,
    bool EntitlementRevoked);

public sealed record MarketplaceRiskAuthorization(
    RiskAuthorization Decision,
    IReadOnlyList<AggregateRiskCounterReservation> CounterReservations);

public sealed class MarketplaceIdempotencyConflictException(string message) : InvalidOperationException(message);
public sealed class MarketplaceRiskExposureException(string message) : InvalidOperationException(message);
public sealed class MarketplaceEntitlementException(string message) : InvalidOperationException(message);
public sealed class MarketplaceAlreadyRefundedException(string message) : InvalidOperationException(message);
public sealed class MarketplaceRefundException(string message) : InvalidOperationException(message);
