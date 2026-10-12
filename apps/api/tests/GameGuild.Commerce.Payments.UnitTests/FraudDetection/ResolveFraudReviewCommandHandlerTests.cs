using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.FraudDetection;

public class ResolveFraudReviewCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<IRevenueAuditService> _auditService = new();
    private readonly Mock<IActorContextAccessor> _actorContextAccessor = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _reviewerId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ShouldApproveReview_AndWriteAuditTrail()
    {
        var payment = ReviewHeldPayment();
        SetupActor(systemAdmin: false);
        SetupPayment(payment);

        var handler = CreateHandler();
        var result = await handler.Handle(new ResolveFraudReviewCommand(payment.Id, Approve: true, "verified customer"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ReviewStatus.Should().Be(FraudReviewStatus.Approved);
        result.ReviewedByUserId.Should().Be(_reviewerId);
        payment.FraudReviewStatus.Should().Be(FraudReviewStatus.Approved);
        payment.FraudReviewNotes.Should().Be("verified customer");
        payment.CanRetry.Should().BeTrue("an approved review releases the fraud hold");

        _auditService.Verify(
            service => service.RecordAuditTrailAsync(
                "Payment",
                payment.Id,
                "FraudReviewResolved",
                _reviewerId,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "the review resolution must be audited");
    }

    [Fact]
    public async Task Handle_ShouldDeclineReview_AndKeepPaymentNonRetryable()
    {
        var payment = ReviewHeldPayment();
        SetupActor(systemAdmin: false);
        SetupPayment(payment);

        var handler = CreateHandler();
        var result = await handler.Handle(new ResolveFraudReviewCommand(payment.Id, Approve: false, "abuse confirmed"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ReviewStatus.Should().Be(FraudReviewStatus.Declined);
        payment.FraudReviewStatus.Should().Be(FraudReviewStatus.Declined);
        payment.CanRetry.Should().BeFalse("a declined review keeps the payment non-retryable");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPaymentHasNoPendingReview()
    {
        var payment = ReviewHeldPayment();
        payment.ResolveFraudReview(approved: true, Guid.NewGuid());
        SetupActor(systemAdmin: false);
        SetupPayment(payment);

        var handler = CreateHandler();
        var result = await handler.Handle(new ResolveFraudReviewCommand(payment.Id, Approve: false), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("no pending fraud review");
        _paymentRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenPaymentIsUnknown()
    {
        SetupActor(systemAdmin: false);
        var paymentId = Guid.NewGuid();
        _paymentRepository
            .Setup(repository => repository.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new ResolveFraudReviewCommand(paymentId, Approve: true), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ShouldRejectReviewerFromAnotherTenant()
    {
        var payment = ReviewHeldPayment();
        SetupActor(systemAdmin: false, tenantId: Guid.NewGuid());
        SetupPayment(payment);

        var handler = CreateHandler();
        var act = () => handler.Handle(new ResolveFraudReviewCommand(payment.Id, Approve: true), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _paymentRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldAllowSystemAdminAcrossTenants()
    {
        var payment = ReviewHeldPayment();
        SetupActor(systemAdmin: true, tenantId: Guid.NewGuid());
        SetupPayment(payment);

        var handler = CreateHandler();
        var result = await handler.Handle(new ResolveFraudReviewCommand(payment.Id, Approve: true), CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldRequireAnAuthenticatedReviewer()
    {
        var actorContext = ActorContext.Anonymous;
        _actorContextAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actorContext);

        var handler = CreateHandler();
        var act = () => handler.Handle(new ResolveFraudReviewCommand(Guid.NewGuid(), Approve: true), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private ResolveFraudReviewCommandHandler CreateHandler() =>
        new(
            _paymentRepository.Object,
            _auditService.Object,
            _actorContextAccessor.Object,
            Mock.Of<ILogger<ResolveFraudReviewCommandHandler>>());

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
