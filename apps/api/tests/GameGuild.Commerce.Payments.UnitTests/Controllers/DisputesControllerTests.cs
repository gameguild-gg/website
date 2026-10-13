using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Controllers;

public class DisputesControllerTests
{
    [Fact]
    public async Task GetById_ShouldRejectDisputeFromAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var sender = CreateSenderWithDispute(disputeId, Guid.NewGuid());
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.GetById(disputeId, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Create_ShouldRejectPaymentFromAnotherTenant_BeforeDispatchingMutation()
    {
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var sender = CreateSenderWithPayment(paymentId, Guid.NewGuid());
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.Create(
            new DisputesController.CreateDisputeRequest(paymentId, DisputeType.Duplicate, 50m, "charged twice"),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            sender => sender.Send(It.IsAny<CreateDisputeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_ShouldUseTheAuthenticatedActorAsTheDisputingUser()
    {
        var tenantId = Guid.NewGuid();
        var actor = AuthenticatedActorContext(tenantId);
        var paymentId = Guid.NewGuid();
        var sender = CreateSenderWithPayment(paymentId, tenantId);
        sender
            .Setup(service => service.Send(It.IsAny<CreateDisputeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentDispute());
        var controller = CreateController(sender.Object, actor);

        await controller.Create(
            new DisputesController.CreateDisputeRequest(paymentId, DisputeType.Duplicate, 50m, "charged twice"),
            CancellationToken.None);

        sender.Verify(
            service => service.Send(
                It.Is<CreateDisputeCommand>(command => command.UserId == actor.SubjectIdAsGuid),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Accept_ShouldRejectDisputeFromAnotherTenant_BeforeDispatchingMutation()
    {
        var tenantId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var sender = CreateSenderWithDispute(disputeId, Guid.NewGuid());
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.Accept(disputeId, null, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            sender => sender.Send(It.IsAny<AcceptDisputeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Accept_WithinTenant_DispatchesTheCommand()
    {
        var tenantId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var sender = CreateSenderWithDispute(disputeId, tenantId);
        sender
            .Setup(service => service.Send(It.IsAny<AcceptDisputeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DisputeActionResult { Success = true, DisputeId = disputeId });
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.Accept(disputeId, new DisputesController.AcceptDisputeRequest("with notes"), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        sender.Verify(
            service => service.Send(
                It.Is<AcceptDisputeCommand>(command => command.DisputeId == disputeId && command.Notes == "with notes" && command.IssueRefund),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Challenge_ShouldRejectDisputeFromAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var sender = CreateSenderWithDispute(disputeId, Guid.NewGuid());
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.Challenge(disputeId, null, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            sender => sender.Send(It.IsAny<ChallengeDisputeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DisputesController CreateController(ISender sender, ActorContext actorContext)
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actorContext);
        return new DisputesController(sender, actorAccessor.Object);
    }

    private static Mock<ISender> CreateSenderWithDispute(Guid disputeId, Guid paymentTenantId)
    {
        var sender = new Mock<ISender>();
        var paymentId = Guid.NewGuid();
        sender
            .Setup(service => service.Send(It.Is<GetDisputeByIdQuery>(query => query.DisputeId == disputeId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentDispute { PaymentId = paymentId });
        sender
            .Setup(service => service.Send(It.Is<GetPaymentByIdQuery>(query => query.PaymentId == paymentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult
            {
                Success = true,
                PaymentId = paymentId.ToString(),
                TenantId = paymentTenantId,
                Amount = new Money(100m, "USD"),
                Status = PaymentStatus.Succeeded
            });
        return sender;
    }

    private static Mock<ISender> CreateSenderWithPayment(Guid paymentId, Guid tenantId)
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(It.Is<GetPaymentByIdQuery>(query => query.PaymentId == paymentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult
            {
                Success = true,
                PaymentId = paymentId.ToString(),
                TenantId = tenantId,
                Amount = new Money(100m, "USD"),
                Status = PaymentStatus.Succeeded
            });
        return sender;
    }

    private static ActorContext AuthenticatedActorContext(Guid tenantId) => new()
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
}
