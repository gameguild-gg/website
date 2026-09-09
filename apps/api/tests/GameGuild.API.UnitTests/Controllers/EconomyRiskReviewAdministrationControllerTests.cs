using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyRiskReviewAdministrationControllerTests
{
    private static readonly Guid TenantId = Guid.Parse("97000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("97000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_UsesActorTenantAndReturnsTheStablePage()
    {
        var review = Review();
        var page = new RiskReviewPage([review], "next-cursor");
        var store = new Mock<IRiskReviewStore>(MockBehavior.Strict);
        store.Setup(value => value.ListAsync(
                TenantId, RiskReviewStatus.Pending, 25, "cursor", It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);
        var controller = Controller(store.Object);

        var result = await controller.List(RiskReviewStatus.Pending, 25, "cursor", default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [Fact]
    public async Task GetAndAudit_ReturnNotFoundWithoutLeakingAnotherTenant()
    {
        var reviewId = Guid.NewGuid();
        var store = new Mock<IRiskReviewStore>(MockBehavior.Strict);
        store.Setup(value => value.CurrentAsync(TenantId, reviewId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());
        store.Setup(value => value.EventsAsync(TenantId, reviewId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());
        var controller = Controller(store.Object);

        (await controller.Get(reviewId, default)).Should().BeOfType<NotFoundResult>();
        (await controller.Audit(reviewId, default)).Should().BeOfType<NotFoundResult>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resolve_DerivesReviewerFromActorContext(bool approve)
    {
        var review = Review();
        var request = new ResolveEconomyRiskReviewRequest(
            RiskManualDecisionCode.EvidenceVerified, "reviewed evidence");
        var store = new Mock<IRiskReviewStore>(MockBehavior.Strict);
        var setup = approve
            ? store.Setup(value => value.ApproveAsync(
                TenantId, review.Id, ActorId, request.DecisionCode, request.Resolution,
                Now, It.IsAny<CancellationToken>()))
            : store.Setup(value => value.RejectAsync(
                TenantId, review.Id, ActorId, request.DecisionCode, request.Resolution,
                Now, It.IsAny<CancellationToken>()));
        setup.ReturnsAsync(review);
        var controller = Controller(store.Object);

        var result = approve
            ? await controller.Approve(review.Id, request, default)
            : await controller.Reject(review.Id, request, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(review);
        store.VerifyAll();
    }

    [Fact]
    public async Task List_ForbidsActorsWithoutTheCompliancePermission()
    {
        var store = new Mock<IRiskReviewStore>(MockBehavior.Strict);
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = ActorId.ToString(),
            TenantId = TenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        var controller = new EconomyRiskReviewAdministrationController(
            EconomyHandlerSenders.Compliance(riskReviews: store.Object),
            store.Object,
            accessor,
            new FixedTimeProvider(Now));

        (await controller.List(null, 25, null, default)).Should().BeOfType<ForbidResult>();
    }

    private static EconomyRiskReviewAdministrationController Controller(IRiskReviewStore store)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = ActorId.ToString(),
            TenantId = TenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string> { EconomyPermission.Keys.OperateCompliance },
            IsAuthenticated = true
        });
        return new EconomyRiskReviewAdministrationController(
            EconomyHandlerSenders.Compliance(riskReviews: store),
            store,
            accessor,
            new FixedTimeProvider(Now));
    }

    private static RiskReviewCase Review() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RiskReviewStatus.Pending,
        Now.AddMinutes(-1), null, null, null, 2, [], null);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
