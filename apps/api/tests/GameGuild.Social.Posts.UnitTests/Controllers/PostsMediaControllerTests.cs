using FluentAssertions;
using GameGuild.Assets.SocialMedia;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Posts.Commands;
using GameGuild.Social.Posts.Controllers;
using GameGuild.Social.Posts.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Social.Posts.Tests.Controllers;

public sealed class PostsMediaControllerTests
{
    [Fact]
    public async Task CreatePost_ResolvesOwnedReadyAssetInsteadOfTrustingClientMediaMetadata()
    {
        var actorId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var descriptor = new SocialMediaAssetDescriptor(
            assetId,
            $"/api/assets/{assetId}/content",
            "image/webp",
            512,
            SocialMediaProcessingState.Ready);
        var assets = new Mock<ISocialMediaAssetService>();
        assets.Setup(service => service.ResolveReadyOwnedAsync(assetId, actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptor);
        var sender = new Mock<ISender>();
        sender.Setup(candidate => candidate.Send(
                It.Is<CreatePostEndpointCommand>(command =>
                    command.AuthorId == actorId &&
                    command.MediaUrl == descriptor.DeliveryUrl &&
                    command.MediaType == MediaType.Image),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Post.Create(actorId, "Build update", PostVisibility.Public)));
        var controller = Controller(actorId, sender.Object, assets.Object);

        var result = await controller.CreatePost(new CreatePostRequest
        {
            Content = "Build update",
            AssetReferenceId = assetId
        });

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreatePost_RejectsUnownedPendingOrDeletedAsset()
    {
        var actorId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        var assets = new Mock<ISocialMediaAssetService>();
        assets.Setup(service => service.ResolveReadyOwnedAsync(assetId, actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SocialMediaAssetDescriptor?)null);
        var controller = Controller(actorId, sender.Object, assets.Object);

        var result = await controller.CreatePost(new CreatePostRequest
        {
            Content = "Build update",
            AssetReferenceId = assetId
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        sender.Verify(candidate => candidate.Send(It.IsAny<CreatePostEndpointCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PostsCrudController Controller(
        Guid actorId,
        ISender sender,
        ISocialMediaAssetService assets)
    {
        var actor = new ActorContextAccessor();
        actor.SetActorContext(ActorContextBuilder.ForUser(actorId).WithTenantId(Guid.NewGuid()).Build());
        return new PostsCrudController(Mock.Of<IPostService>(), actor, sender, assets);
    }
}
