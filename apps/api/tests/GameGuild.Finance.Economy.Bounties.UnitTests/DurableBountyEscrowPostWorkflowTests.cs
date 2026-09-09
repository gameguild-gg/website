using FluentAssertions;
using GameGuild;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class DurableBountyEscrowPostWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Post_UsesServerLotsReservesExactFifoRangesAndPersistsEscrow()
    {
        var lot = CreateLot(7);
        var request = CreateRequest(7);
        var context = new RecordingContext();
        var reader = new RecordingLotReader([lot]);
        var reservations = new RecordingReservations(lot);
        var store = new RecordingStore();
        var postings = new RecordingPostings();
        var workflow = new PostgreSqlDurableBountyEscrowPostWorkflow(context, reader, reservations, store, postings);

        var posted = await workflow.PostAsync(request);

        posted.Id.Should().Be(request.Id);
        reader.Requests.Should().ContainSingle().Which.Should().Be((request.PosterWalletId, CurrencyCode.HardCoin, request.PostedAt));
        reservations.Requests.Should().ContainSingle().Which.Should().Be(new FifoFragmentReservationRequest(
            request.Id.Value,
            request.PosterWalletId,
            CurrencyCode.HardCoin,
            ProvenanceKind.PurchasedHard,
            request.Amount,
            PersistedFragmentReservationPurpose.BountyEscrow,
            request.PostedAt));
        store.CreateCommands.Should().ContainSingle().Which.Position.EscrowFragments
            .Should().ContainSingle().Which.ParentLot.Id.Should().Be(lot.Id);
        postings.Requests.Should().ContainSingle().Which.Posting.Template.Kind.Should().Be(PostingTemplateKind.BountyEscrow);
        reservations.Transitions.Should().ContainSingle().Which.Should().Be((
            request.Id.Value,
            PersistedFragmentReservationStatus.Reserved,
            PersistedFragmentReservationStatus.Consumed,
            request.PostedAt));
        context.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Post_ReturnsIdempotentReplayWithoutOpeningTransaction()
    {
        var request = CreateRequest(7);
        var replay = CreatePersisted(request);
        var context = new RecordingContext();
        var workflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            context,
            new RecordingLotReader([]),
            new RecordingReservations(CreateLot(7)),
            new RecordingStore(replay),
            new RecordingPostings());

        var posted = await workflow.PostAsync(request);

        posted.Should().BeSameAs(replay);
        context.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_RollsBackWhenPersistedReservationDoesNotMatchServerSelection()
    {
        var lot = CreateLot(7);
        var request = CreateRequest(7);
        var context = new RecordingContext();
        var workflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            context,
            new RecordingLotReader([lot]),
            new RecordingReservations(lot, mismatchedRoot: true),
            new RecordingStore(),
            new RecordingPostings());

        await FluentActions.Invoking(() => workflow.PostAsync(request))
            .Should().ThrowAsync<RegisteredPostingRejectedException>();

        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Post_RollsBackWhenReservedFragmentsCannotBecomeConsumed()
    {
        var lot = CreateLot(7);
        var request = CreateRequest(7);
        var context = new RecordingContext();
        var workflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            context,
            new RecordingLotReader([lot]),
            new RecordingReservations(lot, transitionCount: 0),
            new RecordingStore(),
            new RecordingPostings());

        await FluentActions.Invoking(() => workflow.PostAsync(request))
            .Should().ThrowAsync<RegisteredPostingRejectedException>();

        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
        context.Transactions.Single().CommitCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Post_RejectsInvalidRequestBeforeReadingOrOpeningTransaction()
    {
        var request = CreateRequest(7) with { RequestHash = " " };
        var context = new RecordingContext();
        var reader = new RecordingLotReader([]);
        var workflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            context,
            reader,
            new RecordingReservations(CreateLot(7)),
            new RecordingStore(),
            new RecordingPostings());

        await FluentActions.Invoking(() => workflow.PostAsync(request))
            .Should().ThrowAsync<ArgumentException>();

        reader.Requests.Should().BeEmpty();
        context.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_RejectsUnscopedAuthorityAndReplayBoundToAnotherBounty()
    {
        var unscoped = CreateRequest(7);
        SetTenant(unscoped.Authority, Guid.Empty);
        var workflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            new RecordingContext(), new RecordingLotReader([]),
            new RecordingReservations(CreateLot(7)), new RecordingStore(), new RecordingPostings());

        await FluentActions.Invoking(() => workflow.PostAsync(unscoped))
            .Should().ThrowAsync<ArgumentException>();

        var request = CreateRequest(7);
        var replay = CreatePersisted(request) with { Id = BountyId.New() };
        var replayWorkflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            new RecordingContext(), new RecordingLotReader([]),
            new RecordingReservations(CreateLot(7)), new RecordingStore(replay), new RecordingPostings());
        await FluentActions.Invoking(() => replayWorkflow.PostAsync(request))
            .Should().ThrowAsync<BountyIdempotencyConflictException>();

        var foreignReplay = CreatePersisted(request) with { TenantId = Guid.NewGuid() };
        var foreignReplayWorkflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            new RecordingContext(), new RecordingLotReader([]),
            new RecordingReservations(CreateLot(7)), new RecordingStore(foreignReplay, ignoreTenant: true),
            new RecordingPostings());
        await FluentActions.Invoking(() => foreignReplayWorkflow.PostAsync(request))
            .Should().ThrowAsync<BountyIdempotencyConflictException>();
    }

    private static DurableBountyEscrowPostRequest CreateRequest(long units)
    {
        var posterId = Guid.NewGuid();
        return new DurableBountyEscrowPostRequest(
            BountyId.New(),
            posterId,
            WalletId.New(),
            WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, units),
            BountyEligibilityRequirements.None,
            0,
            Now,
            Now.AddDays(7),
            new IdempotencyKey($"bounty-post-{Guid.NewGuid():N}"),
            "request-hash",
            Authority(posterId),
            new ReserveVersion(3),
            new PolicyVersion(4));
    }

    private static RegisteredPostingAuthority Authority(Guid actorId) => new(
        Guid.NewGuid(), actorId, Guid.NewGuid(), Guid.NewGuid(), "bounty-post", 1);

    private static void SetTenant(RegisteredPostingAuthority authority, Guid tenantId) =>
        typeof(RegisteredPostingAuthority)
            .GetField("<TenantId>k__BackingField", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .SetValue(authority, tenantId);

    private static CreditLot CreateLot(long units)
    {
        var root = SourceStampId.New();
        return new CreditLot(
            CreditLotId.New(),
            WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, units),
            ProvenanceKind.PurchasedHard,
            Now.AddDays(-1),
            Now.AddDays(-1),
            1,
            CreditLotState.Active,
            [new RootTraceRange(root, 0, checked(units * CurrencyTraceScale.HardCoinTraceUnitsPerCoin), 0)],
            CurrencyTraceScale.HardCoinTraceUnitsPerCoin);
    }

    private static PersistedBountyEscrow CreatePersisted(DurableBountyEscrowPostRequest request) => new(
        request.Id,
        request.Authority.TenantId,
        request.PosterId,
        request.PosterWalletId,
        request.EscrowWalletId,
        request.Amount,
        request.Eligibility,
        request.ReclaimFeePpm,
        BountyStatus.Open,
        request.IdempotencyKey,
        request.RequestHash,
        request.PostedAt,
        request.ExpiresAt,
        1,
        []);

    private sealed class RecordingContext : IApplicationDbContext
    {
        public List<RecordingTransaction> Transactions { get; } = [];
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var transaction = new RecordingTransaction();
            Transactions.Add(transaction);
            return Task.FromResult<IDbContextTransaction>(transaction);
        }
    }

    private sealed class RecordingTransaction : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public bool CommitCalled { get; private set; }
        public bool RollbackCalled { get; private set; }
        public void Commit() => CommitCalled = true;
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCalled = true;
            return Task.CompletedTask;
        }
        public void Rollback() => RollbackCalled = true;
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCalled = true;
            return Task.CompletedTask;
        }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingLotReader(IReadOnlyList<CreditLot> lots) : IBountyPostableLotReader
    {
        public List<(WalletId WalletId, CurrencyCode Currency, DateTimeOffset AsOf)> Requests { get; } = [];
        public IReadOnlyList<CreditLot> Read(WalletId walletId, CurrencyCode currency, DateTimeOffset asOf)
        {
            Requests.Add((walletId, currency, asOf));
            return lots.Select(lot => lot.WalletId == walletId ? lot : CopyForWallet(lot, walletId)).ToArray();
        }

        private static CreditLot CopyForWallet(CreditLot lot, WalletId walletId) => new(
            lot.Id, walletId, lot.Amount, lot.Provenance, lot.ConfirmedAt, lot.OriginalMaturesAt,
            lot.JournalSequence, lot.State, lot.Ranges, lot.TraceUnitsPerCoinUnit);
    }

    private sealed class RecordingReservations(
        CreditLot lot,
        bool mismatchedRoot = false,
        long transitionCount = 1) : IFifoFragmentReservationGateway
    {
        public List<FifoFragmentReservationRequest> Requests { get; } = [];
        public List<(Guid OperationId, PersistedFragmentReservationStatus Expected, PersistedFragmentReservationStatus Next, DateTimeOffset TerminalAt)> Transitions { get; } = [];
        public IReadOnlyList<PersistedFragmentReservation> Reserve(FifoFragmentReservationRequest request)
        {
            Requests.Add(request);
            var root = mismatchedRoot ? SourceStampId.New() : lot.Ranges[0].Root;
            var range = new RootTraceRange(
                root,
                lot.Ranges[0].Start,
                checked(request.Amount.Units * CurrencyTraceScale.For(request.Currency)),
                lot.Ranges[0].Epoch);
            return [new PersistedFragmentReservation(
                Guid.NewGuid(), request.OperationId, lot.Id, root, range.Epoch, range, request.Amount)];
        }

        public long Transition(Guid operationId, PersistedFragmentReservationStatus expected, PersistedFragmentReservationStatus next, DateTimeOffset terminalAt)
        {
            Transitions.Add((operationId, expected, next, terminalAt));
            return transitionCount;
        }
    }

    private sealed class RecordingStore(PersistedBountyEscrow? replay = null, bool ignoreTenant = false) : IBountyEscrowStore
    {
        public List<CreateBountyEscrowPersistenceCommand> CreateCommands { get; } = [];
        public PersistedBountyEscrow Get(Guid tenantId, BountyId bountyId) => throw new NotSupportedException();
        public PersistedBountyEscrow? FindPostReplay(Guid tenantId, IdempotencyKey idempotencyKey, string requestHash) =>
            replay is not null && (ignoreTenant || replay.TenantId == tenantId) ? replay : null;
        public PersistedBountyEscrow Create(CreateBountyEscrowPersistenceCommand command)
        {
            CreateCommands.Add(command);
            return CreatePersisted(new DurableBountyEscrowPostRequest(
                command.Position.Id,
                command.Position.PosterId,
                command.Position.PosterWalletId,
                command.Position.EscrowWalletId,
                command.Position.Amount,
                command.Position.Eligibility,
                command.Position.ReclaimFeePpm,
                command.Position.PostedAt,
                command.Position.ExpiresAt,
                command.IdempotencyKey,
                command.RequestHash,
                new RegisteredPostingAuthority(
                    Guid.NewGuid(), command.Position.PosterId, command.TenantId,
                    Guid.NewGuid(), "bounty-post", 1),
                new ReserveVersion(3),
                new PolicyVersion(4)));
        }
    }

    private sealed class RecordingPostings : IRegisteredPostingGateway
    {
        public List<RegisteredPostingRequest> Requests { get; } = [];

        public RegisteredPostingReceipt Post(RegisteredPostingRequest request)
        {
            Requests.Add(request);
            return new RegisteredPostingReceipt(request.Posting.Id, 1, "journal-hash", false);
        }
    }
}
