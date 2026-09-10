using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using Fixture = GameGuild.Finance.Economy.Payouts.UnitTests.PayoutCoordinatorScenarioTests.Fixture;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PayoutCoordinatorGuardTests
{
    private static readonly DateTimeOffset Time = PayoutCoordinatorScenarioTests.Time;

    [Fact]
    public void Constructor_RejectsEveryMissingSecurityDependency()
    {
        var f = new Fixture();
        Action[] constructors =
        [
            () => new PayoutCoordinator(null!, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, null!, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, null!, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, null!, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, null!,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                null!, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, null!, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, null!, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, null!, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, null!, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, null!, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, null!,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                null!, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, null!, f.Evidence, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, null!, f.Anchors, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, null!, f.AnchorVerifier, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, null!, f.Execution),
            () => new PayoutCoordinator(f.Ledger, f.Operations, f.RootFences, f.RiskAuthorizer, f.ReserveAuthority,
                f.Cooldowns, f.EntityGraph, f.Provider, f.Kyc, f.FinancialCrime, f.TrustSafety, f.RollingReserve,
                f.Risk, f.Reauthentication, f.Evidence, f.Anchors, f.AnchorVerifier, null!)
        ];

        constructors.Should().AllSatisfy(action => action.Should().Throw<ArgumentNullException>());
    }

    [Fact]
    public async Task Reserve_ValidatesEveryPublicRequestInvariant()
    {
        var f = new Fixture();
        var valid = f.Request(1);
        var cases = new (PayoutReservationRequest Request, Type ExceptionType)[]
        {
            (valid with { OperationId = Guid.Empty }, typeof(ArgumentException)),
            (valid with { TenantId = Guid.Empty }, typeof(ArgumentException)),
            (valid with { ActorId = Guid.Empty }, typeof(ArgumentException)),
            (valid with { PayeeId = Guid.Empty }, typeof(ArgumentException)),
            (valid with { Amount = new CoinAmount(CurrencyCode.SoftCoin, 1) }, typeof(PayoutEligibilityException)),
            (valid with { Amount = new CoinAmount(CurrencyCode.HardCoin, 0) }, typeof(PayoutEligibilityException)),
            (valid with { ExpectedProviderAccountId = " " }, typeof(ArgumentException)),
            (valid with { DestinationHash = " " }, typeof(ArgumentException)),
            (valid with { AccountNode = new RiskEntityNode(RiskEntityType.Tenant, "tenant") }, typeof(ArgumentException)),
            (valid with { DestinationNode = new RiskEntityNode(RiskEntityType.BankAccount, "bank") }, typeof(ArgumentException))
        };

        foreach (var item in cases)
        {
            var exception = await Record.ExceptionAsync(async () => await f.Coordinator.ReserveAsync(item.Request));
            exception.Should().BeOfType(item.ExceptionType);
        }

        var nullRequest = async () => await f.Coordinator.ReserveAsync(null!);
        await nullRequest.Should().ThrowAsync<ArgumentNullException>();
        var emptyPayee = async () => await f.Coordinator.CreateOrRefreshConnectAccountAsync(Guid.Empty);
        await emptyPayee.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Reserve_RequiresEveryConnectReadinessAndBindingPredicate()
    {
        Action<Fixture>[] invalidReadiness =
        [
            f => f.Provider.Account = f.Provider.Account with { State = ConnectAccountState.Restricted },
            f => f.Provider.Account = f.Provider.Account with { ChargesEnabled = false },
            f => f.Provider.Account = f.Provider.Account with { PayoutsEnabled = false },
            f => f.Provider.Account = f.Provider.Account with { Version = 0 },
            f => f.Provider.Account = f.Provider.Account with { ObservedAt = Time.AddSeconds(1) },
            f => f.Provider.Account = f.Provider.Account with { ExpiresAt = Time },
            f => f.Provider.Account = f.Provider.Account with { EvidenceHash = " " }
        ];
        foreach (var mutate in invalidReadiness)
            await AssertReserveThrows<PayoutEligibilityException>(mutate);

        await AssertReserveThrows<PayoutProviderBindingException>(
            f => f.Provider.Account = f.Provider.Account with { ProviderAccountId = "other" });
        await AssertReserveThrows<PayoutProviderBindingException>(
            f => f.Provider.Account = f.Provider.Account with { DestinationHash = "other" });
        await AssertReserveThrows<PayoutProviderBindingException>(
            f => f.Provider.Account = f.Provider.Account with { PayeeId = Guid.NewGuid() });
    }

    [Fact]
    public async Task Reserve_RequiresEveryKycAndRollingReservePredicate()
    {
        Action<Fixture>[] invalidKyc =
        [
            f => f.Kyc.Snapshot = f.Kyc.Snapshot with { PayeeId = Guid.NewGuid() },
            f => f.Kyc.Snapshot = f.Kyc.Snapshot with { Version = 0 },
            f => f.Kyc.Snapshot = f.Kyc.Snapshot with { IsApproved = false },
            f => f.Kyc.Snapshot = f.Kyc.Snapshot with { ObservedAt = Time.AddSeconds(1) },
            f => f.Kyc.Snapshot = f.Kyc.Snapshot with { ExpiresAt = Time },
            f => f.Kyc.Snapshot = f.Kyc.Snapshot with { EvidenceHash = " " }
        ];
        foreach (var mutate in invalidKyc)
            await AssertReserveThrows<PayoutEligibilityException>(mutate);

        Action<Fixture>[] invalidReserve =
        [
            f => f.RollingReserve.Snapshot = f.RollingReserve.Snapshot with { Version = 0 },
            f => f.RollingReserve.Snapshot = f.RollingReserve.Snapshot with { EligibleHardUnits = -1 },
            f => f.RollingReserve.Snapshot = f.RollingReserve.Snapshot with { ReservedHardUnits = -1 },
            f => f.RollingReserve.Snapshot = f.RollingReserve.Snapshot with { ReserveBasisPoints = -1 },
            f => f.RollingReserve.Snapshot = f.RollingReserve.Snapshot with { ReserveBasisPoints = 10_001 },
            f => f.RollingReserve.Snapshot = f.RollingReserve.Snapshot with { ObservedAt = Time.AddSeconds(1) },
            f => f.RollingReserve.Snapshot = f.RollingReserve.Snapshot with { ExpiresAt = Time },
            f => f.RollingReserve.Snapshot = f.RollingReserve.Snapshot with { EvidenceHash = " " },
            f => f.RollingReserve.Snapshot = f.RollingReserve.Snapshot with { EligibleHardUnits = 1 }
        ];
        foreach (var mutate in invalidReserve)
            await AssertReserveThrows<PayoutEligibilityException>(mutate, units: 10);
    }

    [Fact]
    public async Task Reserve_FailsClosedAcrossComplianceCooldownGraphRiskMfaAndReserveGates()
    {
        await AssertReserveThrows<PayoutExecutionDisabledException>(f => f.Execution.Stop());
        await AssertReserveThrows<ExternalRiskEvidenceException>(f =>
            f.FinancialCrime.Value = f.FinancialCrime.Value with { Outcome = ExternalRiskOutcome.Deny });
        await AssertReserveThrows<ExternalRiskEvidenceException>(f =>
            f.TrustSafety.Value = f.TrustSafety.Value with { Outcome = ExternalRiskOutcome.Review });
        await AssertReserveThrows<PayoutEligibilityException>(f => f.Cooldowns.Record(
            f.PayeeId, ProtectedChangeKind.PayoutDestination, f.DestinationHash, Time, TimeSpan.FromDays(1)));
        await AssertReserveThrows<PayoutEligibilityException>(f => f.Cooldowns.Record(
            f.PayeeId, ProtectedChangeKind.PayoutDestination, "other", Time.AddDays(-2), TimeSpan.FromDays(1)));
        await AssertReserveThrows<PayoutEligibilityException>(_ => { }, f => f.Request(1) with
        {
            DestinationNode = new RiskEntityNode(RiskEntityType.PayoutDestination, "unlinked")
        });
        await AssertReserveThrows<RiskAuthorizationDeniedException>(f => f.Risk.Outcome = RiskOutcome.Deny);
        await AssertReserveThrows<ReauthenticationEvidenceException>(f =>
            f.Reauthentication.Factory = (actor, binding, now) => new ReauthenticationEvidence(
                actor, ProtectedOperationKind.Payout, binding, ReauthenticationAssurance.Password,
                now.AddMinutes(-1), now.AddMinutes(1), "weak"));
        await AssertReserveThrows<ReserveAuthorizationException>(_ => { }, f => f.Request(1) with
        {
            ReserveVersion = new ReserveVersion(2)
        });
    }

    [Fact]
    public async Task Reserve_RejectsWalletRestrictionsAndDetectsConcurrentEligibilityLossAsStale()
    {
        await AssertReserveThrows<InsufficientFragmentsException>(_ => { }, f => f.Request(1) with
        {
            WalletState = WalletLifecycleState.Frozen
        });
        await AssertReserveThrows<InsufficientFragmentsException>(f => f.Ledger.Execute(transaction =>
        {
            transaction.RecordDebt(f.WalletId, f.Ledger.CreditLots[0].Ranges[0].Root, 1, Time);
            return 0;
        }));
        await AssertReserveThrows<InsufficientFragmentsException>(f => f.Ledger.Execute(transaction =>
        {
            transaction.PlaceHold(HoldId.New(), f.WalletId, new CoinAmount(CurrencyCode.HardCoin, 1),
                HoldReason.RiskReview, Time);
            return 0;
        }));

        var raced = new Fixture();
        var lot = raced.AddLot(10, ProvenanceKind.EarnedHard, 150, 1);
        raced.Risk.OnDecide = _ => raced.Ledger.Execute(transaction =>
        {
            var confirmed = transaction.LatestSource(lot.Ranges[0].Root)!;
            transaction.AddSource(confirmed.Dispute(Time));
            return 0;
        });
        var race = async () => await raced.Coordinator.ReserveAsync(raced.Request(1));
        await race.Should().ThrowAsync<PayoutStaleCommandException>();
    }

    [Fact]
    public async Task Dispatch_RejectsDisabledStaleMissingAndNonReservedCommands()
    {
        var disabled = await ReservedFixture();
        disabled.Execution.Stop();
        var disabledAct = async () => await Dispatch(disabled);
        await disabledAct.Should().ThrowAsync<PayoutExecutionDisabledException>();

        foreach (var command in new Func<PayoutOperation, (long Version, long Fence, long Epoch)>[]
                 {
                     op => (op.Version + 1, op.FencingToken, op.KillSwitchEpoch),
                     op => (op.Version, op.FencingToken + 1, op.KillSwitchEpoch),
                     op => (op.Version, op.FencingToken, op.KillSwitchEpoch + 1)
                 })
        {
            var stale = await ReservedFixture();
            var op = stale.Operations.Operations.Single();
            var values = command(op);
            var act = async () => await stale.Coordinator.DispatchAsync(
                op.Id, values.Version, values.Fence, values.Epoch, Time.AddMinutes(1));
            await act.Should().ThrowAsync<PayoutStaleCommandException>();
        }

        var repeated = await ReservedFixture();
        var original = repeated.Operations.Operations.Single();
        await Dispatch(repeated);
        var repeatedAct = async () => await repeated.Coordinator.DispatchAsync(
            original.Id, original.Version, original.FencingToken, original.KillSwitchEpoch, Time.AddMinutes(2));
        await repeatedAct.Should().ThrowAsync<PayoutStaleCommandException>();

        var missing = new Fixture();
        var missingOperation = Operation(missing, PayoutOperationState.Reserved);
        missing.Operations.Add(missingOperation);
        var missingAct = async () => await missing.Coordinator.DispatchAsync(
            missingOperation.Id, 1, 1, 1, Time.AddMinutes(1));
        await missingAct.Should().ThrowAsync<PayoutStaleCommandException>();

        var wrongStatus = await ReservedFixture();
        var wrongOperation = wrongStatus.Operations.Operations.Single();
        wrongStatus.Ledger.Execute(transaction =>
        {
            transaction.TransitionFragmentReservations(wrongOperation.Id, FragmentReservationStatus.Reserved,
                FragmentReservationStatus.Dispatching, Time.AddSeconds(1));
            return 0;
        });
        var statusAct = async () => await Dispatch(wrongStatus);
        await statusAct.Should().ThrowAsync<PayoutStaleCommandException>();
    }

    [Fact]
    public async Task Dispatch_RechecksDebtHoldsReserveAccountAndIndependentAnchor()
    {
        var debt = await ReservedFixture();
        debt.Ledger.Execute(transaction =>
        {
            transaction.RecordDebt(debt.WalletId, debt.Ledger.CreditLots[0].Ranges[0].Root, 1, Time.AddSeconds(1));
            return 0;
        });
        await AssertDispatchThrows<PayoutEligibilityException>(debt);

        var hold = await ReservedFixture();
        hold.AddLot(5, ProvenanceKind.EarnedHard, 150, 2);
        hold.Ledger.Execute(transaction =>
        {
            transaction.PlaceHold(HoldId.New(), hold.WalletId, new CoinAmount(CurrencyCode.HardCoin, 1),
                HoldReason.RiskReview, Time.AddSeconds(1));
            return 0;
        });
        await AssertDispatchThrows<PayoutEligibilityException>(hold);

        var reserve = await ReservedFixture();
        await AssertDispatchThrows<ReserveAuthorizationException>(reserve, reserveVersionMutation: true);

        Action<Fixture>[] invalidAccounts =
        [
            f => f.Provider.Account = f.Provider.Account with { State = ConnectAccountState.Restricted },
            f => f.Provider.Account = f.Provider.Account with { PayoutsEnabled = false },
            f => f.Provider.Account = f.Provider.Account with { ExpiresAt = Time.AddMinutes(1) },
            f => f.Provider.Account = f.Provider.Account with { ProviderAccountId = "other" },
            f => f.Provider.Account = f.Provider.Account with { DestinationHash = "other" },
            f => f.Provider.Account = f.Provider.Account with { PayeeId = Guid.NewGuid() }
        ];
        foreach (var mutate in invalidAccounts)
        {
            var invalid = await ReservedFixture();
            mutate(invalid);
            await AssertDispatchThrows<PayoutProviderBindingException>(invalid);
        }

        var anchor = await ReservedFixture();
        anchor.AnchorVerifier.IsValid = false;
        await AssertDispatchThrows<PayoutEvidenceException>(anchor);
    }

    [Fact]
    public async Task Dispatch_RejectsEveryReceiptBindingAndSignatureFailure()
    {
        Func<PayoutDispatchCommand, PayoutDispatchReceipt>[] invalidReceipts =
        [
            command => Receipt(command) with { OperationId = Guid.NewGuid() },
            command => Receipt(command) with { ProviderAccountId = "other" },
            command => Receipt(command) with { DestinationHash = "other" },
            command => Receipt(command) with { ProviderPayoutId = " " },
            command => Receipt(command) with { Outcome = (PayoutProviderOutcome)99 }
        ];
        for (var index = 0; index < invalidReceipts.Length; index++)
        {
            var f = await ReservedFixture();
            f.Provider.DispatchFactory = invalidReceipts[index];
            await AssertDispatchThrows<PayoutEvidenceException>(f);
            f.Operations.Operations.Single().State.Should().Be(index < 4
                ? PayoutOperationState.Ambiguous
                : PayoutOperationState.Dispatching);
        }

        var signature = await ReservedFixture();
        signature.Evidence.ReceiptValid = false;
        await AssertDispatchThrows<PayoutEvidenceException>(signature);
        signature.Operations.Operations.Single().State.Should().Be(PayoutOperationState.Ambiguous);
    }

    [Fact]
    public async Task ProviderEventAndReconciliationRejectEveryUntrustedOrOutOfOrderState()
    {
        var nullEvent = new Fixture();
        var nullAct = async () => await nullEvent.Coordinator.ApplyProviderEventAsync(null!);
        await nullAct.Should().ThrowAsync<ArgumentNullException>();
        using var source = new CancellationTokenSource();
        source.Cancel();
        var cancelAct = async () => await nullEvent.Coordinator.ApplyProviderEventAsync(
            nullEvent.Provider.Event(Guid.NewGuid(), PayoutProviderOutcome.Succeeded, "cancel"), source.Token);
        await cancelAct.Should().ThrowAsync<OperationCanceledException>();

        var unsigned = await DispatchedFixture();
        unsigned.Evidence.EventValid = false;
        var unsignedEvent = unsigned.Provider.Event(
            unsigned.Operations.Operations.Single().Id, PayoutProviderOutcome.Succeeded, "unsigned");
        var unsignedAct = async () => await unsigned.Coordinator.ApplyProviderEventAsync(unsignedEvent);
        await unsignedAct.Should().ThrowAsync<PayoutEvidenceException>();

        foreach (var mutate in new Func<PayoutProviderEvent, PayoutProviderEvent>[]
                 {
                     item => item with { ProviderAccountId = "other" },
                     item => item with { DestinationHash = "other" },
                     item => item with { ProviderPayoutId = "other" }
                 })
        {
            var f = await DispatchedFixture();
            var providerEvent = mutate(f.Provider.Event(
                f.Operations.Operations.Single().Id, PayoutProviderOutcome.Succeeded, Guid.NewGuid().ToString("N")));
            var act = async () => await f.Coordinator.ApplyProviderEventAsync(providerEvent);
            await act.Should().ThrowAsync<PayoutProviderBindingException>();
        }

        var nonterminal = await DispatchedFixture();
        var nonterminalEvent = nonterminal.Provider.Event(
            nonterminal.Operations.Operations.Single().Id, PayoutProviderOutcome.Submitted, "nonterminal");
        var nonterminalAct = async () => await nonterminal.Coordinator.ApplyProviderEventAsync(nonterminalEvent);
        await nonterminalAct.Should().ThrowAsync<PayoutEvidenceException>();

        var completed = await DispatchedFixture();
        var terminal = completed.Provider.Event(
            completed.Operations.Operations.Single().Id, PayoutProviderOutcome.Succeeded, "terminal");
        await completed.Coordinator.ApplyProviderEventAsync(terminal);
        var late = terminal with { EventId = "late", Outcome = PayoutProviderOutcome.Failed };
        var lateAct = async () => await completed.Coordinator.ApplyProviderEventAsync(late);
        await lateAct.Should().ThrowAsync<PayoutStaleCommandException>();

        var noProviderId = await ReservedFixture();
        var reconcileAct = async () => await noProviderId.Coordinator.ReconcileAsync(
            noProviderId.Operations.Operations.Single().Id);
        await reconcileAct.Should().ThrowAsync<PayoutStaleCommandException>();
    }

    [Fact]
    public async Task DispatchAndTerminalEventFailWhenImmutableEvidenceOrReservationsAreMissing()
    {
        var noJournal = new Fixture();
        var lot = noJournal.AddLot(2, ProvenanceKind.EarnedHard, 150, 1);
        var operation = Operation(noJournal, PayoutOperationState.Reserved);
        noJournal.Operations.Add(operation);
        noJournal.Ledger.Execute(transaction =>
        {
            transaction.AddFragmentReservation(new ValueFragmentReservation(
                Guid.NewGuid(), operation.Id, FragmentReservationPurpose.Payout, lot.Id, noJournal.WalletId,
                new CoinAmount(CurrencyCode.HardCoin, 1), [lot.Ranges[0].Take(1_000).Selected],
                1, 1, 1, FragmentReservationStatus.Reserved, Time, null));
            return 0;
        });
        await AssertDispatchThrows<PayoutEvidenceException>(noJournal);

        var noReservations = new Fixture();
        var dispatching = Operation(noReservations, PayoutOperationState.Dispatching) with
        {
            ProviderPayoutId = noReservations.Provider.ProviderPayoutId
        };
        noReservations.Operations.Add(dispatching);
        var providerEvent = noReservations.Provider.Event(
            dispatching.Id, PayoutProviderOutcome.Succeeded, "no-reservations");
        var eventAct = async () => await noReservations.Coordinator.ApplyProviderEventAsync(providerEvent);
        await eventAct.Should().ThrowAsync<PayoutStaleCommandException>();
    }

    private static async Task AssertReserveThrows<TException>(
        Action<Fixture> mutate,
        Func<Fixture, PayoutReservationRequest>? request = null,
        long units = 1)
        where TException : Exception
    {
        var f = new Fixture();
        f.AddLot(20, ProvenanceKind.EarnedHard, 150, 1);
        mutate(f);
        var act = async () => await f.Coordinator.ReserveAsync(request?.Invoke(f) ?? f.Request(units));
        await act.Should().ThrowAsync<TException>();
    }

    private static async Task<Fixture> ReservedFixture()
    {
        var f = new Fixture();
        f.AddLot(20, ProvenanceKind.EarnedHard, 150, 1);
        await f.Coordinator.ReserveAsync(f.Request(5));
        return f;
    }

    private static async Task<Fixture> DispatchedFixture()
    {
        var f = await ReservedFixture();
        await Dispatch(f);
        return f;
    }

    private static ValueTask<PayoutOperation> Dispatch(Fixture f)
    {
        var operation = f.Operations.Operations.Single();
        return f.Coordinator.DispatchAsync(
            operation.Id, operation.Version, operation.FencingToken, operation.KillSwitchEpoch, Time.AddMinutes(1));
    }

    private static async Task AssertDispatchThrows<TException>(Fixture f, bool reserveVersionMutation = false)
        where TException : Exception
    {
        var operation = f.Operations.Operations.Single();
        if (reserveVersionMutation)
        {
            var changed = operation with { Version = operation.Version + 1, ReserveVersion = new ReserveVersion(2) };
            operation = f.Operations.Update(changed, operation.Version);
        }
        var act = async () => await f.Coordinator.DispatchAsync(
            operation.Id, operation.Version, operation.FencingToken, operation.KillSwitchEpoch, Time.AddMinutes(1));
        await act.Should().ThrowAsync<TException>();
    }

    private static PayoutDispatchReceipt Receipt(PayoutDispatchCommand command) => new(
        command.OperationId, PayoutProviderOutcome.Submitted, "po_123", command.ProviderAccountId,
        command.DestinationHash, "evidence", "signature", command.RequestedAt);

    private static PayoutOperation Operation(Fixture f, PayoutOperationState state) => new(
        Guid.NewGuid(), new IdempotencyKey(Guid.NewGuid().ToString("N")), "request", f.ActorId, f.PayeeId,
        f.WalletId, new CoinAmount(CurrencyCode.HardCoin, 1), f.ProviderAccountId, f.DestinationHash,
        "provider-binding", "eligibility", null, null, state, 1, 1, 1,
        new ReserveVersion(1), 1, new PolicyVersion(1), Guid.NewGuid(), Time, Time, f.TenantId);
}
