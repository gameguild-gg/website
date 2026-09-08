using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using GameGuild.Assets;
using GameGuild.Assets.Security;
using GameGuild.Assets.Storage;
using GameGuild.Assets.Transformation;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Assets.UnitTests;

public class ControllerAndServiceCtorTests2
{
    // ═══════════════════════════════════════════════════════════════════
    // SecureAssetDeliveryController
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SecureAssetDeliveryController_CanBeConstructed()
    {
        var ctrl = new SecureAssetDeliveryController(
            Mock.Of<IAssetAccessService>(),
            Mock.Of<IAssetRateLimitService>(),
            Mock.Of<ITenantAssetValidationService>(),
            Mock.Of<ITransformationValidator>(),
            Mock.Of<IDownloadWindowService>(),
            Mock.Of<IAssetContentRepository>(),
            Mock.Of<IAssetReferenceRepository>(),
            Mock.Of<IActorContextAccessor>(),
            Mock.Of<IAssetStorageService>(),
            Options.Create(new AssetAccessOptions()),
            Mock.Of<ILogger<SecureAssetDeliveryController>>());

        ctrl.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // DownloadWindowService
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DownloadWindowService_CanBeConstructed()
    {
        var svc = new DownloadWindowService(
            Mock.Of<IAssetReferenceRepository>(),
            Mock.Of<IOrderValidationService>(),
            Options.Create(new DownloadWindowOptions()),
            Mock.Of<ILogger<DownloadWindowService>>());

        svc.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // StorageConfigurationEncryption
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void StorageConfigurationEncryption_CanBeConstructed()
    {
        var svc = new StorageConfigurationEncryption(
            Mock.Of<IDataProtectionProvider>());

        svc.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Options types
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DownloadWindowOptions_DefaultValues()
    {
        var opts = new DownloadWindowOptions();
        opts.Should().NotBeNull();
    }
}
