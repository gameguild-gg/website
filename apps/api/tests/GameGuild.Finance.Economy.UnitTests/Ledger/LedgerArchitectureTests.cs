using FluentAssertions;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class LedgerArchitectureTests
{
    [Fact]
    public void AppendOnlyLedgerEntities_HaveNoSettersAndDoNotInheritEntityBase()
    {
        Type[] entityTypes =
        [
            typeof(SourceEvidence),
            typeof(CreditLot),
            typeof(JournalEntry),
            typeof(JournalEntryLine),
            typeof(FragmentConsumption),
            typeof(ParentFragmentLineage),
            typeof(DerivedCreditLot),
            typeof(FragmentRetirement),
            typeof(WalletProjectionUpdate),
            typeof(IdempotencyRecord),
            typeof(ImmutableOutboxMessage),
            typeof(ChainAnchor)
        ];

        entityTypes.SelectMany(type => type.GetProperties())
            .Should().OnlyContain(property => property.SetMethod == null);
        entityTypes.Select(type => type.BaseType?.Name)
            .Should().NotContain("EntityBase");
    }

    [Fact]
    public void CorePostingService_ExposesTypedCommandsOnly()
    {
        var methods = typeof(TransactionalPostingService).GetMethods()
            .Where(method => method.DeclaringType == typeof(TransactionalPostingService))
            .ToArray();

        methods.Select(method => method.Name).Should().BeEquivalentTo(
            nameof(TransactionalPostingService.ObserveTopUp),
            nameof(TransactionalPostingService.ConfirmObservedTopUp),
            nameof(TransactionalPostingService.FinalizeObservedTopUp),
            nameof(TransactionalPostingService.ConvertHardToSoft),
            nameof(TransactionalPostingService.IssueSystemBackedGrant),
            nameof(TransactionalPostingService.IssueAdReward),
            nameof(TransactionalPostingService.ReverseTopUp),
            nameof(TransactionalPostingService.Transfer));
        methods.SelectMany(method => method.GetParameters()).Select(parameter => parameter.ParameterType)
            .Should().OnlyContain(type => type.Name.EndsWith("Command", StringComparison.Ordinal));
    }

    [Fact]
    public void AppendOnlyEntityCollections_AreDefensiveCopies()
    {
        var root = new RootTraceRange(global::GameGuild.Finance.Economy.Contracts.SourceStampId.New(), 0, 1, 0);
        var ranges = new List<RootTraceRange> { root };
        var lot = new CreditLot(
            global::GameGuild.Finance.Economy.Contracts.CreditLotId.New(), global::GameGuild.Finance.Economy.Contracts.WalletId.New(),
            new global::GameGuild.Finance.Economy.Contracts.CoinAmount(global::GameGuild.Finance.Economy.Contracts.CurrencyCode.HardCoin, 1),
            global::GameGuild.Finance.Economy.Contracts.ProvenanceKind.EarnedHard,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1,
            CreditLotState.Active, ranges);
        var consumption = new FragmentConsumption(
            global::GameGuild.Finance.Economy.Contracts.PostingId.New(), lot.Id, lot.Amount, ranges);
        var parent = new ParentFragmentLineage(lot.Id, lot.Amount, ranges);
        var parents = new List<ParentFragmentLineage> { parent };
        var derived = new DerivedCreditLot(lot, parents);
        var retirement = new FragmentRetirement(global::GameGuild.Finance.Economy.Contracts.PostingId.New(), lot.Amount, parents);

        ranges.Clear();
        parents.Clear();

        lot.Ranges.Should().ContainSingle();
        consumption.Ranges.Should().ContainSingle();
        consumption.PostingId.Value.Should().NotBeEmpty();
        parent.Ranges.Should().ContainSingle();
        derived.Parents.Should().ContainSingle();
        retirement.Parents.Should().ContainSingle();
        retirement.PostingId.Value.Should().NotBeEmpty();
        lot.Ranges.Should().NotBeAssignableTo<RootTraceRange[]>();
        derived.Parents.Should().NotBeAssignableTo<ParentFragmentLineage[]>();
    }
    [Fact]
    public void KernelContractsExposePostingTraceProjectionAndOutboxIdentity()
    {
        var postingId = global::GameGuild.Finance.Economy.Contracts.PostingId.New();
        var walletId = global::GameGuild.Finance.Economy.Contracts.WalletId.New();
        var lotId = global::GameGuild.Finance.Economy.Contracts.CreditLotId.New();
        var occurredAt = DateTimeOffset.Parse("2026-07-30T12:00:00Z");
        var projection = new WalletProjectionUpdate(
            postingId, walletId, global::GameGuild.Finance.Economy.Contracts.CurrencyCode.HardCoin, 17, 23);
        var outboxId = Guid.NewGuid();
        var outbox = new ImmutableOutboxMessage(outboxId, " economy.test ", "{\"ok\":true}", occurredAt);
        var journalLine = new JournalEntryLine(
            1, Guid.NewGuid(), global::GameGuild.Finance.Economy.Contracts.EntrySide.Debit,
            global::GameGuild.Finance.Economy.Contracts.EconomyAccountCode.PurchasedHardLiability,
            new global::GameGuild.Finance.Economy.Contracts.CoinAmount(
                global::GameGuild.Finance.Economy.Contracts.CurrencyCode.HardCoin, 17),
            walletId, lotId, global::GameGuild.Finance.Economy.Contracts.ProvenanceKind.PurchasedHard);
        var counts = new LedgerKernelCounts(1, 2, 3, 4, 5, 6, 7, 8);

        (projection.PostingId, projection.WalletId, projection.Currency, projection.DeltaUnits, projection.JournalSequence)
            .Should().Be((postingId, walletId, global::GameGuild.Finance.Economy.Contracts.CurrencyCode.HardCoin, 17, 23));
        (outbox.Id, outbox.Type, outbox.Payload, outbox.OccurredAt)
            .Should().Be((outboxId, "economy.test", "{\"ok\":true}", occurredAt));
        outbox.PayloadHash.Should().HaveLength(64);
        journalLine.LotId.Should().Be(lotId);
        (counts.Sources, counts.JournalEntries, counts.CreditLots, counts.FragmentConsumptions,
            counts.Lineages, counts.ProjectionUpdates, counts.IdempotencyRecords, counts.OutboxMessages)
            .Should().Be((1, 2, 3, 4, 5, 6, 7, 8));
    }

}
