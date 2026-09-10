using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.API.Setup;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Commands;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Integrations;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyWalletControllerTopUpTests
{
    [Fact]
    public async Task CreateMyTopUp_RequiresAnAuthenticatedTenantActor()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = Controller(sender.Object, authenticated: false);

        var result = await controller.CreateMyTopUp(Request(), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        sender.Verify(value => value.Send(
            It.IsAny<CreateMyHardCoinTopUpCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMyTopUp_ReturnsCreatedProviderIntent()
    {
        var request = Request();
        var receipt = Receipt();
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.Is<CreateMyHardCoinTopUpCommand>(command => command.Request == request),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        var controller = Controller(sender.Object);

        var result = await controller.CreateMyTopUp(request, CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Which;
        created.ActionName.Should().Be(nameof(EconomyWalletController.GetMyTopUp));
        created.RouteValues.Should().ContainKey("topUpId").WhoseValue.Should().Be(receipt.TopUpId);
        created.Value.Should().Be(receipt);
    }

    [Theory]
    [InlineData(typeof(EconomyTopUpProviderUnavailableException), "ProviderUnavailable", StatusCodes.Status503ServiceUnavailable)]
    [InlineData(typeof(EconomyTopUpProviderAmbiguousException), "Ambiguous", StatusCodes.Status409Conflict)]
    [InlineData(typeof(EconomyTopUpReplayConflictException), "Conflict", StatusCodes.Status409Conflict)]
    [InlineData(typeof(EconomySelfServiceCommandRejectedException), "Disabled", StatusCodes.Status503ServiceUnavailable)]
    [InlineData(typeof(EconomyWalletUnavailableException), "WalletUnavailable", StatusCodes.Status409Conflict)]
    public async Task CreateMyTopUp_ReturnsSafeStructuredFailure(
        Type exceptionType,
        string state,
        int statusCode)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "private detail", null)!;
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.IsAny<CreateMyHardCoinTopUpCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        var controller = Controller(sender.Object);

        var result = await controller.CreateMyTopUp(Request(), CancellationToken.None);

        var response = result.Should().BeAssignableTo<ObjectResult>().Which;
        response.StatusCode.Should().Be(statusCode);
        response.Value.Should().BeEquivalentTo(new EconomyTopUpFailureResponse(
            state,
            state switch
            {
                "ProviderUnavailable" => "The top-up provider is not available.",
                "Ambiguous" => "The top-up provider outcome requires reconciliation.",
                "Conflict" => "The idempotency key is already bound to another top-up.",
                "WalletUnavailable" => "An active Economy wallet is required.",
                _ => "HardCoin top-up is disabled by the active Economy controls."
            }));
    }

    [Fact]
    public async Task ListMyTopUps_RequiresContextAndValidTake()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = Controller(sender.Object, authenticated: false);
        (await controller.ListMyTopUps(50, CancellationToken.None)).Should().BeOfType<ForbidResult>();

        controller = Controller(sender.Object);
        (await controller.ListMyTopUps(0, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.ListMyTopUps(101, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ListMyTopUps_ReturnsTenantScopedQueryResult()
    {
        var dto = Status();
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.Is<ListMyHardCoinTopUpsQuery>(query => query.Take == 25),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<EconomyTopUpStatusDto>)[dto]);
        var controller = Controller(sender.Object);

        var result = await controller.ListMyTopUps(25, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(
            new EconomyTopUpStatusDto[] { dto });
    }

    [Fact]
    public async Task GetMyTopUp_RequiresContextAndAValidId()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = Controller(sender.Object, authenticated: false);
        (await controller.GetMyTopUp(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<ForbidResult>();

        controller = Controller(sender.Object);
        (await controller.GetMyTopUp(Guid.Empty, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetMyTopUp_ReturnsNotFoundOrOwnedStatus()
    {
        var topUpId = Guid.Parse("a5000000-0000-0000-0000-000000000001");
        var sender = new Mock<ISender>();
        sender.SetupSequence(value => value.Send(
                It.Is<GetMyHardCoinTopUpQuery>(query => query.TopUpId == topUpId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EconomyTopUpStatusDto?)null)
            .ReturnsAsync(Status(topUpId));
        var controller = Controller(sender.Object);

        (await controller.GetMyTopUp(topUpId, CancellationToken.None)).Should().BeOfType<NotFoundResult>();
        (await controller.GetMyTopUp(topUpId, CancellationToken.None)).Should()
            .BeOfType<OkObjectResult>().Which.Value.Should().Be(Status(topUpId));
    }

    private static CreateMyHardCoinTopUpRequest Request() => new(2500, "top-up-controller-key");

    private static SelfServiceHardCoinTopUpReceipt Receipt() => new(
        Guid.Parse("a5000000-0000-0000-0000-000000000001"),
        Guid.Parse("a5000000-0000-0000-0000-000000000002"),
        2500,
        2500,
        "USD",
        EconomyTopUpProviderStatus.RequiresAction,
        "pi_secret",
        "pk_live",
        false)
    {
        ProviderObjectId = "pi_top_up"
    };

    private static EconomyTopUpStatusDto Status(Guid? id = null) => new(
        id ?? Guid.Parse("a5000000-0000-0000-0000-000000000001"),
        2500,
        2500,
        "USD",
        EconomyTopUpProviderStatus.RequiresAction,
        "pi_top_up",
        DateTimeOffset.Parse("2026-08-27T12:00:00Z"),
        DateTimeOffset.Parse("2026-08-27T12:00:01Z"));

    private static EconomyWalletController Controller(ISender sender, bool authenticated = true)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.Parse("a5000000-0000-0000-0000-000000000003").ToString(),
            TenantId = Guid.Parse("a5000000-0000-0000-0000-000000000004"),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = authenticated
        });
        return new EconomyWalletController(
            sender,
            accessor,
            Mock.Of<IEconomyProviderCapabilityReadiness>());
    }
}
