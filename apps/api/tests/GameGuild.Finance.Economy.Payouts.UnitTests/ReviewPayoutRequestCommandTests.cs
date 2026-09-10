using FluentAssertions;
using FluentValidation;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Payouts.Commands;
using GameGuild.Finance.Economy.Payouts.Queries;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class ReviewPayoutRequestCommandTests
{
    [Fact]
    public void Validator_AcceptsOnlyAReasonedApprovalOrRejection()
    {
        var validator = new ReviewPayoutRequestCommandValidator();

        validator.Validate(Command(PayoutRequestState.Approved, "Identity check completed.")).IsValid.Should().BeTrue();
        validator.Validate(Command(PayoutRequestState.Submitted, "Identity check completed.")).IsValid.Should().BeFalse();
        validator.Validate(Command(PayoutRequestState.Rejected, "no")).IsValid.Should().BeFalse();
        validator.Validate(Command(PayoutRequestState.Rejected, new string('x', 1001))).IsValid.Should().BeFalse();
        validator.Validate(new ReviewPayoutRequestCommand(Guid.Empty, PayoutRequestState.Approved, new ReviewPayoutRequestRequest("valid reason")))
            .IsValid.Should().BeFalse();
        validator.Validate(new ReviewPayoutRequestCommand(Guid.NewGuid(), PayoutRequestState.Approved, null!)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_UsesTenantAndActorFromAdministratorContextAndTrimsAuditReason()
    {
        var tenantId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var request = CreateRequest();
        var store = new RecordingStore(request);
        var handler = new ReviewPayoutRequestCommandHandler(Administrator(reviewerId, tenantId), store);

        var result = await handler.Handle(
            Command(PayoutRequestState.Approved, "  Identity and risk checks passed.  ", request.Id),
            CancellationToken.None);

        store.GetForReviewTenantId.Should().Be(tenantId);
        store.ReviewTenantId.Should().Be(tenantId);
        store.ReviewerId.Should().Be(reviewerId);
        store.ExpectedVersion.Should().Be(request.Version);
        store.Reason.Should().Be("Identity and risk checks passed.");
        result.Id.Should().Be(request.Id);
        result.PayeeId.Should().Be(request.PayeeId);
        result.WalletId.Should().Be(request.WalletId.Value);
        result.HardCoinUnits.Should().Be(request.Amount.Units);
        result.State.Should().Be(PayoutRequestState.AwaitingSecondApproval);
        result.Version.Should().Be(request.Version + 1);
        result.CreatedAt.Should().Be(request.CreatedAt);
        result.UpdatedAt.Should().BeOnOrAfter(request.UpdatedAt);
    }

    [Fact]
    public async Task Handler_RejectsMissingAdministratorAuthorityBeforeReadingTheRequest()
    {
        var store = new RecordingStore(CreateRequest());
        var handler = new ReviewPayoutRequestCommandHandler(new ActorContextAccessor(), store);

        await FluentActions.Awaiting(() => handler.Handle(Command(PayoutRequestState.Rejected, "Insufficient evidence."), CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        store.ReadWasAttempted.Should().BeFalse();
    }

    private static ReviewPayoutRequestCommand Command(PayoutRequestState outcome, string reason, Guid? requestId = null) =>
        new(requestId ?? Guid.NewGuid(), outcome, new ReviewPayoutRequestRequest(reason));

    private static ActorContextAccessor Administrator(Guid actorId, Guid tenantId)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = actorId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string> { EconomyPermission.Keys.ReviewPayouts },
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        });
        return accessor;
    }

    private static PayoutRequest CreateRequest() => new(
        Guid.NewGuid(),
        new IdempotencyKey($"request-{Guid.NewGuid():N}"),
        new string('a', 64),
        Guid.NewGuid(),
        new WalletId(Guid.NewGuid()),
        new CoinAmount(CurrencyCode.HardCoin, 250),
        PayoutRequestState.Submitted,
        1,
        DateTimeOffset.UtcNow.AddMinutes(-1),
        DateTimeOffset.UtcNow.AddMinutes(-1));

    private sealed class RecordingStore(PayoutRequest request) : IPayoutRequestStore
    {
        public Guid? GetForReviewTenantId { get; private set; }
        public Guid? ReviewTenantId { get; private set; }
        public Guid? ReviewerId { get; private set; }
        public long? ExpectedVersion { get; private set; }
        public string? Reason { get; private set; }
        public bool ReadWasAttempted { get; private set; }

        public PayoutRequest? FindReplay(Guid tenantId, Guid payeeId, string idempotencyKey, string requestHash) => throw new NotSupportedException();

        public void Add(PayoutRequest payoutRequest) => throw new NotSupportedException();

        public PayoutRequest GetForPayee(Guid tenantId, Guid requestId, Guid payeeId) => throw new NotSupportedException();

        public PayoutRequest GetForReview(Guid requestId, Guid tenantId)
        {
            ReadWasAttempted = true;
            GetForReviewTenantId = tenantId;
            return request;
        }

        public IReadOnlyList<PayoutRequest> ListForPayee(Guid tenantId, Guid payeeId, int take) => throw new NotSupportedException();

        public IReadOnlyList<PayoutRequest> ListForReview(Guid tenantId, int take) => throw new NotSupportedException();

        public IReadOnlyList<PayoutRequestReviewAuditEvent> ListReviewAudit(Guid requestId, Guid tenantId) => throw new NotSupportedException();

        public PayoutRequest Update(PayoutRequest payoutRequest, long expectedVersion) => throw new NotSupportedException();

        public PayoutRequest Review(
            PayoutRequest payoutRequest,
            long expectedVersion,
            Guid tenantId,
            Guid reviewerId,
            PayoutRequestState outcome,
            string reason)
        {
            ReviewTenantId = tenantId;
            ReviewerId = reviewerId;
            ExpectedVersion = expectedVersion;
            Reason = reason;
            return payoutRequest;
        }
    }
}
