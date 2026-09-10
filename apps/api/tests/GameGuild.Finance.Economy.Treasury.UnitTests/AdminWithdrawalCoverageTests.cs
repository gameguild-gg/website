using System.Reflection;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed partial class AdminWithdrawalWorkflowTests
{
    [Fact]
    public void ExecutionGateIsObservableFenceableAndDisabledByDefault()
    {
        var disabled = new AdminWithdrawalExecutionGate();
        disabled.IsEnabled.Should().BeFalse();
        disabled.Epoch.Should().Be(1);
        ((Action)disabled.EnsureEnabled).Should().Throw<AdminWithdrawalExecutionDisabledException>();

        var enabled = new AdminWithdrawalExecutionGate(true, 7);
        enabled.IsEnabled.Should().BeTrue();
        enabled.EnsureEnabled();
        enabled.Stop().Should().Be(8);
        enabled.IsEnabled.Should().BeFalse();
        enabled.Epoch.Should().Be(8);
        ((Action)(() => new AdminWithdrawalExecutionGate(epoch: 0)))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DispatchContractExposesEveryFencedField()
    {
        var command = new AdminWithdrawalDispatchCommand(
            Guid.NewGuid(), Guid.NewGuid(), 2, 3, 4, new CoinAmount(CurrencyCode.HardCoin, 5),
            "asset", "destination", "snapshot", "idempotency", Now);

        command.ExpectedVersion.Should().Be(2);
        command.DispatchSnapshotHash.Should().Be("snapshot");
        command.IdempotencyKey.Should().Be("idempotency");
    }

    [Fact]
    public void StoreEnforcesIdentityPeriodIdempotencyAndVersions()
    {
        var store = new InMemoryAdminWithdrawalStore();
        store.Count.Should().Be(0);
        ((Action)(() => store.FindReplay("", "hash"))).Should().Throw<ArgumentException>();
        ((Action)(() => store.FindReplay("key", ""))).Should().Throw<ArgumentException>();
        store.FindReplay("key", "hash").Should().BeNull();
        store.FindPeriod(new DateOnly(2026, 8, 1)).Should().BeNull();
        ((Action)(() => store.Add(null!))).Should().Throw<ArgumentNullException>();

        var run = StandaloneRun();
        store.Add(run);
        store.Count.Should().Be(1);
        store.Get(run.Id).Should().Be(run);
        store.FindReplay($"  {run.IdempotencyKey.Value}  ", run.RequestHash).Should().Be(run);
        store.FindPeriod(run.PeriodStart).Should().Be(run);
        ((Action)(() => store.FindReplay(run.IdempotencyKey.Value, "different")))
            .Should().Throw<AdminWithdrawalStaleCommandException>();
        ((Action)(() => store.Add(run))).Should().Throw<AdminWithdrawalStaleCommandException>();
        ((Action)(() => store.Add(run with
        {
            Id = Guid.NewGuid(),
            RequestHash = "other"
        }))).Should().Throw<AdminWithdrawalStaleCommandException>();
        ((Action)(() => store.Add(run with
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = new IdempotencyKey("different-key"),
            RequestHash = "other"
        }))).Should().Throw<AdminWithdrawalOverlapException>();

        ((Action)(() => store.Get(Guid.Empty))).Should().Throw<ArgumentException>();
        ((Action)(() => store.Get(Guid.NewGuid()))).Should().Throw<KeyNotFoundException>();
        ((Action)(() => store.Update(null!, 1))).Should().Throw<ArgumentNullException>();
        ((Action)(() => store.Update(run with { Version = 2 }, 0)))
            .Should().Throw<AdminWithdrawalStaleCommandException>();
        ((Action)(() => store.Update(run with { Version = 3 }, 1)))
            .Should().Throw<AdminWithdrawalStaleCommandException>();

        var failed = run with { State = AdminWithdrawalRunState.Failed, Version = 2 };
        store.Update(failed, 1).Should().Be(failed);
        store.FindPeriod(run.PeriodStart).Should().BeNull();
        var replacement = run with
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = new IdempotencyKey("replacement"),
            RequestHash = "replacement",
            State = AdminWithdrawalRunState.Cancelled
        };
        store.Add(replacement);
        store.FindPeriod(run.PeriodStart).Should().BeNull();
    }

    [Fact]
    public void StoreEnforcesProviderEventReplayAndCompareAndSwap()
    {
        var store = new InMemoryAdminWithdrawalStore();
        var run = StandaloneRun();
        store.Add(run);

        ((Action)(() => store.FindProviderEvent("", "hash"))).Should().Throw<ArgumentException>();
        ((Action)(() => store.FindProviderEvent("event", ""))).Should().Throw<ArgumentException>();
        store.FindProviderEvent("event", "hash").Should().BeNull();
        ((Action)(() => store.RecordProviderEvent("", "hash", run, 1))).Should().Throw<ArgumentException>();
        ((Action)(() => store.RecordProviderEvent("event", "", run, 1))).Should().Throw<ArgumentException>();

        var terminal = run with { State = AdminWithdrawalRunState.Succeeded, Version = 2 };
        ((Action)(() => store.RecordProviderEvent("event", "hash", terminal, 0)))
            .Should().Throw<AdminWithdrawalStaleCommandException>();
        ((Action)(() => store.RecordProviderEvent("event", "hash", terminal with { Version = 3 }, 1)))
            .Should().Throw<AdminWithdrawalStaleCommandException>();
        ((Action)(() => store.RecordProviderEvent("event", "hash", terminal with { Id = Guid.NewGuid() }, 1)))
            .Should().Throw<KeyNotFoundException>();

        store.RecordProviderEvent(" event ", "hash", terminal, 1);
        store.FindProviderEvent("event", "hash").Should().Be(run.Id);
        ((Action)(() => store.FindProviderEvent("event", "different")))
            .Should().Throw<AdminWithdrawalEvidenceException>();
        ((Action)(() => store.RecordProviderEvent("event", "hash", terminal, 1)))
            .Should().Throw<AdminWithdrawalEvidenceException>();
    }

    [Fact]
    public void AuditTrailValidatesInputsAndBuildsAHashChain()
    {
        var audit = new AdminWithdrawalAuditTrail();
        var runId = Guid.NewGuid();
        audit.Events(runId).Should().BeEmpty();
        audit.Verify(runId).Should().BeFalse();
        ((Action)(() => audit.Append(Guid.Empty, "kind", null, "evidence", Now)))
            .Should().Throw<ArgumentException>();
        ((Action)(() => audit.Append(runId, "", null, "evidence", Now)))
            .Should().Throw<ArgumentException>();
        ((Action)(() => audit.Append(runId, "kind", null, "", Now)))
            .Should().Throw<ArgumentException>();

        var first = audit.Append(runId, " first ", null, " evidence-1 ", Now);
        var second = audit.Append(runId, "second", Guid.NewGuid(), "evidence-2", Now.AddMinutes(1));
        first.Sequence.Should().Be(1);
        second.PreviousHash.Should().Be(first.Hash);
        audit.Events(runId).Should().Equal(first, second);
        audit.Verify(runId).Should().BeTrue();
    }

    [Theory]
    [InlineData("run")]
    [InlineData("sequence")]
    [InlineData("previous")]
    [InlineData("hash")]
    public void AuditTrailDetectsEveryKindOfChainTampering(string field)
    {
        var audit = new AdminWithdrawalAuditTrail();
        var runId = Guid.NewGuid();
        var original = audit.Append(runId, "reserved", Guid.NewGuid(), "evidence", Now);
        var eventsField = typeof(AdminWithdrawalAuditTrail)
            .GetField("_events", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var events = (Dictionary<Guid, List<AdminWithdrawalAuditEvent>>)eventsField.GetValue(audit)!;
        events[runId][0] = field switch
        {
            "run" => original with { RunId = Guid.NewGuid() },
            "sequence" => original with { Sequence = 2 },
            "previous" => original with { PreviousHash = "tampered" },
            _ => original with { Hash = "tampered" }
        };

        audit.Verify(runId).Should().BeFalse();
    }

    [Fact]
    public void CoordinatorRejectsEveryNullDependency()
    {
        var fixture = new Fixture();
        Action[] constructors =
        [
            () => _ = new AdminWithdrawalCoordinator(null!, fixture.Store, fixture.Fences,
                fixture.TreasuryGate, fixture.Authority, fixture.Provider, fixture.EvidenceVerifier,
                fixture.Audit, fixture.Execution),
            () => _ = new AdminWithdrawalCoordinator(fixture.Ledger, null!, fixture.Fences,
                fixture.TreasuryGate, fixture.Authority, fixture.Provider, fixture.EvidenceVerifier,
                fixture.Audit, fixture.Execution),
            () => _ = new AdminWithdrawalCoordinator(fixture.Ledger, fixture.Store, null!,
                fixture.TreasuryGate, fixture.Authority, fixture.Provider, fixture.EvidenceVerifier,
                fixture.Audit, fixture.Execution),
            () => _ = new AdminWithdrawalCoordinator(fixture.Ledger, fixture.Store, fixture.Fences,
                null!, fixture.Authority, fixture.Provider, fixture.EvidenceVerifier,
                fixture.Audit, fixture.Execution),
            () => _ = new AdminWithdrawalCoordinator(fixture.Ledger, fixture.Store, fixture.Fences,
                fixture.TreasuryGate, null!, fixture.Provider, fixture.EvidenceVerifier,
                fixture.Audit, fixture.Execution),
            () => _ = new AdminWithdrawalCoordinator(fixture.Ledger, fixture.Store, fixture.Fences,
                fixture.TreasuryGate, fixture.Authority, null!, fixture.EvidenceVerifier,
                fixture.Audit, fixture.Execution),
            () => _ = new AdminWithdrawalCoordinator(fixture.Ledger, fixture.Store, fixture.Fences,
                fixture.TreasuryGate, fixture.Authority, fixture.Provider, null!,
                fixture.Audit, fixture.Execution),
            () => _ = new AdminWithdrawalCoordinator(fixture.Ledger, fixture.Store, fixture.Fences,
                fixture.TreasuryGate, fixture.Authority, fixture.Provider, fixture.EvidenceVerifier,
                null!, fixture.Execution),
            () => _ = new AdminWithdrawalCoordinator(fixture.Ledger, fixture.Store, fixture.Fences,
                fixture.TreasuryGate, fixture.Authority, fixture.Provider, fixture.EvidenceVerifier,
                fixture.Audit, null!)
        ];

        constructors.Should().AllSatisfy(constructor =>
            constructor.Should().Throw<ArgumentNullException>());
    }

    [Fact]
    public void ReservationValidatesRequestsAndReplaysOnlyIdenticalCommands()
    {
        var fixture = new Fixture();
        fixture.AddFee(10, Now.AddDays(-130));
        ((Action)(() => fixture.Coordinator.ReserveMonthlyRun(null!)))
            .Should().Throw<ArgumentNullException>();
        var valid = fixture.Request();
        AdminWithdrawalReservationRequest[] invalid =
        [
            valid with { RunId = Guid.Empty },
            valid with { RequestedBy = Guid.Empty },
            valid with { PeriodStart = new DateOnly(2026, 8, 2) },
            valid with { ReserveAuthorizationEpoch = 0 },
            valid with { SourceAssetKey = " " },
            valid with { DestinationHash = " " }
        ];
        foreach (var request in invalid)
            ((Action)(() => fixture.Coordinator.ReserveMonthlyRun(request)))
                .Should().Throw<ArgumentException>();

        var created = fixture.Coordinator.ReserveMonthlyRun(valid);
        fixture.Coordinator.ReserveMonthlyRun(valid).Should().Be(created);
        ((Action)(() => fixture.Coordinator.ReserveMonthlyRun(valid with { RunId = Guid.NewGuid() })))
            .Should().Throw<AdminWithdrawalStaleCommandException>();
    }

    [Fact]
    public void ApprovalRequiresAnApproverIdentity()
    {
        var fixture = new Fixture();
        fixture.AddFee(10, Now.AddDays(-130));
        var run = fixture.Coordinator.ReserveMonthlyRun(fixture.Request());
        ((Action)(() => fixture.Coordinator.Approve(run.Id, run.Version, Guid.Empty, Now)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReservationSnapshotGuardRejectsHoldsAndChangedSelections()
    {
        AdminWithdrawalReservationSnapshotGuard.EnsureUnchanged(0, "same", "same");
        ((Action)(() => AdminWithdrawalReservationSnapshotGuard.EnsureUnchanged(1, "same", "same")))
            .Should().Throw<AdminWithdrawalEligibilityException>();
        ((Action)(() => AdminWithdrawalReservationSnapshotGuard.EnsureUnchanged(0, "before", "after")))
            .Should().Throw<AdminWithdrawalStaleCommandException>();
    }

    [Theory]
    [InlineData(AdminWithdrawalProviderOutcome.Submitted, AdminWithdrawalRunState.Dispatching)]
    [InlineData(AdminWithdrawalProviderOutcome.Ambiguous, AdminWithdrawalRunState.Ambiguous)]
    [InlineData(AdminWithdrawalProviderOutcome.Succeeded, AdminWithdrawalRunState.Succeeded)]
    public async Task DispatchHandlesEveryValidProviderReceiptOutcome(
        AdminWithdrawalProviderOutcome outcome,
        AdminWithdrawalRunState expectedState)
    {
        var fixture = PreparedFixture();
        fixture.Provider.DispatchOutcome = outcome;
        var approved = fixture.Store.FindPeriod(new DateOnly(2026, 8, 1))!;

        var result = await fixture.Coordinator.DispatchAsync(
            approved.Id, approved.Version, approved.FencingToken, approved.ExecutionEpoch,
            fixture.CustodyReport(), Now);

        result.State.Should().Be(expectedState);
        result.ProviderTransferId.Should().Be("transfer-1");
    }

    [Theory]
    [InlineData("run")]
    [InlineData("outcome")]
    [InlineData("transfer")]
    [InlineData("fence")]
    [InlineData("epoch")]
    [InlineData("amount")]
    [InlineData("asset")]
    [InlineData("destination")]
    [InlineData("evidence")]
    [InlineData("signature")]
    public async Task DispatchRejectsEveryUnboundProviderReceiptField(string field)
    {
        var fixture = PreparedFixture();
        var approved = fixture.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        fixture.Provider.ReceiptFactory = command =>
        {
            var receipt = ReceiptFor(command, AdminWithdrawalProviderOutcome.Submitted);
            return field switch
            {
                "run" => receipt with { RunId = Guid.NewGuid() },
                "outcome" => receipt with { Outcome = (AdminWithdrawalProviderOutcome)999 },
                "transfer" => receipt with { ProviderTransferId = " " },
                "fence" => receipt with { FencingToken = receipt.FencingToken + 1 },
                "epoch" => receipt with { ExecutionEpoch = receipt.ExecutionEpoch + 1 },
                "amount" => receipt with { Amount = new CoinAmount(CurrencyCode.HardCoin, receipt.Amount.Units + 1) },
                "asset" => receipt with { SourceAssetKey = "other" },
                "destination" => receipt with { DestinationHash = "other" },
                "evidence" => receipt with { EvidenceHash = null! },
                _ => receipt with { Signature = " " }
            };
        };

        var act = async () => await fixture.Coordinator.DispatchAsync(
            approved.Id, approved.Version, approved.FencingToken, approved.ExecutionEpoch,
            fixture.CustodyReport(), Now);

        await act.Should().ThrowAsync<AdminWithdrawalEvidenceException>();
        fixture.Store.Get(approved.Id).State.Should().Be(AdminWithdrawalRunState.Ambiguous);
    }

    [Fact]
    public async Task ReconciliationRejectsWrongStateAndNonTerminalOrUnsignedEvidence()
    {
        var pendingFixture = new Fixture();
        pendingFixture.AddFee(10, Now.AddDays(-130));
        var pending = pendingFixture.Coordinator.ReserveMonthlyRun(pendingFixture.Request());
        var stale = async () => await pendingFixture.Coordinator.ReconcileAsync(pending.Id, Now);
        await stale.Should().ThrowAsync<AdminWithdrawalStaleCommandException>();

        foreach (var outcome in new[]
                 {
                     AdminWithdrawalProviderOutcome.Submitted,
                     AdminWithdrawalProviderOutcome.Ambiguous
                 })
        {
            var fixture = PreparedFixture();
            var approved = fixture.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
            var dispatching = await fixture.Coordinator.DispatchAsync(
                approved.Id, approved.Version, approved.FencingToken, approved.ExecutionEpoch,
                fixture.CustodyReport(), Now);
            var providerEvent = EventFor(dispatching, outcome, $"non-terminal-{outcome}");
            ((Action)(() => fixture.Coordinator.ApplyProviderEvent(providerEvent, Now.AddMinutes(1))))
                .Should().Throw<AdminWithdrawalEvidenceException>();
        }

        var unsigned = PreparedFixture();
        var unsignedRun = unsigned.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        var inFlight = await unsigned.Coordinator.DispatchAsync(
            unsignedRun.Id, unsignedRun.Version, unsignedRun.FencingToken, unsignedRun.ExecutionEpoch,
            unsigned.CustodyReport(), Now);
        unsigned.EvidenceVerifier.IsValid = false;
        ((Action)(() => unsigned.Coordinator.ApplyProviderEvent(
                EventFor(inFlight, AdminWithdrawalProviderOutcome.Succeeded, "unsigned"), Now.AddMinutes(1))))
            .Should().Throw<AdminWithdrawalEvidenceException>();
    }

    [Fact]
    public async Task ProviderEventReplayIsIdempotentAndTerminalEvidenceIsOrdered()
    {
        var fixture = PreparedFixture();
        var approved = fixture.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        var dispatching = await fixture.Coordinator.DispatchAsync(
            approved.Id, approved.Version, approved.FencingToken, approved.ExecutionEpoch,
            fixture.CustodyReport(), Now);
        var providerEvent = EventFor(
            dispatching, AdminWithdrawalProviderOutcome.Succeeded, "terminal-1");

        var succeeded = fixture.Coordinator.ApplyProviderEvent(providerEvent, Now.AddMinutes(1));
        fixture.Coordinator.ApplyProviderEvent(providerEvent, Now.AddMinutes(2)).Should().Be(succeeded);
        ((Action)(() => fixture.Coordinator.ApplyProviderEvent(
                providerEvent with { EvidenceHash = "different" }, Now.AddMinutes(2))))
            .Should().Throw<AdminWithdrawalEvidenceException>();
        ((Action)(() => fixture.Coordinator.ApplyProviderEvent(
                providerEvent with { EventId = "terminal-2" }, Now.AddMinutes(2))))
            .Should().Throw<AdminWithdrawalStaleCommandException>();
    }

    [Theory]
    [InlineData("event")]
    [InlineData("transfer")]
    [InlineData("outcome")]
    [InlineData("fence")]
    [InlineData("epoch")]
    [InlineData("amount")]
    [InlineData("asset")]
    [InlineData("destination")]
    [InlineData("provider-transfer")]
    [InlineData("evidence")]
    [InlineData("signature")]
    public async Task ProviderEventsRequireEveryFencedBinding(string field)
    {
        var fixture = PreparedFixture();
        var approved = fixture.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        var dispatching = await fixture.Coordinator.DispatchAsync(
            approved.Id, approved.Version, approved.FencingToken, approved.ExecutionEpoch,
            fixture.CustodyReport(), Now);
        var providerEvent = EventFor(dispatching, AdminWithdrawalProviderOutcome.Succeeded, $"invalid-{field}");
        providerEvent = field switch
        {
            "event" => providerEvent with { EventId = " " },
            "transfer" => providerEvent with { ProviderTransferId = " " },
            "outcome" => providerEvent with { Outcome = (AdminWithdrawalProviderOutcome)999 },
            "fence" => providerEvent with { FencingToken = providerEvent.FencingToken + 1 },
            "epoch" => providerEvent with { ExecutionEpoch = providerEvent.ExecutionEpoch + 1 },
            "amount" => providerEvent with
            {
                Amount = new CoinAmount(CurrencyCode.HardCoin, providerEvent.Amount.Units + 1)
            },
            "asset" => providerEvent with { SourceAssetKey = "other" },
            "destination" => providerEvent with { DestinationHash = "other" },
            "provider-transfer" => providerEvent with { ProviderTransferId = "other-transfer" },
            "evidence" => providerEvent with { EvidenceHash = " " },
            _ => providerEvent with { Signature = " " }
        };

        ((Action)(() => fixture.Coordinator.ApplyProviderEvent(providerEvent, Now.AddMinutes(1))))
            .Should().Throw<AdminWithdrawalEvidenceException>();
    }

    [Theory]
    [InlineData("state")]
    [InlineData("version")]
    [InlineData("fence")]
    [InlineData("command-epoch")]
    [InlineData("gate-epoch")]
    [InlineData("approver")]
    [InlineData("same-actor")]
    public async Task DispatchRejectsEveryStaleOrUnapprovedCommand(string field)
    {
        var fixture = new Fixture();
        fixture.AddFee(10, Now.AddDays(-130));
        var reserved = fixture.Coordinator.ReserveMonthlyRun(fixture.Request());
        var run = field == "state"
            ? reserved
            : fixture.Coordinator.Approve(reserved.Id, reserved.Version, fixture.ApprovedBy, Now);
        var expectedVersion = run.Version;
        var fencingToken = run.FencingToken;
        var executionEpoch = run.ExecutionEpoch;
        if (field == "version") expectedVersion++;
        if (field == "fence") fencingToken++;
        if (field == "command-epoch") executionEpoch++;
        if (field is "gate-epoch" or "approver" or "same-actor")
        {
            run = run with
            {
                ExecutionEpoch = field == "gate-epoch" ? run.ExecutionEpoch + 1 : run.ExecutionEpoch,
                ApprovedBy = field switch
                {
                    "approver" => null,
                    "same-actor" => run.RequestedBy,
                    _ => run.ApprovedBy
                },
                Version = run.Version + 1
            };
            fixture.Store.Update(run, run.Version - 1);
            expectedVersion = run.Version;
            executionEpoch = run.ExecutionEpoch;
        }

        var act = async () => await fixture.Coordinator.DispatchAsync(
            run.Id, expectedVersion, fencingToken, executionEpoch, fixture.CustodyReport(), Now);

        await act.Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
        fixture.Provider.DispatchCalls.Should().Be(0);
    }

    [Fact]
    public async Task DispatchRechecksCustodyHoldsAndExactReservations()
    {
        var held = PreparedFixture();
        var heldRun = held.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        held.AddFee(1, Now.AddDays(-130));
        held.PlaceHold(1);
        var heldAct = async () => await held.Coordinator.DispatchAsync(
            heldRun.Id, heldRun.Version, heldRun.FencingToken, heldRun.ExecutionEpoch,
            held.CustodyReport(), Now);
        await heldAct.Should().ThrowAsync<AdminWithdrawalEligibilityException>();

        var nullCustody = PreparedFixture();
        var nullRun = nullCustody.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        var nullAct = async () => await nullCustody.Coordinator.DispatchAsync(
            nullRun.Id, nullRun.Version, nullRun.FencingToken, nullRun.ExecutionEpoch, null!, Now);
        await nullAct.Should().ThrowAsync<ArgumentNullException>();

        var wrongStatus = PreparedFixture();
        var statusRun = wrongStatus.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        wrongStatus.Ledger.Execute(tx =>
        {
            tx.TransitionFragmentReservations(
                statusRun.Id, FragmentReservationStatus.Reserved, FragmentReservationStatus.Released, Now);
            return 0;
        });
        var statusAct = async () => await wrongStatus.Coordinator.DispatchAsync(
            statusRun.Id, statusRun.Version, statusRun.FencingToken, statusRun.ExecutionEpoch,
            wrongStatus.CustodyReport(), Now);
        await statusAct.Should().ThrowAsync<AdminWithdrawalStaleCommandException>();

        var missing = PreparedFixture();
        var missingRun = missing.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        var emptyLedgerCoordinator = CoordinatorFor(missing, ledger: new InMemoryLedgerKernelStore());
        var missingAct = async () => await emptyLedgerCoordinator.DispatchAsync(
            missingRun.Id, missingRun.Version, missingRun.FencingToken, missingRun.ExecutionEpoch,
            missing.CustodyReport(), Now);
        await missingAct.Should().ThrowAsync<AdminWithdrawalStaleCommandException>();

        var wrongPurpose = PreparedFixture();
        var purposeRun = wrongPurpose.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        var original = wrongPurpose.Ledger.GetFragmentReservations(purposeRun.Id).Single();
        var customLedger = new InMemoryLedgerKernelStore();
        var source = wrongPurpose.Ledger.SourceEvidenceHistory.Single();
        var lot = wrongPurpose.Ledger.CreditLots.Single();
        customLedger.Execute(tx =>
        {
            tx.AddSource(source);
            tx.AddCreditLot(lot);
            tx.AddFragmentReservation(new ValueFragmentReservation(
                Guid.NewGuid(), purposeRun.Id, FragmentReservationPurpose.Payout,
                original.LotId, original.WalletId, original.Amount, original.Ranges,
                original.OperationVersion, original.FencingToken, original.KillSwitchEpoch,
                FragmentReservationStatus.Reserved, original.ReservedAt, null));
            return 0;
        });
        var purposeCoordinator = CoordinatorFor(wrongPurpose, ledger: customLedger);
        var purposeAct = async () => await purposeCoordinator.DispatchAsync(
            purposeRun.Id, purposeRun.Version, purposeRun.FencingToken, purposeRun.ExecutionEpoch,
            wrongPurpose.CustodyReport(), Now);
        await purposeAct.Should().ThrowAsync<AdminWithdrawalStaleCommandException>();
    }

    [Fact]
    public async Task DispatchRejectsChangedReserveHeadsAndInvalidSourceAllocations()
    {
        await AssertReserveAuthorityFailure(
            new CoreReserveAuthority(), typeof(ReserveAuthorizationException));
        await AssertReserveAuthorityFailure(
            BuildAuthority(2, 1, [HardAsset("stripe:platform:cash", 3_000_000_000)]),
            typeof(AdminWithdrawalStaleCommandException));
        await AssertReserveAuthorityFailure(
            BuildAuthority(1, 2, [HardAsset("stripe:platform:cash", 3_000_000_000)]),
            typeof(AdminWithdrawalStaleCommandException));
        await AssertReserveAuthorityFailure(
            BuildAuthority(1, 1, [HardAsset("other", 3_000_000_000)]),
            typeof(AdminWithdrawalEligibilityException));
        await AssertReserveAuthorityFailure(
            BuildAuthority(1, 1,
            [
                new ExternalReserveAsset("stripe:platform:cash", ReserveBackingPurpose.SoftCoin, 3_000_000_000),
                HardAsset("other", 3_000_000_000)
            ]),
            typeof(AdminWithdrawalEligibilityException));
        await AssertReserveAuthorityFailure(
            BuildAuthority(1, 1,
            [
                HardAsset("stripe:platform:cash", 50_000_000),
                HardAsset("other", 3_000_000_000)
            ]),
            typeof(ReserveShortfallException));
    }

    [Fact]
    public async Task DispatchRejectsMissingOrInsufficientSourceCustody()
    {
        var missingFixture = PreparedFixture();
        var missingRun = missingFixture.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        var missingAuthority = BuildAuthority(1, 1, [HardAsset("other", 3_000_000_000)]);
        var missingSigner = new TreasuryCustodySigner(Enumerable.Repeat((byte)31, 32).ToArray());
        var missingGate = new TreasuryOperationGate(missingAuthority, missingSigner);
        var missingReport = CustodyFor(missingAuthority, missingSigner);
        var missingCoordinator = CoordinatorFor(missingFixture, treasuryGate: missingGate);
        var missingAct = async () => await missingCoordinator.DispatchAsync(
            missingRun.Id, missingRun.Version, missingRun.FencingToken, missingRun.ExecutionEpoch,
            missingReport, Now);
        await missingAct.Should().ThrowAsync<ReserveShortfallException>();

        var lowFixture = PreparedFixture();
        var lowRun = lowFixture.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        var lowAuthority = BuildAuthority(1, 1,
        [
            HardAsset("stripe:platform:cash", 50_000_000),
            HardAsset("other", 3_000_000_000)
        ]);
        var lowSigner = new TreasuryCustodySigner(Enumerable.Repeat((byte)37, 32).ToArray());
        var lowGate = new TreasuryOperationGate(lowAuthority, lowSigner);
        var lowReport = CustodyFor(lowAuthority, lowSigner);
        var lowCoordinator = CoordinatorFor(lowFixture, treasuryGate: lowGate);
        var lowAct = async () => await lowCoordinator.DispatchAsync(
            lowRun.Id, lowRun.Version, lowRun.FencingToken, lowRun.ExecutionEpoch,
            lowReport, Now);
        await lowAct.Should().ThrowAsync<ReserveShortfallException>();
    }

    [Fact]
    public void ReservationEligibilityRejectsUnconfirmedMissingAndLateSourceEvidence()
    {
        var observedAt = Now.AddDays(-130);
        var observed = SourceEvidence.Observe(
            SourceStampId.New(), "provider", "observed", "evidence", observedAt);
        var late = SourceEvidence.Observe(
                SourceStampId.New(), "provider", "late", "evidence", observedAt)
            .Confirm(observedAt.AddHours(1));
        var confirmedWithoutTimestamp = ConfirmedSourceWithoutTimestamp(observedAt);

        AssertIneligibleLot(null, ProvenanceKind.EarnedHard, CreditLotState.Active, observedAt, Now.AddDays(-1));
        AssertIneligibleLot(observed, ProvenanceKind.EarnedHard, CreditLotState.Active, observedAt, Now.AddDays(-1));
        AssertIneligibleLot(confirmedWithoutTimestamp, ProvenanceKind.EarnedHard, CreditLotState.Active,
            observedAt, Now.AddDays(-1));
        AssertIneligibleLot(late, ProvenanceKind.EarnedHard, CreditLotState.Active, observedAt, Now.AddDays(-1));
        AssertIneligibleLot(late, ProvenanceKind.PurchasedHard, CreditLotState.Active,
            late.ConfirmedAt!.Value, Now.AddDays(-1));
        AssertIneligibleLot(late, ProvenanceKind.EarnedHard, CreditLotState.Held,
            late.ConfirmedAt!.Value, Now.AddDays(-1));
    }

    private static Fixture PreparedFixture()
    {
        var fixture = new Fixture();
        fixture.AddFee(10, Now.AddDays(-130));
        fixture.ReserveAndApprove();
        return fixture;
    }

    private static AdminWithdrawalCoordinator CoordinatorFor(
        Fixture fixture,
        InMemoryLedgerKernelStore? ledger = null,
        TreasuryOperationGate? treasuryGate = null,
        CoreReserveAuthority? reserveAuthority = null) => new(
        ledger ?? fixture.Ledger,
        fixture.Store,
        fixture.Fences,
        treasuryGate ?? fixture.TreasuryGate,
        reserveAuthority ?? fixture.Authority,
        fixture.Provider,
        fixture.EvidenceVerifier,
        fixture.Audit,
        fixture.Execution);

    private static AdminWithdrawalProviderReceipt ReceiptFor(
        AdminWithdrawalDispatchCommand command,
        AdminWithdrawalProviderOutcome outcome) => new(
        command.RunId, command.TenantId, outcome, "transfer-1", command.FencingToken,
        command.ExecutionEpoch, command.Amount, command.SourceAssetKey,
        command.DestinationHash, "receipt", "signature", command.RequestedAt);

    private static AdminWithdrawalProviderEvent EventFor(
        AdminWithdrawalRun run,
        AdminWithdrawalProviderOutcome outcome,
        string eventId) => new(
        eventId, run.Id, run.TenantId, outcome, run.ProviderTransferId ?? "transfer-1",
        run.FencingToken, run.ExecutionEpoch, run.Amount, run.SourceAssetKey,
        run.DestinationHash, "event", "signature", Now.AddMinutes(1));

    private static ExternalReserveAsset HardAsset(string key, long nanos) =>
        new(key, ReserveBackingPurpose.HardCoin, nanos);

    private static CoreReserveAuthority BuildAuthority(
        long version,
        long epoch,
        IReadOnlyCollection<ExternalReserveAsset> assets)
    {
        var authority = new CoreReserveAuthority();
        authority.ValidateAndActivate(new ReserveProposal(
            new ReserveVersion(version), null, new PolicyVersion(1), epoch,
            Now.AddMinutes(-1), Now.AddMinutes(5),
            new ReserveLiabilityPosition(0, 0, 0, 0),
            new ReserveBufferPosition(0, 0, 100, 0, 0, 0, 0),
            [], assets, "reserve"), Now);
        return authority;
    }

    private static TreasuryCustodyReport CustodyFor(
        CoreReserveAuthority authority,
        TreasuryCustodySigner signer)
    {
        var head = authority.ActiveHead!;
        return new TreasuryCustodyReconciler(signer).Reconcile(
            head,
            head.AssetAllocations.Select(asset => new TreasuryCustodyObservation(
                asset.AssetKey, asset.EligibleUsdNanos, 0,
                Now.AddMinutes(-1), Now.AddMinutes(5), "custody")).ToArray(),
            Now);
    }

    private static async Task AssertReserveAuthorityFailure(
        CoreReserveAuthority authority,
        Type expectedException)
    {
        var fixture = PreparedFixture();
        var run = fixture.Store.FindPeriod(new DateOnly(2026, 8, 1))!;
        var coordinator = CoordinatorFor(fixture, reserveAuthority: authority);
        Func<Task> act = async () => await coordinator.DispatchAsync(
            run.Id, run.Version, run.FencingToken, run.ExecutionEpoch,
            fixture.CustodyReport(), Now);
        var exception = await Record.ExceptionAsync(act);
        exception.Should().BeOfType(expectedException);
    }

    private static void AssertIneligibleLot(
        SourceEvidence? source,
        ProvenanceKind provenance,
        CreditLotState state,
        DateTimeOffset confirmedAt,
        DateTimeOffset maturesAt)
    {
        var fixture = new Fixture();
        var root = source?.Id ?? SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), fixture.PlatformFeeWalletId,
            new CoinAmount(CurrencyCode.HardCoin, 10), provenance,
            confirmedAt, maturesAt, 50, state,
            [new RootTraceRange(root, 0, 10 * CurrencyTraceScale.HardCoinTraceUnitsPerCoin, 0)],
            CurrencyTraceScale.HardCoinTraceUnitsPerCoin);
        fixture.Ledger.Execute(tx =>
        {
            if (source is not null) tx.AddSource(source);
            tx.AddCreditLot(lot);
            return 0;
        });
        ((Action)(() => fixture.Coordinator.ReserveMonthlyRun(fixture.Request())))
            .Should().Throw<AdminWithdrawalEligibilityException>();
    }

    private static SourceEvidence ConfirmedSourceWithoutTimestamp(DateTimeOffset observedAt)
    {
        var constructor = typeof(SourceEvidence).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return (SourceEvidence)constructor.Invoke(
        [
            SourceStampId.New(), "provider", "reference", "hash",
            SourceConfirmationState.Confirmed, observedAt, null, null
        ]);
    }
    private static AdminWithdrawalRun StandaloneRun() => new(
        Guid.NewGuid(), Guid.NewGuid(), new IdempotencyKey("standalone"), "request-hash",
        new DateOnly(2026, 8, 1), Guid.NewGuid(), null, WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 10), "asset", "destination",
        AdminWithdrawalRunState.PendingApproval, 1, 1, 1,
        new ReserveVersion(1), 1, new PolicyVersion(1), null, null, Now, Now);
}
