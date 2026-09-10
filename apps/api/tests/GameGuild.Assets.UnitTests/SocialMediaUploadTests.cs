using GameGuild.Assets.SocialMedia;
using GameGuild.Assets.Commands;
using GameGuild.Assets.Controllers;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Assets.UnitTests;

public sealed class SocialMediaUploadTests
{
    [Theory]
    [InlineData("image/jpeg", 10 * 1024 * 1024)]
    [InlineData("image/png", 10 * 1024 * 1024)]
    [InlineData("image/webp", 10 * 1024 * 1024)]
    [InlineData("image/gif", 10 * 1024 * 1024)]
    [InlineData("video/mp4", 100 * 1024 * 1024)]
    public async Task ValidateAsync_AllowsSupportedMediaAtExactBoundary(string mimeType, long size)
    {
        await using var stream = new MemoryStream(HeaderFor(mimeType));

        var result = await SocialMediaAssetPolicy.ValidateAsync(stream, mimeType, size);

        result.IsValid.Should().BeTrue();
        stream.Position.Should().Be(0);
    }

    [Theory]
    [InlineData("image/jpeg", 10 * 1024 * 1024 + 1L)]
    [InlineData("video/mp4", 100 * 1024 * 1024 + 1L)]
    [InlineData("image/svg+xml", 100L)]
    public async Task ValidateAsync_RejectsOversizedOrUnsupportedMedia(string mimeType, long size)
    {
        await using var stream = new MemoryStream(HeaderFor(mimeType));

        var result = await SocialMediaAssetPolicy.ValidateAsync(stream, mimeType, size);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_RejectsContentWhoseSignatureDoesNotMatchMimeType()
    {
        await using var stream = new MemoryStream(HeaderFor("image/png"));

        var result = await SocialMediaAssetPolicy.ValidateAsync(stream, "video/mp4", stream.Length);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveReadyOwnedAsync_RequiresOwnerLiveReferenceAndCompletedProcessing()
    {
        var ownerId = Guid.NewGuid();
        var reference = ReadyAsset(ownerId);
        var repository = new Mock<IAssetReferenceRepository>();
        repository.Setup(value => value.GetByIdWithContentAsync(reference.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        var service = new SocialMediaAssetService(repository.Object);

        var ready = await service.ResolveReadyOwnedAsync(reference.Id, ownerId);
        var wrongOwner = await service.ResolveReadyOwnedAsync(reference.Id, Guid.NewGuid());
        reference.Content.SetVirusScanStatus(VirusScanStatus.Pending);
        var pending = await service.ResolveReadyOwnedAsync(reference.Id, ownerId);
        reference.Content.SetVirusScanStatus(VirusScanStatus.Clean);
        reference.SoftDelete();
        var deleted = await service.ResolveReadyOwnedAsync(reference.Id, ownerId);

        ready.Should().NotBeNull();
        ready!.AssetReferenceId.Should().Be(reference.Id);
        ready.MimeType.Should().Be("image/png");
        ready.SizeBytes.Should().Be(128);
        ready.DeliveryUrl.Should().Be($"/api/assets/{reference.Id}/content");
        wrongOwner.Should().BeNull();
        pending.Should().BeNull();
        deleted.Should().BeNull();
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

    private static byte[] HeaderFor(string mimeType) => mimeType switch
    {
        "image/jpeg" => [0xff, 0xd8, 0xff, 0xe0],
        "image/png" => [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a],
        "image/webp" => [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50],
        "image/gif" => "GIF89a"u8.ToArray(),
        "video/mp4" => [0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6f, 0x6d],
        _ => [1, 2, 3, 4]
    };
}

public sealed class SocialMediaAssetsControllerTests
{
    [Fact]
    public async Task Upload_RejectsSpoofedMediaBeforePersisting()
    {
        var sender = new Mock<ISender>();
        var assets = new Mock<ISocialMediaAssetService>();
        var controller = new SocialMediaAssetsController(sender.Object, Actor(Guid.NewGuid()), assets.Object);
        var file = FormFile(HeaderFor("image/png"), "video/mp4");

        var result = await controller.Upload(file, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        sender.Verify(candidate => candidate.Send(It.IsAny<UploadAssetCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_ReturnsFirstPartyAssetMetadataAndProcessingState()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var descriptor = new SocialMediaAssetDescriptor(
            assetId,
            $"/api/assets/{assetId}/content",
            "image/png",
            128,
            SocialMediaProcessingState.Processing);
        var sender = new Mock<ISender>();
        sender.Setup(candidate => candidate.Send(
                It.Is<UploadAssetCommand>(command =>
                    command.UserId == actorId &&
                    command.TenantId == tenantId &&
                    command.AccessPolicy == AssetAccessPolicy.Authenticated),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadAssetResponse(assetId, Guid.NewGuid(), new string('a', 64), false));
        var assets = new Mock<ISocialMediaAssetService>();
        assets.Setup(service => service.InspectOwnedAsync(assetId, actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptor);
        var controller = new SocialMediaAssetsController(sender.Object, Actor(actorId, tenantId), assets.Object);
        var file = FormFile(HeaderFor("image/png"), "image/png");

        var result = await controller.Upload(file, CancellationToken.None);

        result.Should().BeOfType<CreatedResult>()
            .Which.Value.Should().Be(descriptor);
    }

    [Fact]
    public async Task GetStatus_ReturnsOnlyTheAuthenticatedOwnersAsset()
    {
        var actorId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var descriptor = new SocialMediaAssetDescriptor(
            assetId,
            $"/api/assets/{assetId}/content",
            "video/mp4",
            128,
            SocialMediaProcessingState.Ready);
        var assets = new Mock<ISocialMediaAssetService>();
        assets.Setup(service => service.InspectOwnedAsync(assetId, actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(descriptor);
        var controller = new SocialMediaAssetsController(
            Mock.Of<ISender>(),
            Actor(actorId),
            assets.Object);

        var result = await controller.GetStatus(assetId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(descriptor);
        assets.Verify(service => service.InspectOwnedAsync(assetId, actorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IActorContextAccessor Actor(Guid userId, Guid? tenantId = null)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(ActorContextBuilder.ForUser(userId).WithTenantId(tenantId ?? Guid.NewGuid()).Build());
        return accessor;
    }

    private static FormFile FormFile(byte[] contents, string mimeType)
        => new(new MemoryStream(contents), 0, contents.Length, "file", "upload")
        {
            Headers = new HeaderDictionary(),
            ContentType = mimeType
        };

    private static byte[] HeaderFor(string mimeType) => mimeType switch
    {
        "image/png" => [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a],
        _ => [1, 2, 3, 4]
    };
}
