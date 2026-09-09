using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Projections;
using GameGuild.Finance.Economy.UnitTests.Funding;

namespace GameGuild.Finance.Economy.UnitTests.Projections;

public sealed class JournalProjectionRebuilderTests
{
    private static readonly DateTimeOffset Time = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void JournalRebuildMatchesLotRebuildAfterTopUpAndTransfer()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var sourceWallet = WalletId.New();
        var destinationWallet = WalletId.New();
        var claim = FundingTestDriver.Observe(
            service,
            Time,
            10,
            sourceWallet,
            providerObject: "pi_projection");
        FundingTestDriver.Confirm(service, claim, Time.AddMinutes(1), "projection-top-up");
        service.Transfer(new TransferFragmentsCommand(
            PostingId.New(), new IdempotencyKey("projection-transfer"), sourceWallet, destinationWallet,
            new CoinAmount(CurrencyCode.HardCoin, 4), ProvenanceKind.PurchasedHard,
            new ReserveVersion(1), new PolicyVersion(1), Time.AddMinutes(2)));

        var sourceJournal = JournalProjectionRebuilder.Rebuild(sourceWallet, store.JournalEntries);
        var destinationJournal = JournalProjectionRebuilder.Rebuild(destinationWallet, store.JournalEntries);
        var sourceLots = WalletProjectionRebuilder.Rebuild(Input(sourceWallet, store));
        var destinationLots = WalletProjectionRebuilder.Rebuild(Input(destinationWallet, store));

        sourceJournal.PurchasedHard.Should().Be(6);
        destinationJournal.PurchasedHard.Should().Be(4);
        JournalProjectionRebuilder.Compare(sourceJournal, sourceLots).IsMatch.Should().BeTrue();
        JournalProjectionRebuilder.Compare(destinationJournal, destinationLots).IsMatch.Should().BeTrue();
    }

    [Fact]
    public void JournalComparisonDetectsCompositionCorruptionAndRejectsNegativeHistory()
    {
        var wallet = WalletId.New();
        var journal = new JournalWalletProjection(10, 5, 0, 20);
        var lot = new WalletBalanceProjection(0, 0, 9, 5, 0, 20, 0, 0, 0, 14, 20, 5);

        var comparison = JournalProjectionRebuilder.Compare(journal, lot);

        comparison.IsMatch.Should().BeFalse();
        comparison.Differences.Should().ContainSingle(difference => difference.Component == "PurchasedHard");

        var request = new PostingRequest(
            PostingId.New(), new PostingTemplate(PostingTemplateKind.Spend, 1), new IdempotencyKey("negative-journal"),
            PostingAuthority.WalletOwner, new ReserveVersion(1), new PolicyVersion(1), null, Time,
            [
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability,
                    new CoinAmount(CurrencyCode.HardCoin, 1), wallet, null, ProvenanceKind.PurchasedHard),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.PurchasedHardLiability,
                    new CoinAmount(CurrencyCode.HardCoin, 1), WalletId.New(), null, ProvenanceKind.PurchasedHard)
            ]);
        var chain = new JournalChain();
        chain.Append(request, Time);

        FluentActions.Invoking(() => JournalProjectionRebuilder.Rebuild(wallet, chain.Entries))
            .Should().Throw<ProjectionCorruptionException>();
        FluentActions.Invoking(() => JournalProjectionRebuilder.Rebuild(wallet, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => JournalProjectionRebuilder.Compare(null!, lot))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => JournalProjectionRebuilder.Compare(journal, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void JournalRebuildDerivesEveryCurrencyAndProvenanceComponent()
    {
        var wallet = WalletId.New();
        IReadOnlyList<JournalEntry> journal =
        [
            Entry(
                Line(wallet, EntrySide.Credit, CurrencyCode.HardCoin, 3, ProvenanceKind.PurchasedHard),
                Line(wallet, EntrySide.Credit, CurrencyCode.HardCoin, 5, ProvenanceKind.EarnedHard),
                Line(wallet, EntrySide.Credit, CurrencyCode.HardCoin, 7, ProvenanceKind.RefundRestoration),
                Line(wallet, EntrySide.Credit, CurrencyCode.SoftCoin, 11, ProvenanceKind.AdRewardSoft))
        ];

        var projection = JournalProjectionRebuilder.Rebuild(wallet, journal);

        projection.Should().Be(new JournalWalletProjection(3, 5, 7, 11));
    }

    [Theory]
    [InlineData(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard)]
    [InlineData(CurrencyCode.HardCoin, ProvenanceKind.EarnedHard)]
    [InlineData(CurrencyCode.HardCoin, ProvenanceKind.RefundRestoration)]
    [InlineData(CurrencyCode.SoftCoin, ProvenanceKind.AdRewardSoft)]
    public void JournalRebuildRejectsNegativeHistoryForEveryComponent(
        CurrencyCode currency,
        ProvenanceKind provenance)
    {
        var wallet = WalletId.New();

        FluentActions.Invoking(() => JournalProjectionRebuilder.Rebuild(
                wallet,
                [Entry(Line(wallet, EntrySide.Debit, currency, 1, provenance))]))
            .Should().Throw<ProjectionCorruptionException>();
    }

    [Fact]
    public void JournalComparisonReportsEveryComponentMismatch()
    {
        var journal = new JournalWalletProjection(1, 2, 3, 4);
        var lots = new WalletBalanceProjection(0, 0, 5, 6, 7, 8, 0, 0, 0, 0, 0, 0);

        var comparison = JournalProjectionRebuilder.Compare(journal, lots);

        comparison.IsMatch.Should().BeFalse();
        comparison.Differences.Select(difference => difference.Component)
            .Should().Equal("PurchasedHard", "EarnedHard", "RestrictedHard", "Soft");
    }

    [Fact]
    public void JournalRebuildRejectsArithmeticOverflow()
    {
        var wallet = WalletId.New();

        FluentActions.Invoking(() => JournalProjectionRebuilder.Rebuild(
                wallet,
                [Entry(
                    Line(wallet, EntrySide.Credit, CurrencyCode.SoftCoin, long.MaxValue, ProvenanceKind.AdRewardSoft),
                    Line(wallet, EntrySide.Credit, CurrencyCode.SoftCoin, 1, ProvenanceKind.AdRewardSoft))]))
            .Should().Throw<ProjectionCorruptionException>()
            .WithMessage("Journal projection arithmetic overflowed:*");
    }

    private static JournalEntry Entry(params JournalEntryLine[] lines) =>
        new(
            1,
            PostingId.New(),
            JournalChain.GenesisHash,
            new string('1', 64),
            new string('2', 64),
            Time,
            lines);

    private static JournalEntryLine Line(
        WalletId wallet,
        EntrySide side,
        CurrencyCode currency,
        long units,
        ProvenanceKind provenance) =>
        new(
            1,
            Guid.NewGuid(),
            side,
            currency == CurrencyCode.SoftCoin
                ? EconomyAccountCode.SoftCoinLiability
                : EconomyAccountCode.PurchasedHardLiability,
            new CoinAmount(currency, units),
            wallet,
            null,
            provenance);

    private static WalletProjectionRebuildInput Input(WalletId wallet, InMemoryLedgerKernelStore store) =>
        new(
            wallet, [], store.CreditLots, store.FragmentConsumptions, [], [], [], [], Time.AddDays(1));
}
