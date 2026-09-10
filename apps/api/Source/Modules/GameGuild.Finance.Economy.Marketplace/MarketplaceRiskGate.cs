using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Marketplace;

public sealed class MarketplaceRiskGate
{
    private static readonly IReadOnlyList<RiskEntityType> EntityTypes = Array.AsReadOnly(
        new[]
        {
            RiskEntityType.Account,
            RiskEntityType.Referral,
            RiskEntityType.Product,
            RiskEntityType.MarketplaceCounterparty
        });

    private static readonly IReadOnlyList<RiskLimitDimension> LimitDimensions = Array.AsReadOnly(
        new[]
        {
            RiskLimitDimension.Wallet,
            RiskLimitDimension.IdentityCluster,
            RiskLimitDimension.SourceRoot,
            RiskLimitDimension.Destination,
            RiskLimitDimension.CounterpartyPair,
            RiskLimitDimension.Product,
            RiskLimitDimension.GlobalLossBudget
        });

    private readonly RiskDecisionAuthorizer _decisions;
    private readonly AggregateRiskCounterStore _counters;

    public MarketplaceRiskGate(
        RiskDecisionAuthorizer decisions,
        AggregateRiskCounterStore counters)
    {
        _decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        _counters = counters ?? throw new ArgumentNullException(nameof(counters));
    }

    public static IReadOnlyList<RiskEntityType> RequiredEntityTypes => EntityTypes;
    public static IReadOnlyList<RiskLimitDimension> RequiredLimitDimensions => LimitDimensions;

    public MarketplaceRiskAuthorization Authorize(
        SettleMarketplaceOrderCommand command,
        IReadOnlyList<MarketplaceFundingFragment> fundingFragments)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(fundingFragments);
        ArgumentNullException.ThrowIfNull(command.Risk);
        ArgumentNullException.ThrowIfNull(command.Risk.Decision);
        ArgumentNullException.ThrowIfNull(command.Risk.Context);
        ArgumentNullException.ThrowIfNull(command.Risk.EntityCluster);
        ArgumentNullException.ThrowIfNull(command.Risk.Limits);
        ArgumentNullException.ThrowIfNull(command.Risk.CounterReservationIds);

        var risk = command.Risk;
        var context = risk.Context;
        var roots = fundingFragments
            .SelectMany(fragment => fragment.SelectedRanges)
            .Select(range => range.Root)
            .Distinct()
            .OrderBy(root => root.Value)
            .ToArray();
        var contextRoots = context.SourceRoots.Distinct().OrderBy(root => root.Value).ToArray();
        var legs = command.Quote.Legs
            .Select(leg => new RiskCurrencyLeg(leg.Currency, leg.Units))
            .ToArray();

        if (command.BuyerId == Guid.Empty ||
            context.ActorId != command.BuyerId ||
            context.Operation != PostingTemplateKind.Spend ||
            context.SourceWalletId != command.BuyerWalletId ||
            context.DestinationWalletId != command.SellerWalletId ||
            context.Amount != command.Quote.Legs[0].Amount ||
            !context.CurrencyLegs.SequenceEqual(legs) ||
            !contextRoots.SequenceEqual(roots) ||
            !string.Equals(context.ProviderReferenceHash, ProviderReference(command.Id), StringComparison.Ordinal) ||
            context.PolicyVersion.Value != command.Quote.PolicyVersion ||
            context.EntityGraphVersion != risk.EntityCluster.Version ||
            !string.Equals(context.EntityGraphEvidenceHash, risk.EntityCluster.EvidenceHash, StringComparison.Ordinal))
            throw new MarketplaceRiskExposureException(
                "The risk decision is not bound to the final marketplace settlement.");

        var nodes = risk.EntityCluster.Nodes;
        if (EntityTypes.Any(type => nodes.All(node => node.Type != type)) ||
            !HasNode(nodes, RiskEntityType.Account, command.BuyerId.ToString("N")) ||
            !HasNode(nodes, RiskEntityType.Account, command.SellerId.ToString("N")) ||
            !HasNode(nodes, RiskEntityType.Product, command.ProductId.ToString("N")) ||
            !HasNode(
                nodes,
                RiskEntityType.MarketplaceCounterparty,
                CounterpartyPair(command.BuyerId, command.SellerId)))
            throw new MarketplaceRiskExposureException(
                "Marketplace entity-graph exposure is incomplete or unbound.");

        var dimensions = risk.Limits.Select(limit => limit.Key.Dimension).ToHashSet();
        if (LimitDimensions.Any(dimension => !dimensions.Contains(dimension)))
            throw new MarketplaceRiskExposureException(
                "Marketplace aggregate-limit exposure is incomplete.");

        var requiredSubjects = new[]
        {
            new RiskLimitKey(RiskLimitDimension.Wallet, command.BuyerWalletId.Value.ToString("N")),
            new RiskLimitKey(RiskLimitDimension.IdentityCluster, risk.EntityCluster.Id),
            new RiskLimitKey(RiskLimitDimension.Destination, command.SellerWalletId.Value.ToString("N")),
            new RiskLimitKey(RiskLimitDimension.Destination, command.PlatformFeeWalletId.Value.ToString("N")),
            new RiskLimitKey(
                RiskLimitDimension.CounterpartyPair,
                CounterpartyPair(command.BuyerId, command.SellerId)),
            new RiskLimitKey(RiskLimitDimension.Product, command.ProductId.ToString("N")),
            new RiskLimitKey(RiskLimitDimension.GlobalLossBudget, RefundPattern(command.BuyerId))
        };
        var subjects = risk.Limits.Select(limit => limit.Key).ToHashSet();
        if (requiredSubjects.Any(subject => !subjects.Contains(subject)) ||
            roots.Any(root => !subjects.Contains(
                new RiskLimitKey(RiskLimitDimension.SourceRoot, root.Value.ToString("N")))))
            throw new MarketplaceRiskExposureException(
                "Marketplace limits are not bound to every material subject.");

        if (risk.CounterReservationIds.Count != command.Quote.Legs.Count ||
            risk.CounterReservationIds.Any(id => id == Guid.Empty) ||
            risk.CounterReservationIds.Distinct().Count() != risk.CounterReservationIds.Count)
            throw new MarketplaceRiskExposureException(
                "Every currency leg requires one unique aggregate counter reservation.");

        var decision = _decisions.AuthorizeValueMovement(
            risk.Decision, context, command.SettledAt);
        var reservations = command.Quote.Legs.Select((leg, index) =>
            _counters.Reserve(
                risk.CounterReservationIds[index],
                PostingTemplateKind.Spend,
                leg.Amount,
                risk.Limits,
                command.SettledAt)).ToArray();
        return new MarketplaceRiskAuthorization(decision, reservations);
    }

    public static string ProviderReference(MarketplaceSettlementId settlementId) =>
        $"marketplace:{settlementId.Value:N}:settle";

    public static string CounterpartyPair(Guid buyerId, Guid sellerId)
    {
        if (buyerId == Guid.Empty) throw new ArgumentException("Buyer ID cannot be empty.", nameof(buyerId));
        if (sellerId == Guid.Empty) throw new ArgumentException("Seller ID cannot be empty.", nameof(sellerId));
        return $"{buyerId:N}:{sellerId:N}";
    }

    public static string RefundPattern(Guid buyerId)
    {
        if (buyerId == Guid.Empty) throw new ArgumentException("Buyer ID cannot be empty.", nameof(buyerId));
        return $"refund-pattern:{buyerId:N}";
    }

    private static bool HasNode(
        IReadOnlyList<RiskEntityNode> nodes,
        RiskEntityType type,
        string subject) =>
        nodes.Any(node =>
            node.Type == type &&
            string.Equals(node.IdentifierHash, subject, StringComparison.Ordinal));
}
