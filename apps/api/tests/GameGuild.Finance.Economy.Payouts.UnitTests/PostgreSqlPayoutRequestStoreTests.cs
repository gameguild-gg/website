using FluentAssertions;
using GameGuild;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Payouts.Queries;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PostgreSqlPayoutRequestStoreTests
{
    private static readonly DateTimeOffset Time = new(2026, 8, 11, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Store_PersistsReadsReplaysAndCancelsAnOwnedRequest()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("payout_request_store");

        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString)
                .Options);
        await context.Database.MigrateAsync();

        var payeeId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ({walletId}, {payeeId}, {tenantId}, 1, {Time});
            """);

        var store = new PostgreSqlPayoutRequestStore(context);
        var request = Request(tenantId, payeeId, walletId);

        store.Add(request);
        store.GetForPayee(tenantId, request.Id, payeeId).Should().BeEquivalentTo(request);
        store.ListForPayee(tenantId, payeeId, 10).Should().ContainSingle().Which.Should().BeEquivalentTo(request);
        store.FindReplay(tenantId, payeeId, request.IdempotencyKey.Value, request.RequestHash)
            .Should().BeEquivalentTo(request);
        store.FindReplay(tenantId, payeeId, "missing", request.RequestHash).Should().BeNull();

        FluentActions.Invoking(() => store.FindReplay(tenantId, payeeId, request.IdempotencyKey.Value, "changed"))
            .Should().Throw<PayoutRequestReplayConflictException>();
        FluentActions.Invoking(() => store.Add(request with { Id = Guid.NewGuid() }))
            .Should().Throw<PayoutRequestReplayConflictException>();
        FluentActions.Invoking(() => store.GetForPayee(tenantId, Guid.NewGuid(), payeeId))
            .Should().Throw<KeyNotFoundException>();

        var cancelled = request.Cancel(Time.AddMinutes(1));
        store.Update(cancelled, request.Version).Should().BeSameAs(cancelled);
        store.GetForPayee(tenantId, request.Id, payeeId).Should().BeEquivalentTo(cancelled);
        FluentActions.Invoking(() => store.Update(cancelled, request.Version))
            .Should().Throw<PayoutRequestStaleCommandException>();
    }

    [Fact]
    public void Store_RejectsInvalidConstructionAndArguments()
    {
        FluentActions.Invoking(() => new PostgreSqlPayoutRequestStore(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlPayoutRequestStore(new NonRelationalContext()))
            .Should().Throw<InvalidOperationException>();

        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"payout-request-store-validation-{Guid.NewGuid():N}")
                .Options);
        var store = new PostgreSqlPayoutRequestStore(context);
        var request = Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        FluentActions.Invoking(() => store.FindReplay(Guid.Empty, Guid.NewGuid(), "request", "hash"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.FindReplay(Guid.NewGuid(), Guid.Empty, "request", "hash"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.FindReplay(Guid.NewGuid(), Guid.NewGuid(), "", "hash"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.FindReplay(Guid.NewGuid(), Guid.NewGuid(), "request", ""))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Add(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => store.Add(request with { TenantId = Guid.Empty }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.GetForPayee(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.GetForPayee(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.GetForPayee(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.ListForPayee(Guid.Empty, Guid.NewGuid(), 10))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.ListForPayee(Guid.NewGuid(), Guid.Empty, 10))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.ListForPayee(Guid.NewGuid(), Guid.NewGuid(), 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.ListForPayee(Guid.NewGuid(), Guid.NewGuid(), 101))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.Update(null!, 1)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => store.Update(request, 0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.GetForReview(Guid.Empty, Guid.NewGuid()))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.GetForReview(Guid.NewGuid(), Guid.Empty))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.ListForReview(Guid.Empty, 10))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.ListForReview(Guid.NewGuid(), 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.ListForReview(Guid.NewGuid(), 101))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.ListReviewAudit(Guid.Empty, Guid.NewGuid()))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.ListReviewAudit(Guid.NewGuid(), Guid.Empty))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Review(null!, 1, Guid.NewGuid(), Guid.NewGuid(), PayoutRequestState.Approved, "reason"))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => store.Review(request, 0, Guid.NewGuid(), Guid.NewGuid(), PayoutRequestState.Approved, "reason"))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.Review(request, 1, Guid.Empty, Guid.NewGuid(), PayoutRequestState.Approved, "reason"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Review(request, 1, Guid.NewGuid(), Guid.Empty, PayoutRequestState.Approved, "reason"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Review(request, 1, Guid.NewGuid(), Guid.NewGuid(), PayoutRequestState.Approved, " "))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.FindReplay(Guid.NewGuid(), Guid.NewGuid(), "request", "hash"))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => store.Add(request))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Store_ReviewsTenantScopedRequestWithTwoIndependentAdministratorsAndAuditTrail()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("payout_request_review");
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString)
                .Options);
        await context.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        var payeeId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ({walletId}, {payeeId}, {tenantId}, 1, {Time});
            """);

        var store = new PostgreSqlPayoutRequestStore(context);
        var request = Request(tenantId, payeeId, walletId);
        store.Add(request);

        store.GetForReview(request.Id, tenantId).Should().BeEquivalentTo(request);
        store.ListForReview(tenantId, 10).Should().ContainSingle().Which.Should().BeEquivalentTo(request);
        FluentActions.Invoking(() => store.GetForReview(request.Id, Guid.NewGuid()))
            .Should().Throw<KeyNotFoundException>();

        var firstReviewer = Guid.NewGuid();
        var firstReview = request.Review(firstReviewer, PayoutRequestState.Approved, Time.AddMinutes(1));
        var afterFirstApproval = store.Review(
            firstReview,
            request.Version,
            tenantId,
            firstReviewer,
            PayoutRequestState.Approved,
            " Identity and risk checks passed. ");
        afterFirstApproval.State.Should().Be(PayoutRequestState.AwaitingSecondApproval);
        afterFirstApproval.FirstApprovalActorId.Should().Be(firstReviewer);
        store.ListForReview(tenantId, 10).Should().ContainSingle().Which.State
            .Should().Be(PayoutRequestState.AwaitingSecondApproval);

        var secondReviewer = Guid.NewGuid();
        var secondReview = afterFirstApproval.Review(secondReviewer, PayoutRequestState.Approved, Time.AddMinutes(2));
        var approved = store.Review(
            secondReview,
            afterFirstApproval.Version,
            tenantId,
            secondReviewer,
            PayoutRequestState.Approved,
            " Second administrator approved payout. ");
        approved.State.Should().Be(PayoutRequestState.Approved);
        approved.FirstApprovalActorId.Should().Be(firstReviewer);
        store.ListForReview(tenantId, 10).Should().BeEmpty();

        var audit = store.ListReviewAudit(request.Id, tenantId);
        audit.Should().HaveCount(2);
        audit.Select(item => item.ActorId).Should().Equal(firstReviewer, secondReviewer);
        audit.Select(item => item.Outcome).Should().OnlyContain(outcome => outcome == PayoutRequestState.Approved);
        audit.Select(item => item.Reason).Should().Equal(
            "Identity and risk checks passed.",
            "Second administrator approved payout.");
        store.ListReviewAudit(request.Id, Guid.NewGuid()).Should().BeEmpty();

        FluentActions.Invoking(() => store.Review(
                approved,
                afterFirstApproval.Version,
                tenantId,
                secondReviewer,
                PayoutRequestState.Approved,
                "stale"))
            .Should().Throw<PayoutRequestStaleCommandException>();
    }

    [Fact]
    public void AuditRow_AndReviewAuditDto_ExposeThePersistedEvidence()
    {
        var id = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var occurredAt = Time.AddMinutes(1);
        var row = new PayoutRequestReviewAuditEventRow
        {
            Id = id,
            RequestId = requestId,
            TenantId = tenantId,
            ActorId = actorId,
            Outcome = PayoutRequestState.Rejected,
            Reason = "Evidence is insufficient.",
            OccurredAt = occurredAt
        };

        row.Id.Should().Be(id);
        row.RequestId.Should().Be(requestId);
        row.TenantId.Should().Be(tenantId);
        row.ActorId.Should().Be(actorId);
        row.Outcome.Should().Be(PayoutRequestState.Rejected);
        row.Reason.Should().Be("Evidence is insufficient.");
        row.OccurredAt.Should().Be(occurredAt);

        var dto = EconomyPayoutRequestReviewAuditDto.From(new PayoutRequestReviewAuditEvent(
            id,
            requestId,
            tenantId,
            actorId,
            row.Outcome,
            row.Reason,
            occurredAt));
        dto.Id.Should().Be(id);
        dto.ActorId.Should().Be(actorId);
        dto.Outcome.Should().Be(PayoutRequestState.Rejected);
        dto.Reason.Should().Be("Evidence is insufficient.");
        dto.OccurredAt.Should().Be(occurredAt);
    }

    private static PayoutRequest Request(Guid tenantId, Guid payeeId, Guid walletId) => new(
        Guid.NewGuid(),
        new IdempotencyKey($"request-{Guid.NewGuid():N}"),
        new string('a', 64),
        payeeId,
        new WalletId(walletId),
        new CoinAmount(CurrencyCode.HardCoin, 250),
        PayoutRequestState.Submitted,
        1,
        Time,
        Time,
        TenantId: tenantId);

    private sealed class NonRelationalContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException();
        }
    }

}
