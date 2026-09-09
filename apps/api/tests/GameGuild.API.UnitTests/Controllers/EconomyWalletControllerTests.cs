using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.API.Setup;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Commands;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Payouts;
using GameGuild.Finance.Economy.Payouts.Queries;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyWalletControllerTests
{
    [Fact]
    public async Task ConvertMyHardToSoft_WhenActorHasNoSelfServiceContext_ReturnsForbid()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = CreateController(sender.Object, new ActorContextAccessor());

        var result = await controller.ConvertMyHardToSoft(
            new ConvertMyHardToSoftRequest(100, "conversion-key"),
            CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        sender.Verify(value => value.Send(It.IsAny<ConvertMyHardToSoftCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConvertMyHardToSoft_WhenActorHasSelfServiceContext_UsesCqrsAndReturnsReceipt()
    {
        var actorId = Guid.Parse("93000000-0000-0000-0000-000000000001");
        var tenantId = Guid.Parse("93000000-0000-0000-0000-000000000002");
        var request = new ConvertMyHardToSoftRequest(100, "conversion-key");
        var receipt = new SelfServiceHardToSoftConversionReceipt(
            Guid.Parse("93000000-0000-0000-0000-000000000004"), null, 17, "journal-hash", false);
        var sender = new Mock<ISender>();
        sender
            .Setup(value => value.Send(
                It.Is<ConvertMyHardToSoftCommand>(command => command.Request == request),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = actorId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true,
        });
        var controller = CreateController(sender.Object, accessor);

        var result = await controller.ConvertMyHardToSoft(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(receipt);
        sender.Verify(value => value.Send(
            It.Is<ConvertMyHardToSoftCommand>(command => command.Request == request),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListMyPayouts_RequiresSelfServiceContextAndAValidTake()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = CreateController(sender.Object, new ActorContextAccessor());

        (await controller.ListMyPayouts(50, CancellationToken.None)).Should().BeOfType<ForbidResult>();

        var accessor = CreateAccessor();
        controller = CreateController(sender.Object, accessor);
        (await controller.ListMyPayouts(0, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        sender.Verify(value => value.Send(
            It.IsAny<ListMyPayoutOperationsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListMyPayouts_UsesAuthenticatedActorAndReturnsStatusDtos()
    {
        var actorId = Guid.Parse("93000000-0000-0000-0000-000000000010");
        var payout = new EconomyPayoutOperationDto(
            Guid.Parse("93000000-0000-0000-0000-000000000011"),
            150,
            PayoutOperationState.Reserved,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.Is<ListMyPayoutOperationsQuery>(query => query.PayeeId == actorId && query.Take == 25),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<EconomyPayoutOperationDto>)[payout]);
        var controller = CreateController(sender.Object, CreateAccessor(actorId));

        var result = await controller.ListMyPayouts(25, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(
            new EconomyPayoutOperationDto[] { payout });
    }

    [Fact]
    public async Task GetMyPayout_ReturnsNotFoundWhenQueryDoesNotFindAnOwnedOperation()
    {
        var actorId = Guid.Parse("93000000-0000-0000-0000-000000000020");
        var operationId = Guid.Parse("93000000-0000-0000-0000-000000000021");
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.Is<GetMyPayoutOperationQuery>(query => query.PayeeId == actorId && query.OperationId == operationId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EconomyPayoutOperationDto?)null);
        var controller = CreateController(sender.Object, CreateAccessor(actorId));

        var result = await controller.GetMyPayout(operationId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyPayout_RequiresSelfServiceContext()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = CreateController(sender.Object, new ActorContextAccessor());

        var result = await controller.GetMyPayout(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        sender.Verify(value => value.Send(
            It.IsAny<GetMyPayoutOperationQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMyPayout_ReturnsTheAuthenticatedPayeeStatusDto()
    {
        var actorId = Guid.Parse("93000000-0000-0000-0000-000000000040");
        var operationId = Guid.Parse("93000000-0000-0000-0000-000000000041");
        var payout = new EconomyPayoutOperationDto(
            operationId,
            200,
            PayoutOperationState.Succeeded,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow);
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.Is<GetMyPayoutOperationQuery>(query => query.PayeeId == actorId && query.OperationId == operationId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(payout);
        var controller = CreateController(sender.Object, CreateAccessor(actorId));

        var result = await controller.GetMyPayout(operationId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(payout);
    }

    [Fact]
    public void GetMyCapabilityReadiness_ReturnsOnlySelfServiceCapabilityStates()
    {
        EconomyValueMovementCapability[] expectedCapabilities =
        [
            EconomyValueMovementCapability.ConfirmHardCoinFunding,
            EconomyValueMovementCapability.ConvertHardToSoft,
            EconomyValueMovementCapability.Transfer,
            EconomyValueMovementCapability.IssueAdReward,
            EconomyValueMovementCapability.BountyEscrow,
            EconomyValueMovementCapability.BountyClaim,
            EconomyValueMovementCapability.BountyReclaim,
            EconomyValueMovementCapability.MarketplaceSettlement,
            EconomyValueMovementCapability.MarketplaceRefund,
            EconomyValueMovementCapability.PayoutExecution
        ];
        var readiness = new Mock<IEconomyProviderCapabilityReadiness>(MockBehavior.Strict);
        readiness.Setup(value => value.Assess(It.IsAny<EconomyValueMovementCapability>()))
            .Returns((EconomyValueMovementCapability capability) => new EconomyCapabilityReadinessResult(
                capability,
                capability == EconomyValueMovementCapability.PayoutExecution
                    ? EconomyCapabilityReadinessState.ProviderNotReady
                    : EconomyCapabilityReadinessState.Ready,
                capability == EconomyValueMovementCapability.PayoutExecution
                    ? ["Provider configuration is incomplete."]
                    : []));
        var controller = new EconomyWalletController(
            Mock.Of<ISender>(),
            CreateAccessor(),
            readiness.Object);

        var result = controller.GetMyCapabilityReadiness();

        var capabilities = result.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeAssignableTo<IReadOnlyList<EconomySelfServiceCapabilityDto>>().Subject;
        capabilities.Select(item => item.Capability).Should().Equal(expectedCapabilities);
        capabilities.Single(item => item.Capability == EconomyValueMovementCapability.PayoutExecution)
            .Should().BeEquivalentTo(new EconomySelfServiceCapabilityDto(
                EconomyValueMovementCapability.PayoutExecution,
                EconomyCapabilityReadinessState.ProviderNotReady,
                ["Provider configuration is incomplete."]));
    }

    private static ActorContextAccessor CreateAccessor(Guid? actorId = null)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = (actorId ?? Guid.Parse("93000000-0000-0000-0000-000000000030")).ToString(),
            TenantId = Guid.Parse("93000000-0000-0000-0000-000000000031"),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true,
        });
        return accessor;
    }

    private static EconomyWalletController CreateController(ISender sender, IActorContextAccessor accessor) => new(
        sender,
        accessor,
        Mock.Of<IEconomyProviderCapabilityReadiness>());
}
