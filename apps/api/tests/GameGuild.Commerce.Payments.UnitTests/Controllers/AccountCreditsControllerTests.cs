using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Controllers;

/// <summary>
///     Authorization contract for the account credits API (issue #410): self-service
///     endpoints require an authenticated user+tenant actor, admin surfaces fail closed
///     for non-admins, and mutations flow through ISender commands only.
/// </summary>
public class AccountCreditsControllerTests
{
    private static ActorContext Actor(Guid tenantId, Guid userId, bool systemAdmin = false) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = userId.ToString(),
        TenantId = tenantId,
        Roles = new HashSet<string>(systemAdmin ? ["SystemAdmin"] : []),
        Permissions = new HashSet<string>(),
        TypedAttributes = ActorAttributes.Empty,
        AuthScheme = "Bearer",
        IsAuthenticated = true
    };

    private static ActorContext TenantlessActor(Guid userId) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = userId.ToString(),
        TenantId = null,
        Roles = new HashSet<string>(),
        Permissions = new HashSet<string>(),
        TypedAttributes = ActorAttributes.Empty,
        AuthScheme = "Bearer",
        IsAuthenticated = true
    };

    private static AccountCreditsController CreateController(
        ActorContext actor, ISender? sender = null)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(actor);

        return new AccountCreditsController(sender ?? Mock.Of<ISender>(), accessor.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task Balance_Requires_Self_Service_Context()
    {
        var controller = CreateController(TenantlessActor(Guid.NewGuid()));

        var result = await controller.GetMyBalance(CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Balance_Sends_The_Actor_Scoped_Query()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(instance => instance.Send(It.IsAny<IRequest<IReadOnlyList<AccountCreditBalance>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AccountCreditBalance>());
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid()), sender.Object);

        var result = await controller.GetMyBalance(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        sender.Verify(instance => instance.Send(
            It.IsAny<GetMyCreditBalancesQuery>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task History_Requires_Self_Service_Context()
    {
        var controller = CreateController(TenantlessActor(Guid.NewGuid()));

        var result = await controller.GetMyHistory();

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Metrics_Fails_Closed_For_Non_Admins()
    {
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid(), systemAdmin: false));

        var result = await controller.GetMetrics(CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task IssueGrant_Fails_Closed_For_Non_Admins()
    {
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid(), systemAdmin: false));

        var result = await controller.IssueGrant(
            new AccountCreditsController.IssueCreditRequest(Guid.NewGuid(), 10m, "USD", AccountCreditType.General, "ops credit"),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task RevokeGrant_Fails_Closed_For_Non_Admins()
    {
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid(), systemAdmin: false));

        var result = await controller.RevokeGrant(
            Guid.NewGuid(), new AccountCreditsController.RevokeCreditRequest("cleanup"), CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task IssueGrant_Sends_The_Command_With_Actor_Derived_Tenant()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(instance => instance.Send(
                It.IsAny<IRequest<Result<AccountCreditGrant>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AccountCreditGrant(
                Guid.NewGuid(), Guid.NewGuid(), 10m, "USD", AccountCreditSource.Manual,
                AccountCreditType.General, "ops credit")));
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid(), systemAdmin: true), sender.Object);

        var result = await controller.IssueGrant(
            new AccountCreditsController.IssueCreditRequest(Guid.NewGuid(), 10m, "USD", AccountCreditType.General, "ops credit"),
            CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
        sender.Verify(instance => instance.Send(
            It.Is<IRequest<Result<AccountCreditGrant>>>(request => request is IssueAccountCreditCommand),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeGrant_Returns_NoContent_On_Success()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(instance => instance.Send(It.IsAny<IRequest<Result>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var controller = CreateController(Actor(Guid.NewGuid(), Guid.NewGuid(), systemAdmin: true), sender.Object);

        var result = await controller.RevokeGrant(
            Guid.NewGuid(), new AccountCreditsController.RevokeCreditRequest("cleanup"), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }
}
