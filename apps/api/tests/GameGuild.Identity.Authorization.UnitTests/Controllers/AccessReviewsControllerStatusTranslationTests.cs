using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization.Commands;
using GameGuild.Identity.Authorization.Controllers;
using GameGuild.Identity.Authorization.Queries;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Controllers;

/// <summary>
///     Verifies that the consolidated access-review controller translates workflow
///     authorization and lifecycle failures into the documented HTTP status codes
///     instead of surfacing unhandled exceptions as 500s (issue #390).
/// </summary>
public sealed class AccessReviewsControllerStatusTranslationTests
{
    [Fact]
    public async Task ApproveItem_ReturnsForbidden_WhenTheServiceDeniesAccess()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<ApproveAccessReviewItemCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Tenant administrator access to this access review is required."));
        var controller = new AccessReviewsController(sender.Object);

        var result = await controller.ApproveItem(Guid.NewGuid(), null, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task RevokeItem_ReturnsNotFound_WhenTheItemDoesNotExist()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<RevokeAccessReviewItemCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException($"Review item {Guid.NewGuid()} not found"));
        var controller = new AccessReviewsController(sender.Object);

        var result = await controller.RevokeItem(
            Guid.NewGuid(),
            new RevokeItemRequest("no longer required"),
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task StartCampaign_ReturnsConflict_WhenTheLifecycleTransitionIsInvalid()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<StartAccessReviewCampaignCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Only draft campaigns can be started"));
        var controller = new AccessReviewsController(sender.Object);

        var result = await controller.StartCampaign(Guid.NewGuid(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task CreateCampaign_ReturnsForbidden_WhenTheCreatorIdentityIsSpoofed()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<CreateAccessReviewCampaignCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Access review actor IDs must match the authenticated user."));
        var controller = new AccessReviewsController(sender.Object);

        var command = new CreateAccessReviewCampaignCommand(
            "Recertification",
            "Annual review",
            Guid.NewGuid(),
            AccessReviewType.UserAccessReview,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddDays(7),
            Guid.NewGuid());

        var result = await controller.CreateCampaign(command, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetPendingItems_ReturnsForbidden_WhenCrossTenantScopeIsRequested()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetPendingReviewItemsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Access reviews cannot cross tenant boundaries."));
        var controller = new AccessReviewsController(sender.Object);

        var result = await controller.GetPendingItems(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }
}
