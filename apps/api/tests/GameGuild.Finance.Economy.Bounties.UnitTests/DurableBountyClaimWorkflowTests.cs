using FluentAssertions;
using GameGuild;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class DurableBountyClaimWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Claim_PostsEscrowLotsAndFinalizesTheTerminalEvidenceInOneTransaction()
    {
        var request = CreateRequest();
        var escrow = CreateEscrow(request);
        var context = new RecordingContext();
        var terminals = new RecordingTerminalStore();
        var postings = new RecordingPostings();
        var writer = new RecordingTerminalWriter(terminals, request);
        var workflow = new PostgreSqlDurableBountyClaimWorkflow(
            context,
            new RecordingEscrows(escrow),
            terminals,
            postings,
            writer);

        var completed = await workflow.ClaimAsync(request);

        completed.BountyId.Should().Be(request.BountyId);
        completed.Status.Should().Be(BountyStatus.Claimed);
        postings.Requests.Should().ContainSingle().Which.Posting.Template.Kind.Should().Be(PostingTemplateKind.BountyClaim);
        writer.Commands.Should().ContainSingle().Which.Should().Match<BountyClaimTerminalWriteCommand>(command =>
            command.BountyId == request.BountyId &&
            command.ClaimantId == request.ClaimantId &&
            command.ClaimantWalletId == request.ClaimantWalletId &&
            command.PostingId == postings.Requests.Single().Posting.Id &&
            command.RiskDecisionId == request.Authority.RiskDecisionId);
        context.Transactions.Should().ContainSingle().Which.CommitCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Claim_ReturnsTheExistingTerminalEventWithoutOpeningATransaction()
    {
        var request = CreateRequest();
        var existing = CreateTerminal(request);
        var context = new RecordingContext();
        var terminals = new RecordingTerminalStore(existing);
        var workflow = new PostgreSqlDurableBountyClaimWorkflow(
            context,
            new RecordingEscrows(CreateEscrow(request)),
            terminals,
            new RecordingPostings(),
            new RecordingTerminalWriter(terminals, request));

        var completed = await workflow.ClaimAsync(request);

        completed.Should().BeSameAs(existing);
        context.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Claim_RollsBackWhenTheSpecializedTerminalWriterRejectsThePosting()
    {
        var request = CreateRequest();
        var context = new RecordingContext();
        var terminals = new RecordingTerminalStore();
        var workflow = new PostgreSqlDurableBountyClaimWorkflow(
            context,
            new RecordingEscrows(CreateEscrow(request)),
            terminals,
            new RecordingPostings(),
            new RecordingTerminalWriter(terminals, request, shouldThrow: true));

        await FluentActions.Invoking(() => workflow.ClaimAsync(request))
            .Should().ThrowAsync<RegisteredPostingRejectedException>();

        context.Transactions.Should().ContainSingle().Which.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Claim_RejectsRequestsOutsideTheOpenBountyWindowBeforeOpeningATransaction()
    {
        var request = CreateRequest() with { ClaimedAt = Now.AddDays(2) };
        var context = new RecordingContext();
        var terminals = new RecordingTerminalStore();
        var workflow = new PostgreSqlDurableBountyClaimWorkflow(
            context,
            new RecordingEscrows(CreateEscrow(request with { ClaimedAt = Now })),
            terminals,
            new RecordingPostings(),
            new RecordingTerminalWriter(terminals, request));

        await FluentActions.Invoking(() => workflow.ClaimAsync(request))
            .Should().ThrowAsync<BountyExpiredException>();

        context.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Claim_RejectsForeignTenantAndUnscopedAuthority()
    {
        var request = CreateRequest();
        var foreign = CreateEscrow(request) with { TenantId = Guid.NewGuid() };
        var foreignTerminals = new RecordingTerminalStore();
        var foreignWorkflow = new PostgreSqlDurableBountyClaimWorkflow(
            new RecordingContext(), new RecordingEscrows(foreign, ignoreTenant: true),
            foreignTerminals, new RecordingPostings(),
            new RecordingTerminalWriter(foreignTerminals, request));
        await FluentActions.Invoking(() => foreignWorkflow.ClaimAsync(request))
            .Should().ThrowAsync<BountyClaimIneligibleException>();

        var unscoped = CreateRequest();
        SetTenant(unscoped.Authority, Guid.Empty);
        var unscopedTerminals = new RecordingTerminalStore();
        var unscopedWorkflow = new PostgreSqlDurableBountyClaimWorkflow(
            new RecordingContext(), new RecordingEscrows(CreateEscrow(unscoped)),
            unscopedTerminals, new RecordingPostings(),
            new RecordingTerminalWriter(unscopedTerminals, unscoped));
        await FluentActions.Invoking(() => unscopedWorkflow.ClaimAsync(unscoped))
            .Should().ThrowAsync<ArgumentException>();
    }

    private static DurableBountyClaimRequest CreateRequest()
    {
        var claimantId = Guid.NewGuid();
        return new DurableBountyClaimRequest(
            BountyId.New(),
            claimantId,
            WalletId.New(),
            Now,
            new IdempotencyKey($"claim-{Guid.NewGuid():N}"),
            "claim-evidence",
            new RegisteredPostingAuthority(Guid.NewGuid(), claimantId, Guid.NewGuid(), Guid.NewGuid(), "bounty-claim", 1),
            new ReserveVersion(2),
            new PolicyVersion(3));
    }

    private static void SetTenant(RegisteredPostingAuthority authority, Guid tenantId) =>
        typeof(RegisteredPostingAuthority)
            .GetField("<TenantId>k__BackingField", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .SetValue(authority, tenantId);

    private static PersistedBountyEscrow CreateEscrow(DurableBountyClaimRequest request) => new(
        request.BountyId,
        request.Authority.TenantId,
        Guid.NewGuid(),
        WalletId.New(),
        WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 10),
        BountyEligibilityRequirements.None,
        0,
        BountyStatus.Open,
        new IdempotencyKey($"post-{request.BountyId.Value:N}"),
        "post-hash",
        Now.AddDays(-1),
        Now.AddDays(1),
        1,
        [CreateFragment(5), CreateFragment(5)]);

    private static PersistedBountyEscrowFragment CreateFragment(long units) => new(
        CreditLotId.New(),
        CreditLotId.New(),
        new CoinAmount(CurrencyCode.HardCoin, units),
        ProvenanceKind.PurchasedHard,
        CurrencyTraceScale.HardCoinTraceUnitsPerCoin,
        [new RootTraceRange(SourceStampId.New(), 0, units * CurrencyTraceScale.HardCoinTraceUnitsPerCoin, 0)]);

    private static PersistedBountyTerminalEvent CreateTerminal(DurableBountyClaimRequest request) => new(
        Guid.NewGuid(),
        request.Authority.TenantId,
        request.BountyId,
        BountyStatus.Claimed,
        request.ClaimantId,
        request.ClaimantWalletId,
        request.IdempotencyKey,
        request.Authority.RiskDecisionId,
        SourceStampId.New(),
        CreditLotId.New(),
        0,
        0,
        1,
        [],
        request.ClaimedAt);

    private sealed class RecordingEscrows(PersistedBountyEscrow escrow, bool ignoreTenant = false) : IBountyEscrowStore
    {
        public PersistedBountyEscrow Get(Guid tenantId, BountyId bountyId) =>
            (ignoreTenant || tenantId == escrow.TenantId) && bountyId == escrow.Id
            ? escrow
            : throw new KeyNotFoundException();
        public PersistedBountyEscrow? FindPostReplay(Guid tenantId, IdempotencyKey idempotencyKey, string requestHash) => null;
        public PersistedBountyEscrow Create(CreateBountyEscrowPersistenceCommand command) => throw new NotSupportedException();
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
        DurableBountyClaimRequest request,
        bool shouldThrow = false) : IBountyTerminalClaimWriter
    {
        public List<BountyClaimTerminalWriteCommand> Commands { get; } = [];
        public void Complete(BountyClaimTerminalWriteCommand command)
        {
            Commands.Add(command);
            if (shouldThrow)
                throw new RegisteredPostingRejectedException("Terminal writer rejected claim.");
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
