using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Finance.Economy.Marketplace.UnitTests;

public sealed partial class MarketplaceSettlementCoordinatorTests
{
    [Fact]
    public void ConstructorsAndContracts_RejectUnboundDependenciesAndFragments()
    {
        var decisions = new RiskDecisionAuthorizer();
        var counters = new AggregateRiskCounterStore();
        var risk = new MarketplaceRiskGate(decisions, counters);
        var fences = new RootReversalFenceRegistry();
        var entitlements = new FakeEntitlementGateway();

        FluentActions.Invoking(() => new MarketplaceRiskGate(null!, counters))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new MarketplaceRiskGate(decisions, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new MarketplaceSettlementCoordinator(null!, fences, entitlements))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new MarketplaceSettlementCoordinator(risk, null!, entitlements))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new MarketplaceSettlementCoordinator(risk, fences, null!))
            .Should().Throw<ArgumentNullException>();

        FluentActions.Invoking(() => new MarketplaceSettlementId(Guid.Empty))
            .Should().Throw<ArgumentException>();
        MarketplaceSettlementId.New().Value.Should().NotBeEmpty();

        var harness = new Harness();
        var parent = harness.Lot(
            CurrencyCode.HardCoin, 10, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);
        var selection = FifoFragmentSelector
            .Select([parent], new CoinAmount(CurrencyCode.HardCoin, 10))
            .Selections.Single();

        FluentActions.Invoking(() => new MarketplaceFundingFragment(null!, selection))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new MarketplaceFundingFragment(parent, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() =>
                new MarketplaceFundingFragment(parent, selection with { ParentLotId = CreditLotId.New() }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() =>
                new MarketplaceFundingFragment(
                    parent,
                    selection with { Amount = new CoinAmount(CurrencyCode.SoftCoin, 10) }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() =>
                new MarketplaceFundingFragment(parent, selection with { TraceUnitsPerCoinUnit = 2 }))
            .Should().Throw<ArgumentException>();

        var fragment = new MarketplaceFundingFragment(parent, selection);
        fragment.ParentLot.Should().BeSameAs(parent);
        fragment.Selection.Should().BeSameAs(selection);
        fragment.Amount.Should().Be(selection.Amount);
        fragment.SelectedRanges.Should().Equal(selection.SelectedRanges);
    }

    [Fact]
    public void SettlementValidation_RejectsEveryMalformedBoundary()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var lot = harness.Lot(
            CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);
        var valid = harness.Command(quote, [lot], Now);
        var differentProductQuote = new MarketplaceQuoteSnapshot(
            Guid.NewGuid(), harness.SellerId, quote.PolicyVersion, quote.Mode, quote.Legs);
        var differentSellerQuote = new MarketplaceQuoteSnapshot(
            harness.ProductId, Guid.NewGuid(), quote.PolicyVersion, quote.Mode, quote.Legs);

        var malformed = new SettleMarketplaceOrderCommand[]
        {
            valid with { Quote = null! },
            valid with { OrderId = Guid.Empty },
            valid with { ProductId = Guid.Empty },
            valid with { BuyerId = Guid.Empty },
            valid with { SellerId = Guid.Empty },
            valid with { SellerId = harness.BuyerId },
            valid with { Quote = differentProductQuote },
            valid with { Quote = differentSellerQuote },
            valid with { SellerWalletId = harness.BuyerWalletId },
            valid with { PlatformFeeWalletId = harness.BuyerWalletId },
            valid with { PlatformFeeWalletId = harness.SellerWalletId },
            valid with { RefundHoldUntil = Now },
            valid with { FirstJournalSequence = 0 }
        };

        foreach (var command in malformed)
        {
            harness.Invoking(value => value.Coordinator.Settle(command))
                .Should().Throw<Exception>();
            harness.Coordinator.Count.Should().Be(0);
        }

        harness.Invoking(value => value.Coordinator.Settle(
                valid with { AvailableBuyerLots = [lot, null!] }))
            .Should().Throw<Exception>();
    }

    [Fact]
    public void RiskGate_RejectsNullApprovalComponentsAndExposesRequiredControls()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var lot = harness.Lot(
            CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);
        var command = harness.Command(quote, [lot], Now);
        var selection = FifoFragmentSelector.Select([lot], quote.Legs[0].Amount).Selections.Single();
        var funding = new[] { new MarketplaceFundingFragment(lot, selection) };
        var gate = new MarketplaceRiskGate(harness.Decisions, harness.Counters);

        MarketplaceRiskGate.RequiredEntityTypes.Should().BeEquivalentTo(
            new[]
            {
                RiskEntityType.Account, RiskEntityType.Referral,
                RiskEntityType.Product, RiskEntityType.MarketplaceCounterparty
            });
        MarketplaceRiskGate.RequiredLimitDimensions.Should().Contain(
            RiskLimitDimension.GlobalLossBudget);

        FluentActions.Invoking(() => gate.Authorize(null!, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gate.Authorize(command, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gate.Authorize(command with { Risk = null! }, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() =>
                gate.Authorize(command with { Risk = command.Risk with { Decision = null! } }, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() =>
                gate.Authorize(command with { Risk = command.Risk with { Context = null! } }, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() =>
                gate.Authorize(command with { Risk = command.Risk with { EntityCluster = null! } }, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() =>
                gate.Authorize(command with { Risk = command.Risk with { Limits = null! } }, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() =>
                gate.Authorize(
                    command with { Risk = command.Risk with { CounterReservationIds = null! } },
                    funding))
            .Should().Throw<ArgumentNullException>();

        var authorization = gate.Authorize(command, funding);
        authorization.Decision.DecisionId.Should().Be(command.Risk.Decision.Id);
        authorization.CounterReservations.Should().ContainSingle();
    }

    [Fact]
    public void RiskGate_RejectsEveryUnboundSettlementContextField()
    {
        var mutations = new Func<SettleMarketplaceOrderCommand, SettleMarketplaceOrderCommand>[]
        {
            command => command with { BuyerId = Guid.Empty },
            command => WithContext(command, command.Risk.Context with { ActorId = Guid.NewGuid() }),
            command => WithContext(command, command.Risk.Context with { Operation = PostingTemplateKind.Burn }),
            command => WithContext(command, command.Risk.Context with { SourceWalletId = WalletId.New() }),
            command => WithContext(command, command.Risk.Context with { DestinationWalletId = WalletId.New() }),
            command => WithContext(command, command.Risk.Context with
            {
                Amount = new CoinAmount(CurrencyCode.HardCoin, command.Quote.Legs[0].Units - 1)
            }),
            command => WithContext(command, command.Risk.Context with { CurrencyLegs = [] }),
            command => WithContext(command, command.Risk.Context with { SourceRoots = [] }),
            command => WithContext(command, command.Risk.Context with { ProviderReferenceHash = "wrong" }),
            command => WithContext(command, command.Risk.Context with { PolicyVersion = new PolicyVersion(99) }),
            command => WithContext(command, command.Risk.Context with { EntityGraphVersion = 99 }),
            command => WithContext(command, command.Risk.Context with { EntityGraphEvidenceHash = "wrong" })
        };

        foreach (var mutate in mutations)
        {
            var (harness, gate, command, funding) = ValidRiskInvocation();
            FluentActions.Invoking(() => gate.Authorize(mutate(command), funding))
                .Should().Throw<MarketplaceRiskExposureException>();
            harness.Counters.Reservations.Should().BeEmpty();
        }
    }

    [Fact]
    public void RiskGate_RejectsMissingOrMismatchedEntityGraphNodes()
    {
        var mutations = new Func<SettleMarketplaceOrderCommand, EntityRiskCluster>[]
        {
            command => command.Risk.EntityCluster with
            {
                Nodes = command.Risk.EntityCluster.Nodes
                    .Where(node => node.Type != RiskEntityType.Referral).ToArray()
            },
            command => ReplaceNode(
                command.Risk.EntityCluster, RiskEntityType.Account,
                command.BuyerId.ToString("N"), "wrong-buyer"),
            command => ReplaceNode(
                command.Risk.EntityCluster, RiskEntityType.Account,
                command.SellerId.ToString("N"), "wrong-seller"),
            command => ReplaceNode(
                command.Risk.EntityCluster, RiskEntityType.Product,
                command.ProductId.ToString("N"), "wrong-product"),
            command => ReplaceNode(
                command.Risk.EntityCluster, RiskEntityType.MarketplaceCounterparty,
                MarketplaceRiskGate.CounterpartyPair(command.BuyerId, command.SellerId),
                "wrong-counterparty")
        };

        foreach (var mutate in mutations)
        {
            var (_, gate, command, funding) = ValidRiskInvocation();
            var cluster = mutate(command);
            var context = command.Risk.Context with
            {
                EntityGraphVersion = cluster.Version,
                EntityGraphEvidenceHash = cluster.EvidenceHash
            };
            var risk = command.Risk with { EntityCluster = cluster, Context = context };

            FluentActions.Invoking(() => gate.Authorize(command with { Risk = risk }, funding))
                .Should().Throw<MarketplaceRiskExposureException>();
        }
    }

    [Fact]
    public void RiskGate_RejectsMissingDimensionsSubjectsRootsAndReservations()
    {
        var mutations = new Func<SettleMarketplaceOrderCommand, MarketplaceRiskApproval>[]
        {
            command => command.Risk with
            {
                Limits = command.Risk.Limits
                    .Where(limit => limit.Key.Dimension != RiskLimitDimension.IdentityCluster).ToArray()
            },
            command => ReplaceLimit(command.Risk, RiskLimitDimension.Wallet,
                command.BuyerWalletId.Value.ToString("N")),
            command => ReplaceLimit(command.Risk, RiskLimitDimension.IdentityCluster,
                command.Risk.EntityCluster.Id),
            command => ReplaceLimit(command.Risk, RiskLimitDimension.Destination,
                command.SellerWalletId.Value.ToString("N")),
            command => ReplaceLimit(command.Risk, RiskLimitDimension.Destination,
                command.PlatformFeeWalletId.Value.ToString("N")),
            command => ReplaceLimit(command.Risk, RiskLimitDimension.CounterpartyPair,
                MarketplaceRiskGate.CounterpartyPair(command.BuyerId, command.SellerId)),
            command => ReplaceLimit(command.Risk, RiskLimitDimension.Product,
                command.ProductId.ToString("N")),
            command => ReplaceLimit(command.Risk, RiskLimitDimension.GlobalLossBudget,
                MarketplaceRiskGate.RefundPattern(command.BuyerId)),
            command => ReplaceLimit(command.Risk, RiskLimitDimension.SourceRoot,
                command.Risk.Context.SourceRoots[0].Value.ToString("N")),
            command => command.Risk with { CounterReservationIds = [] },
            command => command.Risk with { CounterReservationIds = [Guid.Empty] },
            command => command.Risk with
            {
                CounterReservationIds =
                [
                    command.Risk.CounterReservationIds[0],
                    command.Risk.CounterReservationIds[0]
                ]
            }
        };

        foreach (var mutate in mutations)
        {
            var (_, gate, command, funding) = ValidRiskInvocation();
            FluentActions.Invoking(() =>
                    gate.Authorize(command with { Risk = mutate(command) }, funding))
                .Should().Throw<MarketplaceRiskExposureException>();
        }
    }

    [Fact]
    public void RiskHelperIdentifiers_RejectEmptyActors()
    {
        FluentActions.Invoking(() => MarketplaceRiskGate.CounterpartyPair(Guid.Empty, Guid.NewGuid()))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => MarketplaceRiskGate.CounterpartyPair(Guid.NewGuid(), Guid.Empty))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => MarketplaceRiskGate.RefundPattern(Guid.Empty))
            .Should().Throw<ArgumentException>();

        var buyer = Guid.NewGuid();
        var seller = Guid.NewGuid();
        MarketplaceRiskGate.CounterpartyPair(buyer, seller)
            .Should().Be($"{buyer:N}:{seller:N}");
        MarketplaceRiskGate.RefundPattern(buyer).Should().Be($"refund-pattern:{buyer:N}");
    }

    [Fact]
    public void EntitlementReceipt_MustBindEverySettlementIdentity()
    {
        var mutations = new Func<MarketplaceEntitlementReceipt, MarketplaceEntitlementReceipt>[]
        {
            receipt => receipt with { Id = Guid.Empty },
            receipt => receipt with { SettlementId = MarketplaceSettlementId.New() },
            receipt => receipt with { OrderId = Guid.NewGuid() },
            receipt => receipt with { ProductId = Guid.NewGuid() },
            receipt => receipt with { BuyerId = Guid.NewGuid() }
        };

        foreach (var mutate in mutations)
        {
            var harness = new Harness();
            harness.Entitlements.ReceiptMutator = mutate;
            var quote = harness.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
                .Quote(MarketplaceCurrencyChoice.Hard);
            var lot = harness.Lot(
                CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);

            harness.Invoking(value => value.Settle(quote, [lot], Now))
                .Should().Throw<MarketplaceEntitlementException>();
            harness.Coordinator.Count.Should().Be(0);
        }
    }

    [Fact]
    public void SettlementResult_ExposesSnapshotAndRefundAggregate()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var settlement = harness.Settle(
            quote,
            [harness.Lot(
                CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1)],
            Now);

        settlement.Id.Value.Should().NotBeEmpty();
        settlement.OrderId.Should().NotBeEmpty();
        settlement.ProductId.Should().Be(harness.ProductId);
        settlement.BuyerId.Should().Be(harness.BuyerId);
        settlement.SellerId.Should().Be(harness.SellerId);
        settlement.Quote.Should().BeSameAs(quote);
        settlement.FundingFragments.Should().ContainSingle();
        settlement.Credits.Should().ContainSingle();
        settlement.Entitlement.Id.Should().NotBeEmpty();
        settlement.SettledAt.Should().Be(Now);
        settlement.RefundedLegs.Should().BeEmpty();

        var refund = harness.Refund(
            settlement, [new CoinAmount(CurrencyCode.HardCoin, 5)], Now.AddDays(1));

        settlement.RefundedLegs.Should().Equal(
            new CoinAmount(CurrencyCode.HardCoin, 5));
        refund.SettlementId.Should().Be(settlement.Id);
        refund.RefundedLegs.Should().Equal(new CoinAmount(CurrencyCode.HardCoin, 5));
        refund.RestoredBuyerLots.Should().ContainSingle();
        refund.IsFullRefund.Should().BeFalse();
        refund.EntitlementRevoked.Should().BeFalse();

        var lineage = settlement.Credits.Single().ParentLineage.Single();
        lineage.ParentLot.Should().BeSameAs(settlement.FundingFragments.Single().ParentLot);
        lineage.Amount.Units.Should().Be(20);
        lineage.Ranges.Should().NotBeEmpty();
    }

    [Fact]
    public void Refund_RejectsReplayConflictsUnknownSettlementOwnershipCurrencyAndOverdraft()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var first = harness.Settle(
            quote,
            [harness.Lot(
                CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-2), 1)],
            Now);
        var second = harness.Settle(
            quote,
            [harness.Lot(
                CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 2)],
            Now);
        var firstRefund = harness.RefundCommand(
            first, [new CoinAmount(CurrencyCode.HardCoin, 5)], Now.AddDays(1), "shared-refund");
        harness.Coordinator.Refund(firstRefund);

        harness.Invoking(value => value.Coordinator.Refund(
                value.RefundCommand(
                    second, [new CoinAmount(CurrencyCode.HardCoin, 5)],
                    Now.AddDays(1), "shared-refund")))
            .Should().Throw<MarketplaceIdempotencyConflictException>();
        harness.Invoking(value => value.Coordinator.Refund(
                value.RefundCommand(
                    second, [new CoinAmount(CurrencyCode.HardCoin, 1)], Now.AddDays(1)) with
                {
                    SettlementId = MarketplaceSettlementId.New()
                }))
            .Should().Throw<KeyNotFoundException>();
        harness.Invoking(value => value.Coordinator.Refund(
                value.RefundCommand(
                    second, [new CoinAmount(CurrencyCode.HardCoin, 1)], Now.AddDays(1)) with
                {
                    BuyerId = Guid.NewGuid()
                }))
            .Should().Throw<MarketplaceRefundException>();
        harness.Invoking(value => value.Coordinator.Refund(
                value.RefundCommand(
                    second, [new CoinAmount(CurrencyCode.HardCoin, 1)], Now.AddDays(1)) with
                {
                    BuyerWalletId = WalletId.New()
                }))
            .Should().Throw<MarketplaceRefundException>();
        harness.Invoking(value => value.Refund(
                second, [new CoinAmount(CurrencyCode.SoftCoin, 1)], Now.AddDays(1)))
            .Should().Throw<MarketplaceRefundException>();
        harness.Invoking(value => value.Refund(
                second, [new CoinAmount(CurrencyCode.HardCoin, 21)], Now.AddDays(1)))
            .Should().Throw<InsufficientFragmentsException>();
        harness.Invoking(value => value.Coordinator.Get(MarketplaceSettlementId.New()))
            .Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void RefundValidation_RejectsMalformedCommands()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.FixedMix, 20, 20_000, 0)
            .Quote(MarketplaceCurrencyChoice.FixedMix);
        var settlement = harness.Settle(
            quote,
            [
                harness.Lot(
                    CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1),
                harness.Lot(
                    CurrencyCode.SoftCoin, 20_000, ProvenanceKind.AdRewardSoft, Now.AddDays(-1), 2)
            ],
            Now);
        var valid = harness.RefundCommand(
            settlement, [new CoinAmount(CurrencyCode.HardCoin, 1)], Now.AddDays(1));

        FluentActions.Invoking(() => harness.Coordinator.Refund(null!))
            .Should().Throw<ArgumentNullException>();
        harness.Invoking(value => value.Coordinator.Refund(valid with { RefundLegs = null! }))
            .Should().Throw<ArgumentNullException>();
        harness.Invoking(value => value.Coordinator.Refund(valid with { BuyerId = Guid.Empty }))
            .Should().Throw<ArgumentException>();
        harness.Invoking(value => value.Coordinator.Refund(valid with { RefundLegs = [] }))
            .Should().Throw<MarketplaceRefundException>();
        harness.Invoking(value => value.Coordinator.Refund(valid with
            {
                RefundLegs = [new CoinAmount(CurrencyCode.HardCoin, 0)]
            }))
            .Should().Throw<MarketplaceRefundException>();
        harness.Invoking(value => value.Coordinator.Refund(valid with
            {
                RefundLegs =
                [
                    new CoinAmount(CurrencyCode.HardCoin, 1),
                    new CoinAmount(CurrencyCode.HardCoin, 1)
                ]
            }))
            .Should().Throw<MarketplaceRefundException>();
        harness.Invoking(value => value.Coordinator.Refund(valid with { FirstJournalSequence = 0 }))
            .Should().Throw<ArgumentOutOfRangeException>();

        new MarketplaceRefundException("refund").Message.Should().Be("refund");
    }

    [Fact]
    public void MarketplaceModule_IsEnabledAndRegistersDurableWorkflows()
    {
        var module = new MarketplaceModule();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        module.Name.Should().Be("Finance.Economy.Marketplace");
        module.EnabledByDefault.Should().BeTrue();
        module.ConfigureServices(services, configuration).Should().BeSameAs(services);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IDurableMarketplaceSettlementService) &&
            descriptor.ImplementationType == typeof(DurableMarketplaceSettlementService));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IDurableMarketplaceRefundService) &&
            descriptor.ImplementationType == typeof(DurableMarketplaceRefundService));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IMarketplaceOutboxProcessor) &&
            descriptor.ImplementationType == typeof(PostgreSqlMarketplaceOutboxProcessor));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(GameGuild.Commerce.Orders.IOrderMarketplaceSettlementAuthority) &&
            descriptor.ImplementationType == typeof(CommerceOrderMarketplaceSettlementAuthority));
        services.AddMarketplaceComposition(configuration).Should().BeSameAs(services);
    }

    private static SettleMarketplaceOrderCommand WithContext(
        SettleMarketplaceOrderCommand command,
        ProtectedOperationContext context) =>
        command with
        {
            Risk = command.Risk with
            {
                Context = context,
                Decision = RiskDecisionSnapshot.Create(
                    Guid.NewGuid(),
                    RiskOutcome.Allow,
                    context,
                    command.SettledAt.AddMinutes(-1),
                    command.SettledAt.AddMinutes(1),
                    [RiskReasonCode.WithinLimits])
            }
        };

    private static EntityRiskCluster ReplaceNode(
        EntityRiskCluster cluster,
        RiskEntityType type,
        string currentSubject,
        string replacementSubject) =>
        cluster with
        {
            Nodes = cluster.Nodes.Select(node =>
                    node.Type == type &&
                    string.Equals(node.IdentifierHash, currentSubject, StringComparison.Ordinal)
                        ? new RiskEntityNode(type, replacementSubject)
                        : node)
                .ToArray()
        };

    private static MarketplaceRiskApproval ReplaceLimit(
        MarketplaceRiskApproval risk,
        RiskLimitDimension dimension,
        string currentSubject) =>
        risk with
        {
            Limits = risk.Limits.Select(limit =>
                    limit.Key.Dimension == dimension &&
                    string.Equals(limit.Key.SubjectHash, currentSubject, StringComparison.Ordinal)
                        ? new AggregateRiskLimit(
                            new RiskLimitKey(dimension, $"wrong-{currentSubject}"),
                            limit.CounterVersion,
                            limit.MaxUnits,
                            limit.Window)
                        : limit)
                .ToArray()
        };

    private static (
        Harness Harness,
        MarketplaceRiskGate Gate,
        SettleMarketplaceOrderCommand Command,
        MarketplaceFundingFragment[] Funding) ValidRiskInvocation()
    {
        var harness = new Harness();
        var quote = harness.Policy(ProductCurrencyMode.HardOnly, 20, 0, 0)
            .Quote(MarketplaceCurrencyChoice.Hard);
        var lot = harness.Lot(
            CurrencyCode.HardCoin, 20, ProvenanceKind.PurchasedHard, Now.AddDays(-1), 1);
        var command = harness.Command(quote, [lot], Now);
        var selection = FifoFragmentSelector.Select([lot], quote.Legs[0].Amount).Selections.Single();

        return (
            harness,
            new MarketplaceRiskGate(harness.Decisions, harness.Counters),
            command,
            [new MarketplaceFundingFragment(lot, selection)]);
    }
}
