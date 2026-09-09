using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class ProviderReversalServiceTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

    [Fact]
    public void ReverseTopUp_TraversesConvertedSoftAndHardDescendantsAtExactRootParity()
    {
        var fixture = Setup(10);
        fixture.Service.ConvertHardToSoft(Convert(fixture, 4));

        var reversal = fixture.Service.ReverseTopUp(Reverse(
            fixture.SourceId,
            cumulativeHardUnits: 5,
            ProviderReversalDisposition.ResponsibleDebt));

        reversal.State.AuthoritativeHardUnits.Should().Be(10);
        reversal.State.CumulativeProviderHardUnits.Should().Be(5);
        reversal.State.RecoveredConvertedSoftUnits.Should().Be(4_000);
        reversal.State.RecoveredHardUnits.Should().Be(1);
        reversal.State.ResponsibleDebtHardUnits.Should().Be(0);
        reversal.State.PlatformLossHardUnits.Should().Be(0);
        reversal.State.ReversedRanges.Sum(range => range.Length).Should().Be(5_000);
        fixture.Store.GetAvailableLots(fixture.WalletId, CurrencyCode.SoftCoin).Should().BeEmpty();
        fixture.Store.GetAvailableLots(fixture.WalletId, CurrencyCode.HardCoin)
            .Should().ContainSingle().Which.Amount.Units.Should().Be(5);
        fixture.Store.FundingClaims.Single().State.Should().Be(SourceConfirmationState.Disputed);
    }

    [Fact]
    public void ReverseTopUp_ExtendsCumulativeRefundWithoutReversingAnyRangeTwice()
    {
        var fixture = Setup(10);
        fixture.Service.ReverseTopUp(Reverse(fixture.SourceId, 3, ProviderReversalDisposition.ResponsibleDebt));

        var extended = fixture.Service.ReverseTopUp(Reverse(
            fixture.SourceId, 8, ProviderReversalDisposition.ResponsibleDebt));
        var full = fixture.Service.ReverseTopUp(Reverse(
            fixture.SourceId, 10, ProviderReversalDisposition.ResponsibleDebt));

        extended.State.CumulativeProviderHardUnits.Should().Be(8);
        full.State.CumulativeProviderHardUnits.Should().Be(10);
        full.State.ReversedRanges.Should().HaveCount(3);
        full.State.ReversedRanges.Sum(range => range.Length).Should().Be(10_000);
        full.State.ReversedRanges.Should().OnlyHaveUniqueItems();
        fixture.Store.FundingClaims.Single().State.Should().Be(SourceConfirmationState.Reversed);
        fixture.Store.CreditLots.Should().ContainSingle();
    }

    [Theory]
    [InlineData(ProviderReversalDisposition.ResponsibleDebt, 1, 0)]
    [InlineData(ProviderReversalDisposition.PlatformLoss, 0, 1)]
    public void ReverseTopUp_ExactlyPartitionsIrrecoverableFeeConsumption(
        ProviderReversalDisposition disposition,
        long expectedDebt,
        long expectedLoss)
    {
        var fixture = Setup(10);
        fixture.Service.ConvertHardToSoft(Convert(fixture, 4, fee: 1));

        var reversal = fixture.Service.ReverseTopUp(Reverse(fixture.SourceId, 10, disposition));

        reversal.State.RecoveredHardUnits.Should().Be(5);
        reversal.State.RecoveredConvertedSoftUnits.Should().Be(4_000);
        reversal.State.ResponsibleDebtHardUnits.Should().Be(expectedDebt);
        reversal.State.PlatformLossHardUnits.Should().Be(expectedLoss);
        reversal.State.PartitionedHardEquivalentUnits.Should().Be(10);
        reversal.Postings.SelectMany(posting => posting.Lines)
            .Should().Contain(line => line.JournalLineId != Guid.Empty);
    }

    [Fact]
    public void ReverseTopUp_DuplicateWebhookIsIdempotentAndCannotExceedConfirmedProviderTotal()
    {
        var fixture = Setup(10);
        var command = Reverse(fixture.SourceId, 5, ProviderReversalDisposition.ResponsibleDebt);

        var first = fixture.Service.ReverseTopUp(command);
        var duplicate = fixture.Service.ReverseTopUp(command);

        duplicate.Should().BeEquivalentTo(first);
        fixture.Store.ProviderReversalStates.Should().ContainSingle();
        FluentActions.Invoking(() => fixture.Service.ReverseTopUp(
                Reverse(fixture.SourceId, 11, ProviderReversalDisposition.ResponsibleDebt)))
            .Should().Throw<ProviderMonetaryTotalExceededException>();
        fixture.Store.ProviderReversalStates.Single().CumulativeProviderHardUnits.Should().Be(5);
    }

    [Fact]
    public void ReverseTopUp_DoesNotTouchUnrelatedRoot()
    {
        var first = Setup(5);
        var unrelated = first.Service.ObserveTopUp(Observe(first.WalletId, 7));
        first.Service.ConfirmObservedTopUp(Confirm(unrelated));

        first.Service.ReverseTopUp(Reverse(
            first.SourceId, 5, ProviderReversalDisposition.ResponsibleDebt));

        first.Store.GetAvailableLots(first.WalletId, CurrencyCode.HardCoin)
            .Should().ContainSingle().Which.Ranges.Should().OnlyContain(range => range.Root == unrelated.SourceId);
    }

    [Fact]
    public void Planner_RejectsForeignHistoryRegressionAndParityFractions()
    {
        var root = SourceStampId.New();
        var foreign = SourceStampId.New();

        FluentActions.Invoking(() => ProviderReversalPlanner.Plan(
                root, 1_000, [new RootTraceRange(foreign, 0, 1_000, 0)], []))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => ProviderReversalPlanner.Plan(
                root, 999, [new RootTraceRange(root, 0, 1_000, 0)], []))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ProviderReversalPlanner.Plan(root, 1, [], []))
            .Should().Throw<UnrecoverableParityFractionException>();
    }

    [Fact]
    public void Planner_SubtractsPreviouslyReversedInteriorAndCoveringRanges()
    {
        var root = SourceStampId.New();
        var tail = Lot(root, new RootTraceRange(root, 0, 2_000, 0));
        var tailPlan = ProviderReversalPlanner.Plan(
            root, 2_000, [new RootTraceRange(root, 0, 1_000, 0)], [tail]);

        tailPlan.Fragments.Should().ContainSingle();
        tailPlan.Fragments[0].Ranges.Should().Equal(new RootTraceRange(root, 1_000, 1_000, 0));

        var prefix = SoftLot(root, new RootTraceRange(root, 0, 1_000, 0));
        var prefixPlan = ProviderReversalPlanner.Plan(
            root, 1_500, [new RootTraceRange(root, 500, 1_000, 0)], [prefix]);

        prefixPlan.Fragments.Should().ContainSingle();
        prefixPlan.Fragments[0].Ranges.Should().Equal(new RootTraceRange(root, 0, 500, 0));
    }

    [Fact]
    public void PlannerStopsScanningRemainingLotRangesAfterTargetIsSatisfied()
    {
        var root = SourceStampId.New();
        var first = new RootTraceRange(root, 0, 1_000, 0);
        var later = new RootTraceRange(root, 2_000, 1_000, 0);
        var lot = new CreditLot(
            CreditLotId.New(),
            WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, 2),
            ProvenanceKind.PurchasedHard,
            Time,
            Time.AddDays(120),
            1,
            CreditLotState.Active,
            [first, later],
            CurrencyTraceScale.HardCoinTraceUnitsPerCoin);

        var plan = ProviderReversalPlanner.Plan(root, 1_000, [], [lot]);

        plan.Fragments.Should().ContainSingle();
        plan.Fragments[0].Ranges.Should().Equal(first);
    }

    [Fact]
    public void PlannerStopsScanningRemainingLotsAfterTargetIsSatisfied()
    {
        var root = SourceStampId.New();
        var first = Lot(root, new RootTraceRange(root, 0, 1_000, 0));
        var later = Lot(root, new RootTraceRange(root, 1_000, 1_000, 0));

        var plan = ProviderReversalPlanner.Plan(root, 1_000, [], [first, later]);

        plan.Fragments.Should().ContainSingle()
            .Which.Lot.Should().BeSameAs(first);
    }

    [Fact]
    public void ReverseTopUp_RejectsPendingClaimWithoutMutatingFundingState()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var pending = service.ObserveTopUp(Observe(WalletId.New(), 2));
        var before = store.SnapshotCounts();

        FluentActions.Invoking(() => service.ReverseTopUp(
                Reverse(pending.SourceId, 1, ProviderReversalDisposition.ResponsibleDebt)))
            .Should().Throw<InvalidFundingStateTransitionException>();

        store.SnapshotCounts().Should().Be(before);
        store.FundingClaims.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Observed);
    }

    [Fact]
    public void ReverseTopUp_RejectsNonIncreasingCumulativeProviderTotal()
    {
        var fixture = Setup(10);
        fixture.Service.ReverseTopUp(
            Reverse(fixture.SourceId, 5, ProviderReversalDisposition.ResponsibleDebt));
        var before = fixture.Store.SnapshotCounts();

        FluentActions.Invoking(() => fixture.Service.ReverseTopUp(
                Reverse(fixture.SourceId, 5, ProviderReversalDisposition.ResponsibleDebt)))
            .Should().Throw<ProviderMonetaryTotalExceededException>().WithMessage("*monotonically*");

        fixture.Store.SnapshotCounts().Should().Be(before);
        fixture.Store.ProviderReversalStates.Should().ContainSingle()
            .Which.CumulativeProviderHardUnits.Should().Be(5);
    }

    [Fact]
    public void ReverseTopUp_IdempotencyRecordWithoutAtomicResultFailsClosed()
    {
        var fixture = Setup(2);
        var command = Reverse(fixture.SourceId, 1, ProviderReversalDisposition.ResponsibleDebt);
        var hashMethod = typeof(TransactionalPostingService).GetMethod(
            "ComputeProviderReversalHash",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var hash = (string)hashMethod!.Invoke(null, new object?[] { command })!;
        var committedPosting = fixture.Store.IdempotencyRecords.Single().Result;
        fixture.Store.Execute(transaction =>
        {
            transaction.AddIdempotency(new IdempotencyRecord(command.IdempotencyKey, hash, committedPosting));
            return true;
        });
        var before = fixture.Store.SnapshotCounts();

        FluentActions.Invoking(() => fixture.Service.ReverseTopUp(command))
            .Should().Throw<InvalidOperationException>().WithMessage("*not committed atomically*");

        fixture.Store.SnapshotCounts().Should().Be(before);
        fixture.Store.ProviderReversalStates.Should().BeEmpty();
    }

    [Fact]
    public void ReverseTopUp_ConfirmedClaimWithoutSourceEvidenceFailsClosed()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var wallet = WalletId.New();
        var (claim, evidence) = ConfirmedClaim(wallet, 2);
        var rootLot = ConfirmedCreditFactory.CreateRootLot(
            CreditLotId.New(), wallet, claim.Amount, ProvenanceKind.PurchasedHard, evidence, 1);
        store.Execute(transaction =>
        {
            transaction.AddFundingClaim(claim);
            transaction.AddCreditLot(rootLot);
            return true;
        });
        var before = store.SnapshotCounts();

        FluentActions.Invoking(() => service.ReverseTopUp(
                Reverse(claim.SourceId, 1, ProviderReversalDisposition.ResponsibleDebt)))
            .Should().Throw<InvalidOperationException>().WithMessage("*Confirmed source evidence*");

        store.SnapshotCounts().Should().Be(before);
        store.FundingClaims.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Confirmed);
        store.SourceEvidenceHistory.Should().BeEmpty();
    }

    [Fact]
    public void ReverseTopUp_RejectsConvertedSoftParityFractionsAndRollsBack()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var wallet = WalletId.New();
        var (claim, evidence) = ConfirmedClaim(wallet, 1);
        var confirmedAt = evidence.ConfirmedAt!.Value;
        store.Execute(transaction =>
        {
            transaction.AddFundingClaim(claim);
            transaction.AddSource(evidence);
            transaction.AddCreditLot(new CreditLot(
                CreditLotId.New(),
                wallet,
                new CoinAmount(CurrencyCode.SoftCoin, 999),
                ProvenanceKind.ConvertedSoft,
                confirmedAt,
                confirmedAt,
                1,
                CreditLotState.Active,
                [new RootTraceRange(claim.SourceId, 0, 999, 0)],
                CurrencyTraceScale.SoftCoinTraceUnitsPerCoin));
            transaction.AddCreditLot(new CreditLot(
                CreditLotId.New(),
                wallet,
                new CoinAmount(CurrencyCode.SoftCoin, 1),
                ProvenanceKind.ConvertedSoft,
                confirmedAt,
                confirmedAt,
                2,
                CreditLotState.Active,
                [new RootTraceRange(claim.SourceId, 999, 1, 0)],
                CurrencyTraceScale.SoftCoinTraceUnitsPerCoin));
            return true;
        });
        var before = store.SnapshotCounts();

        FluentActions.Invoking(() => service.ReverseTopUp(
                Reverse(claim.SourceId, 1, ProviderReversalDisposition.ResponsibleDebt)))
            .Should().Throw<UnrecoverableParityFractionException>();

        store.SnapshotCounts().Should().Be(before);
        store.FundingClaims.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Confirmed);
        store.SourceEvidenceHistory.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Confirmed);
    }

    [Fact]
    public void ReverseTopUp_RejectsAStoredPartitionThatCannotConserveTheProviderTotal()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var wallet = WalletId.New();
        var (claim, evidence) = ConfirmedClaim(wallet, 2);
        var rootLot = ConfirmedCreditFactory.CreateRootLot(
            CreditLotId.New(), wallet, claim.Amount, ProvenanceKind.PurchasedHard, evidence, 1);
        var inconsistentState = new ProviderReversalState(
            claim.SourceId,
            2,
            1,
            0,
            0,
            0,
            0,
            [new RootTraceRange(claim.SourceId, 0, 1_000, 0)]);
        store.Execute(transaction =>
        {
            transaction.AddFundingClaim(claim);
            transaction.AddSource(evidence);
            transaction.AddCreditLot(rootLot);
            transaction.SetProviderReversalState(inconsistentState);
            return true;
        });
        var before = store.SnapshotCounts();

        FluentActions.Invoking(() => service.ReverseTopUp(
                Reverse(claim.SourceId, 2, ProviderReversalDisposition.ResponsibleDebt)))
            .Should().Throw<LineageConservationException>();

        store.SnapshotCounts().Should().Be(before);
        store.ProviderReversalStates.Should().ContainSingle()
            .Which.CumulativeProviderHardUnits.Should().Be(1);
        store.JournalEntries.Should().BeEmpty();
    }

    [Fact]
    public void ReverseTopUp_RejectsUnknownDisposition()
    {
        var fixture = Setup(10);

        FluentActions.Invoking(() => fixture.Service.ReverseTopUp(
                Reverse(fixture.SourceId, 1, (ProviderReversalDisposition)99)))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    private static (HardCoinFundingClaim Claim, SourceEvidence Evidence) ConfirmedClaim(
        WalletId wallet,
        long hardUnits)
    {
        var command = Observe(wallet, hardUnits);
        var confirmedAt = Time.AddMinutes(1);
        var claim = HardCoinFundingClaim.Observe(
                command.SourceId,
                command.WalletId,
                command.ProviderLeg,
                command.Evidence,
                command.AuthoritativeUsdMinorUnits,
                command.ObservedAt)
            .Transition(SourceConfirmationState.Confirmed, "provider-confirmation", confirmedAt);
        var evidence = SourceEvidence.Observe(
                command.SourceId,
                command.ProviderLeg.Provider,
                command.ProviderLeg.Key,
                command.Evidence,
                command.ObservedAt)
            .Confirm(confirmedAt);
        return (claim, evidence);
    }

    private static Fixture Setup(long hardUnits)
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var wallet = WalletId.New();
        var claim = service.ObserveTopUp(Observe(wallet, hardUnits));
        service.ConfirmObservedTopUp(Confirm(claim));
        return new Fixture(store, service, wallet, claim.SourceId);
    }

    private static ObserveHardCoinTopUpCommand Observe(WalletId wallet, long units) => new(
        SourceStampId.New(),
        wallet,
        new ProviderMonetaryLeg("stripe", "live", "acct_gameguild", $"pi_{Guid.NewGuid():N}", "capture"),
        "provider-observation",
        units,
        Time);

    private static ConfirmObservedTopUpCommand Confirm(HardCoinFundingClaim claim)
    {
        var key = new IdempotencyKey($"confirm-{claim.SourceId.Value:N}");
        var confirmedAt = Time.AddMinutes(1);
        return new ConfirmObservedTopUpCommand(
            PostingId.New(), key, claim.SourceId, CreditLotId.New(),
            new ReserveVersion(1), new PolicyVersion(1), "provider-confirmation", confirmedAt,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.ConfirmedTopUpMint, key, claim.WalletId, claim.Amount,
                [claim.SourceId], confirmedAt));
    }

    private static ConvertHardToSoftCommand Convert(Fixture fixture, long principal, long fee = 0)
    {
        var key = new IdempotencyKey($"convert-{Guid.NewGuid():N}");
        var total = new CoinAmount(CurrencyCode.HardCoin, principal + fee);
        var requestedAt = Time.AddMinutes(2);
        return new ConvertHardToSoftCommand(
            PostingId.New(), PostingId.New(), key, fixture.WalletId, CreditLotId.New(),
            principal, fee, new ReserveVersion(1), new PolicyVersion(1), requestedAt,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.HardToSoftConversion, key, fixture.WalletId, total,
                [fixture.SourceId], requestedAt,
                new CoinAmount(CurrencyCode.SoftCoin, principal * 1_000)));
    }

    private static ReverseTopUpCommand Reverse(
        SourceStampId sourceId,
        long cumulativeHardUnits,
        ProviderReversalDisposition disposition) => new(
        PostingId.New(),
        new IdempotencyKey($"reversal-{cumulativeHardUnits}-{Guid.NewGuid():N}"),
        sourceId,
        cumulativeHardUnits,
        disposition,
        "provider-reversal-evidence",
        new ReserveVersion(1),
        new PolicyVersion(1),
        Time.AddDays(1));

    private static CreditLot Lot(SourceStampId root, RootTraceRange range) => new(
        CreditLotId.New(),
        WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, range.Length / CurrencyTraceScale.HardCoinTraceUnitsPerCoin),
        ProvenanceKind.PurchasedHard,
        Time,
        Time.AddDays(120),
        1,
        CreditLotState.Active,
        [range],
        CurrencyTraceScale.HardCoinTraceUnitsPerCoin);

    private static CreditLot SoftLot(SourceStampId root, RootTraceRange range) => new(
        CreditLotId.New(),
        WalletId.New(),
        new CoinAmount(CurrencyCode.SoftCoin, range.Length),
        ProvenanceKind.ConvertedSoft,
        Time,
        Time.AddDays(120),
        1,
        CreditLotState.Active,
        [range],
        CurrencyTraceScale.SoftCoinTraceUnitsPerCoin);

    private sealed record Fixture(
        InMemoryLedgerKernelStore Store,
        TransactionalPostingService Service,
        WalletId WalletId,
        SourceStampId SourceId);
}
