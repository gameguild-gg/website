using FluentAssertions;
using GameGuild;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class BountyWorkflowBoundaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ClaimReplaysInsideTheTransactionAndRequiresPersistedTerminalEvidence()
    {
        var request = ClaimRequest();
        var escrow = ClaimEscrow(request);
        var replay = ClaimTerminal(request);
        var replayContext = new RecordingContext();
        var replayTerminals = new ScriptedTerminals(null, replay);
        var replayWorkflow = new PostgreSqlDurableBountyClaimWorkflow(
            replayContext, new ScriptedEscrows(escrow), replayTerminals,
            new RecordingPostings(), new RecordingClaimWriter(replayTerminals, request));

        (await replayWorkflow.ClaimAsync(request)).Should().BeSameAs(replay);
        replayContext.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();

        var missingContext = new RecordingContext();
        var missingTerminals = new ScriptedTerminals(null, null, null);
        var missingWorkflow = new PostgreSqlDurableBountyClaimWorkflow(
            missingContext, new ScriptedEscrows(escrow), missingTerminals,
            new RecordingPostings(), new RecordingClaimWriter(missingTerminals, request, persist: false));
        await FluentActions.Invoking(() => missingWorkflow.ClaimAsync(request))
            .Should().ThrowAsync<RegisteredPostingRejectedException>();
        missingContext.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimRejectsEveryRequestAndEscrowBoundary()
    {
        var request = ClaimRequest();
        var escrow = ClaimEscrow(request);

        await AssertClaimFails(null!, escrow, typeof(ArgumentNullException));
        await AssertClaimFails(request with { ClaimantId = Guid.Empty }, escrow, typeof(ArgumentException));
        await AssertClaimFails(
            request with { Authority = Authority(Guid.NewGuid(), "claim") }, escrow, typeof(ArgumentException));
        await AssertClaimFails(request with { EvidenceHash = " " }, escrow, typeof(ArgumentException));
        await AssertClaimFails(request with { EvidenceHash = new string('x', 129) }, escrow, typeof(ArgumentException));
        await AssertClaimFails(request, escrow with { Status = BountyStatus.Claimed },
            typeof(BountyTerminalConflictException));
        await AssertClaimFails(request with { ClaimedAt = escrow.ExpiresAt }, escrow,
            typeof(BountyExpiredException));
        await AssertClaimFails(request, escrow with { PosterId = request.ClaimantId },
            typeof(BountyClaimIneligibleException));
        await AssertClaimFails(request, escrow with { PosterWalletId = request.ClaimantWalletId },
            typeof(BountyClaimIneligibleException));
        await AssertClaimFails(request, escrow with { EscrowWalletId = request.ClaimantWalletId },
            typeof(BountyClaimIneligibleException));
        await AssertClaimFails(request, escrow with { Fragments = [] },
            typeof(RegisteredPostingRejectedException));
        await AssertClaimFails(request, escrow with
        {
            Fragments = [escrow.Fragments[0] with { EscrowLotId = null }]
        }, typeof(RegisteredPostingRejectedException));
    }

    [Fact]
    public async Task ClaimRejectsEveryIdempotentReplayMismatch()
    {
        var request = ClaimRequest();
        var escrow = ClaimEscrow(request);
        var valid = ClaimTerminal(request);
        var conflicts = new[]
        {
            valid with { BountyId = BountyId.New() },
            valid with { Status = BountyStatus.Reclaimed },
            valid with { ActorId = Guid.NewGuid() },
            valid with { DestinationWalletId = WalletId.New() },
            valid with { RiskDecisionId = Guid.NewGuid() },
            valid with { RiskDecisionId = null }
        };

        foreach (var conflict in conflicts)
        {
            var workflow = new PostgreSqlDurableBountyClaimWorkflow(
                new RecordingContext(), new ScriptedEscrows(escrow), new ScriptedTerminals(conflict),
                new RecordingPostings(), new NoOpClaimWriter());
            await FluentActions.Invoking(() => workflow.ClaimAsync(request))
                .Should().ThrowAsync<BountyIdempotencyConflictException>();
        }
    }

    [Fact]
    public async Task ReclaimReplaysInsideTheTransactionAndRequiresPersistedTerminalEvidence()
    {
        var request = ReclaimRequest();
        var escrow = ReclaimEscrow(request);
        var replay = ReclaimTerminal(request);
        var replayContext = new RecordingContext();
        var replayTerminals = new ScriptedTerminals(null, replay);
        var replayWorkflow = new PostgreSqlDurableBountyReclaimWorkflow(
            replayContext, new ScriptedEscrows(escrow), replayTerminals,
            new RecordingPostings(), new RecordingReclaimWriter(replayTerminals, request));

        (await replayWorkflow.ReclaimAsync(request)).Should().BeSameAs(replay);
        replayContext.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();

        var missingContext = new RecordingContext();
        var missingTerminals = new ScriptedTerminals(null, null, null);
        var missingWorkflow = new PostgreSqlDurableBountyReclaimWorkflow(
            missingContext, new ScriptedEscrows(escrow), missingTerminals,
            new RecordingPostings(), new RecordingReclaimWriter(missingTerminals, request, persist: false));
        await FluentActions.Invoking(() => missingWorkflow.ReclaimAsync(request))
            .Should().ThrowAsync<RegisteredPostingRejectedException>();
        missingContext.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ReclaimRejectsEveryRequestAndEscrowBoundary()
    {
        var request = ReclaimRequest();
        var escrow = ReclaimEscrow(request);

        await AssertReclaimFails(null!, escrow, typeof(ArgumentNullException));
        await AssertReclaimFails(request with { PosterId = Guid.Empty }, escrow, typeof(ArgumentException));
        await AssertReclaimFails(
            request with { Authority = Authority(Guid.NewGuid(), "reclaim") }, escrow, typeof(ArgumentException));
        await AssertReclaimFails(request, escrow with { Status = BountyStatus.Reclaimed },
            typeof(BountyTerminalConflictException));
        await AssertReclaimFails(request with { ReclaimedAt = escrow.ExpiresAt.AddTicks(-1) }, escrow,
            typeof(BountyNotExpiredException));
        await AssertReclaimFails(request, escrow with { PosterId = Guid.NewGuid() },
            typeof(BountyOwnershipException));
        await AssertReclaimFails(request, escrow with { PosterWalletId = WalletId.New() },
            typeof(BountyOwnershipException));
        await AssertReclaimFails(request, escrow with { Fragments = [] },
            typeof(RegisteredPostingRejectedException));
        await AssertReclaimFails(request, escrow with
        {
            Fragments = [escrow.Fragments[0] with { EscrowLotId = null }]
        }, typeof(RegisteredPostingRejectedException));
    }

    [Fact]
    public async Task ReclaimRejectsEveryIdempotentReplayMismatch()
    {
        var request = ReclaimRequest();
        var escrow = ReclaimEscrow(request);
        var valid = ReclaimTerminal(request);
        var conflicts = new[]
        {
            valid with { BountyId = BountyId.New() },
            valid with { Status = BountyStatus.Claimed },
            valid with { ActorId = Guid.NewGuid() },
            valid with { DestinationWalletId = WalletId.New() },
            valid with { RiskDecisionId = Guid.NewGuid() },
            valid with { RiskDecisionId = null }
        };

        foreach (var conflict in conflicts)
        {
            var workflow = new PostgreSqlDurableBountyReclaimWorkflow(
                new RecordingContext(), new ScriptedEscrows(escrow), new ScriptedTerminals(conflict),
                new RecordingPostings(), new NoOpReclaimWriter());
            await FluentActions.Invoking(() => workflow.ReclaimAsync(request))
                .Should().ThrowAsync<BountyIdempotencyConflictException>();
        }
    }

    [Fact]
    public async Task EscrowPostReplaysInsideTransactionAndRejectsIdempotencyCollision()
    {
        var request = PostRequest(10);
        var replay = PersistedPost(request);
        var context = new RecordingContext();
        var store = new ScriptedPostStore(replay, null, replay);
        var workflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            context, new RecordingLotReader([]), new ScriptedReservations([]), store,
            new RecordingPostings());

        (await workflow.PostAsync(request)).Should().BeSameAs(replay);
        context.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();

        var collision = replay with { Id = BountyId.New() };
        var collisionWorkflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            new RecordingContext(), new RecordingLotReader([]), new ScriptedReservations([]),
            new ScriptedPostStore(collision, collision), new RecordingPostings());
        await FluentActions.Invoking(() => collisionWorkflow.PostAsync(request))
            .Should().ThrowAsync<BountyIdempotencyConflictException>();
    }

    [Fact]
    public async Task EscrowPostRejectsEveryRequestBoundary()
    {
        var request = PostRequest(10);
        await AssertPostFails(null!, typeof(ArgumentNullException));
        await AssertPostFails(request with { Eligibility = null! }, typeof(ArgumentNullException));
        await AssertPostFails(request with { RequestHash = new string('x', 129) }, typeof(ArgumentException));
        await AssertPostFails(request with { PosterId = Guid.Empty }, typeof(ArgumentException));
        await AssertPostFails(
            request with { Authority = Authority(Guid.NewGuid(), "post") }, typeof(ArgumentException));
        await AssertPostFails(
            request with { EscrowWalletId = request.PosterWalletId }, typeof(ArgumentException));
        await AssertPostFails(request with { ExpiresAt = request.PostedAt }, typeof(ArgumentException));
        await AssertPostFails(request with { Amount = default }, typeof(ArgumentOutOfRangeException));
        await AssertPostFails(request with { ReclaimFeePpm = 1_000_000 }, typeof(ArgumentOutOfRangeException));
    }

    [Fact]
    public async Task EscrowPostComparesMultipleReservationRangesAndAmountConservation()
    {
        var request = PostRequest(10);
        var first = Lot(request.PosterWalletId, 6, ProvenanceKind.PurchasedHard, 1);
        var second = Lot(request.PosterWalletId, 4, ProvenanceKind.EarnedHard, 2);
        var firstReservation = Reservation(request, first, 6);
        var secondReservation = Reservation(request, second, 4);
        var successContext = new RecordingContext();
        var successWorkflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            successContext, new RecordingLotReader([first, second]),
            new ScriptedReservations([firstReservation], [secondReservation]),
            new ScriptedPostStore(PersistedPost(request), null, null), new RecordingPostings());

        (await successWorkflow.PostAsync(request)).Id.Should().Be(request.Id);
        successContext.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();

        var oneLotRequest = PostRequest(10);
        var lot = Lot(oneLotRequest.PosterWalletId, 10, ProvenanceKind.PurchasedHard, 1);
        var wrongAmount = Reservation(oneLotRequest, lot, 9);
        var mismatchContext = new RecordingContext();
        var mismatchWorkflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            mismatchContext, new RecordingLotReader([lot]), new ScriptedReservations([wrongAmount]),
            new ScriptedPostStore(PersistedPost(oneLotRequest), null, null), new RecordingPostings());
        await FluentActions.Invoking(() => mismatchWorkflow.PostAsync(oneLotRequest))
            .Should().ThrowAsync<RegisteredPostingRejectedException>();
        mismatchContext.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    private static async Task AssertClaimFails(
        DurableBountyClaimRequest request,
        PersistedBountyEscrow escrow,
        Type expected)
    {
        var workflow = new PostgreSqlDurableBountyClaimWorkflow(
            new RecordingContext(), new ScriptedEscrows(escrow), new ScriptedTerminals(),
            new RecordingPostings(), new NoOpClaimWriter());
        var exception = await Record.ExceptionAsync(() => workflow.ClaimAsync(request));
        exception.Should().BeOfType(expected);
    }

    private static async Task AssertReclaimFails(
        DurableBountyReclaimRequest request,
        PersistedBountyEscrow escrow,
        Type expected)
    {
        var workflow = new PostgreSqlDurableBountyReclaimWorkflow(
            new RecordingContext(), new ScriptedEscrows(escrow), new ScriptedTerminals(),
            new RecordingPostings(), new NoOpReclaimWriter());
        var exception = await Record.ExceptionAsync(() => workflow.ReclaimAsync(request));
        exception.Should().BeOfType(expected);
    }

    private static async Task AssertPostFails(DurableBountyEscrowPostRequest request, Type expected)
    {
        var workflow = new PostgreSqlDurableBountyEscrowPostWorkflow(
            new RecordingContext(), new RecordingLotReader([]), new ScriptedReservations([]),
            new ScriptedPostStore(PersistedPost(PostRequest(1))), new RecordingPostings());
        var exception = await Record.ExceptionAsync(() => workflow.PostAsync(request));
        exception.Should().BeOfType(expected);
    }

    private static DurableBountyClaimRequest ClaimRequest()
    {
        var claimant = Guid.NewGuid();
        return new DurableBountyClaimRequest(
            BountyId.New(), claimant, WalletId.New(), Now,
            new IdempotencyKey($"claim-{Guid.NewGuid():N}"), "evidence",
            Authority(claimant, "claim"), new ReserveVersion(1), new PolicyVersion(1));
    }

    private static DurableBountyReclaimRequest ReclaimRequest()
    {
        var poster = Guid.NewGuid();
        return new DurableBountyReclaimRequest(
            BountyId.New(), poster, WalletId.New(), Now,
            new IdempotencyKey($"reclaim-{Guid.NewGuid():N}"),
            Authority(poster, "reclaim"), new ReserveVersion(1), new PolicyVersion(1));
    }

    private static DurableBountyEscrowPostRequest PostRequest(long units)
    {
        var poster = Guid.NewGuid();
        return new DurableBountyEscrowPostRequest(
            BountyId.New(), poster, WalletId.New(), WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, units), BountyEligibilityRequirements.None, 0,
            Now, Now.AddDays(1), new IdempotencyKey($"post-{Guid.NewGuid():N}"), "hash",
            Authority(poster, "post"), new ReserveVersion(1), new PolicyVersion(1));
    }

    private static RegisteredPostingAuthority Authority(Guid actor, string operation) => new(
        Guid.NewGuid(), actor, Guid.NewGuid(), Guid.NewGuid(), operation, 1);

    private static PersistedBountyEscrow ClaimEscrow(DurableBountyClaimRequest request) => new(
        request.BountyId, request.Authority.TenantId, Guid.NewGuid(), WalletId.New(), WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 10), BountyEligibilityRequirements.None, 0,
        BountyStatus.Open, new IdempotencyKey("post"), "hash", Now.AddDays(-1), Now.AddDays(1), 1,
        [Fragment(10)]);

    private static PersistedBountyEscrow ReclaimEscrow(DurableBountyReclaimRequest request) => new(
        request.BountyId, request.Authority.TenantId, request.PosterId, request.PosterWalletId, WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 10), BountyEligibilityRequirements.None, 0,
        BountyStatus.Open, new IdempotencyKey("post"), "hash", Now.AddDays(-2), Now.AddDays(-1), 1,
        [Fragment(10)]);

    private static PersistedBountyEscrowFragment Fragment(long units) => new(
        CreditLotId.New(), CreditLotId.New(), new CoinAmount(CurrencyCode.HardCoin, units),
        ProvenanceKind.PurchasedHard, 1000,
        [new RootTraceRange(SourceStampId.New(), 0, checked(units * 1000), 0)]);

    private static PersistedBountyTerminalEvent ClaimTerminal(DurableBountyClaimRequest request) => new(
        Guid.NewGuid(), request.Authority.TenantId, request.BountyId, BountyStatus.Claimed, request.ClaimantId,
        request.ClaimantWalletId, request.IdempotencyKey, request.Authority.RiskDecisionId,
        SourceStampId.New(), CreditLotId.New(), 0, 0, 1, [], request.ClaimedAt);

    private static PersistedBountyTerminalEvent ReclaimTerminal(DurableBountyReclaimRequest request) => new(
        Guid.NewGuid(), request.Authority.TenantId, request.BountyId, BountyStatus.Reclaimed, request.PosterId,
        request.PosterWalletId, request.IdempotencyKey, request.Authority.RiskDecisionId,
        null, null, 10, 0, 1, [], request.ReclaimedAt);

    private static PersistedBountyEscrow PersistedPost(DurableBountyEscrowPostRequest request) => new(
        request.Id, request.Authority.TenantId, request.PosterId, request.PosterWalletId, request.EscrowWalletId, request.Amount,
        request.Eligibility, request.ReclaimFeePpm, BountyStatus.Open, request.IdempotencyKey,
        request.RequestHash, request.PostedAt, request.ExpiresAt, 1, []);

    private static CreditLot Lot(
        WalletId wallet,
        long units,
        ProvenanceKind provenance,
        long sequence)
    {
        var root = SourceStampId.New();
        return new CreditLot(
            CreditLotId.New(), wallet, new CoinAmount(CurrencyCode.HardCoin, units), provenance,
            Now.AddDays(-2).AddTicks(sequence), Now.AddDays(-1), sequence, CreditLotState.Active,
            [new RootTraceRange(root, 0, checked(units * 1000), 0)], 1000);
    }

    private static PersistedFragmentReservation Reservation(
        DurableBountyEscrowPostRequest request,
        CreditLot lot,
        long amountUnits) => new(
        Guid.NewGuid(), request.Id.Value, lot.Id, lot.Ranges[0].Root, lot.Ranges[0].Epoch,
        lot.Ranges[0], new CoinAmount(CurrencyCode.HardCoin, amountUnits));

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
        { CommitCalled = true; return Task.CompletedTask; }
        public void Rollback() => RollbackCalled = true;
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        { RollbackCalled = true; return Task.CompletedTask; }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ScriptedEscrows(PersistedBountyEscrow escrow) : IBountyEscrowStore
    {
        public PersistedBountyEscrow Get(Guid tenantId, BountyId bountyId) =>
            tenantId == escrow.TenantId ? escrow : throw new KeyNotFoundException();
        public PersistedBountyEscrow? FindPostReplay(Guid tenantId, IdempotencyKey idempotencyKey, string requestHash) => null;
        public PersistedBountyEscrow Create(CreateBountyEscrowPersistenceCommand command) => escrow;
    }

    private sealed class ScriptedTerminals(params PersistedBountyTerminalEvent?[] results)
        : IBountyTerminalEventStore
    {
        private readonly Queue<PersistedBountyTerminalEvent?> _results = new(results);
        private PersistedBountyTerminalEvent? _current;
        public PersistedBountyTerminalEvent? FindByBounty(Guid tenantId, BountyId bountyId) =>
            _current?.TenantId == tenantId && _current.BountyId == bountyId ? _current : null;
        public PersistedBountyTerminalEvent? FindByIdempotency(Guid tenantId, IdempotencyKey idempotencyKey) =>
            (_results.Count > 0 ? _results.Dequeue() : _current) is { } result && result.TenantId == tenantId
                ? result
                : null;
        public void Set(PersistedBountyTerminalEvent value) => _current = value;
    }

    private sealed class RecordingPostings : IRegisteredPostingGateway
    {
        public RegisteredPostingReceipt Post(RegisteredPostingRequest request) =>
            new(request.Posting.Id, 1, "hash", false);
    }

    private sealed class RecordingClaimWriter(
        ScriptedTerminals terminals,
        DurableBountyClaimRequest request,
        bool persist = true) : IBountyTerminalClaimWriter
    {
        public void Complete(BountyClaimTerminalWriteCommand command)
        { if (persist) terminals.Set(ClaimTerminal(request)); }
    }

    private sealed class NoOpClaimWriter : IBountyTerminalClaimWriter
    { public void Complete(BountyClaimTerminalWriteCommand command) { } }

    private sealed class RecordingReclaimWriter(
        ScriptedTerminals terminals,
        DurableBountyReclaimRequest request,
        bool persist = true) : IBountyTerminalReclaimWriter
    {
        public void Complete(BountyReclaimTerminalWriteCommand command)
        { if (persist) terminals.Set(ReclaimTerminal(request)); }
    }

    private sealed class NoOpReclaimWriter : IBountyTerminalReclaimWriter
    { public void Complete(BountyReclaimTerminalWriteCommand command) { } }

    private sealed class ScriptedPostStore(
        PersistedBountyEscrow persisted,
        params PersistedBountyEscrow?[] replays) : IBountyEscrowStore
    {
        private readonly Queue<PersistedBountyEscrow?> _replays = new(replays);
        public PersistedBountyEscrow Get(Guid tenantId, BountyId bountyId) => persisted;
        public PersistedBountyEscrow? FindPostReplay(Guid tenantId, IdempotencyKey idempotencyKey, string requestHash) =>
            (_replays.Count > 0 ? _replays.Dequeue() : null) is { } replay && replay.TenantId == tenantId
                ? replay
                : null;
        public PersistedBountyEscrow Create(CreateBountyEscrowPersistenceCommand command) => persisted;
    }

    private sealed class RecordingLotReader(IReadOnlyList<CreditLot> lots) : IBountyPostableLotReader
    {
        public IReadOnlyList<CreditLot> Read(WalletId walletId, CurrencyCode currency, DateTimeOffset asOf) => lots;
    }

    private sealed class ScriptedReservations(params IReadOnlyList<PersistedFragmentReservation>[] batches)
        : IFifoFragmentReservationGateway
    {
        private readonly Queue<IReadOnlyList<PersistedFragmentReservation>> _batches = new(batches);
        public IReadOnlyList<PersistedFragmentReservation> Reserve(FifoFragmentReservationRequest request) =>
            _batches.Count > 0 ? _batches.Dequeue() : [];
        public long Transition(
            Guid operationId,
            PersistedFragmentReservationStatus expected,
            PersistedFragmentReservationStatus next,
            DateTimeOffset terminalAt) => batches.Sum(batch => batch.Count);
    }
}
