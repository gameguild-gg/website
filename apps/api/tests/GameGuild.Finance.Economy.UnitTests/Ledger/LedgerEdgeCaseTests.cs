using System.Security.Cryptography;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.UnitTests.Funding;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class LedgerEdgeCaseTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-05-01T00:00:00Z");

    [Fact]
    public void KernelTransaction_RejectsDuplicateOrMissingFundingAndPostingRecords()
    {
        var store = new InMemoryLedgerKernelStore();
        var claim = HardCoinFundingClaim.Observe(
            SourceStampId.New(),
            WalletId.New(),
            new ProviderMonetaryLeg("stripe", "test", "acct", "pi_edge", "capture"),
            "edge-evidence",
            1,
            Time);
        store.Execute(transaction =>
        {
            transaction.AddFundingClaim(claim);
            return 0;
        });

        FluentActions.Invoking(() => store.Execute(transaction =>
            {
                transaction.AddFundingClaim(claim);
                return 0;
            }))
            .Should().Throw<InvalidOperationException>();
        var unknown = HardCoinFundingClaim.Observe(
            SourceStampId.New(),
            WalletId.New(),
            new ProviderMonetaryLeg("stripe", "test", "acct", "pi_unknown", "capture"),
            "unknown-evidence",
            1,
            Time);
        FluentActions.Invoking(() => store.Execute(transaction =>
            {
                transaction.UpdateFundingClaim(unknown);
                return 0;
            }))
            .Should().Throw<KeyNotFoundException>();
        FluentActions.Invoking(() => store.Execute(transaction => transaction.GetCreditLot(CreditLotId.New())))
            .Should().Throw<KeyNotFoundException>();
        FluentActions.Invoking(() => store.Execute(transaction => transaction.GetPostingResult(PostingId.New())))
            .Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void ImmutableModels_RejectMalformedConstruction()
    {
        FluentActions.Invoking(() => new ChainAnchor(
                Guid.Empty, ChainAnchorKind.Periodic, 1, "head", null, "credential", "payload", "signature", Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new ChainAnchor(
                Guid.NewGuid(), (ChainAnchorKind)0, 1, "head", null, "credential", "payload", "signature", Time))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new ImmutableOutboxMessage(Guid.Empty, "type", "payload", Time))
            .Should().Throw<ArgumentException>();

        var root = SourceStampId.New();
        FluentActions.Invoking(() => Lot((ProvenanceKind)0, CreditLotState.Active, Time.AddDays(1), [new RootTraceRange(root, 0, 1, 0)]))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => Lot(ProvenanceKind.EarnedHard, (CreditLotState)0, Time.AddDays(1), [new RootTraceRange(root, 0, 1, 0)]))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => Lot(ProvenanceKind.EarnedHard, CreditLotState.Active, Time.AddDays(-1), [new RootTraceRange(root, 0, 1, 0)]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Lot(ProvenanceKind.EarnedHard, CreditLotState.Active, Time.AddDays(1), []))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FifoSelection_PreservesAllTrailingRangesAfterRequestIsSatisfied()
    {
        var root = SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 9),
            ProvenanceKind.EarnedHard, Time, Time.AddDays(120), 1, CreditLotState.Active,
            [
                new RootTraceRange(root, 0, 3, 0),
                new RootTraceRange(root, 3, 3, 0),
                new RootTraceRange(root, 6, 3, 0)
            ]);

        var result = FifoFragmentSelector.Select([lot], new CoinAmount(CurrencyCode.HardCoin, 3));

        result.Selections[0].RemainingRanges.Should().Equal(
            new RootTraceRange(root, 3, 3, 0),
            new RootTraceRange(root, 6, 3, 0));
    }

    [Fact]
    public void AvailableLots_SubtractsMiddleConsumptionIntoTwoOrderedRanges()
    {
        var store = new InMemoryLedgerKernelStore();
        var wallet = WalletId.New();
        var root = SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), wallet, new CoinAmount(CurrencyCode.HardCoin, 10),
            ProvenanceKind.EarnedHard, Time, Time.AddDays(120), 1, CreditLotState.Active,
            [new RootTraceRange(root, 0, 10, 0)]);
        store.Execute(transaction =>
        {
            transaction.AddCreditLot(lot);
            transaction.AddConsumption(new FragmentConsumption(
                PostingId.New(), lot.Id, new CoinAmount(CurrencyCode.HardCoin, 2),
                [new RootTraceRange(root, 4, 2, 0)]));
            return true;
        });

        store.GetAvailableLots(wallet, CurrencyCode.HardCoin).Single().Ranges.Should().Equal(
            new RootTraceRange(root, 0, 4, 0),
            new RootTraceRange(root, 6, 4, 0));
    }

    [Fact]
    public void AvailableLots_SubtractsConsumptionThatReachesTheEndOfTheLot()
    {
        var store = new InMemoryLedgerKernelStore();
        var wallet = WalletId.New();
        var root = SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), wallet, new CoinAmount(CurrencyCode.SoftCoin, 10),
            ProvenanceKind.AdRewardSoft, Time, Time, 1, CreditLotState.Active,
            [new RootTraceRange(root, 0, 10, 0)]);
        store.Execute(transaction =>
        {
            transaction.AddCreditLot(lot);
            transaction.AddConsumption(new FragmentConsumption(
                PostingId.New(), lot.Id, new CoinAmount(CurrencyCode.SoftCoin, 6),
                [new RootTraceRange(root, 4, 6, 0)]));
            return true;
        });

        store.GetAvailableLots(wallet, CurrencyCode.SoftCoin).Single().Ranges.Should().Equal(
            new RootTraceRange(root, 0, 4, 0));
    }

    [Fact]
    public void AvailableLots_RejectsConsumptionThatLeavesFractionalCoinTrace()
    {
        var store = new InMemoryLedgerKernelStore();
        var wallet = WalletId.New();
        var root = SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), wallet, new CoinAmount(CurrencyCode.HardCoin, 1),
            ProvenanceKind.EarnedHard, Time, Time.AddDays(120), 1, CreditLotState.Active,
            [new RootTraceRange(root, 0, 1_000, 0)], 1_000);
        store.Execute(transaction =>
        {
            transaction.AddCreditLot(lot);
            transaction.AddConsumption(new FragmentConsumption(
                PostingId.New(), lot.Id, new CoinAmount(CurrencyCode.HardCoin, 1),
                [new RootTraceRange(root, 0, 1, 0)]));
            return true;
        });

        FluentActions.Invoking(() => store.GetAvailableLots(wallet, CurrencyCode.HardCoin))
            .Should().Throw<LineageConservationException>();
    }

    [Fact]
    public void CurrencyTraceScale_MapsBothCurrenciesAndRejectsUnknownValues()
    {
        CurrencyTraceScale.For(CurrencyCode.HardCoin).Should().Be(1_000);
        CurrencyTraceScale.For(CurrencyCode.SoftCoin).Should().Be(1);
        FluentActions.Invoking(() => CurrencyTraceScale.For((CurrencyCode)0))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void JournalVerifier_DetectsSequencePreviousHashAndEntryHashCorruption()
    {
        var requestHash = new string('a', 64);
        var hash = JournalChain.ComputeVerificationHash(1, JournalChain.GenesisHash, requestHash, Time);
        var valid = new JournalVerificationEntry(1, JournalChain.GenesisHash, requestHash, hash, Time);

        JournalChain.VerifyEntries([valid]).Should().BeTrue();
        JournalChain.VerifyEntries([valid with { Sequence = 2 }]).Should().BeFalse();
        JournalChain.VerifyEntries([valid with { PreviousHash = "bad" }]).Should().BeFalse();
        JournalChain.VerifyEntries([valid with { Hash = "bad" }]).Should().BeFalse();
    }

    [Fact]
    public void ReversalFence_RejectsNestedStartAndSnapshotMissingRequestedRoot()
    {
        var root = SourceStampId.New();
        var fences = new RootReversalFenceRegistry();
        fences.BeginReversal(root);

        FluentActions.Invoking(() => fences.BeginReversal(root)).Should().Throw<InvalidOperationException>();
        var missing = new RootReversalFenceRegistry();
        FluentActions.Invoking(() => missing.EnsureAllocatable(missing.Capture([]), [root]))
            .Should().Throw<StaleRootFenceException>();
    }

    [Fact]
    public void ReversalSelector_RejectsForeignHistoryReductionAndUntracedHistory()
    {
        var root = SourceStampId.New();
        var other = SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 5),
            ProvenanceKind.EarnedHard, Time, Time.AddDays(120), 1, CreditLotState.Active,
            [new RootTraceRange(root, 0, 5, 0)]);

        FluentActions.Invoking(() => RootReversalSelector.Select(
                root, 1, [new RootTraceRange(other, 0, 1, 0)], [lot]))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => RootReversalSelector.Select(
                root, 1, [new RootTraceRange(root, 0, 2, 0)], [lot]))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => RootReversalSelector.Select(
                root, 2, [new RootTraceRange(root, 10, 2, 0)], [lot]))
            .Should().Throw<LineageConservationException>();
    }

    [Fact]
    public void ReversalSelector_PreservesAvailablePrefixBeforeAReversedMiddleInterval()
    {
        var root = SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 10),
            ProvenanceKind.EarnedHard, Time, Time.AddDays(120), 1, CreditLotState.Active,
            [new RootTraceRange(root, 0, 10, 0)]);

        var selection = RootReversalSelector.Select(
            root,
            4,
            [new RootTraceRange(root, 4, 2, 0)],
            [lot]);

        selection.NewFragments.SelectMany(fragment => fragment.Ranges)
            .Should().Equal(new RootTraceRange(root, 0, 2, 0));
    }

    [Fact]
    public void ReversalSelector_PreservesAvailablePrefixBeforeHistoryThatReachesTheEnd()
    {
        var root = SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 10),
            ProvenanceKind.EarnedHard, Time, Time.AddDays(120), 1, CreditLotState.Active,
            [new RootTraceRange(root, 0, 10, 0)]);

        var selection = RootReversalSelector.Select(
            root,
            7,
            [new RootTraceRange(root, 4, 6, 0)],
            [lot]);

        selection.NewFragments.SelectMany(fragment => fragment.Ranges)
            .Should().Equal(new RootTraceRange(root, 0, 1, 0));
    }

    [Fact]
    public void SourceEvidence_RejectsRepeatedConfirmation()
    {
        var confirmed = SourceEvidence
            .Observe(SourceStampId.New(), "stripe", "pi_repeat", "payload", Time)
            .Confirm(Time.AddMinutes(1));

        FluentActions.Invoking(() => confirmed.Confirm(Time.AddMinutes(2)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PostingService_RejectsDuplicateSourceMissingSourceAndSameWallet()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var source = SourceStampId.New();
        var observe = new ObserveHardCoinTopUpCommand(
            source,
            WalletId.New(),
            new ProviderMonetaryLeg("stripe", "test", "platform", "pi_dup", "principal"),
            "payload",
            1,
            Time);
        service.ObserveTopUp(observe);

        FluentActions.Invoking(() => service.ObserveTopUp(observe)).Should().Throw<InvalidOperationException>();
        var absent = FundingTestDriver.Observe(
            new TransactionalPostingService(new InMemoryLedgerKernelStore()),
            Time,
            1);
        FluentActions.Invoking(() => service.ConfirmObservedTopUp(
                FundingTestDriver.Confirmation(absent, Time.AddMinutes(1), "missing-source")))
            .Should().Throw<KeyNotFoundException>();

        var wallet = WalletId.New();
        FluentActions.Invoking(() => service.Transfer(new TransferFragmentsCommand(
                PostingId.New(), new IdempotencyKey("same-wallet"), wallet, wallet,
                new CoinAmount(CurrencyCode.HardCoin, 1), ProvenanceKind.EarnedHard,
                new ReserveVersion(1), new PolicyVersion(1), Time)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PostingService_RoutesEarnedHardAndSoftCoinTransfersToTheirLiabilities()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var earnedWallet = SeedLot(store, CurrencyCode.HardCoin, ProvenanceKind.EarnedHard, 5, 1);
        var softWallet = SeedLot(store, CurrencyCode.SoftCoin, ProvenanceKind.AdRewardSoft, 5, 2);

        service.Transfer(Transfer(earnedWallet, CurrencyCode.HardCoin, ProvenanceKind.EarnedHard, "earned"));
        service.Transfer(Transfer(softWallet, CurrencyCode.SoftCoin, ProvenanceKind.AdRewardSoft, "soft"));

        store.JournalEntries[0].Lines.Should().OnlyContain(line => line.Account == EconomyAccountCode.EarnedHardLiability);
        store.JournalEntries[1].Lines.Should().OnlyContain(line => line.Account == EconomyAccountCode.SoftCoinLiability);
    }

    [Fact]
    public void PostingService_RejectsUnsupportedDefaultCurrencyAndNullDependencies()
    {
        FluentActions.Invoking(() => new TransactionalPostingService(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ChainAnchorService(null!, new HmacChainHeadSigner("key", RandomNumberGenerator.GetBytes(32))))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ChainAnchorService(new InMemoryLedgerKernelStore(), null!))
            .Should().Throw<ArgumentNullException>();

        var service = new TransactionalPostingService(new InMemoryLedgerKernelStore());
        FluentActions.Invoking(() => service.Transfer(new TransferFragmentsCommand(
                PostingId.New(), new IdempotencyKey("bad-currency"), WalletId.New(), WalletId.New(),
                default, ProvenanceKind.EarnedHard,
                new ReserveVersion(1), new PolicyVersion(1), Time)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Lineage_RejectsEmptySelections()
    {
        var fences = new RootReversalFenceRegistry();
        FluentActions.Invoking(() => LineageAllocator.CreateRetirement(
                PostingId.New(), [], fences.Capture([]), fences))
            .Should().Throw<LineageConservationException>();
    }

    [Fact]
    public void AvailableLotsIgnoreForeignAndDisjointConsumptionRanges()
    {
        var store = new InMemoryLedgerKernelStore();
        var wallet = WalletId.New();
        var root = SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), wallet, new CoinAmount(CurrencyCode.SoftCoin, 10),
            ProvenanceKind.AdRewardSoft, Time, Time, 1, CreditLotState.Active,
            [new RootTraceRange(root, 10, 10, 0)]);
        var ignored = new[]
        {
            new RootTraceRange(SourceStampId.New(), 12, 2, 0),
            new RootTraceRange(root, 0, 2, 0),
            new RootTraceRange(root, 30, 2, 0)
        };
        store.Execute(transaction =>
        {
            transaction.AddCreditLot(lot);
            foreach (var range in ignored.Append(new RootTraceRange(root, 14, 2, 0)))
                transaction.AddConsumption(new FragmentConsumption(
                    PostingId.New(), lot.Id, new CoinAmount(CurrencyCode.SoftCoin, 2), [range]));
            return true;
        });

        var available = store.GetAvailableLots(wallet, CurrencyCode.SoftCoin).Single();
        available.Amount.Units.Should().Be(8);
        available.Ranges.Should().Equal(
            new RootTraceRange(root, 10, 4, 0),
            new RootTraceRange(root, 16, 4, 0));
    }

    [Fact]
    public void ReversalSelectorRejectsHistoryThatStartsBeforeKnownTrace()
    {
        var root = SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.SoftCoin, 5),
            ProvenanceKind.ConvertedSoft, Time, Time, 1, CreditLotState.Active,
            [new RootTraceRange(root, 10, 5, 0)]);

        FluentActions.Invoking(() => RootReversalSelector.Select(
                root, 2, [new RootTraceRange(root, 0, 2, 0)], [lot]))
            .Should().Throw<LineageConservationException>();
    }

    private static CreditLot Lot(
        ProvenanceKind provenance,
        CreditLotState state,
        DateTimeOffset maturity,
        IReadOnlyCollection<RootTraceRange> ranges) =>
        new(
            CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
            provenance, Time, maturity, 1, state, ranges);

    private static WalletId SeedLot(
        InMemoryLedgerKernelStore store,
        CurrencyCode currency,
        ProvenanceKind provenance,
        long units,
        long sequence)
    {
        var wallet = WalletId.New();
        store.Execute(transaction =>
        {
            transaction.AddCreditLot(new CreditLot(
                CreditLotId.New(), wallet, new CoinAmount(currency, units), provenance,
                Time, Time.AddDays(120), sequence, CreditLotState.Active,
                [new RootTraceRange(SourceStampId.New(), 0, units, 0)]));
            return true;
        });
        return wallet;
    }

    private static TransferFragmentsCommand Transfer(
        WalletId source,
        CurrencyCode currency,
        ProvenanceKind provenance,
        string key) =>
        new(
            PostingId.New(), new IdempotencyKey(key), source, WalletId.New(),
            new CoinAmount(currency, 2), provenance,
            new ReserveVersion(1), new PolicyVersion(1), Time.AddMinutes(1));
}
