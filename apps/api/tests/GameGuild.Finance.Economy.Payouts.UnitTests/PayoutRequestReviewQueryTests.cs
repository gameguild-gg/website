using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Payouts.Queries;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PayoutRequestReviewQueryTests
{
    [Fact]
    public async Task ReviewQueueDerivesTheTenantFromTheAdministratorContext()
    {
        var tenantId = Guid.NewGuid();
        var older = Request(PayoutRequestState.Submitted, DateTimeOffset.UtcNow.AddMinutes(-2));
        var latest = Request(PayoutRequestState.AwaitingSecondApproval, DateTimeOffset.UtcNow.AddMinutes(-1));
        var store = new ReviewStore([latest, older]);
        var handler = new ListPayoutRequestsForReviewQueryHandler(Administrator(tenantId), store);

        var result = await handler.Handle(new ListPayoutRequestsForReviewQuery(10), CancellationToken.None);

        store.LastTenantId.Should().Be(tenantId);
        result.Select(item => item.Id).Should().Equal(older.Id, latest.Id);
    }

    [Fact]
    public async Task ReviewAuditDerivesTheTenantFromTheAdministratorContext()
    {
        var tenantId = Guid.NewGuid();
        var request = Request(PayoutRequestState.Approved, DateTimeOffset.UtcNow.AddMinutes(-1));
        var audit = new PayoutRequestReviewAuditEvent(
            Guid.NewGuid(),
            request.Id,
            tenantId,
            Guid.NewGuid(),
            PayoutRequestState.Approved,
            "Identity and risk review passed.",
            DateTimeOffset.UtcNow);
        var store = new ReviewStore([request], [audit]);
        var handler = new ListPayoutRequestReviewAuditQueryHandler(Administrator(tenantId), store);

        var result = await handler.Handle(new ListPayoutRequestReviewAuditQuery(request.Id), CancellationToken.None);

        store.LastTenantId.Should().Be(tenantId);
        result.Should().ContainSingle(item =>
            item.ActorId == audit.ActorId &&
            item.Outcome == PayoutRequestState.Approved &&
            item.Reason == audit.Reason);
    }

    [Fact]
    public async Task ReviewQueueRejectsEveryMissingAdministratorContextPredicate()
    {
        var tenantId = Guid.NewGuid();

        foreach (var actor in NonAdministratorContexts(tenantId))
        {
            var handler = new ListPayoutRequestsForReviewQueryHandler(actor, new ReviewStore([]));

            await FluentActions.Awaiting(async () => await handler.Handle(
                    new ListPayoutRequestsForReviewQuery(10),
                    CancellationToken.None))
                .Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }

    [Fact]
    public async Task ReviewAuditRejectsEveryMissingAdministratorContextPredicate()
    {
        var tenantId = Guid.NewGuid();

        foreach (var actor in NonAdministratorContexts(tenantId))
        {
            var handler = new ListPayoutRequestReviewAuditQueryHandler(actor, new ReviewStore([]));

            await FluentActions.Awaiting(async () => await handler.Handle(
                    new ListPayoutRequestReviewAuditQuery(Guid.NewGuid()),
                    CancellationToken.None))
                .Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }

    private static ActorContextAccessor Administrator(Guid tenantId)
    {
        return Actor(true, Guid.NewGuid().ToString(), tenantId, true);
    }

    private static IEnumerable<ActorContextAccessor> NonAdministratorContexts(Guid tenantId)
    {
        yield return Actor(false, Guid.NewGuid().ToString(), tenantId, true);
        yield return Actor(true, "not-a-guid", tenantId, true);
        yield return Actor(true, Guid.NewGuid().ToString(), null, true);
        yield return Actor(true, Guid.NewGuid().ToString(), tenantId, false);
    }

    private static ActorContextAccessor Actor(
        bool isAuthenticated,
        string subjectId,
        Guid? tenantId,
        bool hasAdministratorPermission)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = subjectId,
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = hasAdministratorPermission
                ? new HashSet<string> { EconomyPermission.Keys.ReviewPayouts }
                : new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = isAuthenticated
        });
        return accessor;
    }

    private static PayoutRequest Request(PayoutRequestState state, DateTimeOffset createdAt) => new(
        Guid.NewGuid(),
        new IdempotencyKey($"request-{Guid.NewGuid():N}"),
        new string('a', 64),
        Guid.NewGuid(),
        new WalletId(Guid.NewGuid()),
        new CoinAmount(CurrencyCode.HardCoin, 250),
        state,
        1,
        createdAt,
        createdAt);

    private sealed class ReviewStore(
        IReadOnlyList<PayoutRequest> requests,
        IReadOnlyList<PayoutRequestReviewAuditEvent>? audit = null) : IPayoutRequestStore
    {
        public Guid? LastTenantId { get; private set; }

        public PayoutRequest? FindReplay(Guid tenantId, Guid payeeId, string idempotencyKey, string requestHash) =>
            requests.SingleOrDefault(item => item.TenantId == tenantId && item.PayeeId == payeeId && item.IdempotencyKey.Value == idempotencyKey);

        public void Add(PayoutRequest request) => throw new NotSupportedException();

        public PayoutRequest GetForPayee(Guid tenantId, Guid requestId, Guid payeeId) =>
            requests.Single(item => item.TenantId == tenantId && item.Id == requestId && item.PayeeId == payeeId);

        public PayoutRequest GetForReview(Guid requestId, Guid tenantId) =>
            requests.Single(item => item.Id == requestId);

        public IReadOnlyList<PayoutRequest> ListForPayee(Guid tenantId, Guid payeeId, int take) => requests
            .Where(item => item.TenantId == tenantId && item.PayeeId == payeeId)
            .Take(take)
            .ToArray();

        public IReadOnlyList<PayoutRequest> ListForReview(Guid tenantId, int take)
        {
            LastTenantId = tenantId;
            return requests
                .Where(item => item.State is PayoutRequestState.Submitted or PayoutRequestState.AwaitingSecondApproval)
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .Take(take)
                .ToArray();
        }

        public IReadOnlyList<PayoutRequestReviewAuditEvent> ListReviewAudit(Guid requestId, Guid tenantId)
        {
            LastTenantId = tenantId;
            return (audit ?? [])
                .Where(item => item.RequestId == requestId && item.TenantId == tenantId)
                .ToArray();
        }

        public PayoutRequest Update(PayoutRequest request, long expectedVersion) => throw new NotSupportedException();

        public PayoutRequest Review(
            PayoutRequest request,
            long expectedVersion,
            Guid tenantId,
            Guid reviewerId,
            PayoutRequestState outcome,
            string reason) => throw new NotSupportedException();
    }
}
