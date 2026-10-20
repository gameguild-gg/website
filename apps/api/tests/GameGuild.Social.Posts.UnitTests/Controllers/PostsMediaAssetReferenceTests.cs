using FluentAssertions;
using GameGuild.Assets;
using GameGuild.Assets.Commands;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Posts.Commands;
using GameGuild.Social.Posts.Controllers;
using GameGuild.Social.Posts.Services;
using GameGuild.Social.Assets.SocialMedia;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Social.Posts.Tests.Controllers;

public sealed class PostsMediaAssetReferenceTests
{
    [Fact]
    public async Task CreatePost_PersistsMediaAssetReferenceIdAndDenormalizedUrl()
    {
        var (controller, sender) = ControllerWithSender();

        var result = await controller.CreatePost(new CreatePostRequest
        {
            Content = "With asset",
            AssetReferenceId = AssetId
        });

        result.Should().BeOfType<CreatedAtActionResult>();
        sender.Verify(candidate => candidate.Send(
            It.Is<CreatePostEndpointCommand>(command =>
                command.MediaAssetReferenceId == AssetId &&
                command.MediaUrl == Descriptor.DeliveryUrl),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePost_RebindsAssetParentToTheNewPost()
    {
        var (controller, sender) = ControllerWithSender();

        await controller.CreatePost(new CreatePostRequest
        {
            Content = "With asset",
            AssetReferenceId = AssetId
        });

        sender.Verify(candidate => candidate.Send(
            It.Is<RebindAssetParentCommand>(command =>
                command.AssetReferenceId == AssetId &&
                command.ParentResourceType == "Post" &&
                command.ParentResourceId == CreatedPost.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePost_WithoutAsset_DoesNotRebind()
    {
        var (controller, sender) = ControllerWithSender();

        await controller.CreatePost(new CreatePostRequest { Content = "No asset" });

        sender.Verify(candidate => candidate.Send(
            It.Is<RebindAssetParentCommand>(command => command.AssetReferenceId == AssetId),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePost_RejectsForeignTenantAsset()
    {
        var actorId = Guid.NewGuid();
        var assets = new Mock<ISocialMediaAssetService>();
        assets.Setup(service => service.ResolveReadyOwnedInTenantAsync(AssetId, actorId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SocialMediaAssetDescriptor?)null);
        var sender = new Mock<ISender>();
        var controller = BuildController(actorId, sender.Object, assets.Object);

        var result = await controller.CreatePost(new CreatePostRequest
        {
            Content = "Foreign asset",
            AssetReferenceId = AssetId
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        sender.Verify(candidate => candidate.Send(It.IsAny<CreatePostEndpointCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePost_DetachesAssetParentAfterDelete()
    {
        var (controller, sender, posts) = ControllerWithSenderAndPosts();
        var post = Post.Create(ActorId, "With asset", PostVisibility.Public);
        post.AttachMediaAsset(AssetId, Descriptor.DeliveryUrl, MediaType.Image);
        posts.Add(post);
        sender.Setup(candidate => candidate.Send(
            It.Is<DeletePostEndpointCommand>(command => command.PostId == post.Id),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await controller.DeletePost(post.Id);

        sender.Verify(candidate => candidate.Send(
            It.Is<RebindAssetParentCommand>(command =>
                command.AssetReferenceId == AssetId &&
                command.ParentResourceType == null &&
                command.ParentResourceId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePost_LegacyPostWithoutAssetReference_DoesNotRebind()
    {
        var (controller, sender, posts) = ControllerWithSenderAndPosts();
        var post = Post.Create(ActorId, "Legacy", PostVisibility.Public);
        post.AttachMedia("https://legacy.example/image.png", MediaType.Image);
        posts.Add(post);
        sender.Setup(candidate => candidate.Send(
            It.Is<DeletePostEndpointCommand>(command => command.PostId == post.Id),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await controller.DeletePost(post.Id);

        sender.Verify(candidate => candidate.Send(
            It.IsAny<RebindAssetParentCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid AssetId = Guid.NewGuid();
    private static readonly SocialMediaAssetDescriptor Descriptor = new(
        AssetId,
        $"/api/assets/{AssetId}/content",
        "image/webp",
        512,
        SocialMediaProcessingState.Ready);
    private static readonly Post CreatedPost = Post.Create(ActorId, "With asset", PostVisibility.Public);

    private static (PostsCrudController Controller, Mock<ISender> Sender) ControllerWithSender()
    {
        var (controller, sender, _) = ControllerWithSenderAndPosts();
        return (controller, sender);
    }

    private static PostsCrudController BuildController(
        Guid actorId,
        ISender sender,
        ISocialMediaAssetService assets)
    {
        var actor = new ActorContextAccessor();
        actor.SetActorContext(ActorContextBuilder.ForUser(actorId).WithTenantId(Guid.NewGuid()).Build());
        return new PostsCrudController(Mock.Of<IPostService>(), actor, sender, assets);
    }

    private static (PostsCrudController Controller, Mock<ISender> Sender, List<Post> Posts) ControllerWithSenderAndPosts()
    {
        var assets = new Mock<ISocialMediaAssetService>();
        assets.Setup(service => service.ResolveReadyOwnedInTenantAsync(AssetId, ActorId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Descriptor);

        var sender = new Mock<ISender>();
        sender.Setup(candidate => candidate.Send(
                It.Is<CreatePostEndpointCommand>(command => command.MediaAssetReferenceId == AssetId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(CreatedPost));
        sender.Setup(candidate => candidate.Send(
                It.Is<CreatePostEndpointCommand>(command => command.MediaAssetReferenceId == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Post.Create(ActorId, "No asset", PostVisibility.Public)));

        var posts = new List<Post>();
        var postService = new Mock<IPostService>();
        postService.Setup(service => service.GetPostByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                posts.FirstOrDefault(p => p.Id == id) is { } post
                    ? Result.Success(post)
                    : Result.Failure<Post>(Error.NotFound("Post.NotFound", "Post not found")));

        var actor = new ActorContextAccessor();
        actor.SetActorContext(ActorContextBuilder.ForUser(ActorId).WithTenantId(Guid.NewGuid()).Build());
        return (new PostsCrudController(postService.Object, actor, sender.Object, assets.Object), sender, posts);
    }
}
