using GameGuild.API.Eventing;
using System.Diagnostics.Metrics;
using System.Reflection;
using GameGuild.API.Core.Security;
using GameGuild.API.Database;
using GameGuild.API.Setup;
using GameGuild.Configuration.ConfigurationFromAPI.InfrastructureLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace GameGuild.API.UnitTests.Core;

[CollectionDefinition("Refresh token lifecycle infrastructure", DisableParallelization = true)]
public sealed class RefreshTokenLifecycleInfrastructureCollection;

[Collection("Refresh token lifecycle infrastructure")]
public sealed class RefreshTokenLifecycleInfrastructureTests
{
    [Fact]
    public void ProductionDatabaseOptionsResolveTheSameScopedMetricBufferAsTheRecorder()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=unused;Username=synthetic;Password=synthetic"
        }).Build();
        services.AddLogging();
        services.AddDurableEventTransport();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(Mock.Of<IActorContextAccessor>());
        typeof(InfrastructureLayerExtensions).GetMethod("AddDatabase", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [services, configuration, DatabaseOptions.CreateDefault()]);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var buffer = scope.ServiceProvider.GetRequiredService<RefreshTokenLifecycleMetricBuffer>();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        Assert.Contains(buffer, options.Extensions.OfType<CoreOptionsExtension>().Single().Interceptors!);
        Assert.IsType<RefreshTokenLifecycleRecorder>(scope.ServiceProvider.GetRequiredService<IRefreshTokenLifecycleRecorder>());
        using var other = provider.CreateScope();
        Assert.NotSame(buffer, other.ServiceProvider.GetRequiredService<RefreshTokenLifecycleMetricBuffer>());
    }

    [Fact]
    public void CommitCountsOnlyItsOwnStagedEventsAndRollbackRemovesThem()
    {
        var measurements = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == RefreshTokenLifecycleMetrics.MeterName && instrument.Name == "authentication.refresh_token.operations")
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements += value);
        listener.Start();
        var buffer = new RefreshTokenLifecycleMetricBuffer();
        var committed = Guid.NewGuid();
        var failed = Guid.NewGuid();
        buffer.Enlist(committed, new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.Rotated));
        buffer.Enlist(failed, new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.ReplayContained));
        Assert.Equal(0, measurements);
        buffer.Commit(committed);
        Assert.Equal(1, measurements);
        buffer.Discard(failed);
        buffer.Commit(failed);
        buffer.Commit(committed);
        Assert.Equal(1, measurements);
    }

    [Fact]
    public void EnabledHostOpenTelemetryActuallyCollectsTheRefreshTokenMeter()
    {
        var exporter = new CapturingExporter();
        using var reader = new PeriodicExportingMetricReader(exporter, exportIntervalMilliseconds: int.MaxValue);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Enabled"] = "true", ["OpenTelemetry:ConsoleExporterEnabled"] = "true"
        });
        builder.AddOpenTelemetryObservability();
        builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddReader(reader));
        using var provider = builder.Services.BuildServiceProvider();
        var meterProvider = provider.GetRequiredService<MeterProvider>();
        RefreshTokenLifecycleMetrics.RecordPersisted(new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.Rotated));
        Assert.True(meterProvider.ForceFlush());
        Assert.Contains("authentication.refresh_token.operations", exporter.Names);
    }

    private sealed class CapturingExporter : BaseExporter<Metric>
    {
        public List<string> Names { get; } = [];
        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch) { Names.Add(metric.Name); }
            return ExportResult.Success;
        }
    }
}
