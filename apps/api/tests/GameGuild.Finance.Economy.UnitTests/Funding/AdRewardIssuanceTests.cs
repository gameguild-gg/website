using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class AdRewardIssuanceTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

    [Fact]
    public void IssueAdReward_PostsSoftLiabilityWithAdRewardProvenance()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);

        var result = service.IssueAdReward(Command(112));

        result.Posting.Status.Should().Be(PostingStatus.Accepted);
        result.OutputLot.Amount.Should().Be(new CoinAmount(CurrencyCode.SoftCoin, 112));
        result.OutputLot.Provenance.Should().Be(ProvenanceKind.AdRewardSoft);
        store.JournalEntries.Should().ContainSingle();
        store.JournalEntries[0].Lines.Select(line => (line.Side, line.Account, line.Amount)).Should().Equal(
            (EntrySide.Debit, EconomyAccountCode.SoftCoinReserve, new CoinAmount(CurrencyCode.SoftCoin, 112)),
            (EntrySide.Credit, EconomyAccountCode.SoftCoinLiability, new CoinAmount(CurrencyCode.SoftCoin, 112)));
        store.SourceEvidenceHistory.Select(source => source.State).Should().Equal(
            SourceConfirmationState.Observed,
            SourceConfirmationState.Confirmed);
        store.ProjectionUpdates.Should().ContainSingle().Which.DeltaUnits.Should().Be(112);
    }

    [Fact]
    public void IssueAdReward_IsIdempotentAndRejectsConflictingOrInvalidCommands()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var command = Command(112);
        var first = service.IssueAdReward(command);

        service.IssueAdReward(command).Should().BeEquivalentTo(first);
        store.JournalEntries.Should().ContainSingle();
        FluentActions.Invoking(() => service.IssueAdReward(command with { SoftUnits = 113 }))
            .Should().Throw<IssuanceAuthorizationBindingException>();
        FluentActions.Invoking(() => service.IssueAdReward(command with { Authorization = null! }))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => service.IssueAdReward(command with { SoftUnits = 0 }))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IssueAdRewardRejectsAnExistingSourceWithoutPartialPosting()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var command = Command(112);
        store.Execute(transaction =>
        {
            transaction.AddSource(SourceEvidence.Observe(
                command.SourceId, "ad-network", "existing", "evidence", Time));
            return true;
        });

        FluentActions.Invoking(() => service.IssueAdReward(command))
            .Should().Throw<InvalidOperationException>();
        store.JournalEntries.Should().BeEmpty();
        store.CreditLots.Should().BeEmpty();
    }

    private static IssueAdRewardCommand Command(long softUnits)
    {
        var sourceId = SourceStampId.New();
        var walletId = WalletId.New();
        var idempotencyKey = new IdempotencyKey($"ad-reward-{Guid.NewGuid():N}");
        var authorizationAmount = new CoinAmount(CurrencyCode.SoftCoin, Math.Max(1, softUnits));
        return new IssueAdRewardCommand(
            PostingId.New(),
            idempotencyKey,
            sourceId,
            walletId,
            CreditLotId.New(),
            softUnits,
            new ReserveVersion(1),
            new PolicyVersion(1),
            "provider-proof:event-1",
            Time,
            FundingAuthorizationFixture.Create(
                PostingTemplateKind.AdRewardIssuance,
                idempotencyKey,
                walletId,
                authorizationAmount,
                [sourceId],
                Time,
                authorizationAmount));
    }
}
