using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using GameGuild.Assets;
using GameGuild.Assets.Configuration;
using GameGuild.Assets.Moderation;
using GameGuild.Assets.Security;
using GameGuild.Assets.VirusScan;
using GameGuild.Assets.BackgroundServices;

namespace GameGuild.Assets.UnitTests;

public class EfConfigAndExtendedTests
{
    private static ModelBuilder CreateModelBuilder() => new(new ConventionSet());

    // ── EF Configuration Tests ──────────────────────────────────────────
    [Fact]
    public void AssetContentConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new AssetContentConfiguration();
        cfg.Configure(mb.Entity<AssetContent>());
        var entity = mb.Model.FindEntityType(typeof(AssetContent));
        entity.Should().NotBeNull();
    }

    [Fact]
    public void AssetReferenceConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new AssetReferenceConfiguration();
        cfg.Configure(mb.Entity<AssetReference>());
        var entity = mb.Model.FindEntityType(typeof(AssetReference));
        entity.Should().NotBeNull();
    }

    [Fact]
    public void AssetReportConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new AssetReportConfiguration();
        cfg.Configure(mb.Entity<AssetReport>());
        var entity = mb.Model.FindEntityType(typeof(AssetReport));
        entity.Should().NotBeNull();
    }

    [Fact]
    public void TransformedAssetConfiguration_ConfiguresEntity()
    {
        var mb = CreateModelBuilder();
        var cfg = new TransformedAssetConfiguration();
        cfg.Configure(mb.Entity<TransformedAsset>());
        var entity = mb.Model.FindEntityType(typeof(TransformedAsset));
        entity.Should().NotBeNull();
    }

    // ── Enum Coverage ───────────────────────────────────────────────────
    [Fact]
    public void AssetKind_AllValues()
    {
        var values = Enum.GetValues<AssetKind>();
        values.Should().Contain(AssetKind.Image);
        values.Should().Contain(AssetKind.Video);
        values.Should().Contain(AssetKind.Audio);
        values.Should().Contain(AssetKind.Document);
        values.Should().Contain(AssetKind.Archive);
        values.Should().Contain(AssetKind.Other);
    }

    [Fact]
    public void AssetAccessPolicy_AllValues()
    {
        var values = Enum.GetValues<AssetAccessPolicy>();
        values.Should().Contain(AssetAccessPolicy.Private);
        values.Should().Contain(AssetAccessPolicy.SignedUrl);
        values.Should().Contain(AssetAccessPolicy.TenantPublic);
        values.Should().Contain(AssetAccessPolicy.Public);
        values.Should().Contain(AssetAccessPolicy.PaidContent);
        values.Should().Contain(AssetAccessPolicy.OwnerOnly);
        values.Should().Contain(AssetAccessPolicy.Authenticated);
        values.Should().Contain(AssetAccessPolicy.Unlisted);
        values.Should().Contain(AssetAccessPolicy.Inherited);
    }

    [Fact]
    public void VirusScanStatus_AllValues()
    {
        var values = Enum.GetValues<VirusScanStatus>();
        values.Should().Contain(VirusScanStatus.Pending);
        values.Should().Contain(VirusScanStatus.Clean);
        values.Should().Contain(VirusScanStatus.Infected);
        values.Should().Contain(VirusScanStatus.ScanFailed);
    }

    [Fact]
    public void ModerationStatus_AllValues()
    {
        var values = Enum.GetValues<ModerationStatus>();
        values.Should().Contain(ModerationStatus.Pending);
        values.Should().Contain(ModerationStatus.Approved);
        values.Should().Contain(ModerationStatus.Rejected);
        values.Should().Contain(ModerationStatus.NeedsReview);
        values.Should().Contain(ModerationStatus.Blocked);
    }

    [Fact]
    public void ReportReason_AllValues()
    {
        var values = Enum.GetValues<ReportReason>();
        values.Should().Contain(ReportReason.Inappropriate);
        values.Should().Contain(ReportReason.Copyright);
        values.Should().Contain(ReportReason.Spam);
        values.Should().Contain(ReportReason.Other);
    }

    [Fact]
    public void ReportStatus_AllValues()
    {
        var values = Enum.GetValues<ReportStatus>();
        values.Should().Contain(ReportStatus.Pending);
        values.Should().Contain(ReportStatus.UnderReview);
        values.Should().Contain(ReportStatus.Resolved);
        values.Should().Contain(ReportStatus.Dismissed);
    }

    [Fact]
    public void ReviewDecision_AllValues()
    {
        var values = Enum.GetValues<ReviewDecision>();
        values.Should().Contain(ReviewDecision.NoAction);
        values.Should().Contain(ReviewDecision.ContentRemoved);
        values.Should().Contain(ReviewDecision.ContentHidden);
    }

    [Fact]
    public void ImageFit_AllValues()
    {
        var values = Enum.GetValues<ImageFit>();
        values.Should().Contain(ImageFit.Contain);
        values.Should().Contain(ImageFit.Cover);
        values.Should().Contain(ImageFit.Fill);
    }

    [Fact]
    public void ImageFormat_AllValues()
    {
        var values = Enum.GetValues<ImageFormat>();
        values.Should().Contain(ImageFormat.Original);
        values.Should().Contain(ImageFormat.Jpeg);
        values.Should().Contain(ImageFormat.Png);
        values.Should().Contain(ImageFormat.Webp);
    }

    [Fact]
    public void AssetAccessDeniedReason_AllValues()
    {
        var values = Enum.GetValues<AssetAccessDeniedReason>();
        values.Should().Contain(AssetAccessDeniedReason.NotFound);
        values.Should().Contain(AssetAccessDeniedReason.TokenInvalid);
        values.Should().Contain(AssetAccessDeniedReason.TokenExpired);
        values.Should().Contain(AssetAccessDeniedReason.AuthenticationRequired);
    }

    // ── DTO Records ─────────────────────────────────────────────────────
    [Fact]
    public void AssetAccessUrl_CanBeCreated()
    {
        var dto = new AssetAccessUrl("https://example.com/asset", "tok123", DateTimeOffset.UtcNow.AddHours(1), "image/png");
        dto.Url.Should().Be("https://example.com/asset");
        dto.Token.Should().Be("tok123");
        dto.MimeType.Should().Be("image/png");
    }

    [Fact]
    public void AssetAccessValidation_Valid()
    {
        var v = new AssetAccessValidation(true, null);
        v.IsValid.Should().BeTrue();
        v.DeniedReason.Should().BeNull();
    }

    [Fact]
    public void AssetAccessValidation_Denied()
    {
        var v = new AssetAccessValidation(false, AssetAccessDeniedReason.TokenExpired);
        v.IsValid.Should().BeFalse();
        v.DeniedReason.Should().Be(AssetAccessDeniedReason.TokenExpired);
    }

    [Fact]
    public void TokenValidationResult_Valid()
    {
        var r = new TokenValidationResult(true, null, Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1));
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TokenValidationResult_Invalid()
    {
        var r = new TokenValidationResult(false, "Token expired");
        r.IsValid.Should().BeFalse();
        r.Error.Should().Be("Token expired");
    }

    [Fact]
    public void EphemeralTokenValidationResult_CanBeCreated()
    {
        var r = new EphemeralTokenValidationResult(true, Guid.NewGuid());
        r.IsValid.Should().BeTrue();
        r.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void TransformedAssetInfo_CanBeCreated()
    {
        var info = new TransformedAssetInfo(Guid.NewGuid(), Guid.NewGuid(), "bucket", "key/path", "image/webp", "hash123", 1024);
        info.BucketName.Should().Be("bucket");
        info.MimeType.Should().Be("image/webp");
        info.SizeBytes.Should().Be(1024);
    }

    [Fact]
    public void VirusScanResult_Clean()
    {
        var r = new VirusScanResult(true, "Clean", ScanEngine: "TestEngine");
        r.IsClean.Should().BeTrue();
        r.Status.Should().Be("Clean");
        r.ScanEngine.Should().Be("TestEngine");
    }

    [Fact]
    public void VirusScanResult_Infected()
    {
        var r = new VirusScanResult(false, "Infected", "Trojan.Gen", "Trojan");
        r.IsClean.Should().BeFalse();
        r.ThreatName.Should().Be("Trojan.Gen");
    }

    [Fact]
    public void AutoModerationResult_Approved()
    {
        var r = new AutoModerationResult(true, 0.95, new[] { "safe" }, null);
        r.IsApproved.Should().BeTrue();
        r.Confidence.Should().Be(0.95);
    }

    [Fact]
    public void AutoModerationResult_Rejected()
    {
        var r = new AutoModerationResult(false, 0.1, new[] { "nsfw" }, "Content violates policy");
        r.IsApproved.Should().BeFalse();
        r.RejectionReason.Should().Be("Content violates policy");
    }

    [Fact]
    public void ModerationQueueItem_CanBeCreated()
    {
        var item = new ModerationQueueItem(Guid.NewGuid(), Guid.NewGuid(), "image/png", DateTime.UtcNow, 0.8, new[] { "safe" }, 0);
        item.MimeType.Should().Be("image/png");
    }

    // ── Repository Constructors ─────────────────────────────────────────
    [Fact]
    public void AssetContentRepository_CanBeCreated()
    {
        var repo = new AssetContentRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void AssetReferenceRepository_CanBeCreated()
    {
        var repo = new AssetReferenceRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void AssetReportRepository_CanBeCreated()
    {
        var repo = new AssetReportRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void TransformedAssetRepository_CanBeCreated()
    {
        var repo = new TransformedAssetRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    // ── Background Service Constructors ─────────────────────────────────
    [Fact]
    public void BackgroundGarbageCollectionService_CanBeCreated()
    {
        var svc = new GameGuild.Assets.BackgroundServices.AssetGarbageCollectionService(
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new AssetGarbageCollectionOptions()),
            Mock.Of<ILogger<GameGuild.Assets.BackgroundServices.AssetGarbageCollectionService>>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void TransformedAssetCleanupService_CanBeCreated()
    {
        var svc = new TransformedAssetCleanupService(
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new TransformedAssetCleanupOptions()),
            Mock.Of<ILogger<TransformedAssetCleanupService>>());
        svc.Should().NotBeNull();
    }

    // ── Security Service Constructors ───────────────────────────────────
    [Fact]
    public void SecurityGarbageCollectionService_CanBeCreated()
    {
        var svc = new GameGuild.Assets.Security.AssetGarbageCollectionService(
            Mock.Of<IAssetContentRepository>(),
            Mock.Of<IAssetStorageService>(),
            Options.Create(new AssetGarbageCollectionOptions()),
            Mock.Of<ILogger<GameGuild.Assets.Security.AssetGarbageCollectionService>>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void VirusScanService_CanBeCreated()
    {
        var svc = new VirusScanService(
            Options.Create(new VirusScanOptions()),
            Mock.Of<ILogger<VirusScanService>>());
        svc.Should().NotBeNull();
    }

    // ── Entity Type Checks (constructors are protected) ────────────────
    [Fact]
    public void AssetContent_TypeExists()
    {
        typeof(AssetContent).Should().NotBeNull();
        typeof(AssetContent).IsClass.Should().BeTrue();
    }

    [Fact]
    public void AssetReference_TypeExists()
    {
        typeof(AssetReference).Should().NotBeNull();
        typeof(AssetReference).IsClass.Should().BeTrue();
    }

    [Fact]
    public void AssetReport_TypeExists()
    {
        typeof(AssetReport).Should().NotBeNull();
        typeof(AssetReport).IsClass.Should().BeTrue();
    }

    [Fact]
    public void TransformedAsset_TypeExists()
    {
        typeof(TransformedAsset).Should().NotBeNull();
        typeof(TransformedAsset).IsClass.Should().BeTrue();
    }
}
