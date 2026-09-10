using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.UnitTests.Funding;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class TransactionalPostingServiceTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-03-01T12:00:00Z");

    [Fact]
    public void ObserveTopUp_PersistsPendingEvidenceWithoutCreatingMonetaryState()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);

        var claim = FundingTestDriver.Observe(service, Time, providerObject: "pi_123");

        claim.State.Should().Be(SourceConfirmationState.Observed);
        store.PendingFundingClaims.Should().ContainSingle().Which.Should().BeSameAs(claim);
        store.SourceEvidenceHistory.Should().ContainSingle();
        store.JournalEntries.Should().BeEmpty();
        store.CreditLots.Should().BeEmpty();
        store.FragmentConsumptions.Should().BeEmpty();
        store.Lineages.Should().BeEmpty();
        store.ProjectionUpdates.Should().BeEmpty();
        store.IdempotencyRecords.Should().BeEmpty();
        store.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public void ConfirmObservedTopUp_CommitsSourceJournalLotProjectionIdempotencyAndOutboxTogether()
    {
        var (store, service, observed) = Setup();
        var command = TopUp(observed);

        var result = service.ConfirmObservedTopUp(command);

        result.Status.Should().Be(PostingStatus.Accepted);
        store.SourceEvidenceHistory.Should().HaveCount(2);
        store.SourceEvidenceHistory[^1].State.Should().Be(SourceConfirmationState.Confirmed);
        store.JournalEntries.Should().ContainSingle();
        store.CreditLots.Should().ContainSingle().Which.Amount.Units.Should().Be(10);
        store.ProjectionUpdates.Should().ContainSingle().Which.DeltaUnits.Should().Be(10);
        store.IdempotencyRecords.Should().ContainSingle();
        store.OutboxMessages.Should().ContainSingle().Which.Type.Should().Be("economy.posting.accepted.v1");
        store.GetAvailableLots(observed.WalletId, CurrencyCode.HardCoin)
            .Should().ContainSingle().Which.Amount.Units.Should().Be(10);
    }

    [Fact]
    public void ConfirmObservedTopUp_IsIdempotentAndRejectsKeyReuseWithDifferentCommand()
    {
        var (store, service, observed) = Setup();
        var command = TopUp(observed);

        var first = service.ConfirmObservedTopUp(command);
        var duplicate = service.ConfirmObservedTopUp(command);

        duplicate.Should().Be(first);
        store.JournalEntries.Should().ContainSingle();
        store.CreditLots.Should().ContainSingle();

        var conflict = command with { Evidence = "different-provider-confirmation" };
        FluentActions.Invoking(() => service.ConfirmObservedTopUp(conflict))
            .Should().Throw<IdempotencyConflictException>();
        store.JournalEntries.Should().ContainSingle();
    }

    [Fact]
    public void Transfer_AtomicallyConsumesFifoFragmentsAndCreatesExactDestinationLineage()
    {
        var sourceWallet = WalletId.New();
        var (store, service, observed) = Setup(sourceWallet);
        service.ConfirmObservedTopUp(TopUp(observed));
        var destination = WalletId.New();
        var transfer = new TransferFragmentsCommand(
            PostingId.New(), new IdempotencyKey("transfer-1"),
            sourceWallet, destination,
            new CoinAmount(CurrencyCode.HardCoin, 6), ProvenanceKind.PurchasedHard,
            new ReserveVersion(1), new PolicyVersion(1), Time.AddMinutes(2));

        var result = service.Transfer(transfer);
        var duplicate = service.Transfer(transfer);

        duplicate.Should().Be(result);

        result.Status.Should().Be(PostingStatus.Accepted);
        store.JournalEntries.Should().HaveCount(2);
        store.FragmentConsumptions.Should().ContainSingle();
        store.FragmentConsumptions[0].Ranges.Should().ContainSingle().Which.Length.Should().Be(6_000);
        store.Lineages.Should().ContainSingle();
        store.Lineages[0].Lot.Ranges.Should().Equal(store.FragmentConsumptions[0].Ranges);
        store.GetAvailableLots(sourceWallet, CurrencyCode.HardCoin)
            .Should().ContainSingle().Which.Amount.Units.Should().Be(4);
        store.GetAvailableLots(destination, CurrencyCode.HardCoin)
            .Should().ContainSingle().Which.Amount.Units.Should().Be(6);
        store.ProjectionUpdates.TakeLast(2).Select(update => update.DeltaUnits).Should().Equal(-6, 6);
    }

    [Fact]
    public void Transfer_RollsBackEveryArtifactWhenFragmentsAreInsufficient()
    {
        var sourceWallet = WalletId.New();
        var (store, service, observed) = Setup(sourceWallet, 3);
        service.ConfirmObservedTopUp(TopUp(observed));
        var before = store.SnapshotCounts();

        FluentActions.Invoking(() => service.Transfer(new TransferFragmentsCommand(
                PostingId.New(), new IdempotencyKey("too-large"),
                sourceWallet, WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 4), ProvenanceKind.PurchasedHard,
                new ReserveVersion(1), new PolicyVersion(1), Time.AddMinutes(2))))
            .Should().Throw<InsufficientFragmentsException>();

        store.SnapshotCounts().Should().Be(before);
    }

    [Fact]
    public void ConfirmObservedTopUp_RollsBackLateFailureIncludingSourceConfirmation()
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store, new ThrowingOutboxFactory());
        var observed = FundingTestDriver.Observe(service, Time, providerObject: "pi_rollback");

        FluentActions.Invoking(() => service.ConfirmObservedTopUp(TopUp(observed)))
            .Should().Throw<InvalidOperationException>().WithMessage("outbox failure");

        store.SourceEvidenceHistory.Should().ContainSingle().Which.State.Should().Be(SourceConfirmationState.Observed);
        store.JournalEntries.Should().BeEmpty();
        store.CreditLots.Should().BeEmpty();
        store.ProjectionUpdates.Should().BeEmpty();
        store.IdempotencyRecords.Should().BeEmpty();
        store.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmObservedTopUp_SerializesConcurrentDuplicateCommands()
    {
        var (store, service, observed) = Setup();
        var command = TopUp(observed);

        var results = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => service.ConfirmObservedTopUp(command))));

        results.Select(result => result.Hash).Should().OnlyContain(hash => hash == results[0].Hash);
        store.JournalEntries.Should().ContainSingle();
        store.IdempotencyRecords.Should().ContainSingle();
    }

    [Fact]
    public async Task Transfer_SerializesConcurrentAllocatorsAgainstTheSameSourceFragments()
    {
        var sourceWallet = WalletId.New();
        var (store, service, observed) = Setup(sourceWallet);
        service.ConfirmObservedTopUp(TopUp(observed));
        var commands = new[]
        {
            new TransferFragmentsCommand(
                PostingId.New(), new IdempotencyKey("allocator-race-a"), sourceWallet, WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 7), ProvenanceKind.PurchasedHard,
                new ReserveVersion(1), new PolicyVersion(1), Time.AddMinutes(2)),
            new TransferFragmentsCommand(
                PostingId.New(), new IdempotencyKey("allocator-race-b"), sourceWallet, WalletId.New(),
                new CoinAmount(CurrencyCode.HardCoin, 7), ProvenanceKind.PurchasedHard,
                new ReserveVersion(1), new PolicyVersion(1), Time.AddMinutes(2))
        };

        var outcomes = await Task.WhenAll(commands.Select(command => Task.Run(() =>
        {
            try
            {
                service.Transfer(command);
                return true;
            }
            catch (InsufficientFragmentsException)
            {
                return false;
            }
        })));

        outcomes.Should().ContainSingle(success => success);
        store.FragmentConsumptions.Should().ContainSingle().Which.Amount.Units.Should().Be(7);
        store.GetAvailableLots(sourceWallet, CurrencyCode.HardCoin)
            .Should().ContainSingle().Which.Amount.Units.Should().Be(3);
    }

    private static (InMemoryLedgerKernelStore Store, TransactionalPostingService Service, HardCoinFundingClaim Observed) Setup(
        WalletId? walletId = null,
        long units = 10)
    {
        var store = new InMemoryLedgerKernelStore();
        var service = new TransactionalPostingService(store);
        var observed = FundingTestDriver.Observe(service, Time, units, walletId);
        return (store, service, observed);
    }

    private static ConfirmObservedTopUpCommand TopUp(HardCoinFundingClaim claim) =>
        FundingTestDriver.Confirmation(claim, Time.AddMinutes(1));

    private sealed class ThrowingOutboxFactory : IEconomyOutboxFactory
    {
        public ImmutableOutboxMessage PostingAccepted(PostingResult result) =>
            throw new InvalidOperationException("outbox failure");
    }
}
