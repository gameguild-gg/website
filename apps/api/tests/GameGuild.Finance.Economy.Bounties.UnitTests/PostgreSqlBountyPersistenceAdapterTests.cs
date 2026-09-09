using System.Data;
using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class PostgreSqlBountyPersistenceAdapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void EscrowStoreReadsACompleteBountyAndBothFragmentShapes()
    {
        var bountyId = BountyId.New();
        var posterId = Guid.NewGuid();
        var posterWalletId = WalletId.New();
        var escrowWalletId = WalletId.New();
        var parentLotId = CreditLotId.New();
        var escrowLotId = CreditLotId.New();
        var root = SourceStampId.New();
        var interceptor = new ScriptedRelationalInterceptor();
        interceptor.EnqueueReader(BountyRows(
            bountyId, posterId, posterWalletId, escrowWalletId, "request-hash").Build());
        interceptor.EnqueueReader(FragmentRows()
            .AddRow(
                parentLotId.Value, escrowLotId.Value, (int)CurrencyCode.HardCoin,
                (int)ProvenanceKind.PurchasedHard, 7L, CurrencyTraceScale.HardCoinTraceUnitsPerCoin,
                RootRanges(root, 0, 7000, 2))
            .AddRow(
                CreditLotId.New().Value, null, (int)CurrencyCode.SoftCoin,
                (int)ProvenanceKind.ConvertedSoft, 3L, 1L,
                RootRanges(SourceStampId.New(), 10, 13, 4))
            .Build());
        using var context = new ScriptedBountiesContext(interceptor);

        var result = new PostgreSqlBountyEscrowStore(context).Get(TenantId, bountyId);

        result.Id.Should().Be(bountyId);
        result.PosterId.Should().Be(posterId);
        result.PosterWalletId.Should().Be(posterWalletId);
        result.EscrowWalletId.Should().Be(escrowWalletId);
        result.Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 10));
        result.Eligibility.Should().Be(new BountyEligibilityRequirements(true, 12, true));
        result.ReclaimFeePpm.Should().Be(100_000);
        result.Status.Should().Be(BountyStatus.Open);
        result.IdempotencyKey.Should().Be(new IdempotencyKey("post-key"));
        result.RequestHash.Should().Be("request-hash");
        result.PostedAt.Should().Be(Now);
        result.ExpiresAt.Should().Be(Now.AddDays(2));
        result.Version.Should().Be(3);
        result.Fragments.Should().HaveCount(2);
        result.Fragments[0].ParentLotId.Should().Be(parentLotId);
        result.Fragments[0].EscrowLotId.Should().Be(escrowLotId);
        result.Fragments[0].Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 7));
        result.Fragments[0].Provenance.Should().Be(ProvenanceKind.PurchasedHard);
        result.Fragments[0].TraceUnitsPerCoinUnit.Should().Be(1000);
        result.Fragments[0].SelectedRanges.Should().ContainSingle().Which.Should().Be(
            new RootTraceRange(root, 0, 7000, 2));
        result.Fragments[1].EscrowLotId.Should().BeNull();
        interceptor.Commands.Should().Contain(command => command.Contains("read_bounty_escrow_by_id_v2"));
        interceptor.Commands.Should().Contain(command => command.Contains("read_bounty_escrow_fragments_v4"));
    }

    [Fact]
    public void EscrowStoreReportsMissingAndReplayOutcomesPrecisely()
    {
        var bountyId = BountyId.New();
        var interceptor = new ScriptedRelationalInterceptor();
        interceptor.EnqueueReader(BountyRows().Build());
        interceptor.EnqueueReader(BountyRows().Build());
        interceptor.EnqueueReader(BountyRows(bountyId, requestHash: null).Build());
        interceptor.EnqueueReader(BountyRows(bountyId, requestHash: "different").Build());
        interceptor.EnqueueReader(BountyRows(bountyId, requestHash: "request-hash").Build());
        interceptor.EnqueueReader(FragmentRows().Build());
        using var context = new ScriptedBountiesContext(interceptor);
        var store = new PostgreSqlBountyEscrowStore(context);

        FluentActions.Invoking(() => store.Get(TenantId, bountyId)).Should().Throw<KeyNotFoundException>();
        store.FindPostReplay(TenantId, new IdempotencyKey("missing"), "request-hash").Should().BeNull();
        FluentActions.Invoking(() => store.FindPostReplay(TenantId, new IdempotencyKey("post-key"), "request-hash"))
            .Should().Throw<BountyIdempotencyConflictException>();
        FluentActions.Invoking(() => store.FindPostReplay(TenantId, new IdempotencyKey("post-key"), "request-hash"))
            .Should().Throw<BountyIdempotencyConflictException>();
        store.FindPostReplay(TenantId, new IdempotencyKey("post-key"), " request-hash ")
            .Should().NotBeNull().And.Match<PersistedBountyEscrow>(item => item.Id == bountyId);
    }

    [Fact]
    public void EscrowStoreCreatesOpenPositionsAndTranslatesOnlyDatabaseFailures()
    {
        var position = CreatePosition();
        var command = new CreateBountyEscrowPersistenceCommand(
            position, TenantId, new IdempotencyKey("post-key"), " request-hash ", PostingId.New());
        var success = new ScriptedRelationalInterceptor();
        success.EnqueueNonQuery();
        success.EnqueueReader(BountyRows(
            position.Id, position.PosterId, position.PosterWalletId, position.EscrowWalletId, "request-hash").Build());
        success.EnqueueReader(FragmentRows().Build());
        using (var context = new ScriptedBountiesContext(success))
        {
            new PostgreSqlBountyEscrowStore(context).Create(command).Id.Should().Be(position.Id);
            success.Commands.Should().Contain(item => item.Contains("create_bounty_escrow_v4"));
        }

        AssertCreateFailure(command, new InvalidOperationException("database"), typeof(BountyIdempotencyConflictException));
        AssertCreateFailure(command, new DbUpdateException("database"), typeof(BountyIdempotencyConflictException));
        AssertCreateFailure(command, new TestDbException("database"), typeof(BountyIdempotencyConflictException));
        AssertCreateFailure(command, new ArgumentException("caller"), typeof(ArgumentException));
    }

    [Fact]
    public void EscrowStoreRejectsEveryInvalidCreateBoundary()
    {
        var interceptor = new ScriptedRelationalInterceptor();
        using var context = new ScriptedBountiesContext(interceptor);
        var store = new PostgreSqlBountyEscrowStore(context);
        var position = CreatePosition();

        FluentActions.Invoking(() => store.Get(Guid.Empty, BountyId.New()))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.FindPostReplay(TenantId, new IdempotencyKey("key"), " "))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Create(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => store.Create(new CreateBountyEscrowPersistenceCommand(
                null!, TenantId, new IdempotencyKey("key"), "hash", PostingId.New())))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => store.Create(new CreateBountyEscrowPersistenceCommand(
                position, TenantId, new IdempotencyKey("key"), " ", PostingId.New())))
            .Should().Throw<ArgumentException>();

        typeof(BountyEscrowPosition).GetProperty(nameof(BountyEscrowPosition.Status))!
            .SetValue(position, BountyStatus.Claimed);
        FluentActions.Invoking(() => store.Create(new CreateBountyEscrowPersistenceCommand(
                position, TenantId, new IdempotencyKey("key"), "hash", PostingId.New())))
            .Should().Throw<BountyTerminalConflictException>();

        var emptyId = CreatePosition(useEmptyId: true);
        FluentActions.Invoking(() => store.Create(new CreateBountyEscrowPersistenceCommand(
                emptyId, TenantId, new IdempotencyKey("key"), "hash", PostingId.New())))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PostableLotReaderMaterializesConservedRangesAndRejectsCorruptRows()
    {
        var walletId = WalletId.New();
        var root = SourceStampId.New();
        var valid = PostableLotRows().AddRow(
            CreditLotId.New().Value, walletId.Value, (int)CurrencyCode.HardCoin,
            (int)ProvenanceKind.PurchasedHard, Now.AddDays(-2), Now.AddDays(-1), 8L, 2L,
            RootRanges(root, 10, 2010, 3)).Build();
        var lot = ReadLots(valid, walletId).Should().ContainSingle().Which;
        lot.WalletId.Should().Be(walletId);
        lot.Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 2));
        lot.Provenance.Should().Be(ProvenanceKind.PurchasedHard);
        lot.ConfirmedAt.Should().Be(Now.AddDays(-2));
        lot.OriginalMaturesAt.Should().Be(Now.AddDays(-1));
        lot.JournalSequence.Should().Be(8);
        lot.State.Should().Be(CreditLotState.Active);
        lot.Ranges.Should().ContainSingle().Which.Should().Be(new RootTraceRange(root, 10, 2000, 3));
        lot.TraceUnitsPerCoinUnit.Should().Be(1000);

        AssertLotReadFails(PostableLotRows().AddRow(
            Guid.NewGuid(), walletId.Value, 999, (int)ProvenanceKind.PurchasedHard,
            Now, Now, 1L, 1L, RootRanges(root, 0, 1, 0)).Build());
        AssertLotReadFails(PostableLotRows().AddRow(
            Guid.NewGuid(), walletId.Value, (int)CurrencyCode.SoftCoin, 999,
            Now, Now, 1L, 1L, RootRanges(root, 0, 1, 0)).Build());
        AssertLotReadFails(PostableLotRows().AddRow(
            Guid.NewGuid(), walletId.Value, (int)CurrencyCode.SoftCoin, (int)ProvenanceKind.ConvertedSoft,
            Now, Now, 1L, 0L, RootRanges(root, 0, 1, 0)).Build());
        AssertLotReadFails(PostableLotRows().AddRow(
            Guid.NewGuid(), walletId.Value, (int)CurrencyCode.SoftCoin, (int)ProvenanceKind.ConvertedSoft,
            Now, Now, 1L, 1L, "[]").Build());
        AssertLotReadFails(PostableLotRows().AddRow(
            Guid.NewGuid(), walletId.Value, (int)CurrencyCode.SoftCoin, (int)ProvenanceKind.ConvertedSoft,
            Now, Now, 1L, 2L, RootRanges(root, 0, 1, 0)).Build());
        AssertLotReadFails(PostableLotRows().AddRow(
            Guid.NewGuid(), walletId.Value, (int)CurrencyCode.SoftCoin, (int)ProvenanceKind.ConvertedSoft,
            Now, Now, 1L, 1L, "null").Build());
    }

    [Fact]
    public void PostableLotReaderValidatesConstructionCurrencyAndEmptyResults()
    {
        FluentActions.Invoking(() => new PostgreSqlBountyPostableLotReader(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlBountyPostableLotReader(new NonRelationalApplicationContext()))
            .Should().Throw<InvalidOperationException>();

        var interceptor = new ScriptedRelationalInterceptor();
        interceptor.EnqueueReader(PostableLotRows().Build());
        using var context = new ScriptedBountiesContext(interceptor);
        var reader = new PostgreSqlBountyPostableLotReader(context);
        FluentActions.Invoking(() => reader.Read(WalletId.New(), (CurrencyCode)999, Now))
            .Should().Throw<ArgumentOutOfRangeException>();
        reader.Read(WalletId.New(), CurrencyCode.HardCoin, Now).Should().BeEmpty();
    }

    [Fact]
    public void TerminalStoreReadsNullAndCompleteTerminalEvidence()
    {
        var bountyId = BountyId.New();
        var actorId = Guid.NewGuid();
        var walletId = WalletId.New();
        var risk = Guid.NewGuid();
        var source = SourceStampId.New();
        var lot = CreditLotId.New();
        var outputLot = CreditLotId.New();
        var outputRoot = SourceStampId.New();
        var outputJson = $$"""
            [{"LotId":"{{outputLot.Value}}","WalletId":"{{walletId.Value}}","Currency":1,"AmountUnits":4,"Provenance":1,"RootSourceStampId":"{{outputRoot.Value}}","ConfirmedAt":"{{Now:O}}","OriginalMaturesAt":"{{Now.AddDays(1):O}}","CashOutEligible":true}]
            """;
        var interceptor = new ScriptedRelationalInterceptor();
        interceptor.EnqueueReader(TerminalRows().Build());
        interceptor.EnqueueReader(TerminalRows().Build());
        interceptor.EnqueueReader(TerminalRows().AddRow(
            Guid.NewGuid(), TenantId, bountyId.Value, (int)BountyStatus.Reclaimed, actorId, walletId.Value,
            "reclaim-key", null, null, null, 9L, 1L, 40L, "[]", Now).Build());
        interceptor.EnqueueReader(TerminalRows().AddRow(
            Guid.NewGuid(), TenantId, bountyId.Value, (int)BountyStatus.Claimed, actorId, walletId.Value,
            "claim-key", risk, source.Value, lot.Value, 0L, 0L, 41L, outputJson, Now).Build());
        using var context = new ScriptedBountiesContext(interceptor);
        var store = new PostgreSqlBountyTerminalEventStore(context);

        store.FindByBounty(TenantId, bountyId).Should().BeNull();
        store.FindByIdempotency(TenantId, new IdempotencyKey("missing")).Should().BeNull();
        var reclaimed = store.FindByBounty(TenantId, bountyId)!;
        reclaimed.BountyId.Should().Be(bountyId);
        reclaimed.Status.Should().Be(BountyStatus.Reclaimed);
        reclaimed.ActorId.Should().Be(actorId);
        reclaimed.DestinationWalletId.Should().Be(walletId);
        reclaimed.IdempotencyKey.Should().Be(new IdempotencyKey("reclaim-key"));
        reclaimed.RiskDecisionId.Should().BeNull();
        reclaimed.ProceedsSourceStampId.Should().BeNull();
        reclaimed.ProceedsLotId.Should().BeNull();
        reclaimed.ReturnedUnits.Should().Be(9);
        reclaimed.FeeUnits.Should().Be(1);
        reclaimed.FirstJournalSequence.Should().Be(40);
        reclaimed.OutputLots.Should().BeEmpty();
        reclaimed.OccurredAt.Should().Be(Now);

        var claimed = store.FindByIdempotency(TenantId, new IdempotencyKey("claim-key"))!;
        claimed.RiskDecisionId.Should().Be(risk);
        claimed.ProceedsSourceStampId.Should().Be(source);
        claimed.ProceedsLotId.Should().Be(lot);
        var output = claimed.OutputLots.Should().ContainSingle().Which;
        output.LotId.Should().Be(outputLot);
        output.WalletId.Should().Be(walletId);
        output.Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 4));
        output.Provenance.Should().Be(ProvenanceKind.PurchasedHard);
        output.RootSourceStampId.Should().Be(outputRoot);
        output.ConfirmedAt.Should().Be(Now);
        output.OriginalMaturesAt.Should().Be(Now.AddDays(1));
        output.CashOutEligible.Should().BeTrue();
    }

    [Fact]
    public void TerminalStoreValidatesConstructionAndMissingOutputEvidence()
    {
        FluentActions.Invoking(() => new PostgreSqlBountyTerminalEventStore(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlBountyTerminalEventStore(new NonRelationalApplicationContext()))
            .Should().Throw<InvalidOperationException>();

        var interceptor = new ScriptedRelationalInterceptor();
        interceptor.EnqueueReader(TerminalRows().AddRow(
            Guid.NewGuid(), TenantId, BountyId.New().Value, (int)BountyStatus.Reclaimed, Guid.NewGuid(), WalletId.New().Value,
            "key", null, null, null, 1L, 0L, 1L, "null", Now).Build());
        using var context = new ScriptedBountiesContext(interceptor);
        FluentActions.Invoking(() => new PostgreSqlBountyTerminalEventStore(context)
                .FindByBounty(Guid.Empty, BountyId.New()))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new PostgreSqlBountyTerminalEventStore(context).FindByBounty(TenantId, BountyId.New()))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TerminalClaimWriterValidatesWritesAndClassifiesDatabaseFailures()
    {
        FluentActions.Invoking(() => new PostgreSqlBountyTerminalClaimWriter(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlBountyTerminalClaimWriter(new NonRelationalApplicationContext()))
            .Should().Throw<InvalidOperationException>();

        var command = ClaimWriteCommand();
        AssertClaimWrite(command, null, null);
        AssertClaimWrite(command, new InvalidOperationException("db"), typeof(RegisteredPostingRejectedException));
        AssertClaimWrite(command, new DbUpdateException("db"), typeof(RegisteredPostingRejectedException));
        AssertClaimWrite(command, new TestDbException("db"), typeof(RegisteredPostingRejectedException));
        AssertClaimWrite(command, new ArgumentException("caller"), typeof(ArgumentException));

        AssertClaimValidation(null!, typeof(ArgumentNullException));
        AssertClaimValidation(command with { ClaimantId = Guid.Empty }, typeof(ArgumentException));
        AssertClaimValidation(command with { TenantId = Guid.Empty }, typeof(ArgumentException));
        AssertClaimValidation(command with { RiskDecisionId = Guid.Empty }, typeof(ArgumentException));
        AssertClaimValidation(command with { EvidenceHash = " " }, typeof(ArgumentException));
        AssertClaimValidation(command with { EvidenceHash = new string('x', 129) }, typeof(ArgumentException));
    }

    [Fact]
    public void TerminalReclaimWriterValidatesWritesAndClassifiesDatabaseFailures()
    {
        FluentActions.Invoking(() => new PostgreSqlBountyTerminalReclaimWriter(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlBountyTerminalReclaimWriter(new NonRelationalApplicationContext()))
            .Should().Throw<InvalidOperationException>();

        var command = ReclaimWriteCommand();
        AssertReclaimWrite(command, null, null);
        AssertReclaimWrite(command, new InvalidOperationException("db"), typeof(RegisteredPostingRejectedException));
        AssertReclaimWrite(command, new DbUpdateException("db"), typeof(RegisteredPostingRejectedException));
        AssertReclaimWrite(command, new TestDbException("db"), typeof(RegisteredPostingRejectedException));
        AssertReclaimWrite(command, new ArgumentException("caller"), typeof(ArgumentException));

        AssertReclaimValidation(null!, typeof(ArgumentNullException));
        AssertReclaimValidation(command with { PosterId = Guid.Empty }, typeof(ArgumentException));
        AssertReclaimValidation(command with { TenantId = Guid.Empty }, typeof(ArgumentException));
        AssertReclaimValidation(command with { RiskDecisionId = Guid.Empty }, typeof(ArgumentException));
    }

    private static void AssertCreateFailure(
        CreateBountyEscrowPersistenceCommand command,
        Exception exception,
        Type expected)
    {
        var interceptor = new ScriptedRelationalInterceptor();
        interceptor.EnqueueNonQueryException(exception);
        using var context = new ScriptedBountiesContext(interceptor);
        FluentActions.Invoking(() => new PostgreSqlBountyEscrowStore(context).Create(command))
            .Should().Throw<Exception>().Which.Should().BeOfType(expected);
    }

    private static IReadOnlyList<CreditLot> ReadLots(DataTable table, WalletId walletId)
    {
        var interceptor = new ScriptedRelationalInterceptor();
        interceptor.EnqueueReader(table);
        using var context = new ScriptedBountiesContext(interceptor);
        return new PostgreSqlBountyPostableLotReader(context).Read(walletId, CurrencyCode.HardCoin, Now);
    }

    private static void AssertLotReadFails(DataTable table)
    {
        var walletId = new WalletId((Guid)table.Rows[0]["WalletId"]);
        FluentActions.Invoking(() => ReadLots(table, walletId)).Should().Throw<InvalidOperationException>();
    }

    private static void AssertClaimWrite(
        BountyClaimTerminalWriteCommand command,
        Exception? exception,
        Type? expected)
    {
        var interceptor = new ScriptedRelationalInterceptor();
        if (exception is null) interceptor.EnqueueNonQuery();
        else interceptor.EnqueueNonQueryException(exception);
        using var context = new ScriptedBountiesContext(interceptor);
        var action = () => new PostgreSqlBountyTerminalClaimWriter(context).Complete(command);
        if (expected is null)
        {
            action.Should().NotThrow();
            interceptor.Commands.Should().ContainSingle(item => item.Contains("complete_bounty_claim_v2"));
        }
        else action.Should().Throw<Exception>().Which.Should().BeOfType(expected);
    }

    private static void AssertClaimValidation(BountyClaimTerminalWriteCommand command, Type expected)
    {
        var interceptor = new ScriptedRelationalInterceptor();
        using var context = new ScriptedBountiesContext(interceptor);
        FluentActions.Invoking(() => new PostgreSqlBountyTerminalClaimWriter(context).Complete(command))
            .Should().Throw<Exception>().Which.Should().BeOfType(expected);
    }

    private static void AssertReclaimWrite(
        BountyReclaimTerminalWriteCommand command,
        Exception? exception,
        Type? expected)
    {
        var interceptor = new ScriptedRelationalInterceptor();
        if (exception is null) interceptor.EnqueueNonQuery();
        else interceptor.EnqueueNonQueryException(exception);
        using var context = new ScriptedBountiesContext(interceptor);
        var action = () => new PostgreSqlBountyTerminalReclaimWriter(context).Complete(command);
        if (expected is null)
        {
            action.Should().NotThrow();
            interceptor.Commands.Should().ContainSingle(item => item.Contains("complete_bounty_reclaim_v2"));
        }
        else action.Should().Throw<Exception>().Which.Should().BeOfType(expected);
    }

    private static void AssertReclaimValidation(BountyReclaimTerminalWriteCommand command, Type expected)
    {
        var interceptor = new ScriptedRelationalInterceptor();
        using var context = new ScriptedBountiesContext(interceptor);
        FluentActions.Invoking(() => new PostgreSqlBountyTerminalReclaimWriter(context).Complete(command))
            .Should().Throw<Exception>().Which.Should().BeOfType(expected);
    }

    private static BountyClaimTerminalWriteCommand ClaimWriteCommand() => new(
        TenantId, BountyId.New(), Guid.NewGuid(), WalletId.New(), new IdempotencyKey("claim-key"), PostingId.New(),
        Guid.NewGuid(), " evidence ", Now);

    private static BountyReclaimTerminalWriteCommand ReclaimWriteCommand() => new(
        TenantId, BountyId.New(), Guid.NewGuid(), WalletId.New(), new IdempotencyKey("reclaim-key"), PostingId.New(),
        Guid.NewGuid(), Now);

    private static BountyEscrowPosition CreatePosition(bool useEmptyId = false)
    {
        var posterWallet = WalletId.New();
        var amount = new CoinAmount(CurrencyCode.HardCoin, 10);
        var root = SourceStampId.New();
        var lot = new CreditLot(
            CreditLotId.New(), posterWallet, amount, ProvenanceKind.PurchasedHard,
            Now.AddDays(-2), Now.AddDays(-1), 1, CreditLotState.Active,
            [new RootTraceRange(root, 0, 10_000, 0)], 1000);
        return BountyEscrowPositionFactory.Create(new PostBountyCommand(
            useEmptyId ? default : BountyId.New(), Guid.NewGuid(), posterWallet, WalletId.New(), amount,
            [lot], new BountyEligibilityRequirements(true, 12, true), 100_000,
            Now, Now.AddDays(2), new IdempotencyKey("post-key")));
    }

    private static TestDataTable BountyRows(
        BountyId? id = null,
        Guid? posterId = null,
        WalletId? posterWalletId = null,
        WalletId? escrowWalletId = null,
        string? requestHash = "request-hash")
    {
        var table = new TestDataTable(
            ("Id", typeof(Guid)), ("TenantId", typeof(Guid)), ("PosterId", typeof(Guid)), ("PosterWalletId", typeof(Guid)),
            ("EscrowWalletId", typeof(Guid)), ("Currency", typeof(int)), ("AmountUnits", typeof(long)),
            ("ReclaimFeePpm", typeof(int)), ("RequiresPrerequisite", typeof(bool)),
            ("MinimumReputation", typeof(int)), ("RequiresInstructorVerification", typeof(bool)),
            ("Status", typeof(int)), ("IdempotencyKey", typeof(string)), ("RequestHash", typeof(string)),
            ("PostedAt", typeof(DateTimeOffset)), ("ExpiresAt", typeof(DateTimeOffset)), ("Version", typeof(long)));
        if (id is not null)
            table.AddRow(
                id.Value.Value, TenantId, posterId ?? Guid.NewGuid(), (posterWalletId ?? WalletId.New()).Value,
                (escrowWalletId ?? WalletId.New()).Value, (int)CurrencyCode.HardCoin, 10L, 100_000,
                true, 12, true, (int)BountyStatus.Open, "post-key", requestHash,
                Now, Now.AddDays(2), 3L);
        return table;
    }

    private static TestDataTable FragmentRows() => new(
        ("ParentLotId", typeof(Guid)), ("EscrowLotId", typeof(Guid)), ("Currency", typeof(int)),
        ("Provenance", typeof(int)), ("AmountUnits", typeof(long)),
        ("TraceUnitsPerCoinUnit", typeof(long)), ("SelectedRootRanges", typeof(string)));

    private static TestDataTable PostableLotRows() => new(
        ("Id", typeof(Guid)), ("WalletId", typeof(Guid)), ("Currency", typeof(int)),
        ("Provenance", typeof(int)), ("ConfirmedAt", typeof(DateTimeOffset)),
        ("OriginalMaturesAt", typeof(DateTimeOffset)), ("JournalSequence", typeof(long)),
        ("AmountUnits", typeof(long)), ("RootRanges", typeof(string)));

    private static TestDataTable TerminalRows() => new(
        ("Id", typeof(Guid)), ("TenantId", typeof(Guid)), ("BountyId", typeof(Guid)), ("Status", typeof(int)),
        ("ActorId", typeof(Guid)), ("DestinationWalletId", typeof(Guid)),
        ("IdempotencyKey", typeof(string)), ("RiskDecisionId", typeof(Guid)),
        ("ProceedsSourceStampId", typeof(Guid)), ("ProceedsLotId", typeof(Guid)),
        ("ReturnedUnits", typeof(long)), ("FeeUnits", typeof(long)),
        ("FirstJournalSequence", typeof(long)), ("OutputLots", typeof(string)),
        ("OccurredAt", typeof(DateTimeOffset)));

    private static string RootRanges(SourceStampId root, long start, long end, long epoch) => $$"""
        [{"RootSourceStampId":"{{root.Value}}","StartInclusive":{{start}},"EndExclusive":{{end}},"ReversalEpoch":{{epoch}}}]
        """;
}
