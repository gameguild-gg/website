using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class TreasuryCoverageTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AssetEvidenceRejectsEveryInvalidDimension()
    {
        var valid = TreasuryProviderSnapshots.StripeReceivable(
            "account", "object", ReserveBackingPurpose.SoftCoin, 1_000,
            TreasurySettlementFinality.Final, 100_000, Now.AddMinutes(-1), Now.AddMinutes(1), "evidence");
        ExternalAssetObservation[] invalid =
        [
            null!,
            valid with { Provider = " " },
            valid with { AccountOrNetwork = " " },
            valid with { ProviderObjectId = " " },
            valid with { EvidenceHash = " " },
            valid with { Kind = (TreasuryAssetKind)99 },
            valid with { Purpose = (ReserveBackingPurpose)99 },
            valid with { Finality = (TreasurySettlementFinality)99 },
            valid with { GrossUsdNanos = 0 },
            valid with { HaircutPpm = 1_000_000 },
            valid with { ObservedAt = Now.AddSeconds(1) },
            valid with { ExpiresAt = Now }
        ];

        foreach (var observation in invalid)
            ((Action)(() => TreasuryAssetAllocator.Allocate([observation], Now)))
                .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => TreasuryAssetAllocator.Allocate(null!, Now))).Should().Throw<ArgumentNullException>();
    }


    [Fact]
    public void BufferPolicyCoversRoundingAndEveryInvalidInput()
    {
        var basePolicy = Policy(new TreasuryBufferRule(0, 1));
        var liabilities = new ReserveLiabilityPosition(1, 1, 1, 0);
        var zero = new TreasuryBufferExposure(0, 0, 0, 0, 0, 0, 0);
        basePolicy.Calculate(liabilities, zero, Now).ChargebackRefundBufferUsdMinor.Should().Be(1);

        TreasuryBufferPolicy[] invalidPolicies =
        [
            basePolicy with { ObservedAt = Now.AddSeconds(1) },
            basePolicy with { ExpiresAt = Now },
            basePolicy with { Owner = " " },
            Policy(null!),
            Policy(new TreasuryBufferRule(-1, 0)),
            Policy(new TreasuryBufferRule(0, -1)),
            Policy(new TreasuryBufferRule(0, 1_000_000))
        ];
        foreach (var policy in invalidPolicies)
            ((Action)(() => policy.Calculate(liabilities, zero, Now)))
                .Should().Throw<ReserveInputUnknownException>();

        var negativeExposure = zero with { ChargebackRefundUsdMinor = -1 };
        ((Action)(() => Policy(new TreasuryBufferRule(0, 0)).Calculate(liabilities, negativeExposure, Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => basePolicy.Calculate(null!, zero, Now))).Should().Throw<ArgumentNullException>();
        ((Action)(() => basePolicy.Calculate(liabilities, null!, Now))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LiabilityRejectsInvalidAuthorizationAndServiceEvidence()
    {
        var store = StoreWithLot(CreateLot(WalletId.New(), CurrencyCode.SoftCoin, 10, CreditLotState.Active, 1));
        var cost = Cost("ai");
        TreasuryOpenServiceAuthorization[] invalid =
        [
            null!,
            new TreasuryOpenServiceAuthorization(" ", "ai", 0, 0),
            new TreasuryOpenServiceAuthorization("key", " ", 0, 0),
            new TreasuryOpenServiceAuthorization("key", "ai", -1, 0),
            new TreasuryOpenServiceAuthorization("key", "ai", 0, -1)
        ];
        foreach (var authorization in invalid)
            ((Action)(() => TreasuryLiabilityCalculator.Calculate(
                    store, new HashSet<WalletId>(), [cost], [authorization], Now)))
                .Should().Throw<ReserveInputUnknownException>();

        ((Action)(() => TreasuryLiabilityCalculator.Calculate(
                store, new HashSet<WalletId>(), [null!], [], Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => TreasuryLiabilityCalculator.Calculate(
                store, new HashSet<WalletId>(), [cost],
                [new TreasuryOpenServiceAuthorization("key", "missing", 1, 0)], Now)))
            .Should().Throw<ReserveInputUnknownException>();

        var nullCode = cost with { ServiceCode = null! };
        TreasuryLiabilityCalculator.Calculate(store, new HashSet<WalletId>(), [nullCode], [], Now)
            .Services.Single().ServiceCode.Should().BeEmpty();
    }

    [Fact]
    public void LiabilityIgnoresTerminalLotsAndUsesUnionOfConsumedRanges()
    {
        var wallet = WalletId.New();
        var root = SourceStampId.New();
        var active = new CreditLot(
            CreditLotId.New(), wallet, new CoinAmount(CurrencyCode.SoftCoin, 2_000), ProvenanceKind.AdRewardSoft,
            Now.AddDays(-1), Now, 1, CreditLotState.Active,
            [new RootTraceRange(root, 1_000, 2_000, 0)]);
        var consumed = CreateLot(wallet, CurrencyCode.SoftCoin, 10, CreditLotState.Consumed, 2);
        var reversed = CreateLot(wallet, CurrencyCode.SoftCoin, 10, CreditLotState.Reversed, 3);
        var store = new InMemoryLedgerKernelStore();
        store.Execute(tx =>
        {
            tx.AddCreditLot(active);
            tx.AddCreditLot(consumed);
            tx.AddCreditLot(reversed);
            tx.AddConsumption(new FragmentConsumption(PostingId.New(), CreditLotId.New(), new CoinAmount(CurrencyCode.SoftCoin, 1), [new RootTraceRange(root, 1_000, 10, 0)]));
            tx.AddConsumption(new FragmentConsumption(PostingId.New(), active.Id, new CoinAmount(CurrencyCode.SoftCoin, 1), [new RootTraceRange(SourceStampId.New(), 1_000, 10, 0)]));
            tx.AddConsumption(new FragmentConsumption(PostingId.New(), active.Id, new CoinAmount(CurrencyCode.SoftCoin, 1), [new RootTraceRange(root, 1_000, 10, 1)]));
            tx.AddConsumption(new FragmentConsumption(PostingId.New(), active.Id, new CoinAmount(CurrencyCode.SoftCoin, 1), [new RootTraceRange(root, 0, 500, 0)]));
            tx.AddConsumption(new FragmentConsumption(PostingId.New(), active.Id, new CoinAmount(CurrencyCode.SoftCoin, 1), [new RootTraceRange(root, 3_000, 500, 0)]));
            tx.AddConsumption(new FragmentConsumption(PostingId.New(), active.Id, new CoinAmount(CurrencyCode.SoftCoin, 1_000), [new RootTraceRange(root, 1_000, 1_000, 0)]));
            tx.AddConsumption(new FragmentConsumption(PostingId.New(), active.Id, new CoinAmount(CurrencyCode.SoftCoin, 500), [new RootTraceRange(root, 1_000, 500, 0)]));
            return 0;
        });

        var result = TreasuryLiabilityCalculator.Calculate(store, new HashSet<WalletId>(), [Cost("ai")], [], Now);
        result.Position.OutstandingSoftUnits.Should().Be(1_000);
        result.Lots.Should().ContainSingle();
    }

    [Fact]
    public void LiabilityRejectsFractionalTraceAndArithmeticOverflow()
    {
        var wallet = WalletId.New();
        var hard = CreateLot(wallet, CurrencyCode.HardCoin, 1, CreditLotState.Active, 1);
        var fractional = StoreWithLot(hard);
        fractional.Execute(tx =>
        {
            tx.AddConsumption(new FragmentConsumption(
                PostingId.New(), hard.Id, new CoinAmount(CurrencyCode.HardCoin, 0),
                [new RootTraceRange(hard.Ranges[0].Root, 0, 1, 0)]));
            return 0;
        });
        ((Action)(() => TreasuryLiabilityCalculator.Calculate(
                fractional, new HashSet<WalletId>(), [], [], Now)))
            .Should().Throw<ReserveInputUnknownException>();

        var overflowLots = new InMemoryLedgerKernelStore();
        overflowLots.Execute(tx =>
        {
            tx.AddCreditLot(CreateLot(wallet, CurrencyCode.SoftCoin, long.MaxValue, CreditLotState.Active, 1));
            tx.AddCreditLot(CreateLot(wallet, CurrencyCode.SoftCoin, long.MaxValue, CreditLotState.Active, 2));
            return 0;
        });
        ((Action)(() => TreasuryLiabilityCalculator.Calculate(
                overflowLots, new HashSet<WalletId>(), [], [], Now)))
            .Should().Throw<OverflowException>();

        var irreversible = StoreWithLot(CreateLot(wallet, CurrencyCode.SoftCoin, 1, CreditLotState.Active, 1));
        ((Action)(() => TreasuryLiabilityCalculator.Calculate(
                irreversible, new HashSet<WalletId>(), [Cost("ai")],
                [
                    new TreasuryOpenServiceAuthorization("one", "ai", 0, long.MaxValue),
                    new TreasuryOpenServiceAuthorization("two", "ai", 0, long.MaxValue)
                ], Now)))
            .Should().Throw<OverflowException>();
    }

    [Fact]
    public void PlannerAndSignerRejectInvalidContracts()
    {
        var signer = new TreasuryProposalSigner(Enumerable.Repeat((byte)1, 32).ToArray());
        var request = Request();
        ((Action)(() => TreasuryReservePlanner.Build(
                request with { PolicyVersion = new PolicyVersion(2) }, signer, Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => TreasuryReservePlanner.Build(null!, signer, Now))).Should().Throw<ArgumentNullException>();
        ((Action)(() => TreasuryReservePlanner.Build(request, null!, Now))).Should().Throw<ArgumentNullException>();
        ((Action)(() => new TreasuryProposalSigner(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => new TreasuryProposalSigner(new byte[31]))).Should().Throw<ArgumentException>();

        var envelope = TreasuryReservePlanner.Build(request, signer, Now);
        signer.Verify(envelope.Proposal, " ").Should().BeFalse();
        signer.Verify(envelope.Proposal, "not-base64").Should().BeFalse();
        signer.Verify(envelope.Proposal, Convert.ToBase64String(new byte[32])).Should().BeFalse();
        ((Action)(() => signer.Sign(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => signer.Verify(null!, envelope.Signature))).Should().Throw<ArgumentNullException>();

        ((Action)(() => new TreasuryCoreActivationGateway(null!, signer))).Should().Throw<ArgumentNullException>();
        ((Action)(() => new TreasuryCoreActivationGateway(new CoreReserveAuthority(), null!))).Should().Throw<ArgumentNullException>();
        var gateway = new TreasuryCoreActivationGateway(new CoreReserveAuthority(), signer);
        ((Action)(() => gateway.Activate(null!, Now))).Should().Throw<ArgumentNullException>();

        var disabled = request with
        {
            Ledger = new InMemoryLedgerKernelStore(),
            ServiceCosts = [Cost("disabled") with { Enabled = false }]
        };
        signer.Verify(TreasuryReservePlanner.Build(disabled, signer, Now).Proposal,
            TreasuryReservePlanner.Build(disabled, signer, Now).Signature).Should().BeTrue();
    }

    [Fact]
    public void CustodyRejectsStaleMissingDuplicateAndInvalidEvidence()
    {
        var head = Head([new ExternalReserveAsset("asset", ReserveBackingPurpose.HardCoin, 10)]);
        ((Action)(() => Reconciler().Reconcile(head with { ObservedAt = Now.AddSeconds(1) }, [], Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => Reconciler().Reconcile(head with { ExpiresAt = Now }, [], Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => Reconciler().Reconcile(head, [], Now)))
            .Should().Throw<ReserveInputUnknownException>();

        var valid = Custody("asset", 10, Now.AddMinutes(1));
        TreasuryCustodyObservation[] invalid =
        [
            null!,
            valid with { AssetKey = " " },
            valid with { EvidenceHash = " " },
            valid with { ActualUsdNanos = -1 },
            valid with { ObservedAt = Now.AddSeconds(1) },
            valid with { ExpiresAt = Now }
        ];
        foreach (var observation in invalid)
            ((Action)(() => Reconciler().Reconcile(head, [observation], Now)))
                .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => Reconciler().Reconcile(head, [valid, valid], Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => Reconciler().Reconcile(null!, [], Now))).Should().Throw<ArgumentNullException>();
        ((Action)(() => Reconciler().Reconcile(head, null!, Now))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CustodyHandlesEmptyExtraAndBothExpiryBranches()
    {
        var emptyHead = Head([]);
        var empty = Reconciler().Reconcile(emptyHead, [], Now);
        empty.Variances.Should().BeEmpty();
        empty.ObservedAt.Should().Be(emptyHead.ObservedAt);
        empty.ExpiresAt.Should().Be(emptyHead.ExpiresAt);

        var head = Head([new ExternalReserveAsset("asset", ReserveBackingPurpose.HardCoin, 10)]);
        Reconciler().Reconcile(head, [Custody("asset", 10, Now.AddMinutes(1))], Now)
            .ExpiresAt.Should().Be(Now.AddMinutes(1));
        Reconciler().Reconcile(head, [Custody("asset", 10, Now.AddMinutes(10))], Now)
            .ExpiresAt.Should().Be(head.ExpiresAt);

        var overflow = new[]
        {
            Custody("asset", 10, Now.AddMinutes(1)),
            Custody("extra", long.MaxValue, Now.AddMinutes(1))
        };
        ((Action)(() => Reconciler().Reconcile(head, overflow, Now)))
            .Should().Throw<OverflowException>();

        var two = Head([
            new ExternalReserveAsset("a", ReserveBackingPurpose.HardCoin, 1),
            new ExternalReserveAsset("b", ReserveBackingPurpose.SoftCoin, 1)
        ]);
        var largeNegative = -(long.MaxValue / 2) - 1;
        ((Action)(() => Reconciler().Reconcile(two,
            [
                Custody("a", 1, Now.AddMinutes(1)) with { ExplainedVarianceUsdNanos = largeNegative },
                Custody("b", 1, Now.AddMinutes(1)) with { ExplainedVarianceUsdNanos = largeNegative }
            ], Now))).Should().Throw<OverflowException>();
    }

    [Fact]
    public void OperationGateRejectsEveryInvalidBindingAndCommandShape()
    {
        var authority = new CoreReserveAuthority();
        var custodySigner = CustodySigner();
        var gate = new TreasuryOperationGate(authority, custodySigner);
        var unsignedReport = new TreasuryCustodyReport(
            new ReserveVersion(1), 1, Now.AddMinutes(-1), Now.AddMinutes(1),
            0, 0, 0, 0, [], "hash", string.Empty);
        var report = Sign(unsignedReport, custodySigner);

        ((Action)(() => new TreasuryOperationGate(null!, custodySigner))).Should().Throw<ArgumentNullException>();
        ((Action)(() => new TreasuryOperationGate(authority, null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => gate.Authorize((TreasuryProtectedOperation)99, new ReserveVersion(1), 1, report, null, Now)))
            .Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => gate.Authorize(TreasuryProtectedOperation.Refund, new ReserveVersion(1), 1, null!, null, Now)))
            .Should().Throw<ArgumentNullException>();
        ((Action)(() => gate.Authorize(TreasuryProtectedOperation.Refund, new ReserveVersion(2), 1, report, null, Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => gate.Authorize(TreasuryProtectedOperation.Refund, new ReserveVersion(1), 2, report, null, Now)))
            .Should().Throw<ReserveInputUnknownException>();
        var future = Sign(report with { ObservedAt = Now.AddSeconds(1) }, custodySigner);
        ((Action)(() => gate.Authorize(TreasuryProtectedOperation.Refund, new ReserveVersion(1), 1,
                future, null, Now)))
            .Should().Throw<ReserveInputUnknownException>();
        var expired = Sign(report with { ExpiresAt = Now }, custodySigner);
        ((Action)(() => gate.Authorize(TreasuryProtectedOperation.Refund, new ReserveVersion(1), 1,
                expired, null, Now)))
            .Should().Throw<ReserveInputUnknownException>();
        ((Action)(() => gate.Authorize(TreasuryProtectedOperation.Issuance, new ReserveVersion(1), 1, report, null, Now)))
            .Should().Throw<ArgumentException>();
        ((Action)(() => gate.Authorize(TreasuryProtectedOperation.Issuance, new ReserveVersion(1), 1, report,
                new CoinAmount(CurrencyCode.HardCoin, 0), Now)))
            .Should().Throw<ArgumentException>();
        ((Action)(() => gate.Authorize(TreasuryProtectedOperation.Refund, new ReserveVersion(1), 1, report,
                new CoinAmount(CurrencyCode.HardCoin, 1), Now)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PositionalEvidenceRecordsSupportCompleteAuditSemantics()
    {
        var lot = new TreasuryLotLiability(CreditLotId.New(), WalletId.New(), CurrencyCode.HardCoin, 1, CreditLotState.Active);
        var (lotId, walletId, currency, units, state) = lot;
        lotId.Should().Be(lot.LotId);
        walletId.Should().Be(lot.WalletId);
        currency.Should().Be(lot.Currency);
        units.Should().Be(1);
        state.Should().Be(CreditLotState.Active);
        lot.ToString().Should().Contain(nameof(TreasuryLotLiability));

        var signer = new TreasuryProposalSigner(Enumerable.Repeat((byte)5, 32).ToArray());
        var envelope = TreasuryReservePlanner.Build(Request(), signer, Now);
        var (proposal, calculation, assets, manifest, signature) = envelope;
        proposal.Should().Be(envelope.Proposal);
        calculation.Should().Be(envelope.LiabilityCalculation);
        assets.Should().BeSameAs(envelope.AssetObservations);
        manifest.Should().Be(envelope.EvidenceManifest);
        signature.Should().Be(envelope.Signature);
        envelope.ToString().Should().Contain(nameof(TreasuryProposalEnvelope));

        var report = new TreasuryCustodyReport(new ReserveVersion(1), 1, Now, Now.AddMinutes(1), 1, 1, 0, 0, [], "h", "s");
        var (version, epoch, observed, expires, expected, actual, explained, unexplained, variances, hash, custodySignature) = report;
        version.Should().Be(report.ReserveVersion);
        epoch.Should().Be(1);
        observed.Should().Be(Now);
        expires.Should().Be(Now.AddMinutes(1));
        expected.Should().Be(1);
        actual.Should().Be(1);
        explained.Should().Be(0);
        unexplained.Should().Be(0);
        variances.Should().BeEmpty();
        hash.Should().Be("h");
        custodySignature.Should().Be("s");
        report.ToString().Should().Contain(nameof(TreasuryCustodyReport));
    }

    private static TreasuryCustodySigner CustodySigner() =>
        new(Enumerable.Repeat((byte)19, 32).ToArray());

    private static TreasuryCustodyReconciler Reconciler() => new(CustodySigner());

    private static TreasuryCustodyReport Sign(TreasuryCustodyReport report, TreasuryCustodySigner signer)
    {
        var unsigned = report with { Signature = string.Empty };
        return unsigned with { Signature = signer.Sign(unsigned) };
    }

    private static TreasuryBufferPolicy Policy(TreasuryBufferRule rule) => new(
        new PolicyVersion(1), rule, rule, rule, rule, rule, rule, rule,
        Now.AddMinutes(-1), Now.AddMinutes(1), "finance");

    private static TreasuryServiceCostSnapshot Cost(string code) => new(
        code, 10, 1, 1, 1, true, Now.AddMinutes(-1), Now.AddMinutes(1));

    private static TreasuryProposalRequest Request()
    {
        var ledger = StoreWithLot(CreateLot(WalletId.New(), CurrencyCode.SoftCoin, 10, CreditLotState.Active, 1));
        return new TreasuryProposalRequest(
            new ReserveVersion(1), null, new PolicyVersion(1), 1,
            Now.AddMinutes(-1), Now.AddMinutes(1), ledger, new HashSet<WalletId>(),
            Policy(new TreasuryBufferRule(0, 0)), new TreasuryBufferExposure(0, 0, 0, 0, 0, 0, 0),
            [Cost("ai")], [],
            [TreasuryProviderSnapshots.StripeCash("acct", "asset", ReserveBackingPurpose.SoftCoin, 1_000_000_000,
                Now.AddMinutes(-1), Now.AddMinutes(1), "asset")]);
    }

    private static ReserveHead Head(IReadOnlyList<ExternalReserveAsset> assets) => new(
        new ReserveVersion(1), new PolicyVersion(1), 1, Now.AddMinutes(-1), Now.AddMinutes(5),
        new ReserveRequirementSnapshot(0, 0, 0, 0, 0),
        assets.Where(asset => asset.Purpose == ReserveBackingPurpose.HardCoin).Sum(asset => asset.EligibleUsdNanos),
        assets.Where(asset => asset.Purpose == ReserveBackingPurpose.SoftCoin).Sum(asset => asset.EligibleUsdNanos),
        ReserveCoverageState.Covered, assets, "head");

    private static TreasuryCustodyObservation Custody(string key, long actual, DateTimeOffset expires) =>
        new(key, actual, 0, Now.AddMinutes(-1), expires, "evidence");

    private static InMemoryLedgerKernelStore StoreWithLot(CreditLot lot)
    {
        var store = new InMemoryLedgerKernelStore();
        store.Execute(tx =>
        {
            tx.AddCreditLot(lot);
            return 0;
        });
        return store;
    }

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
            [new RootTraceRange(SourceStampId.New(), 0, checked(units * scale), 0)], scale);
    }

}
