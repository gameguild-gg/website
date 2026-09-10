using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class SystemBackedGrantServiceTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

    [Fact]
    public void IssueSystemBackedGrant_DebitsApprovedHardTreasuryInExactParityBlocks()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var command = Command(3);

        var result = service.IssueSystemBackedGrant(command);

        result.Posting.Status.Should().Be(PostingStatus.Accepted);
        result.OutputLot.Amount.Should().Be(new CoinAmount(CurrencyCode.SoftCoin, 3_000));
        result.OutputLot.Provenance.Should().Be(ProvenanceKind.SystemGrantSoft);
        store.JournalEntries.Should().ContainSingle();
        store.JournalEntries[0].Lines.Select(line => (line.Side, line.Account, line.Amount)).Should().Equal(
            (EntrySide.Debit, EconomyAccountCode.PlatformHardTreasury, new CoinAmount(CurrencyCode.HardCoin, 3)),
            (EntrySide.Credit, EconomyAccountCode.HardCoinReserve, new CoinAmount(CurrencyCode.HardCoin, 3)),
            (EntrySide.Debit, EconomyAccountCode.SoftCoinReserve, new CoinAmount(CurrencyCode.SoftCoin, 3_000)),
            (EntrySide.Credit, EconomyAccountCode.SoftCoinLiability, new CoinAmount(CurrencyCode.SoftCoin, 3_000)));
        store.SourceEvidenceHistory.Select(source => source.State).Should().Equal(
            SourceConfirmationState.Observed,
            SourceConfirmationState.Confirmed);
        store.ProjectionUpdates.Should().ContainSingle().Which.DeltaUnits.Should().Be(3_000);
    }

    [Fact]
    public void IssueSystemBackedGrant_IsIdempotent()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var command = Command(1);

        var first = service.IssueSystemBackedGrant(command);
        var duplicate = service.IssueSystemBackedGrant(command);

        duplicate.Should().BeEquivalentTo(first);
        store.JournalEntries.Should().ContainSingle();
        store.CreditLots.Should().ContainSingle();
        store.SourceEvidenceHistory.Should().HaveCount(2);
    }

    [Fact]
    public void IssueSystemBackedGrant_RejectsMissingOrMismatchedAuthorization()
    {
        var service = new TransactionalPostingService(new InMemoryLedgerKernelStore());
        var command = Command(1);

        FluentActions.Invoking(() => service.IssueSystemBackedGrant(command with { Authorization = null! }))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => service.IssueSystemBackedGrant(command with { HardBackingUnits = 2 }))
            .Should().Throw<IssuanceAuthorizationBindingException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IssueSystemBackedGrant_RejectsNonPositiveHardBacking(long units)
    {
        var service = new TransactionalPostingService(new InMemoryLedgerKernelStore());

        FluentActions.Invoking(() => service.IssueSystemBackedGrant(Command(units)))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IssueSystemBackedGrantRejectsAnExistingSourceWithoutPartialPosting()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var command = Command(1);
        store.Execute(transaction =>
        {
            transaction.AddSource(SourceEvidence.Observe(
                command.SourceId, "platform-treasury", "existing", "evidence", Time));
            return true;
        });

        FluentActions.Invoking(() => service.IssueSystemBackedGrant(command))
            .Should().Throw<InvalidOperationException>();
        store.JournalEntries.Should().BeEmpty();
        store.CreditLots.Should().BeEmpty();
    }

    private static IssueSystemBackedGrantCommand Command(long hardUnits)
    {
        var sourceId = SourceStampId.New();
        var walletId = WalletId.New();
        var idempotencyKey = new IdempotencyKey($"grant-{Guid.NewGuid():N}");
        var softUnits = hardUnits > 0 ? checked(hardUnits * 1_000) : 1;
        var amount = new CoinAmount(CurrencyCode.SoftCoin, softUnits);
        return new IssueSystemBackedGrantCommand(
            PostingId.New(),
            idempotencyKey,
            sourceId,
            walletId,
            CreditLotId.New(),
            hardUnits,
            new ReserveVersion(1),
            new PolicyVersion(1),
            "approved-platform-treasury-debit",
            Time,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.SystemBackedGrant,
                idempotencyKey,
                walletId,
                amount,
                [sourceId],
                Time,
                amount));
    }
}
