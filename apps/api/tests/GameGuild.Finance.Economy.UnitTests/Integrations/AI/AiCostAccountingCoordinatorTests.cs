using FluentAssertions;
using GameGuild.Finance.Economy.Integrations.AI;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using Xunit;

namespace GameGuild.Finance.Economy.UnitTests;

public sealed class AiCostAccountingCoordinatorTests
{
    [Fact]
    public void Authorize_ReservesSafetyPricedSoftFragmentsAfterRiskApproval()
    {
        var harness = new Harness();

        var authorization = harness.Coordinator.Authorize(harness.Command());

        authorization.Status.Should().Be(AiServiceChargeStatus.Reserved);
        authorization.SourceWalletId.Should().NotBe(authorization.ServiceWalletId);
        authorization.AuthorizedAt.Should().Be(Harness.Now);
        authorization.Reservation.ReservedAt.Should().Be(Harness.Now);
        authorization.Price.PriceSoftUnits.Should().Be(100_000);
        authorization.FundingFragments.Should().ContainSingle();
        authorization.FundingFragments[0].Amount.Should().Be(new CoinAmount(CurrencyCode.SoftCoin, 100_000));
        harness.Gateway.Reservations.Should().ContainSingle();
        harness.Gateway.Events.Should().Equal("reserve");
    }

    [Fact]
    public void Authorize_IsIdempotentAndRejectsKeyReuseForAnotherRequest()
    {
        var harness = new Harness();
        var command = harness.Command();

        var first = harness.Coordinator.Authorize(command);
        var duplicate = harness.Coordinator.Authorize(command);

        duplicate.Should().BeSameAs(first);
        harness.Gateway.Reservations.Should().ContainSingle();
        var conflicting = harness.Command(requestId: Guid.NewGuid()) with { IdempotencyKey = command.IdempotencyKey };
        FluentActions.Invoking(() => harness.Coordinator.Authorize(conflicting))
            .Should().Throw<AiCostAccountingIdempotencyException>();
    }

    [Fact]
    public void Authorize_RejectsAggregateLimitBeforeChargeReservation()
    {
        var harness = new Harness();
        var command = harness.Command(maxUnits: 99_999);

        FluentActions.Invoking(() => harness.Coordinator.Authorize(command))
            .Should().Throw<AggregateRiskLimitExceededException>();

        harness.Gateway.Reservations.Should().BeEmpty();
    }

    [Fact]
    public void Authorize_RejectsRiskDecisionNotBoundToFinalProviderAndFundingRoots()
    {
        var harness = new Harness();
        var valid = harness.Command();
        var foreignContext = valid.Risk.Context with { ProviderReferenceHash = "wrong" };
        var invalid = valid with
        {
            Risk = valid.Risk with
            {
                Context = foreignContext,
                Decision = RiskDecisionSnapshot.Create(
                    Guid.NewGuid(),
                    RiskOutcome.Allow,
                    foreignContext,
                    Harness.Now.AddMinutes(-1),
                    Harness.Now.AddMinutes(5),
                    [RiskReasonCode.WithinLimits])
            }
        };

        FluentActions.Invoking(() => harness.Coordinator.Authorize(invalid))
            .Should().Throw<AiCostRiskExposureException>();
        harness.Gateway.Reservations.Should().BeEmpty();
    }

    [Fact]
    public void Complete_FinalizesChargePersistsExactCostAndPublishesTreasuryFacts()
    {
        var harness = new Harness();
        var authorization = harness.Coordinator.Authorize(harness.Command());

        var fact = harness.Coordinator.Complete(harness.Completion(authorization.Id));

        fact.Provider.Should().Be(AiProvider.OpenAi);
        fact.Model.Should().Be("gpt-test");
        fact.InputTokens.Should().Be(1_250);
        fact.OutputTokens.Should().Be(750);
        fact.TotalTokens.Should().Be(2_000);
        fact.InputCostUsdNanos.Should().Be(500_000);
        fact.OutputCostUsdNanos.Should().Be(300_000);
        fact.ExactProviderCostUsdNanos.Should().Be(800_000);
        fact.ChargedSoftUnits.Should().Be(100_000);
        fact.RateCardVersion.Should().Be("rate-v1");
        authorization.Status.Should().Be(AiServiceChargeStatus.Completed);
        harness.Store.Facts.Should().ContainSingle().Which.Should().BeSameAs(fact);
        harness.Publisher.Facts.Should().ContainSingle();
        harness.Gateway.Events.Should().Equal("reserve", "finalize");
    }

    [Fact]
    public void Complete_ReplaysSameProviderUsageWithoutDoubleChargeOrDuplicateFact()
    {
        var harness = new Harness();
        var authorization = harness.Coordinator.Authorize(harness.Command());
        var command = harness.Completion(authorization.Id);

        var first = harness.Coordinator.Complete(command);
        var duplicate = harness.Coordinator.Complete(command);

        duplicate.Should().BeSameAs(first);
        harness.Store.Facts.Should().ContainSingle();
        harness.Gateway.Events.Should().Equal("reserve", "finalize");
    }

    [Fact]
    public void Complete_RejectsProviderUsageReplayAcrossAuthorizations()
    {
        var harness = new Harness();
        var first = harness.Coordinator.Authorize(harness.Command());
        harness.Coordinator.Complete(harness.Completion(first.Id));
        var second = harness.Coordinator.Authorize(harness.Command(requestId: Guid.NewGuid()));

        var replay = harness.Completion(second.Id) with { ProviderUsageId = "usage-1" };

        FluentActions.Invoking(() => harness.Coordinator.Complete(replay))
            .Should().Throw<AiProviderUsageReplayException>();
    }

    [Fact]
    public void Complete_RejectsUsageAboveReservedTokenEnvelopeBeforeFinalization()
    {
        var harness = new Harness();
        var authorization = harness.Coordinator.Authorize(harness.Command());
        var completion = harness.Completion(authorization.Id) with { InputTokens = 1_000_001 };

        FluentActions.Invoking(() => harness.Coordinator.Complete(completion))
            .Should().Throw<AiProviderUsageExceededReservationException>();

        harness.Gateway.Events.Should().Equal("reserve");
        harness.Store.Facts.Should().BeEmpty();
    }

    [Fact]
    public void Fail_ReleasesReservationAndDoesNotCreateProviderCostFact()
    {
        var harness = new Harness();
        var authorization = harness.Coordinator.Authorize(harness.Command());

        var released = harness.Coordinator.Fail(new FailAiServiceCommand(
            authorization.Id,
            "provider-timeout",
            Harness.Now.AddMinutes(1),
            new IdempotencyKey("fail-1")));
        var duplicate = harness.Coordinator.Fail(new FailAiServiceCommand(
            authorization.Id,
            "provider-timeout",
            Harness.Now.AddMinutes(1),
            new IdempotencyKey("fail-1")));

        released.Status.Should().Be(AiServiceChargeStatus.Released);
        duplicate.Should().BeSameAs(released);
        harness.Gateway.Events.Should().Equal("reserve", "release");
        harness.Store.Facts.Should().BeEmpty();
    }

    [Fact]
    public void Complete_CompensatesFinalizedChargeWhenCostFactPersistenceFails()
    {
        var harness = new Harness { Store = { Failure = new InvalidOperationException("database unavailable") } };
        var authorization = harness.Coordinator.Authorize(harness.Command());

        FluentActions.Invoking(() => harness.Coordinator.Complete(harness.Completion(authorization.Id)))
            .Should().Throw<AiChargeCompensationException>().WithInnerException<InvalidOperationException>();

        authorization.Status.Should().Be(AiServiceChargeStatus.Compensated);
        harness.Gateway.Events.Should().Equal("reserve", "finalize", "compensate");
        harness.Publisher.Facts.Should().BeEmpty();
    }

    [Fact]
    public void TreasuryFact_ProducesReservedServiceObservationForConservativePortfolioFormula()
    {
        var harness = new Harness();
        var authorization = harness.Coordinator.Authorize(harness.Command());
        harness.Coordinator.Complete(harness.Completion(authorization.Id));
        var treasuryFact = harness.Publisher.Facts.Single();
        treasuryFact.ProviderCostFactId.Should().Be(harness.Store.Facts.Single().Id);
        treasuryFact.ObservedAt.Should().Be(Harness.Now.AddMinutes(1));
        var observation = treasuryFact.ToReserveObservation(100_000);
        var expensive = observation with
        {
            ServiceCode = "ai.expensive",
            CurrentServicePriceSoftUnits = 50_000,
            CurrentProviderCostUsdNanos = 900_000_000,
            TrailingHighPercentileCostUsdNanos = 900_000_000,
            ProviderFxStressCostUsdNanos = 900_000_000,
            ReservedSoftUnits = 0
        };

        var stressed = ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
            outstandingSoftUnits: 150_000,
            unreservedSoftUnits: 50_000,
            irreversibleInFlightProviderCostUsdNanos: 10,
            [observation, expensive],
            Harness.Now.AddMinutes(1));

        stressed.Should().Be(1_700_000_010);
    }

    internal sealed class Harness
    {
        internal static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
        private readonly AiServiceRateCardCatalog _catalog = new();
        private readonly RootReversalFenceRegistry _fences = new();
        private readonly RiskDecisionAuthorizer _decisions = new();
        private readonly AggregateRiskCounterStore _counters = new();
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly Guid _actorId = Guid.NewGuid();
        private readonly WalletId _walletId = WalletId.New();
        private readonly WalletId _serviceWalletId = WalletId.New();
        private long _journalSequence = 1;

        internal Harness()
        {
            _catalog.Publish(AiServicePriceSnapshot.Create(
                "ai.grade",
                new AiProviderRateCard(
                    "rate-v1", AiProvider.OpenAi, "gpt-test",
                    400_000_000, 400_000_000, Now.AddMinutes(-5), Now.AddMinutes(5)),
                1_000_000,
                1_000_000,
                700_000_000,
                800_000_000,
                200_000,
                Now.AddMinutes(-5),
                Now.AddMinutes(5)));
            RiskGate = new AiCostRiskGate(_decisions, _counters);
            Coordinator = new AiCostAccountingCoordinator(
                _catalog,
                RiskGate,
                _fences,
                Gateway,
                Store,
                Publisher);
        }

        internal RecordingChargeGateway Gateway { get; } = new();
        internal RecordingCostFactStore Store { get; } = new();
        internal RecordingTreasuryPublisher Publisher { get; } = new();
        internal AiCostAccountingCoordinator Coordinator { get; }
        internal AiCostRiskGate RiskGate { get; }
        internal AiServiceRateCardCatalog Catalog => _catalog;
        internal RootReversalFenceRegistry Fences => _fences;

        internal IReadOnlyList<AiFundingFragment> Funding(AuthorizeAiServiceCommand command)
        {
            var parents = command.AvailableFundingLots.ToDictionary(lot => lot.Id);
            var selection = FifoFragmentSelector.Select(
                command.AvailableFundingLots,
                new CoinAmount(CurrencyCode.SoftCoin, 100_000));
            return selection.Selections.Select(item => new AiFundingFragment(parents[item.ParentLotId], item)).ToArray();
        }

        internal AuthorizeAiServiceCommand Command(Guid? requestId = null, long maxUnits = 1_000_000)
        {
            var id = requestId ?? Guid.NewGuid();
            var lot = Lot(id);
            var roots = lot.Ranges.Select(range => range.Root).ToArray();
            var cluster = new EntityRiskCluster(
                $"cluster:{_actorId:N}",
                1,
                "entity-evidence",
                [
                    new RiskEntityNode(RiskEntityType.Account, _actorId.ToString("N")),
                    new RiskEntityNode(RiskEntityType.Tenant, _tenantId.ToString("N")),
                    new RiskEntityNode(RiskEntityType.ProviderObject, AiCostRiskGate.ProviderAccount(AiProvider.OpenAi, "gpt-test")),
                    new RiskEntityNode(RiskEntityType.Session, id.ToString("N"))
                ]);
            var limits = new[]
            {
                Limit(RiskLimitDimension.Wallet, _walletId.Value.ToString("N"), maxUnits),
                Limit(RiskLimitDimension.IdentityCluster, cluster.Id, maxUnits),
                Limit(RiskLimitDimension.Tenant, _tenantId.ToString("N"), maxUnits),
                Limit(RiskLimitDimension.ProviderAccount, AiCostRiskGate.ProviderAccount(AiProvider.OpenAi, "gpt-test"), maxUnits),
                Limit(RiskLimitDimension.DeviceIpAsnCluster, "device-ip-asn:trusted", maxUnits),
                Limit(RiskLimitDimension.GlobalLossBudget, AiCostRiskGate.GlobalLossBudget(_tenantId), maxUnits)
            };
            var context = new ProtectedOperationContext(
                new IdempotencyKey($"authorize-{id:N}"),
                _actorId,
                PostingTemplateKind.Burn,
                _walletId,
                _serviceWalletId,
                new CoinAmount(CurrencyCode.SoftCoin, 100_000),
                [new RiskCurrencyLeg(CurrencyCode.SoftCoin, 100_000)],
                roots,
                AiCostRiskGate.ProviderReference(id, "ai.grade", AiProvider.OpenAi, "gpt-test"),
                new PolicyVersion(1),
                new ReserveVersion(1),
                1,
                1,
                cluster.Version,
                cluster.EvidenceHash);
            var decision = RiskDecisionSnapshot.Create(
                Guid.NewGuid(),
                RiskOutcome.Allow,
                context,
                Now.AddMinutes(-1),
                Now.AddMinutes(5),
                [RiskReasonCode.WithinLimits]);
            return new AuthorizeAiServiceCommand(
                id,
                _tenantId,
                _actorId,
                _walletId,
                _serviceWalletId,
                "ai.grade",
                AiProvider.OpenAi,
                "gpt-test",
                [lot],
                new AiCostRiskApproval(decision, context, cluster, limits, Guid.NewGuid()),
                Now,
                new IdempotencyKey($"authorize-{id:N}"));
        }

        internal CompleteAiServiceCommand Completion(Guid authorizationId) => new(
            authorizationId,
            "usage-1",
            AiProvider.OpenAi,
            "gpt-test",
            1_250,
            750,
            2_000,
            Now.AddMinutes(1),
            new IdempotencyKey("complete-1"));

        internal CreditLot Lot(Guid requestId)
        {
            var root = new SourceStampId(requestId);
            return new CreditLot(
                CreditLotId.New(),
                _walletId,
                new CoinAmount(CurrencyCode.SoftCoin, 200_000),
                ProvenanceKind.AdRewardSoft,
                Now.AddDays(-1),
                Now.AddDays(-1),
                _journalSequence++,
                CreditLotState.Active,
                [new RootTraceRange(root, 0, 200_000, 0)]);
        }

        private static AggregateRiskLimit Limit(RiskLimitDimension dimension, string subject, long maxUnits) =>
            new(new RiskLimitKey(dimension, subject), 1, maxUnits, TimeSpan.FromDays(1));
    }

    internal sealed class RecordingChargeGateway : IAiSoftChargeGateway
    {
        internal List<AiSoftChargeReservation> Reservations { get; } = [];
        internal List<string> Events { get; } = [];
        internal Func<AiSoftChargeReservationRequest, AiSoftChargeReservation>? ReservationFactory { get; set; }

        public AiSoftChargeReservation Reserve(AiSoftChargeReservationRequest request)
        {
            Events.Add("reserve");
            var reservation = ReservationFactory is null
                ? new AiSoftChargeReservation(
                    Guid.NewGuid(), request.AuthorizationId, request.Amount, request.FundingFragments, request.ReservedAt)
                : ReservationFactory(request);
            Reservations.Add(reservation);
            return reservation;
        }

        public void Finalize(AiSoftChargeReservation reservation, DateTimeOffset finalizedAt) => Events.Add("finalize");
        public void Release(AiSoftChargeReservation reservation, string reason, DateTimeOffset releasedAt) => Events.Add("release");
        public void Compensate(AiSoftChargeReservation reservation, DateTimeOffset compensatedAt) => Events.Add("compensate");
    }

    internal sealed class RecordingCostFactStore : IAiProviderCostFactStore
    {
        internal List<AiProviderCostFact> Facts { get; } = [];
        internal Exception? Failure { get; set; }

        public void Save(AiProviderCostFact fact)
        {
            if (Failure is not null) throw Failure;
            Facts.Add(fact);
        }
    }

    internal sealed class RecordingTreasuryPublisher : IAiTreasuryCostPublisher
    {
        internal List<AiTreasuryServiceCostFact> Facts { get; } = [];
        public void Publish(AiTreasuryServiceCostFact fact) => Facts.Add(fact);
    }
}
