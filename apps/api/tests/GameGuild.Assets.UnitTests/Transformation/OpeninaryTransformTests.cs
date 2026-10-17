// T6 — Openinary-backed transformations: spec→URL mapper, producer cache-write,
// gated CDN 302 redirect, post-approval prewarm hook.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.Assets.Controllers;
using GameGuild.Assets.Openinary;
using GameGuild.Assets.Transformation;
using GameGuild.Features;
using GameGuild.Identity.Tenants;
using Xunit;

namespace GameGuild.Assets.UnitTests.Transformation;

public class OpeninaryTransformMapperTests
{
    [Fact]
    public void Maps_ExampleSpec_ToOpeninarySegment()
    {
        var spec = TransformationSpec.Parse("w=100,h=100,fit=cover,q=80,f=webp");
        OpeninaryTransformMapper.ToSegment(spec).Should().Be("w_100,h_100,c_fill,q_80,f_webp");
    }

    [Theory]
    [InlineData("fit=contain", "c_fit")]
    [InlineData("fit=cover", "c_fill")]
    [InlineData("fit=fill", "c_fill")]
    [InlineData("fit=outside", "c_fit")]
    public void Maps_EverySupportedFit(string canonical, string segment)
    {
        OpeninaryTransformMapper.ToSegment(TransformationSpec.Parse(canonical))
            .Should().Be(segment);
    }

    [Fact]
    public void Fit_Inside_Throws()
    {
        var act = () => OpeninaryTransformMapper.ToSegment(TransformationSpec.Parse("fit=inside"));
        act.Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData("f=jpeg", "f_jpeg")]
    [InlineData("f=png", "f_png")]
    [InlineData("f=webp", "f_webp")]
    [InlineData("f=avif", "f_avif")]
    public void Maps_EverySupportedFormat(string canonical, string segment)
    {
        OpeninaryTransformMapper.ToSegment(TransformationSpec.Parse(canonical))
            .Should().Be(segment);
    }

    [Theory]
    [InlineData("f=original")]
    public void Format_Original_IsOmitted(string canonical)
    {
        OpeninaryTransformMapper.ToSegment(TransformationSpec.Parse(canonical))
            .Should().NotContain("f_");
    }

    [Fact]
    public void Format_Gif_Throws()
    {
        var act = () => OpeninaryTransformMapper.ToSegment(TransformationSpec.Parse("f=gif"));
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DeterministicOrder_WidthHeightFitQualityFormat()
    {
        var spec = TransformationSpec.Parse("q=80,f=png,fit=cover,h=50,w=25");
        OpeninaryTransformMapper.ToSegment(spec).Should().Be("w_25,h_50,c_fill,q_80,f_png");
    }

    [Fact]
    public void PartialSpec_MapsOnlyPresentParts()
    {
        OpeninaryTransformMapper.ToSegment(TransformationSpec.Parse("w=400"))
            .Should().Be("w_400");
    }
}

public class DefaultTransformVariantsTests
{
    [Fact]
    public void Contains_PinnedDefaultSegments()
    {
        DefaultTransformVariants.Segments.Should().Equal("w_400,h_400,c_fill,f_webp", "w_1200,f_auto");
    }
}

public class OpeninaryTransformationProducerTests
{
    private readonly Mock<IAssetReferenceRepository> _refRepo = new();
    private readonly Mock<ITransformedAssetRepository> _transformedRepo = new();
    private readonly Mock<IAssetStorageService> _storage = new();
    private readonly Mock<IAssetTokenService> _tokenService = new();
    private readonly Mock<ITenantMemberRepository> _tenantMembers = new();
    private readonly Mock<IFeatureFlagEvaluationService> _features = new();
    private readonly Mock<IAssetFolderAuthorizationService> _folders = new();
    private readonly Mock<IAssetScopedAccessService> _scoped = new();
    private readonly Mock<IAssetContentRepository> _contentRepo = new();

    private AssetAccessService CreateService(OpeninaryOptions? openinary = null)
    {
        _features.Setup(f => f.IsEnabledAsync(
                FeatureFlagConstants.AssetFeatureFlags.TransformationsEnabled,
                It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return new AssetAccessService(
            _refRepo.Object,
            _transformedRepo.Object,
            _storage.Object,
            _tokenService.Object,
            _tenantMembers.Object,
            _features.Object,
            Array.Empty<IAssetParentAuthorizationResolver>(),
            _folders.Object,
            _scoped.Object,
            Options.Create(new AssetAccessOptions { BaseUrl = "https://cdn.example.com" }),
            NullLogger<AssetAccessService>.Instance,
            _contentRepo.Object,
            Options.Create(openinary ?? new OpeninaryOptions { ApiKey = "key" }));
    }

    private static AssetContent OpeninaryContent(
        VirusScanStatus scan = VirusScanStatus.Clean,
        ModerationStatus mod = ModerationStatus.Approved,
        string mimeType = "image/png",
        string objectKey = "platform/img.png")
    {
        var c = new AssetContent(
            OpeninaryOptions.StorageBackendName, objectKey, "abc123def456", mimeType, 1024, 100, 100);
        c.VirusScanStatus = scan;
        c.ModerationStatus = mod;
        return c;
    }

    private static AssetContent S3Content()
    {
        var c = new AssetContent("legacy-bucket", "k/img.png", "abc123def456", "image/png", 1024, 100, 100);
        c.VirusScanStatus = VirusScanStatus.Clean;
        c.ModerationStatus = ModerationStatus.Approved;
        return c;
    }

    [Fact]
    public async Task Miss_OpeninaryContent_PersistsRowWithUrl()
    {
        var content = OpeninaryContent();
        var contentId = Guid.NewGuid();
        typeof(AssetContent).GetProperty("Id")!.SetValue(content, contentId);
        _contentRepo.Setup(r => r.GetByIdAsync(contentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        _transformedRepo.Setup(r => r.GetAsync(contentId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransformedAsset?)null);
        _transformedRepo.Setup(r => r.AddAsync(It.IsAny<TransformedAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransformedAsset a, CancellationToken _) => a);

        var svc = CreateService(new OpeninaryOptions { BaseUrl = "https://openinary.test", ApiKey = "k" });
        var result = await svc.GetOrCreateTransformationAsync(
            contentId, TransformationSpec.Parse("w=100,h=100,fit=cover,q=80,f=webp"));

        result.Should().NotBeNull();
        _transformedRepo.Verify(r => r.AddAsync(
            It.Is<TransformedAsset>(t =>
                t.SourceContentId == contentId &&
                t.BucketName == OpeninaryOptions.StorageBackendName &&
                t.ObjectKey == "t/w_100,h_100,c_fill,q_80,f_webp/platform/img.png"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Miss_S3LegacyContent_ReturnsNull()
    {
        var content = S3Content();
        var contentId = Guid.NewGuid();
        typeof(AssetContent).GetProperty("Id")!.SetValue(content, contentId);
        _contentRepo.Setup(r => r.GetByIdAsync(contentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        _transformedRepo.Setup(r => r.GetAsync(contentId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransformedAsset?)null);

        var svc = CreateService();
        var result = await svc.GetOrCreateTransformationAsync(contentId, TransformationSpec.Parse("w=100"));

        result.Should().BeNull();
        _transformedRepo.Verify(r => r.AddAsync(It.IsAny<TransformedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Hit_OpeninaryContent_RecordsAccessWithoutRecreate()
    {
        var content = OpeninaryContent();
        var contentId = Guid.NewGuid();
        typeof(AssetContent).GetProperty("Id")!.SetValue(content, contentId);
        _contentRepo.Setup(r => r.GetByIdAsync(contentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        var cached = new TransformedAsset
        {
            Id = Guid.NewGuid(),
            SourceContentId = contentId,
            TransformationSpec = "w=100",
            BucketName = OpeninaryOptions.StorageBackendName,
            ObjectKey = "t/w_100/platform/img.png",
            MimeType = "image/webp",
            SizeBytes = 10
        };
        _transformedRepo.Setup(r => r.GetAsync(contentId, "w=100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var svc = CreateService();
        var result = await svc.GetOrCreateTransformationAsync(contentId, TransformationSpec.Parse("w=100"));

        result.Should().NotBeNull();
        result!.Id.Should().Be(cached.Id);
        _transformedRepo.Verify(r => r.AddAsync(It.IsAny<TransformedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
        _transformedRepo.Verify(r => r.UpdateAsync(cached, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class OpeninaryPrewarmTests
{
    private readonly Mock<IAssetReferenceRepository> _refRepo = new();
    private readonly Mock<ITransformedAssetRepository> _transformedRepo = new();
    private readonly Mock<IAssetStorageService> _storage = new();
    private readonly Mock<IAssetTokenService> _tokenService = new();
    private readonly Mock<ITenantMemberRepository> _tenantMembers = new();
    private readonly Mock<IFeatureFlagEvaluationService> _features = new();
    private readonly Mock<IAssetFolderAuthorizationService> _folders = new();
    private readonly Mock<IAssetScopedAccessService> _scoped = new();
    private readonly Mock<IAssetContentRepository> _contentRepo = new();

    private AssetAccessService CreateService(OpeninaryOptions openinary) => new(
        _refRepo.Object,
        _transformedRepo.Object,
        _storage.Object,
        _tokenService.Object,
        _tenantMembers.Object,
        _features.Object,
        Array.Empty<IAssetParentAuthorizationResolver>(),
        _folders.Object,
        _scoped.Object,
        Options.Create(new AssetAccessOptions { BaseUrl = "https://cdn.example.com" }),
        NullLogger<AssetAccessService>.Instance,
        _contentRepo.Object,
        Options.Create(openinary));

    private static AssetContent Content(string bucket, string mime = "image/png")
    {
        var c = new AssetContent(bucket, "platform/img.png", "abc123def456", mime, 1024, 100, 100);
        c.VirusScanStatus = VirusScanStatus.Clean;
        c.ModerationStatus = ModerationStatus.Approved;
        return c;
    }

    [Fact]
    public async Task ImageAndEnabled_PersistsBothDefaultRows()
    {
        _features.Setup(f => f.IsEnabledAsync(
                FeatureFlagConstants.AssetFeatureFlags.TransformationsEnabled,
                It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _transformedRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransformedAsset?)null);
        _transformedRepo.Setup(r => r.AddAsync(It.IsAny<TransformedAsset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransformedAsset a, CancellationToken _) => a);

        var svc = CreateService(new OpeninaryOptions { ApiKey = "k", BaseUrl = "https://openinary.test" });
        await svc.PrewarmDefaultVariantsAsync(Content(OpeninaryOptions.StorageBackendName));

        _transformedRepo.Verify(r => r.AddAsync(It.IsAny<TransformedAsset>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task NonImage_Skips()
    {
        var svc = CreateService(new OpeninaryOptions { ApiKey = "k" });
        await svc.PrewarmDefaultVariantsAsync(Content(OpeninaryOptions.StorageBackendName, "video/mp4"));

        _transformedRepo.Verify(r => r.AddAsync(It.IsAny<TransformedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task S3LegacyContent_Skips()
    {
        var svc = CreateService(new OpeninaryOptions { ApiKey = "k" });
        await svc.PrewarmDefaultVariantsAsync(Content("legacy-bucket"));

        _transformedRepo.Verify(r => r.AddAsync(It.IsAny<TransformedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OpeninaryDisabled_Skips()
    {
        var svc = CreateService(new OpeninaryOptions()); // empty ApiKey → disabled
        await svc.PrewarmDefaultVariantsAsync(Content(OpeninaryOptions.StorageBackendName));

        _transformedRepo.Verify(r => r.AddAsync(It.IsAny<TransformedAsset>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class AssetsCdnControllerTransformationGateTests
{
    private readonly Mock<IAssetAccessService> _access = new();
    private readonly Mock<IAssetStorageService> _storage = new();
    private readonly Mock<IAssetContentRepository> _contentRepo = new();
    private readonly Mock<IAssetReferenceRepository> _refRepo = new();

    private AssetsCdnController CreateController()
    {
        var controller = new AssetsCdnController(
            _access.Object, _storage.Object, _contentRepo.Object, _refRepo.Object,
            durableEventProducer: null,
            openinaryOptions: Options.Create(new OpeninaryOptions { ApiKey = "k" }));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private static AssetContent OpeninaryContent(
        VirusScanStatus scan = VirusScanStatus.Clean,
        ModerationStatus mod = ModerationStatus.Approved)
    {
        var c = new AssetContent(
            OpeninaryOptions.StorageBackendName, "platform/img.png", "abc123def456", "image/png", 1024, 100, 100);
        c.VirusScanStatus = scan;
        c.ModerationStatus = mod;
        return c;
    }

    private static AssetReference CreateRef(
        AssetAccessPolicy policy = AssetAccessPolicy.Public,
        Guid contentId = default)
    {
        var r = new AssetReference(
            contentId == default ? Guid.NewGuid() : contentId,
            Guid.NewGuid(), "test", policy, null, null);
        return r;
    }

    private void SetupValidToken(Guid referenceId, Guid contentId, AssetContent content, AssetAccessPolicy policy)
    {
        _access.Setup(a => a.ValidateAccessTokenAsync(referenceId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenValidationResult(true));
        var reference = CreateRef(policy, contentId);
        _refRepo.Setup(r => r.GetByIdAsync(referenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);
        _contentRepo.Setup(r => r.GetByIdAsync(contentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        _access.Setup(a => a.GetOrCreateTransformationAsync(contentId, It.IsAny<TransformationSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransformedAssetInfo(
                Guid.NewGuid(), contentId,
                OpeninaryOptions.StorageBackendName,
                "t/w_100/platform/img.png",
                "image/webp", "etag", 10));
    }

    private Task<IActionResult> Act(Guid referenceId)
        => CreateController().GetTransformedAsset("w=100", referenceId, "tok");

    [Fact]
    public async Task PublicAndSafe_Redirects302ToAbsoluteOpeninaryUrl()
    {
        var referenceId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        SetupValidToken(referenceId, contentId, OpeninaryContent(), AssetAccessPolicy.Public);

        var result = await Act(referenceId);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Permanent.Should().BeFalse();
        redirect.Url.Should().Be("https://openinary.gameguild.gg/t/w_100/platform/img.png");
    }

    [Fact]
    public async Task TenantPublicAndSafe_Redirects302()
    {
        var referenceId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        SetupValidToken(referenceId, contentId, OpeninaryContent(), AssetAccessPolicy.TenantPublic);

        var result = await Act(referenceId);

        result.Should().BeOfType<RedirectResult>();
    }

    [Fact]
    public async Task PrivatePolicy_Returns404()
    {
        var referenceId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        SetupValidToken(referenceId, contentId, OpeninaryContent(), AssetAccessPolicy.Private);

        var result = await Act(referenceId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ModerationBlocked_Returns404()
    {
        var referenceId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        SetupValidToken(referenceId, contentId,
            OpeninaryContent(mod: ModerationStatus.Blocked), AssetAccessPolicy.Public);

        var result = await Act(referenceId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PendingModeration_Returns404()
    {
        var referenceId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        SetupValidToken(referenceId, contentId,
            OpeninaryContent(mod: ModerationStatus.Pending), AssetAccessPolicy.Public);

        var result = await Act(referenceId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task InfectedScan_Returns404()
    {
        var referenceId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        SetupValidToken(referenceId, contentId,
            OpeninaryContent(scan: VirusScanStatus.Infected), AssetAccessPolicy.Public);

        var result = await Act(referenceId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task S3LegacyContent_KeepsLegacyStreamingPath()
    {
        var referenceId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        var s3 = new AssetContent("legacy-bucket", "k/img.png", "abc", "image/png", 1024, 100, 100)
        {
            VirusScanStatus = VirusScanStatus.Clean,
            ModerationStatus = ModerationStatus.Approved
        };
        _access.Setup(a => a.ValidateAccessTokenAsync(referenceId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenValidationResult(true));
        _refRepo.Setup(r => r.GetByIdAsync(referenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRef(AssetAccessPolicy.Private, contentId));
        _contentRepo.Setup(r => r.GetByIdAsync(contentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(s3);
        _access.Setup(a => a.GetOrCreateTransformationAsync(contentId, It.IsAny<TransformationSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransformedAssetInfo(
                Guid.NewGuid(), contentId, "assets-transformed", "transformed/img.webp", "image/webp", "etag", 10));
        _storage.Setup(s => s.DownloadAsync("assets-transformed", "transformed/img.webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        var result = await Act(referenceId);

        result.Should().BeOfType<FileStreamResult>(); // legacy path unchanged
    }
}
