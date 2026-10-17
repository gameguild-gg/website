using FluentAssertions;
using Xunit;

namespace GameGuild.Social.Posts.Controllers.Tests;

/// <summary>
/// T17: the public post projection hides managed media whose asset is not ready
/// to serve (fail closed), while legacy posts (no asset reference) pass through.
/// </summary>
public class PostPublicMediaGateTests
{
    private static Post PostWithManagedMedia()
    {
        var post = Post.Create(Guid.NewGuid(), "caption", PostVisibility.Public);
        post.AttachMediaAsset(Guid.NewGuid(), "/api/assets/abc/content", MediaType.Image);
        return post;
    }

    [Fact]
    public void MapPublicDto_UnsafeMedia_NullsMediaFields()
    {
        var post = PostWithManagedMedia();

        var dto = PostMappings.MapPublicDto(post, mediaReady: false);

        dto.MediaUrl.Should().BeNull();
        dto.MediaAssetReferenceId.Should().BeNull();
        dto.MediaType.Should().BeNull();
        dto.Content.Should().Be("caption");
    }

    [Fact]
    public void MapPublicDto_ReadyMedia_KeepsMediaFields()
    {
        var post = PostWithManagedMedia();

        var dto = PostMappings.MapPublicDto(post, mediaReady: true);

        dto.MediaUrl.Should().Be("/api/assets/abc/content");
        dto.MediaAssetReferenceId.Should().NotBeNull();
        dto.MediaType.Should().Be(nameof(MediaType.Image));
    }

    [Fact]
    public void MapPublicDto_LegacyPost_PreservesMediaUrl()
    {
        var post = Post.Create(Guid.NewGuid(), "legacy", PostVisibility.Public);
        post.AttachMedia("https://legacy.example/a.png", MediaType.Image);

        var dto = PostMappings.MapPublicDto(post, mediaReady: false);

        dto.MediaUrl.Should().Be("https://legacy.example/a.png");
        dto.MediaAssetReferenceId.Should().BeNull();
    }
}
