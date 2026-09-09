using System.Text.Json;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class TreasuryReserveTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AssetAllocatorAppliesFinalityHaircutsAndExclusiveAllocation()
    {
        var observations = new[]
        {
            TreasuryProviderSnapshots.StripeCash(
                "acct-live", "cash-1", ReserveBackingPurpose.HardCoin, 2_000_000_000,
                Now.AddMinutes(-2), Now.AddMinutes(10), "stripe-cash"),
            TreasuryProviderSnapshots.StripeReceivable(
                "acct-live", "recv-1", ReserveBackingPurpose.SoftCoin, 1_000_000_000,
                TreasurySettlementFinality.Final, 100_000, Now.AddMinutes(-2), Now.AddMinutes(10), "stripe-recv"),
            TreasuryProviderSnapshots.AdReceivable(
                "admob", "batch-pending", ReserveBackingPurpose.SoftCoin, 900_000_000,
                TreasurySettlementFinality.Pending, 250_000, Now.AddMinutes(-2), Now.AddMinutes(10), "ad-pending"),
            TreasuryProviderSnapshots.AdReceivable(
                "unity", "batch-disputed", ReserveBackingPurpose.SoftCoin, 700_000_000,
                TreasurySettlementFinality.Disputed, 250_000, Now.AddMinutes(-2), Now.AddMinutes(10), "ad-disputed")
        };

        var allocation = TreasuryAssetAllocator.Allocate(observations, Now);

        allocation.Should().BeEquivalentTo(new[]
        {
            new ExternalReserveAsset("stripe:acct-live:cash-1", ReserveBackingPurpose.HardCoin, 2_000_000_000),
            new ExternalReserveAsset("stripe:acct-live:recv-1", ReserveBackingPurpose.SoftCoin, 900_000_000)
        }, options => options.WithStrictOrdering());

        var duplicated = observations[0] with { Purpose = ReserveBackingPurpose.SoftCoin };
        var act = () => TreasuryAssetAllocator.Allocate([observations[0], duplicated], Now);
        act.Should().Throw<DuplicateReserveAssetException>();
    }

    [Fact]
    public void AssetAllocatorRejectsInvalidOrStaleEvidence()
    {
        var settledWithHaircut = TreasuryProviderSnapshots.StripeCash(
            "acct", "cash", ReserveBackingPurpose.HardCoin, 100, Now.AddMinutes(-1), Now.AddMinutes(1), "e")
            with { HaircutPpm = 1 };
        var zeroEligible = TreasuryProviderSnapshots.StripeReceivable(
            "acct", "recv", ReserveBackingPurpose.SoftCoin, 1,
            TreasurySettlementFinality.Final, 999_999, Now.AddMinutes(-1), Now.AddMinutes(1), "e");
        var stale = settledWithHaircut with { HaircutPpm = 0, ExpiresAt = Now };
        var invalid = settledWithHaircut with { HaircutPpm = -1 };

        ((Action)(() => TreasuryAssetAllocator.Allocate([settledWithHaircut], Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => TreasuryAssetAllocator.Allocate([zeroEligible], Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => TreasuryAssetAllocator.Allocate([stale], Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => TreasuryAssetAllocator.Allocate([invalid], Now)))
            .Should().Throw<ReserveInputUnknownException>();
    }

    [Fact]
    public void LiabilityCalculationCountsHeldFrozenReservedAndDisputedFragmentsUntilConsumption()
    {
        var store = new InMemoryLedgerKernelStore();
        var user = WalletId.New();
        var company = WalletId.New();
        var hard = CreateLot(user, CurrencyCode.HardCoin, 10, CreditLotState.Active, 1);
        var soft = CreateLot(user, CurrencyCode.SoftCoin, 100_000, CreditLotState.Held, 2);
        var companyFee = CreateLot(company, CurrencyCode.HardCoin, 50, CreditLotState.Active, 3);

        store.Execute(tx =>
        {
            tx.AddCreditLot(hard);
            tx.AddCreditLot(soft);
            tx.AddCreditLot(companyFee);
            tx.AddConsumption(new FragmentConsumption(
                PostingId.New(), soft.Id, new CoinAmount(CurrencyCode.SoftCoin, 10_000),
                [new RootTraceRange(soft.Ranges[0].Root, 0, 10_000, 0)]));
            tx.AddFragmentReservation(new ValueFragmentReservation(
                Guid.NewGuid(), Guid.NewGuid(), FragmentReservationPurpose.Payout, hard.Id, user,
                new CoinAmount(CurrencyCode.HardCoin, 2),
                [new RootTraceRange(hard.Ranges[0].Root, 0, 2_000, 0)],
                1, 1, 1, FragmentReservationStatus.Reserved, Now, null));
            return 0;
        });

        var result = TreasuryLiabilityCalculator.Calculate(
            store,
            new HashSet<WalletId> { company },
            [new TreasuryServiceCostSnapshot("ai", 10_000, 50_000_000, 60_000_000, 70_000_000, true, Now.AddMinutes(-1), Now.AddMinutes(5))],
            [new TreasuryOpenServiceAuthorization("auth-1", "ai", 20_000, 5_000_000)],
            Now);

        result.Position.Should().Be(new ReserveLiabilityPosition(10, 90_000, 70_000, 5_000_000));
        result.Services.Should().ContainSingle().Which.ReservedSoftUnits.Should().Be(20_000);
        result.Lots.Should().HaveCount(2);
    }

    [Fact]
    public void LiabilityCalculationRejectsDuplicateOrExcessServiceReservations()
    {
        var store = new InMemoryLedgerKernelStore();
        var wallet = WalletId.New();
        store.Execute(tx =>
        {
            tx.AddCreditLot(CreateLot(wallet, CurrencyCode.SoftCoin, 10, CreditLotState.Active, 1));
            return 0;
        });
        var costs = new[]
        {
            new TreasuryServiceCostSnapshot("ai", 10, 1, 1, 1, true, Now.AddMinutes(-1), Now.AddMinutes(1))
        };

        ((Action)(() => TreasuryLiabilityCalculator.Calculate(
                store, new HashSet<WalletId>(), costs,
                [new TreasuryOpenServiceAuthorization("same", "ai", 5, 0), new TreasuryOpenServiceAuthorization("same", "ai", 1, 0)], Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => TreasuryLiabilityCalculator.Calculate(
                store, new HashSet<WalletId>(), costs,
                [new TreasuryOpenServiceAuthorization("one", "ai", 11, 0)], Now)))
            .Should().Throw<ReserveInputUnknownException>();
    }

    [Fact]
    public void BufferPolicyEnforcesObservedAbsoluteAndPercentageFloors()
    {
        var policy = CreateBufferPolicy();
        var liabilities = new ReserveLiabilityPosition(1_000, 100_000, 100_000, 0);
        var observed = new TreasuryBufferExposure(5, 15, 25, 1_000_000, 2_000_000, 3_000_000, 4_000_000);

        var buffers = policy.Calculate(liabilities, observed, Now);

        buffers.Should().Be(new ReserveBufferPosition(
            100, 100, 100,
            100_000_000, 100_000_000, 100_000_000, 100_000_000));
    }

    [Fact]
    public void BufferPolicyRejectsStaleAndInvalidRules()
    {
        var stale = CreateBufferPolicy() with { ExpiresAt = Now };
        var invalid = CreateBufferPolicy() with { ChargebackRefund = new TreasuryBufferRule(-1, 0) };
        var liabilities = new ReserveLiabilityPosition(0, 0, 0, 0);
        var exposure = new TreasuryBufferExposure(0, 0, 0, 0, 0, 0, 0);

        ((Action)(() => stale.Calculate(liabilities, exposure, Now))).Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => invalid.Calculate(liabilities, exposure, Now))).Should().Throw<ReserveInputUnknownException>();
    }

    [Fact]
    public void SignedProposalActivatesOnlyThroughCoreAndRejectsTamperingAndStaleRaces()
    {
        var authority = new CoreReserveAuthority();
        var signer = new TreasuryProposalSigner(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var gateway = new TreasuryCoreActivationGateway(authority, signer);
        var request = CreateProposalRequest(new ReserveVersion(1), null, 1);
        var envelope = TreasuryReservePlanner.Build(request, signer, Now);

        authority.ActiveHead.Should().BeNull();
        var head = gateway.Activate(envelope, Now);
        head.Version.Should().Be(new ReserveVersion(1));
        head.Coverage.Should().Be(ReserveCoverageState.Covered);

        var tampered = envelope with
        {
            Proposal = envelope.Proposal with { EvidenceHash = new string('0', 64) }
        };
        ((Action)(() => gateway.Activate(tampered, Now))).Should().Throw<TreasurySignatureException>();

        var first = TreasuryReservePlanner.Build(
            CreateProposalRequest(new ReserveVersion(2), new ReserveVersion(1), 2), signer, Now);
        var stale = TreasuryReservePlanner.Build(
            CreateProposalRequest(new ReserveVersion(3), new ReserveVersion(1), 3), signer, Now);
        gateway.Activate(first, Now).Version.Should().Be(new ReserveVersion(2));
        ((Action)(() => gateway.Activate(stale, Now))).Should().Throw<ReserveVersionConflictException>();
    }

    [Fact]
    public async Task ConcurrentReserveActivationHasOneWinner()
    {
        var authority = new CoreReserveAuthority();
        var signer = new TreasuryProposalSigner(Enumerable.Repeat((byte)7, 32).ToArray());
        var gateway = new TreasuryCoreActivationGateway(authority, signer);
        gateway.Activate(TreasuryReservePlanner.Build(CreateProposalRequest(new ReserveVersion(1), null, 1), signer, Now), Now);
        var left = TreasuryReservePlanner.Build(CreateProposalRequest(new ReserveVersion(2), new ReserveVersion(1), 2), signer, Now);
        var right = TreasuryReservePlanner.Build(CreateProposalRequest(new ReserveVersion(3), new ReserveVersion(1), 3), signer, Now);

        var outcomes = await Task.WhenAll(
            Task.Run(() => Capture(() => gateway.Activate(left, Now))),
            Task.Run(() => Capture(() => gateway.Activate(right, Now))));

        outcomes.Count(result => result is ReserveHead).Should().Be(1);
        outcomes.Count(result => result is ReserveVersionConflictException).Should().Be(1);
    }

    [Fact]
    public void CustodyReconciliationAndOperationGateFailClosedOnVarianceShortfallAndStaleness()
    {
        var authority = new CoreReserveAuthority();
        var signer = new TreasuryProposalSigner(Enumerable.Repeat((byte)9, 32).ToArray());
        var gateway = new TreasuryCoreActivationGateway(authority, signer);
        var head = gateway.Activate(
            TreasuryReservePlanner.Build(CreateProposalRequest(new ReserveVersion(1), null, 1), signer, Now), Now);
        var observations = head.AssetAllocations.Select(asset => new TreasuryCustodyObservation(
            asset.AssetKey, asset.EligibleUsdNanos, 0, Now.AddMinutes(-1), Now.AddMinutes(5), $"custody:{asset.AssetKey}")).ToArray();
        var custodySigner = CreateCustodySigner();
        var reconciler = new TreasuryCustodyReconciler(custodySigner);
        var report = reconciler.Reconcile(head, observations, Now);
        var gate = new TreasuryOperationGate(authority, custodySigner);

        report.IsReconciled.Should().BeTrue();
        gate.Authorize(TreasuryProtectedOperation.PayoutDispatch, head.Version, head.AuthorizationEpoch, report, null, Now)
            .Version.Should().Be(head.Version);
        gate.Authorize(TreasuryProtectedOperation.Issuance, head.Version, head.AuthorizationEpoch, report,
                new CoinAmount(CurrencyCode.HardCoin, 1), Now)
            .Version.Should().Be(head.Version);

        var variance = reconciler.Reconcile(
            head, [observations[0] with { ActualUsdNanos = observations[0].ActualUsdNanos - 1 }, .. observations.Skip(1)], Now);
        ((Action)(() => gate.Authorize(TreasuryProtectedOperation.Refund, head.Version, head.AuthorizationEpoch, variance, null, Now)))
            .Should().Throw<TreasuryCustodyVarianceException>();
        var unsignedStale = report with { ExpiresAt = Now, Signature = string.Empty };
        var stale = unsignedStale with { Signature = custodySigner.Sign(unsignedStale) };
        ((Action)(() => gate.Authorize(TreasuryProtectedOperation.AdminWithdrawal, head.Version, head.AuthorizationEpoch, stale, null, Now)))
            .Should().Throw<ReserveInputUnknownException>();

        var shortAuthority = new CoreReserveAuthority();
        var shortGateway = new TreasuryCoreActivationGateway(shortAuthority, signer);
        var shortRequest = CreateProposalRequest(new ReserveVersion(1), null, 1) with
        {
            Assets = [TreasuryProviderSnapshots.StripeCash(
                "acct", "insufficient", ReserveBackingPurpose.HardCoin, 1,
                Now.AddMinutes(-1), Now.AddMinutes(5), "small")]
        };
        var shortHead = shortGateway.Activate(TreasuryReservePlanner.Build(shortRequest, signer, Now), Now);
        var shortReport = reconciler.Reconcile(shortHead,
            [new TreasuryCustodyObservation("stripe:acct:insufficient", 1, 0, Now.AddMinutes(-1), Now.AddMinutes(5), "c")], Now);
        ((Action)(() => new TreasuryOperationGate(shortAuthority, custodySigner).Authorize(
                TreasuryProtectedOperation.PayoutDispatch, shortHead.Version, shortHead.AuthorizationEpoch, shortReport, null, Now)))
            .Should().Throw<ReserveShortfallException>();
    }

    [Fact]
    public void AuditExportIsDeterministicAndBindsProposalHeadAndCustodyEvidence()
    {
        var authority = new CoreReserveAuthority();
        var signer = new TreasuryProposalSigner(Enumerable.Repeat((byte)3, 32).ToArray());
        var envelope = TreasuryReservePlanner.Build(CreateProposalRequest(new ReserveVersion(1), null, 1), signer, Now);
        var head = new TreasuryCoreActivationGateway(authority, signer).Activate(envelope, Now);
        var custody = new TreasuryCustodyReconciler(CreateCustodySigner()).Reconcile(head,
            head.AssetAllocations.Select(asset => new TreasuryCustodyObservation(
                asset.AssetKey, asset.EligibleUsdNanos, 0, Now.AddMinutes(-1), Now.AddMinutes(5), "custody")).ToArray(), Now);

        var json = TreasuryAuditExporter.Export(envelope, head, custody);
        var document = JsonDocument.Parse(json);

        document.RootElement.GetProperty("reserveVersion").GetInt64().Should().Be(1);
        document.RootElement.GetProperty("proposalEvidenceHash").GetString().Should().Be(envelope.Proposal.EvidenceHash);
        document.RootElement.GetProperty("custodyEvidenceHash").GetString().Should().Be(custody.EvidenceHash);
        TreasuryAuditExporter.Export(envelope, head, custody).Should().Be(json);
    }

    private static TreasuryProposalRequest CreateProposalRequest(
        ReserveVersion version,
        ReserveVersion? expected,
        long epoch)
    {
        var store = new InMemoryLedgerKernelStore();
        var wallet = WalletId.New();
        store.Execute(tx =>
        {
            tx.AddCreditLot(CreateLot(wallet, CurrencyCode.HardCoin, 100, CreditLotState.Active, 1));
            tx.AddCreditLot(CreateLot(wallet, CurrencyCode.SoftCoin, 100_000, CreditLotState.Active, 2));
            return 0;
        });
        return new TreasuryProposalRequest(
            version,
            expected,
            new PolicyVersion(1),
            epoch,
            Now.AddMinutes(-1),
            Now.AddMinutes(5),
            store,
            new HashSet<WalletId>(),
            CreateBufferPolicy(),
            new TreasuryBufferExposure(0, 0, 0, 0, 0, 0, 0),
            [new TreasuryServiceCostSnapshot("ai", 100_000, 100_000_000, 100_000_000, 100_000_000, true, Now.AddMinutes(-1), Now.AddMinutes(5))],
            [],
            [
                TreasuryProviderSnapshots.StripeCash(
                    "acct", "hard", ReserveBackingPurpose.HardCoin, 5_000_000_000,
                    Now.AddMinutes(-1), Now.AddMinutes(5), "hard"),
                TreasuryProviderSnapshots.StripeCash(
                    "acct", "soft", ReserveBackingPurpose.SoftCoin, 3_000_000_000,
                    Now.AddMinutes(-1), Now.AddMinutes(5), "soft")
            ]);
    }

    private static TreasuryBufferPolicy CreateBufferPolicy()
    {
        var rule = new TreasuryBufferRule(100, 100_000);
        var nanosRule = new TreasuryBufferRule(10_000_000, 100_000);
        return new TreasuryBufferPolicy(
            new PolicyVersion(1),
            rule, rule, rule,
            nanosRule, nanosRule, nanosRule, nanosRule,
            Now.AddMinutes(-1), Now.AddMinutes(5), "finance");
    }

    private static TreasuryCustodySigner CreateCustodySigner() =>
        new(Enumerable.Repeat((byte)17, 32).ToArray());

    private static CreditLot CreateLot(
        WalletId wallet,
        CurrencyCode currency,
        long units,
        CreditLotState state,
        long sequence)
    {
        var scale = CurrencyTraceScale.For(currency);
        return new CreditLot(
            CreditLotId.New(), wallet, new CoinAmount(currency, units),
            currency == CurrencyCode.HardCoin ? ProvenanceKind.EarnedHard : ProvenanceKind.AdRewardSoft,
            Now.AddDays(-130), Now.AddDays(-10), sequence, state,
            [new RootTraceRange(SourceStampId.New(), 0, units * scale, 0)], scale);
    }

    private static object Capture(Func<ReserveHead> operation)
    {
        try { return operation(); }
        catch (Exception exception) { return exception; }
    }
}
