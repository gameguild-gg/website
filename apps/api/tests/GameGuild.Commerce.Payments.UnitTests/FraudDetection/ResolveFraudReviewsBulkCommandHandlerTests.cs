using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.FraudDetection;

public class ResolveFraudReviewsBulkCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<IRevenueAuditService> _auditService = new();
    private readonly Mock<IActorContextAccessor> _actorContextAccessor = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _reviewerId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ShouldResolveEveryPendingReview_AndAuditEachTransition()
    {
        var first = ReviewHeldPayment();
        var second = ReviewHeldPayment();
        SetupActor(systemAdmin: false);
        SetupPayment(first);
        SetupPayment(second);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ResolveFraudReviewsBulkCommand([first.Id, second.Id], Approve: true, "verified cohort"),
            CancellationToken.None);

        result.Requested.Should().Be(2);
        result.Resolved.Should().Be(2);
        result.Skipped.Should().Be(0);
        result.ReviewedByUserId.Should().Be(_reviewerId);
        first.FraudReviewStatus.Should().Be(FraudReviewStatus.Approved);
        second.FraudReviewStatus.Should().Be(FraudReviewStatus.Approved);

        _auditService.Verify(
            service => service.RecordAuditTrailAsync(
                "Payment",
                It.IsIn(first.Id, second.Id),
                "FraudReviewResolved",
                _reviewerId,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "every resolved review must be audited");
    }

    [Fact]
    public async Task Handle_ShouldSkipNonPendingItems_InsteadOfFailingTheBatch()
    {
        var pending = ReviewHeldPayment();
        var alreadyResolved = ReviewHeldPayment();
        alreadyResolved.ResolveFraudReview(approved: true, Guid.NewGuid());
        SetupActor(systemAdmin: false);
        SetupPayment(pending);
        SetupPayment(alreadyResolved);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ResolveFraudReviewsBulkCommand([pending.Id, alreadyResolved.Id], Approve: false),
            CancellationToken.None);

        result.Requested.Should().Be(2);
        result.Resolved.Should().Be(1);
        result.Skipped.Should().Be(1);
        pending.FraudReviewStatus.Should().Be(FraudReviewStatus.Declined);

        var skipped = result.Items.Single(item => item.PaymentId == alreadyResolved.Id);
        skipped.Success.Should().BeFalse();
        skipped.FailureReason.Should().Contain("no pending fraud review");
    }

    [Fact]
    public async Task Handle_ShouldSkipUnknownPayments_WithoutFailingTheBatch()
    {
        var pending = ReviewHeldPayment();
        var unknownId = Guid.NewGuid();
        SetupActor(systemAdmin: false);
        SetupPayment(pending);
        _paymentRepository
            .Setup(repository => repository.GetByIdAsync(unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ResolveFraudReviewsBulkCommand([pending.Id, unknownId], Approve: true),
            CancellationToken.None);

        result.Resolved.Should().Be(1);
        result.Items.Single(item => item.PaymentId == unknownId).FailureReason.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ShouldRejectReviewerFromAnotherTenant_PerItem()
    {
        var payment = ReviewHeldPayment();
        SetupActor(systemAdmin: false, tenantId: Guid.NewGuid());
        SetupPayment(payment);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ResolveFraudReviewsBulkCommand([payment.Id], Approve: true),
            CancellationToken.None);

        result.Resolved.Should().Be(0);
        result.Items.Single().FailureReason.Should().Contain("reviewer's tenant");
        _paymentRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldDeduplicatePaymentIds()
    {
        var payment = ReviewHeldPayment();
        SetupActor(systemAdmin: false);
        SetupPayment(payment);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ResolveFraudReviewsBulkCommand([payment.Id, payment.Id], Approve: true),
            CancellationToken.None);

        result.Requested.Should().Be(1, "duplicate ids collapse to one resolution");
        result.Resolved.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRejectOversizedBatches()
    {
        SetupActor(systemAdmin: false);
        var paymentIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray();

        var handler = CreateHandler();
        var act = () => handler.Handle(
            new ResolveFraudReviewsBulkCommand(paymentIds, Approve: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _paymentRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRequireAnAuthenticatedReviewer()
    {
        _actorContextAccessor.SetupGet(accessor => accessor.ActorContext).Returns(ActorContext.Anonymous);

        var handler = CreateHandler();
        var act = () => handler.Handle(
            new ResolveFraudReviewsBulkCommand([Guid.NewGuid()], Approve: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private ResolveFraudReviewsBulkCommandHandler CreateHandler() =>
        new(
            _paymentRepository.Object,
            _auditService.Object,
            _actorContextAccessor.Object,
            Mock.Of<ILogger<ResolveFraudReviewsBulkCommandHandler>>());

    private Payment ReviewHeldPayment()
    {
        var payment = Payment.Create(
            tenantId: _tenantId,
            amount: 25m,
            currency: "USD",
            idempotencyKey: $"review-{Guid.NewGuid():N}",
            provider: "stripe",
            subscriptionId: Guid.NewGuid(),
            externalCustomerId: "cus_review",
            paymentMethodId: "pm_review");
        payment.MarkAsProcessing();
        payment.RecordFraudAssessment(FraudDecision.Review, 55, "unit-test review", "{}");
        return payment;
    }

    private void SetupPayment(Payment payment) =>
        _paymentRepository
            .Setup(repository => repository.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

    private void SetupActor(bool systemAdmin, Guid? tenantId = null)
    {
        var roles = new HashSet<string>(systemAdmin ? ["SystemAdmin"] : ["TenantAdmin"], StringComparer.Ordinal);
        var actorContext = new ActorContext
        {
            SubjectId = _reviewerId.ToString(),
            TenantId = tenantId ?? _tenantId,
            ActorKind = ActorKind.User,
            Roles = roles,
            Permissions = new HashSet<string>(StringComparer.Ordinal),
            IsAuthenticated = true
        };
        _actorContextAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actorContext);
    }
}
