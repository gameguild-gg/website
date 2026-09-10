using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Payouts;
using GameGuild.Finance.Economy.Payouts.Commands;
using GameGuild.Finance.Economy.Payouts.Queries;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyPayoutAdministrationControllerTests
{
    [Fact]
    public async Task ListForReviewForbidsANonAdministrator()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = CreateController(sender.Object, walletAdmin: false);

        var result = await controller.ListForReview(10, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ApproveForwardsTheDecisionAndImmutableReasonWithoutATenantFromTheRequestBody()
    {
        var sender = new Mock<ISender>();
        var requestId = Guid.NewGuid();
        var request = new ReviewPayoutRequestRequest("Identity and risk review passed.");
        var response = new EconomyPayoutRequestReviewDto(
            requestId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            250,
            PayoutRequestState.AwaitingSecondApproval,
            2,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow);
        sender.Setup(service => service.Send(
                It.Is<ReviewPayoutRequestCommand>(command =>
                    command.RequestId == requestId &&
                    command.Outcome == PayoutRequestState.Approved &&
                    command.Request == request),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        var controller = CreateController(sender.Object);

        var result = await controller.Approve(requestId, request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().Be(response);
    }

    [Fact]
    public async Task RejectReturnsConflictWhenTheReviewCannotTransition()
    {
        var sender = new Mock<ISender>();
        sender.Setup(service => service.Send(
                It.IsAny<ReviewPayoutRequestCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PayoutRequestTransitionException("A payout requester cannot review their own request."));
        var controller = CreateController(sender.Object);

        var result = await controller.Reject(
            Guid.NewGuid(),
            new ReviewPayoutRequestRequest("Requester must not self approve."),
            CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ListReviewAuditForwardsTheRequestToTheTenantBoundCqrsQuery()
    {
        var sender = new Mock<ISender>();
        var requestId = Guid.NewGuid();
        sender.Setup(service => service.Send(
                It.Is<ListPayoutRequestReviewAuditQuery>(query => query.RequestId == requestId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EconomyPayoutRequestReviewAuditDto>());
        var controller = CreateController(sender.Object);

        var result = await controller.ListReviewAudit(requestId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    private static EconomyPayoutAdministrationController CreateController(ISender sender, bool walletAdmin = true)
    {
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = walletAdmin
                ? new HashSet<string> { EconomyPermission.Keys.ReviewPayouts }
                : [],
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        });
        return new EconomyPayoutAdministrationController(sender, actor.Object);
    }
}
