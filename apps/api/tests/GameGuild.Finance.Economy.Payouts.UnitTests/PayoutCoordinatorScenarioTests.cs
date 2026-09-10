using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PayoutCoordinatorScenarioTests
{
    internal static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-08-03T12:00:00Z");

    [Fact]
    public async Task ConnectOnboarding_RequiresTheRequestedPayeeBinding()
    {
        var fixture = new Fixture();
        var result = await fixture.Coordinator.CreateOrRefreshConnectAccountAsync(fixture.PayeeId);
        result.Account.PayeeId.Should().Be(fixture.PayeeId);
        fixture.Provider.OnboardingCalls.Should().Be(1);
        fixture.Provider.Account = fixture.Provider.Account with { PayeeId = Guid.NewGuid() };
        var act = async () => await fixture.Coordinator.CreateOrRefreshConnectAccountAsync(fixture.PayeeId);
        await act.Should().ThrowAsync<PayoutProviderBindingException>();
    }

    [Fact]
    public async Task Reserve_SelectsOnlyConfirmedMatureEarnedHardInGlobalFifoOrder()
    {
        var fixture = new Fixture();
        var oldest = fixture.AddLot(6, ProvenanceKind.EarnedHard, 150, 2);
        var next = fixture.AddLot(4, ProvenanceKind.EarnedHard, 140, 3);
        fixture.AddLot(8, ProvenanceKind.PurchasedHard, 200, 1);
        fixture.AddLot(8, ProvenanceKind.EarnedHard, 30, 4);
        fixture.AddLot(8, ProvenanceKind.AdRewardSoft, 200, 5, currency: CurrencyCode.SoftCoin);

        var operation = await fixture.Coordinator.ReserveAsync(fixture.Request(8));

        operation.State.Should().Be(PayoutOperationState.Reserved);
        operation.Version.Should().Be(1);
        operation.FencingToken.Should().Be(1);
        operation.KillSwitchEpoch.Should().Be(fixture.Execution.Epoch);
        operation.ProviderAccountId.Should().Be(fixture.ProviderAccountId);
        operation.DestinationHash.Should().Be(fixture.DestinationHash);
        operation.RequestHash.Should().NotBeNullOrWhiteSpace();
        operation.ProviderBindingHash.Should().NotBeNullOrWhiteSpace();
        operation.EligibilityHash.Should().NotBeNullOrWhiteSpace();
        operation.RiskDecisionId.Should().NotBeEmpty();
        var reservations = fixture.Ledger.GetFragmentReservations(operation.Id);
        reservations.Should().HaveCount(2);
        reservations.Select(item => item.LotId).Should().BeEquivalentTo([oldest.Id, next.Id]);
        reservations.Single(item => item.LotId == oldest.Id).Amount.Units.Should().Be(6);
        reservations.Single(item => item.LotId == next.Id).Amount.Units.Should().Be(2);
        reservations.Should().OnlyContain(item => item.Status == FragmentReservationStatus.Reserved);
        reservations.Sum(item => item.Ranges.Sum(range => range.Length)).Should().Be(8_000);
        fixture.Ledger.ProjectionUpdates.Should().ContainSingle(update => update.DeltaUnits == -8);
        fixture.Ledger.OutboxMessages.Should().Contain(message => message.Type == "economy.payout.reserved.v1");
        fixture.Risk.LastRequest.Should().NotBeNull();
        fixture.Risk.LastRequest!.Context.SourceRoots.Should().BeEquivalentTo(reservations
            .SelectMany(item => item.Ranges).Select(item => item.Root).Distinct());
        fixture.Reauthentication.LastBinding.Should().Be(fixture.Risk.LastRequest.Context.Fingerprint());
        var replay = await fixture.Coordinator.ReserveAsync(fixture.Request(8, operation.Id, operation.IdempotencyKey));
        replay.Should().BeSameAs(operation);
        fixture.Provider.AccountReads.Should().Be(1);
    }

    [Fact]
    public async Task Reserve_RejectsUnconfirmedDisputedAndInsufficientFragments()
    {
        var fixture = new Fixture();
        fixture.AddLot(5, ProvenanceKind.EarnedHard, 150, 1, SourceConfirmationState.Observed);
        fixture.AddLot(5, ProvenanceKind.EarnedHard, 150, 2, SourceConfirmationState.Disputed);
        var act = async () => await fixture.Coordinator.ReserveAsync(fixture.Request(1));
        await act.Should().ThrowAsync<InsufficientFragmentsException>();
        fixture.Ledger.FragmentReservations.Should().BeEmpty();
        fixture.Operations.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task Reserve_RejectsMutatedIdempotencyReplay()
    {
        var fixture = new Fixture();
        fixture.AddLot(20, ProvenanceKind.EarnedHard, 150, 1);
        var request = fixture.Request(5);
        await fixture.Coordinator.ReserveAsync(request);
        var act = async () => await fixture.Coordinator.ReserveAsync(request with
        {
            Amount = new CoinAmount(CurrencyCode.HardCoin, 6)
        });
        await act.Should().ThrowAsync<PayoutReplayConflictException>();
    }

    [Fact]
    public async Task Dispatch_SubmittedMovesExactReservationsToDispatchingAndBindsProviderId()
    {
        var fixture = new Fixture();
        fixture.AddLot(10, ProvenanceKind.EarnedHard, 150, 1);
        var reserved = await fixture.Coordinator.ReserveAsync(fixture.Request(4));
        var dispatched = await fixture.Coordinator.DispatchAsync(
            reserved.Id, reserved.Version, reserved.FencingToken, reserved.KillSwitchEpoch, Time.AddMinutes(1));
        dispatched.State.Should().Be(PayoutOperationState.Dispatching);
        dispatched.Version.Should().Be(3);
        dispatched.ProviderPayoutId.Should().Be(fixture.Provider.ProviderPayoutId);
        dispatched.DispatchSnapshotHash.Should().NotBeNullOrWhiteSpace();
        fixture.Provider.LastDispatch.Should().NotBeNull();
        fixture.Provider.LastDispatch!.DispatchSnapshotHash.Should().Be(dispatched.DispatchSnapshotHash);
        fixture.Ledger.GetFragmentReservations(reserved.Id)
            .Should().OnlyContain(item => item.Status == FragmentReservationStatus.Dispatching);
        fixture.Ledger.ChainAnchors.Should().ContainSingle(anchor =>
            anchor.Kind == ChainAnchorKind.OnDemand && anchor.DispatchSnapshotHash == dispatched.DispatchSnapshotHash);
        fixture.Ledger.OutboxMessages.Should().Contain(message => message.Type == "economy.payout.dispatch.v1");
    }

    [Theory]
    [InlineData(PayoutProviderOutcome.Succeeded, PayoutOperationState.Succeeded, FragmentReservationStatus.Consumed)]
    [InlineData(PayoutProviderOutcome.Failed, PayoutOperationState.Failed, FragmentReservationStatus.Released)]
    public async Task Dispatch_TerminalReceiptCompletesOnlyTheExactReservedFragments(
        PayoutProviderOutcome outcome,
        PayoutOperationState expectedState,
        FragmentReservationStatus expectedReservationState)
    {
        var fixture = new Fixture();
        fixture.Provider.DispatchOutcome = outcome;
        var lot = fixture.AddLot(10, ProvenanceKind.EarnedHard, 150, 1);
        var reserved = await fixture.Coordinator.ReserveAsync(fixture.Request(4));
        var completed = await fixture.Coordinator.DispatchAsync(
            reserved.Id, reserved.Version, reserved.FencingToken, reserved.KillSwitchEpoch, Time.AddMinutes(1));
        completed.State.Should().Be(expectedState);
        fixture.Ledger.GetFragmentReservations(reserved.Id)
            .Should().OnlyContain(item => item.Status == expectedReservationState);
        fixture.Ledger.CreditLots.Should().ContainSingle(item => item.Id == lot.Id);
        if (outcome == PayoutProviderOutcome.Succeeded)
        {
            fixture.Ledger.FragmentConsumptions.Should().ContainSingle();
            fixture.Ledger.ProjectionUpdates.Should().ContainSingle(update => update.DeltaUnits == -4);
        }
        else
        {
            fixture.Ledger.FragmentConsumptions.Should().BeEmpty();
            fixture.Ledger.ProjectionUpdates.Should().Contain(update => update.DeltaUnits == 4);
        }
    }

    [Fact]
    public async Task Dispatch_AmbiguousKeepsFragmentsReservedUntilSignedReconciliationSucceeds()
    {
        var fixture = new Fixture();
        fixture.Provider.DispatchOutcome = PayoutProviderOutcome.Ambiguous;
        fixture.AddLot(10, ProvenanceKind.EarnedHard, 150, 1);
        var reserved = await fixture.Coordinator.ReserveAsync(fixture.Request(4));
        var ambiguous = await fixture.Coordinator.DispatchAsync(
            reserved.Id, reserved.Version, reserved.FencingToken, reserved.KillSwitchEpoch, Time.AddMinutes(1));
        ambiguous.State.Should().Be(PayoutOperationState.Ambiguous);
        fixture.Ledger.GetFragmentReservations(reserved.Id)
            .Should().OnlyContain(item => item.Status == FragmentReservationStatus.Dispatching);
        fixture.Provider.ReconcileOutcome = PayoutProviderOutcome.Succeeded;
        var completed = await fixture.Coordinator.ReconcileAsync(reserved.Id);
        completed.State.Should().Be(PayoutOperationState.Succeeded);
        fixture.Provider.ReconcileCalls.Should().Be(1);
        fixture.Ledger.GetFragmentReservations(reserved.Id)
            .Should().OnlyContain(item => item.Status == FragmentReservationStatus.Consumed);
    }

    [Fact]
    public async Task Dispatch_CancelsWhenAReversalReleasedTheReservationBeforeLinearization()
    {
        var fixture = new Fixture();
        var lot = fixture.AddLot(10, ProvenanceKind.EarnedHard, 150, 1);
        var reserved = await fixture.Coordinator.ReserveAsync(fixture.Request(4));
        fixture.Ledger.Execute(transaction =>
        {
            transaction.ReleaseReservedFragmentsForRoot(lot.Ranges[0].Root, Time.AddSeconds(30));
            return 0;
        });
        var cancelled = await fixture.Coordinator.DispatchAsync(
            reserved.Id, reserved.Version, reserved.FencingToken, reserved.KillSwitchEpoch, Time.AddMinutes(1));
        cancelled.State.Should().Be(PayoutOperationState.Cancelled);
        fixture.Provider.DispatchCalls.Should().Be(0);
    }

    [Fact]
    public async Task ProviderEvents_AreSignedBoundReplaySafeAndTerminalOnly()
    {
        var fixture = new Fixture();
        fixture.AddLot(10, ProvenanceKind.EarnedHard, 150, 1);
        var reserved = await fixture.Coordinator.ReserveAsync(fixture.Request(4));
        var dispatching = await fixture.Coordinator.DispatchAsync(
            reserved.Id, reserved.Version, reserved.FencingToken, reserved.KillSwitchEpoch, Time.AddMinutes(1));
        var providerEvent = fixture.Provider.Event(dispatching.Id, PayoutProviderOutcome.Succeeded, "evt_final");
        var completed = await fixture.Coordinator.ApplyProviderEventAsync(providerEvent);
        var replay = await fixture.Coordinator.ApplyProviderEventAsync(providerEvent);
        completed.State.Should().Be(PayoutOperationState.Succeeded);
        replay.Should().BeSameAs(completed);
        fixture.Operations.ProviderEvents.Should().ContainSingle();
        var mutated = providerEvent with { EvidenceHash = "mutated" };
        var act = async () => await fixture.Coordinator.ApplyProviderEventAsync(mutated);
        await act.Should().ThrowAsync<PayoutReplayConflictException>();
    }

    internal sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid PayeeId { get; } = Guid.NewGuid();
        public WalletId WalletId { get; } = WalletId.New();
        public string ProviderAccountId { get; } = "acct_connect_123";
        public string DestinationHash { get; } = "destination-hash";
        public RiskEntityNode AccountNode { get; } = new(RiskEntityType.Account, "account-hash");
        public RiskEntityNode DestinationNode { get; } = new(RiskEntityType.PayoutDestination, "destination-hash");
        public InMemoryLedgerKernelStore Ledger { get; } = new();
        public InMemoryPayoutOperationStore Operations { get; } = new();
        public RootReversalFenceRegistry RootFences { get; } = new();
        public RiskDecisionAuthorizer RiskAuthorizer { get; } = new();
        public CoreReserveAuthority ReserveAuthority { get; } = new();
        public ProtectedChangeCooldownRegistry Cooldowns { get; } = new();
        public EntityRiskGraph EntityGraph { get; } = new();
        public FakeProvider Provider { get; } = new();
        public FakeKyc Kyc { get; } = new();
        public FakeFinancialCrime FinancialCrime { get; } = new();
        public FakeTrustSafety TrustSafety { get; } = new();
        public FakeRollingReserve RollingReserve { get; } = new();
        public FakeRiskDecisions Risk { get; } = new();
        public FakeReauthentication Reauthentication { get; } = new();
        public FakeEvidenceVerifier Evidence { get; } = new();
        public FakeAnchorVerifier AnchorVerifier { get; } = new();
        public PayoutExecutionGate Execution { get; } = new(true, 1);
        public ChainAnchorService Anchors { get; }
        public PayoutCoordinator Coordinator { get; }

        public Fixture()
        {
            Provider.Account = new ConnectAccountSnapshot(PayeeId, ProviderAccountId, DestinationHash,
                ConnectAccountState.Ready, true, true, 1, Time.AddMinutes(-1), Time.AddMinutes(10), "connect-evidence");
            Kyc.Snapshot = new PayoutKycSnapshot(PayeeId, 1, true, Time.AddMinutes(-1), Time.AddMinutes(10), "kyc-evidence");
            RollingReserve.Snapshot = new PayoutRollingReserveSnapshot(1, 1_000_000, 0, 1_000,
                Time.AddMinutes(-1), Time.AddMinutes(10), "rolling-evidence");
            Cooldowns.Record(PayeeId, ProtectedChangeKind.PayoutDestination, DestinationHash,
                Time.AddDays(-2), TimeSpan.FromDays(1));
            EntityGraph.Link(AccountNode, DestinationNode, "graph-evidence", Time.AddDays(-2));
            ReserveAuthority.ValidateAndActivate(ReserveProposal(), Time);
            Anchors = new ChainAnchorService(Ledger,
                new HmacChainHeadSigner("test-anchor", Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()));
            Coordinator = new PayoutCoordinator(Ledger, Operations, RootFences, RiskAuthorizer, ReserveAuthority,
                Cooldowns, EntityGraph, Provider, Kyc, FinancialCrime, TrustSafety, RollingReserve, Risk,
                Reauthentication, Evidence, Anchors, AnchorVerifier, Execution);
        }

        public PayoutReservationRequest Request(long units, Guid? operationId = null, IdempotencyKey? idempotencyKey = null) =>
            new(operationId ?? Guid.NewGuid(), idempotencyKey ?? new IdempotencyKey(Guid.NewGuid().ToString("N")),
                ActorId, PayeeId, WalletId, new CoinAmount(CurrencyCode.HardCoin, units), WalletLifecycleState.Active,
                new PolicyVersion(1), new ReserveVersion(1), 1, 1, ProviderAccountId, DestinationHash,
                AccountNode, DestinationNode, Time, TenantId);

        public CreditLot AddLot(long units, ProvenanceKind provenance, int daysSinceConfirmation,
            long journalSequence, SourceConfirmationState sourceState = SourceConfirmationState.Confirmed,
            CurrencyCode currency = CurrencyCode.HardCoin)
        {
            var sourceId = SourceStampId.New();
            var confirmedAt = Time.AddDays(-daysSinceConfirmation);
            var observed = SourceEvidence.Observe(sourceId, "test-provider", $"source-{sourceId.Value:N}",
                "source-evidence", confirmedAt.AddMinutes(-1));
            var confirmed = observed.Confirm(confirmedAt);
            var latest = sourceState switch
            {
                SourceConfirmationState.Observed => observed,
                SourceConfirmationState.Confirmed => confirmed,
                SourceConfirmationState.Disputed => confirmed.Dispute(confirmedAt.AddDays(1)),
                SourceConfirmationState.Reversed => confirmed.Reverse(confirmedAt.AddDays(1)),
                _ => throw new ArgumentOutOfRangeException(nameof(sourceState))
            };
            var lot = ConfirmedCreditFactory.CreateRootLot(CreditLotId.New(), WalletId,
                new CoinAmount(currency, units), provenance, confirmed, journalSequence);
            Ledger.Execute(transaction =>
            {
                transaction.AddSource(latest);
                transaction.AddCreditLot(lot);
                return 0;
            });
            return lot;
        }

        private static ReserveProposal ReserveProposal() => new(new ReserveVersion(1), null,
            new PolicyVersion(1), 1, Time.AddMinutes(-1), Time.AddMinutes(10),
            new ReserveLiabilityPosition(0, 0, 0, 0), new ReserveBufferPosition(0, 0, 0, 0, 0, 0, 0),
            [new ReserveServiceObservation("test-service", 1, 1, 1, 1, 0, true,
                Time.AddMinutes(-1), Time.AddMinutes(10))],
            [
                new ExternalReserveAsset("hard", ReserveBackingPurpose.HardCoin, 2_000_000_000_000),
                new ExternalReserveAsset("soft", ReserveBackingPurpose.SoftCoin, 2_000_000_000_000)
            ], "reserve-evidence");
    }

    internal sealed class FakeProvider : IConnectPayoutProvider
    {
        public ConnectAccountSnapshot Account { get; set; } = null!;
        public PayoutProviderOutcome DispatchOutcome { get; set; } = PayoutProviderOutcome.Submitted;
        public PayoutProviderOutcome ReconcileOutcome { get; set; } = PayoutProviderOutcome.Succeeded;
        public string ProviderPayoutId { get; set; } = "po_123";
        public int OnboardingCalls { get; private set; }
        public int AccountReads { get; private set; }
        public int DispatchCalls { get; private set; }
        public int ReconcileCalls { get; private set; }
        public PayoutDispatchCommand? LastDispatch { get; private set; }
        public Func<PayoutDispatchCommand, PayoutDispatchReceipt>? DispatchFactory { get; set; }

        public ValueTask<ConnectOnboardingResult> CreateOrRefreshAccountAsync(Guid payeeId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OnboardingCalls++;
            return ValueTask.FromResult(new ConnectOnboardingResult(Account, new Uri("https://connect.test/onboard")));
        }

        public ValueTask<ConnectAccountSnapshot> GetAccountAsync(Guid payeeId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AccountReads++;
            return ValueTask.FromResult(Account);
        }

        public ValueTask<PayoutDispatchReceipt> DispatchAsync(PayoutDispatchCommand command, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DispatchCalls++;
            LastDispatch = command;
            return ValueTask.FromResult(DispatchFactory?.Invoke(command) ?? new PayoutDispatchReceipt(
                command.OperationId, DispatchOutcome, ProviderPayoutId, command.ProviderAccountId,
                command.DestinationHash, "receipt-evidence", "receipt-signature", command.RequestedAt));
        }

        public ValueTask<PayoutProviderEvent> ReconcileAsync(Guid operationId, string providerPayoutId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReconcileCalls++;
            return ValueTask.FromResult(Event(operationId, ReconcileOutcome, $"reconcile-{ReconcileCalls}"));
        }

        public PayoutProviderEvent Event(Guid operationId, PayoutProviderOutcome outcome, string eventId) => new(
            eventId, operationId, outcome, ProviderPayoutId, Account.ProviderAccountId, Account.DestinationHash,
            "event-evidence", "event-signature", Time.AddMinutes(2));
    }

    internal sealed class FakeKyc : IPayoutKycEligibilitySource
    {
        public PayoutKycSnapshot Snapshot { get; set; } = null!;
        public ValueTask<PayoutKycSnapshot> ReadAsync(Guid payeeId, DateTimeOffset observedAt,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Snapshot);
    }

    internal sealed class FakeFinancialCrime : IFinancialCrimeRiskInputSource
    {
        public FinancialCrimeRiskInput Value { get; set; } = new(1, Time.AddMinutes(-1), Time.AddMinutes(10),
            ExternalRiskOutcome.Allow, "financial-crime", true);
        public ValueTask<FinancialCrimeRiskInput> ReadAsync(string opaqueSubjectReference, DateTimeOffset observedAt,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Value);
    }

    internal sealed class FakeTrustSafety : ITrustSafetyRiskInputSource
    {
        public TrustSafetyRiskInput Value { get; set; } = new(1, Time.AddMinutes(-1), Time.AddMinutes(10),
            ExternalRiskOutcome.Allow, "trust-safety", true);
        public ValueTask<TrustSafetyRiskInput> ReadAsync(string opaqueSubjectReference, DateTimeOffset observedAt,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Value);
    }

    internal sealed class FakeRollingReserve : IPayoutRollingReserveSource
    {
        public PayoutRollingReserveSnapshot Snapshot { get; set; } = null!;
        public ValueTask<PayoutRollingReserveSnapshot> ReadAsync(WalletId walletId, DateTimeOffset observedAt,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Snapshot);
    }

    internal sealed class FakeRiskDecisions : IPayoutRiskDecisionSource
    {
        public PayoutRiskRequest? LastRequest { get; private set; }
        public RiskOutcome Outcome { get; set; } = RiskOutcome.Allow;
        public Action<PayoutRiskRequest>? OnDecide { get; set; }
        public ValueTask<RiskDecisionSnapshot> DecideAsync(PayoutRiskRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            OnDecide?.Invoke(request);
            return ValueTask.FromResult(RiskDecisionSnapshot.Create(Guid.NewGuid(), Outcome, request.Context,
                request.RequestedAt.AddSeconds(-1), request.RequestedAt.AddMinutes(5), [RiskReasonCode.WithinLimits]));
        }
    }

    internal sealed class FakeReauthentication : IPayoutReauthenticationSource
    {
        public string? LastBinding { get; private set; }
        public Func<Guid, string, DateTimeOffset, ReauthenticationEvidence>? Factory { get; set; }
        public ValueTask<ReauthenticationEvidence> ReadAsync(Guid actorId, string transactionBinding,
            DateTimeOffset observedAt, CancellationToken cancellationToken = default)
        {
            LastBinding = transactionBinding;
            return ValueTask.FromResult(Factory?.Invoke(actorId, transactionBinding, observedAt) ?? new(
                actorId, ProtectedOperationKind.Payout, transactionBinding, ReauthenticationAssurance.MultiFactor,
                observedAt.AddMinutes(-1), observedAt.AddMinutes(5), "reauth-evidence"));
        }
    }

    internal sealed class FakeEvidenceVerifier : IPayoutProviderEvidenceVerifier
    {
        public bool ReceiptValid { get; set; } = true;
        public bool EventValid { get; set; } = true;
        public bool Verify(PayoutDispatchReceipt receipt) => ReceiptValid;
        public bool Verify(PayoutProviderEvent providerEvent) => EventValid;
    }

    internal sealed class FakeAnchorVerifier : IIndependentAnchorVerifier
    {
        public bool IsValid { get; set; } = true;
        public bool Verify(ChainAnchor anchor) => IsValid;
    }
}
