using FluentAssertions;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
///     Unit tests for the background loop that replays spooled security events:
/// it drains through the scoped logger and records drain status for the delivery endpoint.
/// </summary>
public sealed class SecurityEventSpoolDrainerBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsyncDrainsSpooledEventsAndRecordsSuccess()
    {
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var securityEvents = new Mock<ISecurityEventLogger>();
        securityEvents
            .Setup(logger => logger.ReplaySpooledEventsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => drained.TrySetResult())
            .ReturnsAsync(2);
        await using var provider = new ServiceCollection()
            .AddSingleton(securityEvents.Object)
            .BuildServiceProvider();
        var tracker = new SecurityEventPipelineStatusTracker();
        var worker = new SecurityEventSpoolDrainerBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            tracker,
            Options.Create(new SecurityEventPipelineOptions { DrainInterval = TimeSpan.FromMinutes(5) }),
            NullLogger<SecurityEventSpoolDrainerBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await drained.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(stopTimeout.Token);

        securityEvents.Verify(logger => logger.ReplaySpooledEventsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        tracker.LastDrainAttemptedAtUtc.Should().NotBeNull();
        tracker.LastDrainSucceededAtUtc.Should().NotBeNull();
        tracker.LastDrainError.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsyncRecordsFailureWhenDrainThrows()
    {
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var securityEvents = new Mock<ISecurityEventLogger>();
        securityEvents
            .Setup(logger => logger.ReplaySpooledEventsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => failed.TrySetResult())
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        await using var provider = new ServiceCollection()
            .AddSingleton(securityEvents.Object)
            .BuildServiceProvider();
        var tracker = new SecurityEventPipelineStatusTracker();
        var worker = new SecurityEventSpoolDrainerBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            tracker,
            Options.Create(new SecurityEventPipelineOptions { DrainInterval = TimeSpan.FromMinutes(5) }),
            NullLogger<SecurityEventSpoolDrainerBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(stopTimeout.Token);

        tracker.LastDrainError.Should().Be("database unavailable");
        tracker.LastDrainSucceededAtUtc.Should().BeNull();
    }
}

/// <summary>
///     Unit tests for the scheduled security log retention enforcement loop:
/// every discovered tenant policy is enforced once per pass, system-triggered.
/// </summary>
public sealed class SecurityLogRetentionBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsyncEnforcesEveryTenantPolicyAfterTheInitialDelay()
    {
        var enforced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tenantId = Guid.NewGuid();
        var repository = new Mock<ISecurityLogRetentionRepository>();
        repository
            .Setup(repo => repo.GetAllPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([SecurityLogRetentionPolicy.Create(tenantId, 400, null, null, Guid.NewGuid(), SystemClock.UtcNow)]);
        var retention = new Mock<ISecurityLogRetentionService>();
        retention
            .Setup(service => service.EnforceForTenantAsync(tenantId, null, false, It.IsAny<CancellationToken>()))
            .Callback(() => enforced.TrySetResult())
            .ReturnsAsync((SecurityLogRetentionExecutionResponse?)null);
        await using var provider = new ServiceCollection()
            .AddSingleton(repository.Object)
            .AddSingleton(retention.Object)
            .BuildServiceProvider();
        var worker = new SecurityLogRetentionBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SecurityEventPipelineOptions { RetentionEnforcementInterval = TimeSpan.FromHours(24) }),
            NullLogger<SecurityLogRetentionBackgroundService>.Instance,
            initialDelay: TimeSpan.FromMilliseconds(50));

        await worker.StartAsync(CancellationToken.None);
        await enforced.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(stopTimeout.Token);

        retention.Verify(
            service => service.EnforceForTenantAsync(tenantId, null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsyncSurvivesEnforcementFailuresAndKeepsLooping()
    {
        var calls = 0;
        var firstFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPass = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Mock<ISecurityLogRetentionRepository>();
        repository
            .Setup(repo => repo.GetAllPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([SecurityLogRetentionPolicy.Create(Guid.NewGuid(), 400, null, null, Guid.NewGuid(), SystemClock.UtcNow)]);
        var retention = new Mock<ISecurityLogRetentionService>();
        retention
            .Setup(service => service.EnforceForTenantAsync(It.IsAny<Guid>(), null, false, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                var count = Interlocked.Increment(ref calls);
                if (count == 1)
                {
                    firstFailure.TrySetResult();
                    throw new InvalidOperationException("retention store unavailable");
                }

                secondPass.TrySetResult();
            })
            .ReturnsAsync((SecurityLogRetentionExecutionResponse?)null);
        await using var provider = new ServiceCollection()
            .AddSingleton(repository.Object)
            .AddSingleton(retention.Object)
            .BuildServiceProvider();
        var worker = new SecurityLogRetentionBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SecurityEventPipelineOptions { RetentionEnforcementInterval = TimeSpan.FromMilliseconds(100) }),
            NullLogger<SecurityLogRetentionBackgroundService>.Instance,
            initialDelay: TimeSpan.FromMilliseconds(50));

        await worker.StartAsync(CancellationToken.None);
        await Task.WhenAll(firstFailure.Task, secondPass.Task).WaitAsync(TimeSpan.FromSeconds(10));
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(stopTimeout.Token);

        calls.Should().BeGreaterThanOrEqualTo(2);
    }
}
