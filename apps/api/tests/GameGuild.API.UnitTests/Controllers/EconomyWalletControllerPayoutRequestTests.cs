using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.API.Setup;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Payouts;
using GameGuild.Finance.Economy.Payouts.Commands;
using GameGuild.Finance.Economy.Payouts.Queries;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyWalletControllerPayoutRequestTests
{
    [Fact]
    public async Task CreateReturnsCreatedForTheAuthenticatedActor()
    {
        var sender = new Mock<ISender>();
        var request = new CreateMyPayoutRequestRequest(250, "request-1");
        var response = new EconomyPayoutRequestDto(
            Guid.NewGuid(), 250, PayoutRequestState.Submitted, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        sender.Setup(service => service.Send(
                It.Is<CreateMyPayoutRequestCommand>(command => command.Request == request),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        var controller = CreateController(sender.Object);

        var result = await controller.CreateMyPayoutRequest(request, CancellationToken.None);

        var created = result.Should().BeOfType<ObjectResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Value.Should().Be(response);
    }

    [Fact]
    public async Task CreateForbidsAnUnauthenticatedActor()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = CreateController(sender.Object, isAuthenticated: false);

        var result = await controller.CreateMyPayoutRequest(
            new CreateMyPayoutRequestRequest(250, "request-1"), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateReturnsConflictWhenTheActorHasNoWallet()
    {
        var sender = new Mock<ISender>();
        sender.Setup(service => service.Send(
                It.IsAny<CreateMyPayoutRequestCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PayoutRequestWalletUnavailableException("Create an Economy wallet before requesting a payout."));
        var controller = CreateController(sender.Object);

        var result = await controller.CreateMyPayoutRequest(
            new CreateMyPayoutRequestRequest(250, "request-1"), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateReturnsConflictForAnUnsafePayoutRequest()
    {
        var sender = new Mock<ISender>();
        sender.Setup(service => service.Send(
                It.IsAny<CreateMyPayoutRequestCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PayoutRequestInsufficientWithdrawableFundsException(
                "The payout request exceeds HardCoin value that is confirmed and eligible for withdrawal."));
        var controller = CreateController(sender.Object);

        var result = await controller.CreateMyPayoutRequest(
            new CreateMyPayoutRequestRequest(250, "request-1"), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CancelReturnsNotFoundForARequestThatDoesNotBelongToTheActor()
    {
        var sender = new Mock<ISender>();
        sender.Setup(service => service.Send(
                It.IsAny<CancelMyPayoutRequestCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());
        var controller = CreateController(sender.Object);

        var result = await controller.CancelMyPayoutRequest(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CancelReturnsConflictAfterTheRequestHasBeenReviewed()
    {
        var sender = new Mock<ISender>();
        sender.Setup(service => service.Send(
                It.IsAny<CancelMyPayoutRequestCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PayoutRequestTransitionException("Only a submitted payout request can be cancelled."));
        var controller = CreateController(sender.Object);

        var result = await controller.CancelMyPayoutRequest(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ListRejectsAnInvalidPageSize()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = CreateController(sender.Object);

        var result = await controller.ListMyPayoutRequests(101, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListForwardsTheAuthenticatedActorAndPageSize()
    {
        var actorId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender.Setup(service => service.Send(
                It.Is<ListMyPayoutRequestsQuery>(query => query.PayeeId == actorId && query.Take == 10),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EconomyPayoutRequestDto>());
        var controller = CreateController(sender.Object, actorId);

        var result = await controller.ListMyPayoutRequests(10, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    private static EconomyWalletController CreateController(
        ISender sender,
        Guid? actorId = null,
        bool isAuthenticated = true)
    {
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = (actorId ?? Guid.NewGuid()).ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = isAuthenticated
        });
        return new EconomyWalletController(
            sender,
            actor.Object,
            Mock.Of<IEconomyProviderCapabilityReadiness>());
    }
}
