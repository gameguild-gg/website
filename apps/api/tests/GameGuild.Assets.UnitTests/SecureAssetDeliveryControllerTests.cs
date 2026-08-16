using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.Assets.Security;
using GameGuild.Assets.Transformation;
using GameGuild.Identity.Context.Actors;
using Moq;
using AssetTenantValidationResult = GameGuild.Assets.Security.TenantValidationResult;

namespace GameGuild.Assets.UnitTests;

public sealed class SecureAssetDeliveryControllerTests
{
    [Fact]
    public async Task GetContent_WithValidSignedToken_UsesAssetTenantAndRedirectsToStorage()
    {
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateReference(assetId, tenantId);
        var accessService = new Mock<IAssetAccessService>();
        var tenantValidation = new Mock<ITenantAssetValidationService>();
        var storageService = new Mock<IAssetStorageService>();

        accessService
            .Setup(service => service.ValidateToken("valid-token", assetId, tenantId))
            .Returns(true);
        tenantValidation
            .Setup(service => service.ValidateTokenTenant(tenantId, null))
            .Returns(new AssetTenantValidationResult(true, ResolvedTenantId: tenantId));
        storageService
            .Setup(service => service.GeneratePresignedUrlAsync(
                reference.Content.BucketName,
                reference.Content.ObjectKey,
                TimeSpan.FromMinutes(15),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.example.test/signed-object");

        var controller = CreateController(
            reference,
            ActorContext.Anonymous,
            accessService,
            tenantValidation,
            storageService);

        var result = await controller.GetContent(assetId, "valid-token", null, CancellationToken.None);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be("https://storage.example.test/signed-object");
        tenantValidation.Verify(
            service => service.ValidateTenantAccess(It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<ActorContext>()),
            Times.Never);
    }

    [Fact]
    public async Task GetContent_WithInvalidSignedToken_ReturnsProblem403InsteadOfAuthenticationSchemeFailure()
    {
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateReference(assetId, tenantId);
        var accessService = new Mock<IAssetAccessService>();

        accessService
            .Setup(service => service.ValidateToken("invalid-token", assetId, tenantId))
            .Returns(false);

        var controller = CreateController(
            reference,
            ActorContext.Anonymous,
            accessService,
            new Mock<ITenantAssetValidationService>(),
            new Mock<IAssetStorageService>());

        var result = await controller.GetContent(assetId, "invalid-token", null, CancellationToken.None);

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        response.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Be("Invalid or expired token");
    }

    [Fact]
    public async Task GetContent_WithoutToken_ValidatesActualAssetTenant()
    {
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var unrelatedParentId = Guid.NewGuid();
        var actor = CreateActor(tenantId);
        var reference = CreateReference(assetId, tenantId, unrelatedParentId);
        var accessService = new Mock<IAssetAccessService>();
        var tenantValidation = new Mock<ITenantAssetValidationService>();

        tenantValidation
            .Setup(service => service.ValidateTenantAccess(tenantId, tenantId, actor))
            .Returns(new AssetTenantValidationResult(true, ResolvedTenantId: tenantId));
        accessService
            .Setup(service => service.GenerateDirectStorageUrlAsync(
                assetId,
                actor.SubjectIdAsGuid,
                tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessUrl(
                "https://storage.example.test/actor-object",
                string.Empty,
                DateTimeOffset.UtcNow.AddMinutes(15),
                reference.Content.MimeType));

        var controller = CreateController(
            reference,
            actor,
            accessService,
            tenantValidation,
            new Mock<IAssetStorageService>());

        var result = await controller.GetContent(assetId, null, null, CancellationToken.None);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be("https://storage.example.test/actor-object");
        tenantValidation.Verify(service => service.ValidateTenantAccess(tenantId, tenantId, actor), Times.Once);
        tenantValidation.Verify(
            service => service.ValidateTenantAccess(tenantId, unrelatedParentId, actor),
            Times.Never);
    }

    private static SecureAssetDeliveryController CreateController(
        AssetReference reference,
        ActorContext actor,
        Mock<IAssetAccessService> accessService,
        Mock<ITenantAssetValidationService> tenantValidation,
        Mock<IAssetStorageService> storageService)
    {
        var rateLimitService = new Mock<IAssetRateLimitService>();
        rateLimitService
            .Setup(service => service.IsIpBlockedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        rateLimitService
            .Setup(service => service.CheckAssetAccessRateAsync(reference.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 1, 100));
        rateLimitService
            .Setup(service => service.Record403ResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitResult(true, 1, 100));

        var referenceRepository = new Mock<IAssetReferenceRepository>();
        referenceRepository
            .Setup(repository => repository.GetByIdWithContentAsync(reference.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actor);

        var controller = new SecureAssetDeliveryController(
            accessService.Object,
            rateLimitService.Object,
            tenantValidation.Object,
            Mock.Of<ITransformationValidator>(),
            Mock.Of<IDownloadWindowService>(),
            Mock.Of<IAssetContentRepository>(),
            referenceRepository.Object,
            actorAccessor.Object,
            storageService.Object,
            Options.Create(new AssetAccessOptions { DefaultExpiryMinutes = 15 }),
            NullLogger<SecureAssetDeliveryController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static AssetReference CreateReference(Guid assetId, Guid tenantId, Guid? parentId = null)
    {
        var content = new AssetContent(
            "assets",
            $"objects/{assetId}",
            new string('a', 64),
            "text/plain",
            12,
            null,
            null)
        {
            Id = Guid.NewGuid(),
            VirusScanStatus = VirusScanStatus.Clean,
            ModerationStatus = ModerationStatus.Approved
        };

        return new AssetReference(
            content.Id,
            Guid.NewGuid(),
            "signed asset",
            AssetAccessPolicy.Private,
            parentId.HasValue ? "resource" : null,
            parentId)
        {
            Id = assetId,
            TenantId = tenantId,
            Content = content
        };
    }

    private static ActorContext CreateActor(Guid tenantId)
        => new()
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        };
}
