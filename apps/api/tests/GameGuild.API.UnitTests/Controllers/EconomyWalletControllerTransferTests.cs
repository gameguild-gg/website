using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.API.Setup;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Commands;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.Transfers;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyWalletControllerTransferTests
{
    [Fact]
    public async Task CreateMyTransfer_RequiresAnAuthenticatedTenantActor()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = Controller(sender.Object, authenticated: false);

        var result = await controller.CreateMyTransfer(Request(), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        sender.Verify(value => value.Send(
            It.IsAny<CreateMyEconomyTransferCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateMyTransfer_SendsTheClosedIntentAndReturnsThePostingReceipt()
    {
        var request = Request();
        var receipt = new SelfServiceEconomyTransferReceipt(
            Guid.NewGuid(), request.TransferType, request.Currency, request.AmountUnits,
            request.RecipientUserId, 17, "journal-hash", false);
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.Is<CreateMyEconomyTransferCommand>(command => command.Request == request),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        var controller = Controller(sender.Object);

        var result = await controller.CreateMyTransfer(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(receipt);
    }

    [Theory]
    [InlineData(EconomyProtectedOperationState.Denied, StatusCodes.Status403Forbidden)]
    [InlineData(EconomyProtectedOperationState.ReviewRequired, StatusCodes.Status409Conflict)]
    [InlineData(EconomyProtectedOperationState.Hold, StatusCodes.Status409Conflict)]
    [InlineData(EconomyProtectedOperationState.Challenge, StatusCodes.Status409Conflict)]
    [InlineData(EconomyProtectedOperationState.ComplianceUnavailable, StatusCodes.Status503ServiceUnavailable)]
    public async Task CreateMyTransfer_ReturnsStructuredProtectedOperationStates(
        EconomyProtectedOperationState state,
        int expectedStatus)
    {
        var reviewId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.IsAny<CreateMyEconomyTransferCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EconomyProtectedOperationException(state, reviewId, ["diagnostic"]));
        var controller = Controller(sender.Object);

        var result = await controller.CreateMyTransfer(Request(), CancellationToken.None);

        var response = result.Should().BeOfType<ObjectResult>().Which;
        response.StatusCode.Should().Be(expectedStatus);
        response.Value.Should().BeEquivalentTo(new EconomyTransferProtectedOperationFailureResponse(
            state, reviewId, ["diagnostic"]));
    }

    [Fact]
    public async Task CreateMyTransfer_ReturnsConflictForAnIdempotencyBindingConflict()
    {
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.IsAny<CreateMyEconomyTransferCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SelfServiceEconomyTransferException("conflict"));
        var controller = Controller(sender.Object);

        var result = await controller.CreateMyTransfer(Request(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>().Which.Value.Should().Be("conflict");
    }

    [Fact]
    public async Task CreateMyTransfer_DoesNotRevealWhichPartyHasNoActiveWallet()
    {
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.IsAny<CreateMyEconomyTransferCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EconomyWalletUnavailableException("recipient missing"));
        var controller = Controller(sender.Object);

        var result = await controller.CreateMyTransfer(Request(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>().Which.Value
            .Should().Be("An active sender and recipient Economy wallet are required.");
    }

    [Fact]
    public async Task CreateMyTransfer_RedactsPersistentWriterFailures()
    {
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.IsAny<CreateMyEconomyTransferCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RegisteredPostingRejectedException("database detail"));
        var controller = Controller(sender.Object);

        var result = await controller.CreateMyTransfer(Request(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>().Which.Value
            .Should().Be("The Economy transfer could not be committed.");
    }

    private static SelfServiceEconomyTransferRequest Request() => new(
        Guid.Parse("a4000000-0000-0000-0000-000000000001"),
        SelfServiceEconomyTransferType.Tip,
        CurrencyCode.HardCoin,
        23,
        "controller-transfer-key");

    private static EconomyWalletController Controller(ISender sender, bool authenticated = true)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.Parse("a4000000-0000-0000-0000-000000000002").ToString(),
            TenantId = Guid.Parse("a4000000-0000-0000-0000-000000000003"),
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
