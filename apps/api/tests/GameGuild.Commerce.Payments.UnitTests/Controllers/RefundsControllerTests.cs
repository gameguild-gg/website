using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Controllers;

public class RefundsControllerTests
{
    [Fact]
    public async Task RequestRefund_ShouldRejectPaymentFromAnotherTenant_BeforeDispatchingMutation()
    {
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var sender = CreateSenderWithPayment(paymentId, Guid.NewGuid());
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.RequestRefund(
            new RefundsController.RequestRefundHttpRequest(paymentId, 10m, "duplicate"),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            sender => sender.Send(It.IsAny<RequestRefundCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestRefund_ShouldRejectMissingReason()
    {
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var sender = CreateSenderWithPayment(paymentId, tenantId);
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.RequestRefund(
            new RefundsController.RequestRefundHttpRequest(paymentId, 10m, " "),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        sender.Verify(
            sender => sender.Send(It.IsAny<RequestRefundCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetById_ShouldRejectRefundFromAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(It.Is<GetRefundByIdQuery>(query => query.RefundId == refundId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundRecord
            {
                RefundId = refundId,
                TenantId = Guid.NewGuid(),
                PaymentId = Guid.NewGuid(),
                Amount = 10m,
                Kind = RefundKind.Full,
                Currency = "USD",
                Reason = "duplicate",
                Status = RefundStatus.Pending,
                RequiresApproval = true,
                RequestedByUserId = Guid.NewGuid(),
                RequestedAt = SystemClock.UtcNow
            });
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.GetById(refundId, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Approve_ShouldRejectRefundFromAnotherTenant_BeforeDispatchingMutation()
    {
        var tenantId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(It.Is<GetRefundByIdQuery>(query => query.RefundId == refundId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundRecord
            {
                RefundId = refundId,
                TenantId = Guid.NewGuid(),
                PaymentId = Guid.NewGuid(),
                Amount = 10m,
                Kind = RefundKind.Full,
                Currency = "USD",
                Reason = "duplicate",
                Status = RefundStatus.Pending,
                RequiresApproval = true,
                RequestedByUserId = Guid.NewGuid(),
                RequestedAt = SystemClock.UtcNow
            });
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.Approve(refundId, null, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            sender => sender.Send(It.IsAny<ApproveRefundCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task List_ShouldScopeNonAdminsToTheirOwnTenant()
    {
        var tenantId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(It.Is<ListRefundsQuery>(query => query.TenantId == tenantId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundListPage { Items = [], TotalCount = 0, Page = 1, PageSize = 20 });
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.List(ct: CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        sender.Verify(
            service => service.Send(It.Is<ListRefundsQuery>(query => query.TenantId == tenantId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPendingApprovals_ShouldScopeNonAdminsToTheirOwnTenant()
    {
        var tenantId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(It.Is<GetPendingRefundApprovalsQuery>(query => query.TenantId == tenantId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundApprovalQueuePage { Items = [], TotalCount = 0, Page = 1, PageSize = 20 });
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.GetPendingApprovals(ct: CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        sender.Verify(
            service => service.Send(It.Is<GetPendingRefundApprovalsQuery>(query => query.TenantId == tenantId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStatistics_ShouldScopeNonAdminsToTheirOwnTenant()
    {
        var tenantId = Guid.NewGuid();
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;
        var sender = new Mock<ISender>();
        sender
            .Setup(service => service.Send(It.Is<GetRefundStatisticsQuery>(query => query.TenantId == tenantId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundStatistics
            {
                FromUtc = from.UtcDateTime,
                ToUtc = to.UtcDateTime,
                CountByStatus = new Dictionary<RefundStatus, int>(),
                CompletedAmountByCurrency = new Dictionary<string, decimal>(),
                CompletedCountByReason = new Dictionary<string, int>(),
                TotalCount = 0
            });
        var controller = CreateController(sender.Object, AuthenticatedActorContext(tenantId));

        var result = await controller.GetStatistics(from, to, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        sender.Verify(
            service => service.Send(It.Is<GetRefundStatisticsQuery>(query => query.TenantId == tenantId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static RefundsController CreateController(ISender sender, ActorContext actorContext)
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actorContext);
        return new RefundsController(sender, actorAccessor.Object);
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
