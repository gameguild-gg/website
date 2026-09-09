using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed partial class MarketplaceSettlementCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HardSettlement_CreatesIndependentEarnedCreditsAndGrantsEntitlement()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.HardOnly, 100, 0, 100_000)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var parent = harness.Lot(CurrencyCode.HardCoin, 100, ProvenanceKind.PurchasedHard, Now.AddDays(-10), 1);
        var result = harness.Settle(quote, [parent], Now);

        result.Status.Should().Be(MarketplaceSettlementStatus.Settled);
        result.Credits.Should().HaveCount(2);
        result.Credits.Select(credit => credit.Lot.Amount.Units).Should().BeEquivalentTo([90L, 10L]);
        result.Credits.Should().OnlyContain(credit =>
            credit.Lot.Provenance == ProvenanceKind.EarnedHard &&
            credit.Lot.State == CreditLotState.Held &&
            credit.Lot.ConfirmedAt == Now &&
            credit.Lot.OriginalMaturesAt == Now.AddDays(120) &&
            credit.Source != null &&
            credit.RefundHold.Reason == HoldReason.RefundWindow &&
            credit.RefundHoldUntil == Now.AddDays(30));
        result.Credits.Select(credit => credit.Source!.Id).Should().OnlyHaveUniqueItems();
        result.Credits.SelectMany(credit => credit.ParentLineage)
            .Should().OnlyContain(lineage => lineage.ParentLot.Id == parent.Id);
        result.Entitlement.ProductId.Should().Be(harness.ProductId);
        harness.Entitlements.GrantCount.Should().Be(1);
    }

    [Fact]
    public void SoftSettlement_PreservesParentRootsAndProvenance()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.SoftOnly, 0, 100_000, 100_000)
            .Quote(MarketplaceCurrencyChoice.Soft);
        var parent = harness.Lot(
            CurrencyCode.SoftCoin, 100_000, ProvenanceKind.ConvertedSoft, Now.AddDays(-7), 1);
        var result = harness.Settle(quote, [parent], Now);

        result.Credits.Select(credit => credit.Lot.Amount.Units)
            .Should().BeEquivalentTo([90_000L, 10_000L]);
        result.Credits.Should().OnlyContain(credit =>
            credit.Source == null &&
            credit.Lot.Provenance == parent.Provenance &&
            credit.Lot.ConfirmedAt == parent.ConfirmedAt &&
            credit.Lot.OriginalMaturesAt == parent.OriginalMaturesAt);
        result.Credits.SelectMany(credit => credit.Lot.Ranges)
            .Should().OnlyContain(range => range.Root == parent.Ranges[0].Root);
    }

    [Fact]
    public void FixedMixSettlement_IsAllOrNothingWhenOneLegIsInsufficient()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.FixedMix, 100, 100_000, 50_000)
            .Quote(MarketplaceCurrencyChoice.FixedMix);
        var hard = harness.Lot(
            CurrencyCode.HardCoin, 100, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);
        var soft = harness.Lot(
            CurrencyCode.SoftCoin, 99_999, ProvenanceKind.AdRewardSoft, Now.AddDays(-1), 2);

        harness.Invoking(value => value.Settle(quote, [hard, soft], Now))
            .Should().Throw<InsufficientFragmentsException>();

        harness.Coordinator.Count.Should().Be(0);
        harness.Entitlements.GrantCount.Should().Be(0);
        harness.Decisions.Authorizations.Should().BeEmpty();
        harness.Counters.Reservations.Should().BeEmpty();
    }

    [Fact]
    public void FixedMixSettlement_SnapshotsEveryLegFeeAndParentFragment()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.FixedMix, 100, 100_000, 50_000)
            .Quote(MarketplaceCurrencyChoice.FixedMix);
        var lots = new[]
        {
            harness.Lot(CurrencyCode.HardCoin, 100, ProvenanceKind.PurchasedHard, Now.AddDays(-2), 1),
            harness.Lot(CurrencyCode.SoftCoin, 100_000, ProvenanceKind.AdRewardSoft, Now.AddDays(-1), 2)
        };

        var result = harness.Settle(quote, lots, Now);

        result.Quote.Should().BeSameAs(quote);
        result.FundingFragments.Should().HaveCount(2);
        result.Credits.Where(credit => credit.Purpose == MarketplaceCreditPurpose.SellerProceeds)
            .Sum(credit => credit.Lot.Amount.Units).Should().Be(95 + 95_000);
        result.Credits.Where(credit => credit.Purpose == MarketplaceCreditPurpose.PlatformFee)
            .Sum(credit => credit.Lot.Amount.Units).Should().Be(5 + 5_000);
        result.FundingFragments.SelectMany(fragment => fragment.SelectedRanges)
            .Should().HaveCount(2);
    }

    [Fact]
    public void SettlementReplay_IsStableAndConflictsCannotReuseIdentity()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var lot = harness.Lot(
            CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);
        var command = harness.Command(quote, [lot], Now, "stable-settlement");

        var first = harness.Coordinator.Settle(command);
        harness.Coordinator.Settle(command).Should().BeSameAs(first);
        harness.Coordinator.Get(first.Id).Should().BeSameAs(first);

        harness.Invoking(value => value.Coordinator.Settle(
                value.Command(quote, [lot], Now, "stable-settlement") with
                {
                    Id = MarketplaceSettlementId.New()
                }))
            .Should().Throw<MarketplaceIdempotencyConflictException>();
        harness.Invoking(value => value.Coordinator.Settle(
                command with { IdempotencyKey = new IdempotencyKey("different-key") }))
            .Should().Throw<MarketplaceIdempotencyConflictException>();
    }

    [Fact]
    public void SettlementRisk_RequiresBoundEntitiesLimitsRootsAndCurrencyLegs()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var lot = harness.Lot(
            CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);
        var valid = harness.Command(quote, [lot], Now);
        var invalidApprovals = new[]
        {
            valid.Risk with
            {
                Context = valid.Risk.Context with
                {
                    CurrencyLegs = [new RiskCurrencyLeg(CurrencyCode.HardCoin, 19)]
                }
            },
            valid.Risk with
            {
                EntityCluster = valid.Risk.EntityCluster with
                {
                    Nodes = valid.Risk.EntityCluster.Nodes
                        .Where(node => node.Type != RiskEntityType.Product).ToArray()
                }
            },
            valid.Risk with
            {
                Limits = valid.Risk.Limits
                    .Where(limit => limit.Key.Dimension != RiskLimitDimension.Product).ToArray()
            },
            valid.Risk with
            {
                Limits = valid.Risk.Limits.Select(limit =>
                    limit.Key.Dimension == RiskLimitDimension.SourceRoot
                        ? new AggregateRiskLimit(
                            new RiskLimitKey(RiskLimitDimension.SourceRoot, "wrong-root"),
                            limit.CounterVersion, limit.MaxUnits, limit.Window)
                        : limit).ToArray()
            }
        };

        foreach (var approval in invalidApprovals)
        {
            var isolated = new Harness();
            var isolatedQuote = isolated.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
                .Quote(MarketplaceCurrencyChoice.Hard);
            var isolatedLot = isolated.Lot(
                CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);
            var command = isolated.Command(isolatedQuote, [isolatedLot], Now);
            var mutated = approval == invalidApprovals[0]
                ? command.Risk with
                {
                    Context = command.Risk.Context with
                    {
                        CurrencyLegs = [new RiskCurrencyLeg(CurrencyCode.HardCoin, 19)]
                    }
                }
                : approval == invalidApprovals[1]
                    ? command.Risk with
                    {
                        EntityCluster = command.Risk.EntityCluster with
                        {
                            Nodes = command.Risk.EntityCluster.Nodes
                                .Where(node => node.Type != RiskEntityType.Product).ToArray()
                        }
                    }
                    : approval == invalidApprovals[2]
                        ? command.Risk with
                        {
                            Limits = command.Risk.Limits
                                .Where(limit => limit.Key.Dimension != RiskLimitDimension.Product).ToArray()
                        }
                        : command.Risk with
                        {
                            Limits = command.Risk.Limits.Select(limit =>
                                limit.Key.Dimension == RiskLimitDimension.SourceRoot
                                    ? new AggregateRiskLimit(
                                        new RiskLimitKey(RiskLimitDimension.SourceRoot, "wrong-root"),
                                        limit.CounterVersion, limit.MaxUnits, limit.Window)
                                    : limit).ToArray()
                        };

            isolated.Invoking(value => value.Coordinator.Settle(command with { Risk = mutated }))
                .Should().Throw<MarketplaceRiskExposureException>();
            isolated.Coordinator.Count.Should().Be(0);
        }
    }

    [Fact]
    public void ReversalFenceAndEntitlementFailure_LeaveNoSettlement()
    {
        var fenced = new Harness();
        var quote = fenced.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var lot = fenced.Lot(
            CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);
        fenced.Fences.BeginReversal(lot.Ranges[0].Root);

        fenced.Invoking(value => value.Settle(quote, [lot], Now))
            .Should().Throw<RootReversalInProgressException>();
        fenced.Coordinator.Count.Should().Be(0);

        var failedEntitlement = new Harness();
        failedEntitlement.Entitlements.FailGrant = true;
        var failedQuote = failedEntitlement.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var failedLot = failedEntitlement.Lot(
            CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);

        failedEntitlement.Invoking(value => value.Settle(failedQuote, [failedLot], Now))
            .Should().Throw<MarketplaceEntitlementException>();
        failedEntitlement.Coordinator.Count.Should().Be(0);
    }

    [Fact]
    public void FullRefund_RestoresBuyerProvenanceAndRevokesEntitlement()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.FixedMix, 30, 30_000, 100_000)
            .Quote(MarketplaceCurrencyChoice.FixedMix);
        var hard = harness.Lot(
            CurrencyCode.HardCoin, 30, ProvenanceKind.PurchasedHard, Now.AddDays(-20), 1);
        var soft = harness.Lot(
            CurrencyCode.SoftCoin, 30_000, ProvenanceKind.AdRewardSoft, Now.AddDays(-10), 2);
        var settlement = harness.Settle(quote, [hard, soft], Now);

        var refund = harness.Refund(
            settlement,
            quote.Legs.Select(leg => leg.Amount).ToArray(),
            Now.AddDays(2));

        refund.IsFullRefund.Should().BeTrue();
        refund.EntitlementRevoked.Should().BeTrue();
        refund.RestoredBuyerLots.Should().HaveCount(2);
        refund.RestoredBuyerLots.Should().Contain(lot =>
            lot.Provenance == hard.Provenance &&
            lot.ConfirmedAt == hard.ConfirmedAt &&
            lot.OriginalMaturesAt == hard.OriginalMaturesAt &&
            lot.Ranges[0].Root == hard.Ranges[0].Root);
        refund.RestoredBuyerLots.Should().Contain(lot =>
            lot.Provenance == soft.Provenance &&
            lot.ConfirmedAt == soft.ConfirmedAt &&
            lot.OriginalMaturesAt == soft.OriginalMaturesAt &&
            lot.Ranges[0].Root == soft.Ranges[0].Root);
        harness.Coordinator.Get(settlement.Id).Status.Should().Be(MarketplaceSettlementStatus.Refunded);
        harness.Entitlements.RevokeCount.Should().Be(1);
    }

    [Fact]
    public void PartialThenFullRefund_ConsumesNonOverlappingOriginalRangesAndIsIdempotent()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.HardOnly, 100, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var parent = harness.Lot(
            CurrencyCode.HardCoin, 100, ProvenanceKind.PurchasedHard, Now.AddDays(-3), 1);
        var settlement = harness.Settle(quote, [parent], Now);

        var partialCommand = harness.RefundCommand(
            settlement, [new CoinAmount(CurrencyCode.HardCoin, 40)], Now.AddDays(1), "partial");
        var partial = harness.Coordinator.Refund(partialCommand);
        harness.Coordinator.Refund(partialCommand).Should().BeSameAs(partial);
        partial.IsFullRefund.Should().BeFalse();
        partial.EntitlementRevoked.Should().BeFalse();
        harness.Coordinator.Get(settlement.Id).Status.Should().Be(MarketplaceSettlementStatus.PartiallyRefunded);

        var final = harness.Refund(
            settlement, [new CoinAmount(CurrencyCode.HardCoin, 60)], Now.AddDays(2));
        final.IsFullRefund.Should().BeTrue();
        var firstRange = partial.RestoredBuyerLots.Single().Ranges.Single();
        var secondRange = final.RestoredBuyerLots.Single().Ranges.Single();
        firstRange.EndExclusive.Should().Be(secondRange.Start);
        harness.Entitlements.RevokeCount.Should().Be(1);

        harness.Invoking(value => value.Refund(
                settlement, [new CoinAmount(CurrencyCode.HardCoin, 1)], Now.AddDays(3)))
            .Should().Throw<MarketplaceAlreadyRefundedException>();
    }

    [Fact]
    public void FailedEntitlementRevocation_DoesNotConsumeRefundableLineage()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var settlement = harness.Settle(
            quote,
            [harness.Lot(CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1)],
            Now);
        harness.Entitlements.FailRevoke = true;

        harness.Invoking(value => value.Refund(
                settlement, [new CoinAmount(CurrencyCode.HardCoin, 20)], Now.AddDays(1)))
            .Should().Throw<MarketplaceEntitlementException>();
        harness.Coordinator.Get(settlement.Id).Status.Should().Be(MarketplaceSettlementStatus.Settled);

        harness.Entitlements.FailRevoke = false;
        harness.Refund(
            settlement, [new CoinAmount(CurrencyCode.HardCoin, 20)], Now.AddDays(2))
            .IsFullRefund.Should().BeTrue();
    }

    [Fact]
    public void RepeatedHardSettlements_HaveIndependentAuthoritativeMaturityClocks()
    {
        var first = new Harness();
        var firstQuote = first.Policy(ProductCurrencyMode.HardOnly, 10, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var firstResult = first.Settle(
            firstQuote,
            [first.Lot(CurrencyCode.HardCoin, 10, ProvenanceKind.PurchasedHard, Now.AddDays(-100), 1)],
            Now);

        var second = new Harness();
        var secondQuote = second.Policy(ProductCurrencyMode.HardOnly, 10, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var secondAt = Now.AddDays(1);
        var secondResult = second.Settle(
            secondQuote,
            [second.Lot(CurrencyCode.HardCoin, 10, ProvenanceKind.PurchasedHard, Now.AddDays(-100), 1)],
            secondAt);

        firstResult.Credits.Single().Lot.ConfirmedAt.Should().Be(Now);
        firstResult.Credits.Single().Lot.OriginalMaturesAt.Should().Be(Now.AddDays(120));
        secondResult.Credits.Single().Lot.ConfirmedAt.Should().Be(secondAt);
        secondResult.Credits.Single().Lot.OriginalMaturesAt.Should().Be(secondAt.AddDays(120));
    }

    [Fact]
    public void Settlement_RejectsWrongOwnershipFutureLotsDuplicateLotsAndInvalidCommands()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var validLot = harness.Lot(
            CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);
        var valid = harness.Command(quote, [validLot], Now);

        harness.Invoking(value => value.Coordinator.Settle(null!))
            .Should().Throw<ArgumentNullException>();
        harness.Invoking(value => value.Coordinator.Settle(valid with { AvailableBuyerLots = null! }))
            .Should().Throw<ArgumentNullException>();
        harness.Invoking(value => value.Coordinator.Settle(valid with { Risk = null! }))
            .Should().Throw<ArgumentNullException>();
        harness.Invoking(value => value.Coordinator.Settle(
                valid with { AvailableBuyerLots = [validLot, validLot] }))
            .Should().Throw<ArgumentException>();
        harness.Invoking(value => value.Coordinator.Settle(valid with
            {
                AvailableBuyerLots =
                [
                    harness.Lot(
                        CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard,
                        Now.AddDays(-1), 1, WalletId.New())
                ]
            }))
            .Should().Throw<InsufficientFragmentsException>();
        harness.Invoking(value => value.Coordinator.Settle(valid with
            {
                AvailableBuyerLots =
                [
                    harness.Lot(
                        CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard,
                        Now.AddMinutes(1), 1)
                ]
            }))
            .Should().Throw<InsufficientFragmentsException>();
    }

    private sealed class Harness
    {
        private long _sequence = 100;

        internal Harness()
        {
            Coordinator = new MarketplaceSettlementCoordinator(
                new MarketplaceRiskGate(Decisions, Counters),
                Fences,
                Entitlements);
        }

        internal Guid ProductId { get; } = Guid.Parse("c1000000-0000-0000-0000-000000000001");
        internal Guid BuyerId { get; } = Guid.Parse("c2000000-0000-0000-0000-000000000002");
        internal Guid SellerId { get; } = Guid.Parse("c3000000-0000-0000-0000-000000000003");
        internal WalletId BuyerWalletId { get; } = new(Guid.Parse("c4000000-0000-0000-0000-000000000004"));
        internal WalletId SellerWalletId { get; } = new(Guid.Parse("c5000000-0000-0000-0000-000000000005"));
        internal WalletId FeeWalletId { get; } = new(Guid.Parse("c6000000-0000-0000-0000-000000000006"));
        internal RiskDecisionAuthorizer Decisions { get; } = new();
        internal AggregateRiskCounterStore Counters { get; } = new();
        internal RootReversalFenceRegistry Fences { get; } = new();
        internal FakeEntitlementGateway Entitlements { get; } = new();
        internal MarketplaceSettlementCoordinator Coordinator { get; }

        internal ProductCurrencyPolicyVersion Policy(
            ProductCurrencyMode mode,
            long hard,
            long soft,
            int feePpm) =>
            ProductCurrencyPolicyVersion.Create(
                ProductId, SellerId, 1, mode, hard, soft, feePpm, Now.AddDays(-1));

        internal CreditLot Lot(
            CurrencyCode currency,
            long units,
            ProvenanceKind provenance,
            DateTimeOffset confirmedAt,
            long sequence,
            WalletId? walletId = null)
        {
            var scale = CurrencyTraceScale.For(currency);
            return new CreditLot(
                CreditLotId.New(),
                walletId ?? BuyerWalletId,
                new CoinAmount(currency, units),
                provenance,
                confirmedAt,
                confirmedAt.AddDays(currency == CurrencyCode.HardCoin && provenance == ProvenanceKind.EarnedHard ? 120 : 0),
                sequence,
                CreditLotState.Active,
                [new RootTraceRange(SourceStampId.New(), 0, checked(units * scale), 0)],
                scale);
        }

        internal MarketplaceSettlementResult Settle(
            MarketplaceQuoteSnapshot quote,
            IReadOnlyList<CreditLot> lots,
            DateTimeOffset at) =>
            Coordinator.Settle(Command(quote, lots, at));

        internal SettleMarketplaceOrderCommand Command(
            MarketplaceQuoteSnapshot quote,
            IReadOnlyList<CreditLot> lots,
            DateTimeOffset at,
            string? key = null)
        {
            var id = MarketplaceSettlementId.New();
            var roots = lots.SelectMany(lot => lot.Ranges).Select(range => range.Root)
                .Distinct().OrderBy(root => root.Value).ToArray();
            var cluster = Cluster();
            var idempotency = new IdempotencyKey(key ?? $"settle-{Guid.NewGuid():N}");
            var context = new ProtectedOperationContext(
                idempotency,
                BuyerId,
                PostingTemplateKind.Spend,
                BuyerWalletId,
                SellerWalletId,
                quote.Legs[0].Amount,
                quote.Legs.Select(leg => new RiskCurrencyLeg(leg.Currency, leg.Units)).ToArray(),
                roots,
                MarketplaceRiskGate.ProviderReference(id),
                new PolicyVersion(quote.PolicyVersion),
                new ReserveVersion(1),
                1,
                1,
                cluster.Version,
                cluster.EvidenceHash);
            var decision = RiskDecisionSnapshot.Create(
                Guid.NewGuid(), RiskOutcome.Allow, context,
                at.AddMinutes(-1), at.AddMinutes(1), [RiskReasonCode.WithinLimits]);
            var limits = Limits(roots, cluster);
            return new SettleMarketplaceOrderCommand(
                id,
                Guid.NewGuid(),
                ProductId,
                BuyerId,
                BuyerWalletId,
                SellerId,
                SellerWalletId,
                FeeWalletId,
                quote,
                lots,
                new MarketplaceRiskApproval(
                    decision,
                    context,
                    cluster,
                    limits,
                    quote.Legs.Select(_ => Guid.NewGuid()).ToArray()),
                at,
                at.AddDays(30),
                Interlocked.Add(ref _sequence, 100),
                idempotency);
        }

        internal MarketplaceRefundResult Refund(
            MarketplaceSettlementResult settlement,
            IReadOnlyList<CoinAmount> legs,
            DateTimeOffset at) =>
            Coordinator.Refund(RefundCommand(settlement, legs, at));

        internal RefundMarketplaceOrderCommand RefundCommand(
            MarketplaceSettlementResult settlement,
            IReadOnlyList<CoinAmount> legs,
            DateTimeOffset at,
            string? key = null) =>
            new(
                settlement.Id,
                BuyerId,
                BuyerWalletId,
                legs,
                Interlocked.Add(ref _sequence, 100),
                at,
                new IdempotencyKey(key ?? $"refund-{Guid.NewGuid():N}"));

        private EntityRiskCluster Cluster() =>
            new(
                "marketplace-cluster",
                1,
                "marketplace-evidence",
                [
                    new RiskEntityNode(RiskEntityType.Account, BuyerId.ToString("N")),
                    new RiskEntityNode(RiskEntityType.Account, SellerId.ToString("N")),
                    new RiskEntityNode(RiskEntityType.Referral, "related-accounts"),
                    new RiskEntityNode(RiskEntityType.Product, ProductId.ToString("N")),
                    new RiskEntityNode(
                        RiskEntityType.MarketplaceCounterparty,
                        MarketplaceRiskGate.CounterpartyPair(BuyerId, SellerId))
                ]);

        private IReadOnlyList<AggregateRiskLimit> Limits(
            IReadOnlyList<SourceStampId> roots,
            EntityRiskCluster cluster)
        {
            var limits = new List<AggregateRiskLimit>
            {
                Limit(RiskLimitDimension.Wallet, BuyerWalletId.Value.ToString("N")),
                Limit(RiskLimitDimension.IdentityCluster, cluster.Id),
                Limit(RiskLimitDimension.Destination, SellerWalletId.Value.ToString("N")),
                Limit(RiskLimitDimension.Destination, FeeWalletId.Value.ToString("N")),
                Limit(RiskLimitDimension.CounterpartyPair,
                    MarketplaceRiskGate.CounterpartyPair(BuyerId, SellerId)),
                Limit(RiskLimitDimension.Product, ProductId.ToString("N")),
                Limit(RiskLimitDimension.GlobalLossBudget,
                    MarketplaceRiskGate.RefundPattern(BuyerId))
            };
            limits.AddRange(roots.Select(root =>
                Limit(RiskLimitDimension.SourceRoot, root.Value.ToString("N"))));
            return limits;
        }

        private static AggregateRiskLimit Limit(RiskLimitDimension dimension, string subject) =>
            new(new RiskLimitKey(dimension, subject), 1, 1_000_000, TimeSpan.FromDays(1));
    }

    private sealed class FakeEntitlementGateway : IMarketplaceEntitlementGateway
    {
        internal bool FailGrant { get; set; }
        internal bool FailRevoke { get; set; }
        internal Func<MarketplaceEntitlementReceipt, MarketplaceEntitlementReceipt>? ReceiptMutator { get; set; }
        internal int GrantCount { get; private set; }
        internal int RevokeCount { get; private set; }

        public MarketplaceEntitlementReceipt Grant(MarketplaceEntitlementGrantRequest request)
        {
            if (FailGrant) throw new MarketplaceEntitlementException("grant failed");
            GrantCount++;
            var receipt = new MarketplaceEntitlementReceipt(
                Guid.NewGuid(), request.SettlementId, request.OrderId,
                request.ProductId, request.BuyerId, request.GrantedAt);
            return ReceiptMutator?.Invoke(receipt) ?? receipt;
        }

        public void Revoke(MarketplaceEntitlementReceipt receipt, DateTimeOffset revokedAt)
        {
            if (FailRevoke) throw new MarketplaceEntitlementException("revoke failed");
            RevokeCount++;
        }
    }
}
