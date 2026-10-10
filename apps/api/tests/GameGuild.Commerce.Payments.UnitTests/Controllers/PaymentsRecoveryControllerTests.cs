using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Controllers;

public class PaymentsRecoveryControllerTests
{
    [Fact]
    public async Task GetRecoveryMetrics_ShouldScopeMissingTenantFilterToAuthenticatedTenant()
    {
        var tenantId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<GetPaymentRecoveryMetricsQuery>(query => query.TenantId == tenantId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecoveryMetrics(tenantId));
        var controller = CreateController(sender.Object, CreateAuthenticatedActorContext(tenantId));

        var result = await controller.GetRecoveryMetrics(
            DateTimeOffset.UtcNow.AddDays(-30),
            DateTimeOffset.UtcNow,
            tenantId: null);

        result.Should().BeOfType<OkObjectResult>();
        sender.Verify(
            service => service.Send(
                It.Is<GetPaymentRecoveryMetricsQuery>(query => query.TenantId == tenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRecoveryMetrics_ShouldRejectTenantFilterFromAnotherTenant()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(sender.Object, CreateAuthenticatedActorContext(Guid.NewGuid()));

        var result = await controller.GetRecoveryMetrics(
            DateTimeOffset.UtcNow.AddDays(-30),
            DateTimeOffset.UtcNow,
            tenantId: Guid.NewGuid());

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            service => service.Send(It.IsAny<GetPaymentRecoveryMetricsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRecoveryMetrics_ShouldRejectInvertedWindow()
    {
        var controller = CreateController(Mock.Of<ISender>(), CreateAuthenticatedActorContext(Guid.NewGuid()));

        var result = await controller.GetRecoveryMetrics(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(-1),
            tenantId: null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetRecoveryMetrics_SystemAdmin_CanQueryAcrossTenants()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<GetPaymentRecoveryMetricsQuery>(query => query.TenantId == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecoveryMetrics(null));
        var controller = CreateController(sender.Object, CreateSystemAdminActorContext());

        var result = await controller.GetRecoveryMetrics(
            DateTimeOffset.UtcNow.AddDays(-30),
            DateTimeOffset.UtcNow,
            tenantId: null);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetRetryingCandidates_ShouldScopeMissingTenantFilterToAuthenticatedTenant()
    {
        var tenantId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(
                It.Is<GetRetryingPaymentCandidatesQuery>(query => query.TenantId == tenantId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var controller = CreateController(sender.Object, CreateAuthenticatedActorContext(tenantId));

        var result = await controller.GetRetryingCandidates(tenantId: null, take: 50);

        result.Should().BeOfType<OkObjectResult>();
        sender.Verify(
            service => service.Send(
                It.Is<GetRetryingPaymentCandidatesQuery>(query => query.TenantId == tenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRetryingCandidates_ShouldRejectTenantFilterFromAnotherTenant()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(sender.Object, CreateAuthenticatedActorContext(Guid.NewGuid()));

        var result = await controller.GetRetryingCandidates(tenantId: Guid.NewGuid(), take: 50);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            service => service.Send(It.IsAny<GetRetryingPaymentCandidatesQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static PaymentsController CreateController(
        ISender sender,
        ActorContext actorContext)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(actorContext);

        return new PaymentsController(sender, accessor.Object, Mock.Of<IStripeCustomerService>(), Mock.Of<ISubscriptionPaymentContextService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static PaymentRecoveryMetrics RecoveryMetrics(Guid? tenantId)
        => new(
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc),
            tenantId,
            new PaymentAttemptMetrics(10, 8, 2, 80m),
            new PaymentRetryRecoveryMetrics(4, 4, 2, 50m, [new PaymentCurrencyAmount("USD", 50m, 2)]),
            new PaymentDunningOutcomeMetrics(1, 1, 2));

    private static ActorContext CreateAuthenticatedActorContext(Guid? tenantId)
        => new()
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };

    private static ActorContext CreateSystemAdminActorContext()
        => new()
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string> { "SystemAdmin" },
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };
}
