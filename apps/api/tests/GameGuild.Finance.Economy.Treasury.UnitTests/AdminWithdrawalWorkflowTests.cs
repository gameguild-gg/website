using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed partial class AdminWithdrawalWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReserveMonthlyRunSelectsOldestMatureFeeFragmentsAndRejectsOverlap()
    {
        var fixture = new Fixture();
        var oldest = fixture.AddFee(40, Now.AddDays(-150));
        var next = fixture.AddFee(60, Now.AddDays(-140));
        var immature = fixture.AddFee(500, Now.AddDays(-10));

        var run = fixture.Coordinator.ReserveMonthlyRun(fixture.Request());

        run.State.Should().Be(AdminWithdrawalRunState.PendingApproval);
        run.Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 100));
        var reservedLotIds = fixture.Ledger.GetFragmentReservations(run.Id).Select(item => item.LotId);
        reservedLotIds.Should().BeEquivalentTo([oldest.Id, next.Id]);
        reservedLotIds.Should().NotContain(immature.Id);
        fixture.Ledger.GetFragmentReservations(run.Id)
            .Should().OnlyContain(item => item.Purpose == FragmentReservationPurpose.AdminWithdrawal &&
                                         item.Status == FragmentReservationStatus.Reserved);
        ((Action)(() => fixture.Coordinator.ReserveMonthlyRun(
                fixture.Request() with { RunId = Guid.NewGuid(), IdempotencyKey = new IdempotencyKey("august-overlap") })))
            .Should().Throw<AdminWithdrawalOverlapException>();
    }

    [Fact]
    public void ReserveMonthlyRunRejectsImmatureRevenueAndActiveHolds()
    {
        var immature = new Fixture();
        immature.AddFee(10, Now.AddDays(-10));
        ((Action)(() => immature.Coordinator.ReserveMonthlyRun(immature.Request())))
            .Should().Throw<AdminWithdrawalEligibilityException>();

        var held = new Fixture();
        held.AddFee(10, Now.AddDays(-130));
        held.PlaceHold(1);
        ((Action)(() => held.Coordinator.ReserveMonthlyRun(held.Request())))
            .Should().Throw<AdminWithdrawalEligibilityException>();
    }

    [Fact]
    public void ApprovalRequiresIndependentActorAndCurrentVersion()
    {
        var fixture = new Fixture();
        fixture.AddFee(10, Now.AddDays(-130));
        var run = fixture.Coordinator.ReserveMonthlyRun(fixture.Request());

        ((Action)(() => fixture.Coordinator.Approve(run.Id, run.Version, fixture.RequestedBy, Now)))
            .Should().Throw<AdminWithdrawalApprovalException>();
        var approved = fixture.Coordinator.Approve(run.Id, run.Version, fixture.ApprovedBy, Now);

        approved.State.Should().Be(AdminWithdrawalRunState.Approved);
        approved.ApprovedBy.Should().Be(fixture.ApprovedBy);
        ((Action)(() => fixture.Coordinator.Approve(run.Id, run.Version, Guid.NewGuid(), Now)))
            .Should().Throw<AdminWithdrawalStaleCommandException>();
    }

    [Fact]
    public async Task DispatchRejectsInsufficientPostWithdrawalReserveWithoutCallingProvider()
    {
        var fixture = new Fixture(hardBackingUsdMinor: 150, requiredHardReserveUsdMinor: 100);
        fixture.AddFee(60, Now.AddDays(-130));
        var approved = fixture.ReserveAndApprove();

        var act = async () => await fixture.Coordinator.DispatchAsync(
            approved.Id, approved.Version, approved.FencingToken, fixture.Execution.Epoch,
            fixture.CustodyReport(), Now);

        await act.Should().ThrowAsync<ReserveShortfallException>();
        fixture.Provider.DispatchCalls.Should().Be(0);
        fixture.Ledger.GetFragmentReservations(approved.Id)
            .Should().OnlyContain(item => item.Status == FragmentReservationStatus.Reserved);
    }

    [Fact]
    public async Task ProviderTimeoutLeavesExactFragmentsDispatchingUntilFencedReconciliationSucceeds()
    {
        var fixture = new Fixture();
        var lot = fixture.AddFee(50, Now.AddDays(-130));
        var approved = fixture.ReserveAndApprove();
        fixture.Provider.TimeoutOnDispatch = true;

        var ambiguous = await fixture.Coordinator.DispatchAsync(
            approved.Id, approved.Version, approved.FencingToken, fixture.Execution.Epoch,
            fixture.CustodyReport(), Now);

        ambiguous.State.Should().Be(AdminWithdrawalRunState.Ambiguous);
        fixture.Ledger.GetFragmentReservations(approved.Id)
            .Should().OnlyContain(item => item.Status == FragmentReservationStatus.Dispatching);
        fixture.Provider.ReconcileOutcome = AdminWithdrawalProviderOutcome.Succeeded;

        var succeeded = await fixture.Coordinator.ReconcileAsync(approved.Id, Now.AddMinutes(1));

        succeeded.State.Should().Be(AdminWithdrawalRunState.Succeeded);
        fixture.Ledger.FragmentConsumptions.Should().ContainSingle(item => item.ParentLotId == lot.Id);
        fixture.Ledger.GetFragmentReservations(approved.Id)
            .Should().OnlyContain(item => item.Status == FragmentReservationStatus.Consumed);
        fixture.Audit.Verify(approved.Id).Should().BeTrue();
    }

    [Fact]
    public async Task DefinitiveProviderFailureReleasesOriginalFragmentsWithoutCreatingNewLotsOrMaturity()
    {
        var fixture = new Fixture();
        var lot = fixture.AddFee(50, Now.AddDays(-130));
        var originalLots = fixture.Ledger.CreditLots.ToArray();
        var approved = fixture.ReserveAndApprove();
        fixture.Provider.DispatchOutcome = AdminWithdrawalProviderOutcome.Failed;

        var failed = await fixture.Coordinator.DispatchAsync(
            approved.Id, approved.Version, approved.FencingToken, fixture.Execution.Epoch,
            fixture.CustodyReport(), Now);

        failed.State.Should().Be(AdminWithdrawalRunState.Failed);
        fixture.Ledger.GetFragmentReservations(approved.Id)
            .Should().OnlyContain(item => item.Status == FragmentReservationStatus.Released);
        fixture.Ledger.FragmentConsumptions.Should().BeEmpty();
        fixture.Ledger.CreditLots.Should().Equal(originalLots);
        fixture.Ledger.CreditLots.Single().OriginalMaturesAt.Should().Be(lot.OriginalMaturesAt);
        fixture.Audit.Verify(approved.Id).Should().BeTrue();
    }

    [Fact]
    public async Task ExecutionIsDisabledByDefaultAndInvalidProviderEvidenceFailsClosed()
    {
        var fixture = new Fixture(executionEnabled: false);
        fixture.AddFee(10, Now.AddDays(-130));
        var approved = fixture.ReserveAndApprove();

        var disabled = async () => await fixture.Coordinator.DispatchAsync(
            approved.Id, approved.Version, approved.FencingToken, fixture.Execution.Epoch,
            fixture.CustodyReport(), Now);
        await disabled.Should().ThrowAsync<AdminWithdrawalExecutionDisabledException>();

        var enabled = new Fixture();
        enabled.AddFee(10, Now.AddDays(-130));
        var enabledRun = enabled.ReserveAndApprove();
        enabled.EvidenceVerifier.IsValid = false;
        var invalid = async () => await enabled.Coordinator.DispatchAsync(
            enabledRun.Id, enabledRun.Version, enabledRun.FencingToken, enabled.Execution.Epoch,
            enabled.CustodyReport(), Now);
        await invalid.Should().ThrowAsync<AdminWithdrawalEvidenceException>();
        enabled.Store.Get(enabledRun.Id).State.Should().Be(AdminWithdrawalRunState.Ambiguous);
    }

    private sealed class Fixture
    {
        private const string AssetKey = "stripe:platform:cash";
        private long _sequence = 1;

        internal Fixture(
            long hardBackingUsdMinor = 300,
            long requiredHardReserveUsdMinor = 100,
            bool executionEnabled = true)
        {
            var proposal = new ReserveProposal(
                new ReserveVersion(1), null, new PolicyVersion(1), 1,
                Now.AddMinutes(-1), Now.AddMinutes(5),
                new ReserveLiabilityPosition(0, 0, 0, 0),
                new ReserveBufferPosition(0, 0, requiredHardReserveUsdMinor, 0, 0, 0, 0),
                [],
                [new ExternalReserveAsset(AssetKey, ReserveBackingPurpose.HardCoin,
                    checked(hardBackingUsdMinor * 10_000_000))],
                "reserve");
            Authority.ValidateAndActivate(proposal, Now);
            TreasuryGate = new TreasuryOperationGate(Authority, CustodySigner);
            Execution = new AdminWithdrawalExecutionGate(executionEnabled);
            Coordinator = new AdminWithdrawalCoordinator(
                Ledger, Store, Fences, TreasuryGate, Authority, Provider,
                EvidenceVerifier, Audit, Execution);
        }

        internal InMemoryLedgerKernelStore Ledger { get; } = new();
        internal InMemoryAdminWithdrawalStore Store { get; } = new();
        internal RootReversalFenceRegistry Fences { get; } = new();
        internal CoreReserveAuthority Authority { get; } = new();
        internal TreasuryCustodySigner CustodySigner { get; } =
            new(Enumerable.Repeat((byte)23, 32).ToArray());
        internal TreasuryOperationGate TreasuryGate { get; }
        internal FakeProvider Provider { get; } = new();
        internal FakeEvidenceVerifier EvidenceVerifier { get; } = new();
        internal AdminWithdrawalAuditTrail Audit { get; } = new();
        internal AdminWithdrawalExecutionGate Execution { get; }
        internal AdminWithdrawalCoordinator Coordinator { get; }
        internal WalletId PlatformFeeWalletId { get; } = WalletId.New();
        internal Guid RequestedBy { get; } = Guid.NewGuid();
        internal Guid ApprovedBy { get; } = Guid.NewGuid();
        internal Guid TenantId { get; } = Guid.NewGuid();

        internal AdminWithdrawalReservationRequest Request() => new(
            Guid.NewGuid(), TenantId, new IdempotencyKey("withdraw-2026-08"), RequestedBy,
            PlatformFeeWalletId, new DateOnly(2026, 8, 1), new PolicyVersion(1),
            new ReserveVersion(1), 1, AssetKey, "company-bank", Now);

        internal CreditLot AddFee(long units, DateTimeOffset confirmedAt)
        {
            var sourceId = SourceStampId.New();
            var source = SourceEvidence.Observe(sourceId, "marketplace", Guid.NewGuid().ToString("N"), "fee", confirmedAt)
                .Confirm(confirmedAt);
            var lot = new CreditLot(
                CreditLotId.New(), PlatformFeeWalletId, new CoinAmount(CurrencyCode.HardCoin, units),
                ProvenanceKind.EarnedHard, confirmedAt, confirmedAt.AddDays(120), _sequence++,
                CreditLotState.Active,
                [new RootTraceRange(sourceId, 0, units * CurrencyTraceScale.HardCoinTraceUnitsPerCoin, 0)],
                CurrencyTraceScale.HardCoinTraceUnitsPerCoin);
            Ledger.Execute(tx => { tx.AddSource(source); tx.AddCreditLot(lot); return 0; });
            return lot;
        }

        internal void PlaceHold(long units) => Ledger.Execute(tx =>
        {
            tx.PlaceHold(HoldId.New(), PlatformFeeWalletId,
                new CoinAmount(CurrencyCode.HardCoin, units), HoldReason.RiskReview, Now);
            return 0;
        });

        internal AdminWithdrawalRun ReserveAndApprove()
        {
            var reserved = Coordinator.ReserveMonthlyRun(Request());
            return Coordinator.Approve(reserved.Id, reserved.Version, ApprovedBy, Now);
        }

        internal TreasuryCustodyReport CustodyReport()
        {
            var head = Authority.ActiveHead!;
            return new TreasuryCustodyReconciler(CustodySigner).Reconcile(
                head,
                head.AssetAllocations.Select(asset => new TreasuryCustodyObservation(
                    asset.AssetKey, asset.EligibleUsdNanos, 0,
                    Now.AddMinutes(-1), Now.AddMinutes(5), "custody")).ToArray(),
                Now);
        }
    }

    private sealed class FakeProvider : IAdminWithdrawalProvider
    {
        internal int DispatchCalls { get; private set; }
        internal bool TimeoutOnDispatch { get; set; }
        internal AdminWithdrawalProviderOutcome DispatchOutcome { get; set; } =
            AdminWithdrawalProviderOutcome.Submitted;
        internal AdminWithdrawalProviderOutcome ReconcileOutcome { get; set; } =
            AdminWithdrawalProviderOutcome.Succeeded;
        internal Func<AdminWithdrawalDispatchCommand, AdminWithdrawalProviderReceipt>? ReceiptFactory { get; set; }
        internal Func<Guid, string, string?, AdminWithdrawalProviderEvent>? EventFactory { get; set; }

        public ValueTask<AdminWithdrawalProviderReceipt> DispatchAsync(
            AdminWithdrawalDispatchCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DispatchCalls++;
            if (TimeoutOnDispatch) throw new TimeoutException("provider timeout");
            if (ReceiptFactory is not null) return ValueTask.FromResult(ReceiptFactory(command));
            return ValueTask.FromResult(new AdminWithdrawalProviderReceipt(
                command.RunId, command.TenantId, DispatchOutcome, "transfer-1", command.FencingToken,
                command.ExecutionEpoch, command.Amount, command.SourceAssetKey,
                command.DestinationHash, "receipt", "signature", command.RequestedAt));
        }

        public ValueTask<AdminWithdrawalProviderEvent> ReconcileAsync(
            Guid tenantId,
            Guid runId,
            string idempotencyKey,
            string? providerTransferId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (EventFactory is not null)
                return ValueTask.FromResult(EventFactory(runId, idempotencyKey, providerTransferId));
            return ValueTask.FromResult(new AdminWithdrawalProviderEvent(
                "event-1", runId, tenantId, ReconcileOutcome, providerTransferId ?? "transfer-1",
                1, 1, new CoinAmount(CurrencyCode.HardCoin, 50),
                "stripe:platform:cash", "company-bank", "event", "signature", Now.AddMinutes(1)));
        }
    }

    private sealed class FakeEvidenceVerifier : IAdminWithdrawalProviderEvidenceVerifier
    {
        internal bool IsValid { get; set; } = true;
        public bool Verify(AdminWithdrawalProviderReceipt receipt) => IsValid;
        public bool Verify(AdminWithdrawalProviderEvent providerEvent) => IsValid;
    }
}
