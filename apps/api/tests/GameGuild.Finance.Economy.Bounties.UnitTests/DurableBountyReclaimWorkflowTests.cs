using FluentAssertions;
using GameGuild;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class DurableBountyReclaimWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reclaim_PostsEscrowFragmentsAndFinalizesTheTerminalEvidenceInOneTransaction()
    {
        var request = CreateRequest();
        var escrow = CreateEscrow(request);
        var context = new RecordingContext();
        var terminals = new RecordingTerminalStore();
        var postings = new RecordingPostings();
        var writer = new RecordingTerminalWriter(terminals, request);
        var workflow = new PostgreSqlDurableBountyReclaimWorkflow(
            context, new RecordingEscrows(escrow), terminals, postings, writer);

        var completed = await workflow.ReclaimAsync(request);

        completed.BountyId.Should().Be(request.BountyId);
        completed.Status.Should().Be(BountyStatus.Reclaimed);
        postings.Requests.Should().ContainSingle().Which.Posting.Template.Kind.Should().Be(PostingTemplateKind.BountyReclaim);
        writer.Commands.Should().ContainSingle().Which.Should().Match<BountyReclaimTerminalWriteCommand>(command =>
            command.BountyId == request.BountyId &&
            command.PosterId == request.PosterId &&
            command.PosterWalletId == request.PosterWalletId &&
            command.PostingId == postings.Requests.Single().Posting.Id &&
            command.RiskDecisionId == request.Authority.RiskDecisionId);
        context.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Reclaim_ReturnsExistingTerminalEventWithoutOpeningATransaction()
    {
        var request = CreateRequest();
        var existing = CreateTerminal(request);
        var context = new RecordingContext();
        var terminals = new RecordingTerminalStore(existing);
        var workflow = new PostgreSqlDurableBountyReclaimWorkflow(
            context, new RecordingEscrows(CreateEscrow(request)), terminals,
            new RecordingPostings(), new RecordingTerminalWriter(terminals, request));

        var completed = await workflow.ReclaimAsync(request);

        completed.Should().BeSameAs(existing);
        context.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Reclaim_RollsBackWhenTheSpecializedTerminalWriterRejectsThePosting()
    {
        var request = CreateRequest();
        var context = new RecordingContext();
        var terminals = new RecordingTerminalStore();
        var workflow = new PostgreSqlDurableBountyReclaimWorkflow(
            context, new RecordingEscrows(CreateEscrow(request)), terminals,
            new RecordingPostings(), new RecordingTerminalWriter(terminals, request, shouldThrow: true));

        await FluentActions.Invoking(() => workflow.ReclaimAsync(request))
            .Should().ThrowAsync<RegisteredPostingRejectedException>();

        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Reclaim_RejectsRequestsBeforeExpiryBeforeOpeningATransaction()
    {
        var request = CreateRequest() with { ReclaimedAt = Now };
        var context = new RecordingContext();
        var terminals = new RecordingTerminalStore();
        var escrow = CreateEscrow(request) with { ExpiresAt = Now.AddMinutes(1) };
        var workflow = new PostgreSqlDurableBountyReclaimWorkflow(
            context, new RecordingEscrows(escrow), terminals,
            new RecordingPostings(), new RecordingTerminalWriter(terminals, request));

        await FluentActions.Invoking(() => workflow.ReclaimAsync(request))
            .Should().ThrowAsync<BountyNotExpiredException>();

        context.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Reclaim_RejectsForeignTenantAndUnscopedAuthority()
    {
        var request = CreateRequest();
        var foreign = CreateEscrow(request) with { TenantId = Guid.NewGuid() };
        var foreignTerminals = new RecordingTerminalStore();
        var foreignWorkflow = new PostgreSqlDurableBountyReclaimWorkflow(
            new RecordingContext(), new RecordingEscrows(foreign, ignoreTenant: true),
            foreignTerminals, new RecordingPostings(),
            new RecordingTerminalWriter(foreignTerminals, request));
        await FluentActions.Invoking(() => foreignWorkflow.ReclaimAsync(request))
            .Should().ThrowAsync<BountyOwnershipException>();

        var unscoped = CreateRequest();
        SetTenant(unscoped.Authority, Guid.Empty);
        var unscopedTerminals = new RecordingTerminalStore();
        var unscopedWorkflow = new PostgreSqlDurableBountyReclaimWorkflow(
            new RecordingContext(), new RecordingEscrows(CreateEscrow(unscoped)),
            unscopedTerminals, new RecordingPostings(),
            new RecordingTerminalWriter(unscopedTerminals, unscoped));
        await FluentActions.Invoking(() => unscopedWorkflow.ReclaimAsync(unscoped))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reclaim_ExpiredBountyRequiresSuccessfulLockedPreparation(bool prepared)
    {
        var request = CreateRequest();
        var escrow = CreateEscrow(request) with { Status = BountyStatus.Expired };
        var context = new RecordingContext();
        var terminals = new RecordingTerminalStore();
        var escrows = new RecordingEscrows(escrow);
        var workflow = new PostgreSqlDurableBountyReclaimWorkflow(
            context, escrows, terminals, new RecordingPostings(),
            new RecordingTerminalWriter(terminals, request), new ExpirationTransition(prepared, escrows));

        if (prepared)
        {
            var result = await workflow.ReclaimAsync(request);
            result.Status.Should().Be(BountyStatus.Reclaimed);
            context.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();
        }
        else
        {
            await FluentActions.Invoking(() => workflow.ReclaimAsync(request))
                .Should().ThrowAsync<BountyTerminalConflictException>();
            context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
        }
    }

    [Fact]
    public void Constructor_RejectsEveryNullPort()
    {
        var request = CreateRequest();
        var context = new RecordingContext();
        var escrow = new RecordingEscrows(CreateEscrow(request));
        var terminals = new RecordingTerminalStore();
        var postings = new RecordingPostings();
        var writer = new RecordingTerminalWriter(terminals, request);
        var expiration = new ExpirationTransition(true);
        Action[] actions =
        [
            () => new PostgreSqlDurableBountyReclaimWorkflow(null!, escrow, terminals, postings, writer, expiration),
            () => new PostgreSqlDurableBountyReclaimWorkflow(context, null!, terminals, postings, writer, expiration),
            () => new PostgreSqlDurableBountyReclaimWorkflow(context, escrow, null!, postings, writer, expiration),
            () => new PostgreSqlDurableBountyReclaimWorkflow(context, escrow, terminals, null!, writer, expiration),
            () => new PostgreSqlDurableBountyReclaimWorkflow(context, escrow, terminals, postings, null!, expiration),
            () => new PostgreSqlDurableBountyReclaimWorkflow(context, escrow, terminals, postings, writer, null!)
        ];

        actions.Should().AllSatisfy(action =>
            FluentActions.Invoking(action).Should().Throw<ArgumentNullException>());
    }

    private static DurableBountyReclaimRequest CreateRequest()
    {
        var posterId = Guid.NewGuid();
        return new DurableBountyReclaimRequest(
            BountyId.New(), posterId, WalletId.New(), Now,
            new IdempotencyKey($"reclaim-{Guid.NewGuid():N}"),
            new RegisteredPostingAuthority(Guid.NewGuid(), posterId, Guid.NewGuid(), Guid.NewGuid(), "bounty-reclaim", 1),
            new ReserveVersion(2), new PolicyVersion(3));
    }

    private static void SetTenant(RegisteredPostingAuthority authority, Guid tenantId) =>
        typeof(RegisteredPostingAuthority)
            .GetField("<TenantId>k__BackingField", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .SetValue(authority, tenantId);

    private static PersistedBountyEscrow CreateEscrow(DurableBountyReclaimRequest request) => new(
        request.BountyId, request.Authority.TenantId, request.PosterId, request.PosterWalletId, WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 10), BountyEligibilityRequirements.None, 200_000,
        BountyStatus.Open, new IdempotencyKey($"post-{request.BountyId.Value:N}"), "post-hash",
        Now.AddDays(-2), Now.AddDays(-1), 1, [CreateFragment(5), CreateFragment(5)]);

    private static PersistedBountyEscrowFragment CreateFragment(long units) => new(
        CreditLotId.New(), CreditLotId.New(), new CoinAmount(CurrencyCode.HardCoin, units),
        ProvenanceKind.PurchasedHard, CurrencyTraceScale.HardCoinTraceUnitsPerCoin,
        [new RootTraceRange(SourceStampId.New(), 0, units * CurrencyTraceScale.HardCoinTraceUnitsPerCoin, 0)]);

    private static PersistedBountyTerminalEvent CreateTerminal(DurableBountyReclaimRequest request) => new(
        Guid.NewGuid(), request.Authority.TenantId, request.BountyId, BountyStatus.Reclaimed, request.PosterId, request.PosterWalletId,
        request.IdempotencyKey, request.Authority.RiskDecisionId, null, null, 8, 2, 1, [], request.ReclaimedAt);

    private sealed class RecordingEscrows(PersistedBountyEscrow escrow, bool ignoreTenant = false) : IBountyEscrowStore
    {
        public PersistedBountyEscrow Current { get; set; } = escrow;
        public PersistedBountyEscrow Get(Guid tenantId, BountyId bountyId) =>
            (ignoreTenant || tenantId == Current.TenantId) && bountyId == Current.Id ? Current : throw new KeyNotFoundException();
        public PersistedBountyEscrow? FindPostReplay(Guid tenantId, IdempotencyKey idempotencyKey, string requestHash) => null;
        public PersistedBountyEscrow Create(CreateBountyEscrowPersistenceCommand command) => throw new NotSupportedException();
    }

    private sealed class ExpirationTransition(bool prepared, RecordingEscrows? escrows = null) : IBountyExpirationTransition
    {
        public Task<bool> PrepareForReclaimAsync(BountyId bountyId, DateTimeOffset reclaimedAt,
            CancellationToken cancellationToken = default)
        {
            if (prepared && escrows is not null)
                escrows.Current = escrows.Current with { Status = BountyStatus.Open };
            return Task.FromResult(prepared);
        }
    }

    private sealed class RecordingTerminalStore(PersistedBountyTerminalEvent? existing = null) : IBountyTerminalEventStore
    {
        private PersistedBountyTerminalEvent? _event = existing;
        public PersistedBountyTerminalEvent? FindByBounty(Guid tenantId, BountyId bountyId) =>
            _event?.TenantId == tenantId && _event.BountyId == bountyId ? _event : null;
        public PersistedBountyTerminalEvent? FindByIdempotency(Guid tenantId, IdempotencyKey idempotencyKey) =>
            _event?.TenantId == tenantId && _event.IdempotencyKey == idempotencyKey ? _event : null;
        public void Set(PersistedBountyTerminalEvent value) => _event = value;
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

    private sealed class RecordingTerminalWriter(
        RecordingTerminalStore store,
        DurableBountyReclaimRequest request,
        bool shouldThrow = false) : IBountyTerminalReclaimWriter
    {
        public List<BountyReclaimTerminalWriteCommand> Commands { get; } = [];
        public void Complete(BountyReclaimTerminalWriteCommand command)
        {
            Commands.Add(command);
            if (shouldThrow)
                throw new RegisteredPostingRejectedException("Terminal writer rejected reclaim.");
            store.Set(CreateTerminal(request));
        }
    }

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
        public Task CommitAsync(CancellationToken cancellationToken = default) { CommitCalled = true; return Task.CompletedTask; }
        public void Rollback() => RollbackCalled = true;
        public Task RollbackAsync(CancellationToken cancellationToken = default) { RollbackCalled = true; return Task.CompletedTask; }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
