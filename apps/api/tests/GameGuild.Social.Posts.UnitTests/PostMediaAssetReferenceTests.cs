using FluentAssertions;
using GameGuild.Assets;
using GameGuild.Social.Assets.SocialMedia;
using GameGuild.Social.Posts.Services;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace GameGuild.Social.Posts.Tests;

public sealed class PostMediaAssetEntityTests
{
    [Fact]
    public void AttachMediaAsset_StoresReferenceIdAndDenormalizedAccessUrl()
    {
        var post = Post.Create(Guid.NewGuid(), "caption", PostVisibility.Public);
        var assetId = Guid.NewGuid();

        post.AttachMediaAsset(assetId, "/api/assets/{assetId}/content", MediaType.Image);

        post.MediaAssetReferenceId.Should().Be(assetId);
        post.MediaUrl.Should().Be("/api/assets/{assetId}/content");
        post.MediaType.Should().Be(MediaType.Image);
    }

    [Fact]
    public void AttachMediaAsset_RejectsEmptyIdOrUrl()
    {
        var post = Post.Create(Guid.NewGuid(), "caption", PostVisibility.Public);

        var actId = () => post.AttachMediaAsset(Guid.Empty, "/url", MediaType.Image);
        var actUrl = () => post.AttachMediaAsset(Guid.NewGuid(), " ", MediaType.Image);

        actId.Should().Throw<ArgumentException>();
        actUrl.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LegacyPost_NullReferenceId_RendersUnchangedViaAttachMedia()
    {
        var post = Post.Create(Guid.NewGuid(), "legacy", PostVisibility.Public);
        post.AttachMedia("https://legacy.example/a.png", MediaType.Image);

        post.MediaAssetReferenceId.Should().BeNull();
        post.MediaUrl.Should().Be("https://legacy.example/a.png");
        post.MediaType.Should().Be(MediaType.Image);
    }

    [Fact]
    public void AttachMedia_ClearsStaleAssetReferenceId()
    {
        var post = Post.Create(Guid.NewGuid(), "caption", PostVisibility.Public);
        post.AttachMediaAsset(Guid.NewGuid(), "/api/assets/x/content", MediaType.Image);

        post.AttachMedia("https://elsewhere.example/b.png", MediaType.Image);

        post.MediaAssetReferenceId.Should().BeNull();
        post.MediaUrl.Should().Be("https://elsewhere.example/b.png");
    }
}

public sealed class PostAssetUsageGuardTests
{
    [Fact]
    public async Task IsInUseAsync_TrueWhileLivePostCarriesTheAsset_AndFalseForDeletedOrLegacyPosts()
    {
        var assetId = Guid.NewGuid();
        var live = Post.Create(Guid.NewGuid(), "live", PostVisibility.Public);
        live.AttachMediaAsset(assetId, $"/api/assets/{assetId}/content", MediaType.Image);
        var deleted = Post.Create(Guid.NewGuid(), "deleted", PostVisibility.Public);
        deleted.AttachMediaAsset(assetId, $"/api/assets/{assetId}/content", MediaType.Image);
        typeof(EntityBase)
            .GetProperty("Version")?
            .SetValue(deleted, 1);
        deleted.Delete();
        var legacy = Post.Create(Guid.NewGuid(), "legacy", PostVisibility.Public);

        var db = new Mock<IApplicationDbContext>();
        db.Setup(context => context.Set<Post>())
            .Returns(new List<Post> { live, deleted, legacy }.AsQueryable().BuildMockDbSet().Object);

        var guard = new PostAssetUsageGuard(db.Object);

        (await guard.IsInUseAsync(assetId)).Should().BeTrue();
        (await guard.IsInUseAsync(Guid.NewGuid())).Should().BeFalse();
    }
}

public sealed class SocialMediaAssetTenantResolveTests
{
    [Fact]
    public async Task ResolveReadyOwnedInTenantAsync_RejectsForeignTenantOrMissingReference()
    {
        var ownerId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var reference = ReadyAsset(ownerId);
        var repository = new Mock<IAssetReferenceRepository>();
        repository.Setup(value => value.GetByIdAsync(reference.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        repository.Setup(value => value.GetByIdWithContentAsync(reference.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        var service = new SocialMediaAssetService(repository.Object);

        var sameTenant = await service.ResolveReadyOwnedInTenantAsync(reference.Id, ownerId, reference.TenantId);
        var foreignTenant = await service.ResolveReadyOwnedInTenantAsync(reference.Id, ownerId, tenantA);
        reference.TenantId = null;
        var tenantless = await service.ResolveReadyOwnedInTenantAsync(reference.Id, ownerId, tenantA);
        var noTenantCheck = await service.ResolveReadyOwnedInTenantAsync(reference.Id, ownerId, null);

        sameTenant.Should().NotBeNull();
        sameTenant!.AssetReferenceId.Should().Be(reference.Id);
        sameTenant.DeliveryUrl.Should().Be($"/api/assets/{reference.Id}/content");
        foreignTenant.Should().BeNull();
        tenantless.Should().BeNull();
        noTenantCheck.Should().NotBeNull();
    }

    private static AssetReference ReadyAsset(Guid ownerId)
    {
        var content = new AssetContent("assets", "key", new string('a', 64), "image/png", 128, 1, 1)
        {
            Id = Guid.NewGuid()
        };
        content.SetVirusScanStatus(VirusScanStatus.Clean);
        content.SetModerationStatus(ModerationStatus.Approved);
        return new AssetReference(content.Id, ownerId, "post.png", AssetAccessPolicy.Authenticated, null, null)
        {
            Id = Guid.NewGuid(),
            Version = 1,
            Content = content
        };
    }
}
