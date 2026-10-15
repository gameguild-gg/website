using GameGuild.Assets.BackgroundServices;
using GameGuild.Assets.Extensions;
using GameGuild.Assets.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using BackgroundAssetGarbageCollectionService = GameGuild.Assets.BackgroundServices.AssetGarbageCollectionService;

namespace GameGuild.Assets.UnitTests;

/// <summary>
///     Hosted-loop tests for the GC and transformed-asset-cleanup background wrappers:
/// both resolve their scoped worker per iteration and honor their Enabled flag.
/// </summary>
public sealed class HostedGarbageCollectionTests
{
    private static GarbageCollectionResult CompletedResult() =>
        new(ItemsProcessed: 0, ItemsDeleted: 0, ItemsSkipped: 0, Errors: 0,
            Duration: TimeSpan.Zero, Messages: []);

    private static Mock<IAssetGarbageCollectionService> RegisterGc(
        ServiceCollection services, TaskCompletionSource invoked)
    {
        var gc = new Mock<IAssetGarbageCollectionService>();
        gc.Setup(service => service.RunGarbageCollectionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => invoked.TrySetResult())
            .ReturnsAsync(CompletedResult());
        services.AddScoped(_ => gc.Object);
        return gc;
    }

    [Fact]
    public async Task GarbageCollectionHostedLoop_DelegatesToScopedServiceEachCycle()
    {
        var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        var gc = RegisterGc(services, invoked);

        await using var provider = services.BuildServiceProvider();
        var worker = new BackgroundAssetGarbageCollectionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AssetGarbageCollectionOptions { MinIntervalHours = 1 }),
            NullLogger<BackgroundAssetGarbageCollectionService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(stop.Token);

        gc.Verify(service => service.RunGarbageCollectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GarbageCollectionHostedLoop_WhenDisabled_NeverResolvesScopedService()
    {
        var services = new ServiceCollection();
        var gc = new Mock<IAssetGarbageCollectionService>(MockBehavior.Strict);
        services.AddScoped(_ => gc.Object);

        await using var provider = services.BuildServiceProvider();
        var worker = new BackgroundAssetGarbageCollectionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AssetGarbageCollectionOptions { Enabled = false }),
            NullLogger<BackgroundAssetGarbageCollectionService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await worker.StopAsync(stop.Token);

        gc.Verify(service => service.RunGarbageCollectionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GarbageCollectionHostedLoop_SwallowsCycleErrorsWithoutCrashing()
    {
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gc = new Mock<IAssetGarbageCollectionService>();
        gc.Setup(service => service.RunGarbageCollectionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => failed.TrySetResult())
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));

        var services = new ServiceCollection();
        services.AddScoped(_ => gc.Object);

        await using var provider = services.BuildServiceProvider();
        var worker = new BackgroundAssetGarbageCollectionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new AssetGarbageCollectionOptions { MinIntervalHours = 1 }),
            NullLogger<BackgroundAssetGarbageCollectionService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200); // must not crash or re-run before the interval elapses
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stopTask = worker.StopAsync(stop.Token);
        stopTask.IsFaulted.Should().BeFalse();
        await stopTask;

        gc.Verify(service => service.RunGarbageCollectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransformedCleanupHostedLoop_DelegatesToScopedRepositoryEachCycle()
    {
        var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stale = new AssetContent("assets", "key", new string('a', 64), "image/png", 128, 16, 16)
        {
            Id = Guid.NewGuid()
        };
        var transformed = new Mock<ITransformedAssetRepository>();
        transformed
            .Setup(repo => repo.GetStaleAssetsAsync(It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => invoked.TrySetResult())
            .ReturnsAsync(new List<TransformedAsset>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SourceContentId = stale.Id,
                    BucketName = "assets",
                    ObjectKey = "obj-key"
                }
            });
        var storage = new Mock<IAssetStorageService>();

        var services = new ServiceCollection();
        services.AddScoped(_ => transformed.Object);
        services.AddScoped(_ => storage.Object);
        services.AddSingleton(Options.Create(new AssetStorageOptions()));

        await using var provider = services.BuildServiceProvider();
        var worker = new TransformedAssetCleanupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new TransformedAssetCleanupOptions()),
            NullLogger<TransformedAssetCleanupService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(stop.Token);

        transformed.Verify(
            repo => repo.GetStaleAssetsAsync(It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        storage.Verify(
            service => service.DeleteAsync("assets", "obj-key", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TransformedCleanupHostedLoop_WhenDisabled_NeverResolvesScopedServices()
    {
        var services = new ServiceCollection();
        var transformed = new Mock<ITransformedAssetRepository>(MockBehavior.Strict);
        services.AddScoped(_ => transformed.Object);

        await using var provider = services.BuildServiceProvider();
        var worker = new TransformedAssetCleanupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new TransformedAssetCleanupOptions { Enabled = false }),
            NullLogger<TransformedAssetCleanupService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await worker.StopAsync(stop.Token);

        transformed.Verify(
            repo => repo.GetStaleAssetsAsync(It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void AddAssetsModule_RegistersGcAndTransformedCleanupAsHostedServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAssetsModule(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>();

        hosted.OfType<BackgroundAssetGarbageCollectionService>().Should().ContainSingle();
        hosted.OfType<TransformedAssetCleanupService>().Should().ContainSingle();
        hosted.OfType<VirusScanBackgroundService>().Should().ContainSingle();
    }
}
