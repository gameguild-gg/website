using GameGuild.Compliance.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class ScheduledAuditExportBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsyncProcessesDueExportsAndExpiresOldFiles()
    {
        var dueRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expirationRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduledExportService = new Mock<IScheduledAuditExportService>();
        scheduledExportService
            .Setup(service => service.ProcessDueAsync(It.IsAny<CancellationToken>()))
            .Callback(() => dueRun.TrySetResult())
            .ReturnsAsync(0);
        scheduledExportService
            .Setup(service => service.ExpireExpiredFilesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => expirationRun.TrySetResult())
            .ReturnsAsync(0);
        await using var provider = new ServiceCollection()
            .AddSingleton(scheduledExportService.Object)
            .BuildServiceProvider();
        var worker = new ScheduledAuditExportBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ScheduledAuditExportBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.WhenAll(dueRun.Task, expirationRun.Task).WaitAsync(TimeSpan.FromSeconds(5));
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(stopTimeout.Token);

        scheduledExportService.Verify(service => service.ProcessDueAsync(It.IsAny<CancellationToken>()), Times.Once);
        scheduledExportService.Verify(service => service.ExpireExpiredFilesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
