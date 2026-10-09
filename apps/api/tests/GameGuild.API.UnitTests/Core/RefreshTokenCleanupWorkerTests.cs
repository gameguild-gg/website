using System.Collections.Concurrent;
using GameGuild.API.Core.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameGuild.API.UnitTests.Core;

// BackgroundService schedules initialization on the shared thread pool. Keep
// lifecycle deadline assertions independent of parallel host/model setup.
[Collection("Refresh token lifecycle infrastructure")]
public sealed class RefreshTokenCleanupWorkerTests
{
    [Fact]
    public void DefaultPolicyEnablesBoundedRetainedCleanup()
    {
        var policy = new RefreshTokenCleanupOptions();
        Assert.True(policy.Enabled);
        Assert.Equal(30, policy.RetentionDays);
        Assert.Equal(500, policy.BatchSize);
        Assert.Equal(10, policy.MaxBatchesPerCycle);
        Assert.Empty(policy.Validate());
    }

    [Theory]
    [InlineData("RetentionDays", 0)]
    [InlineData("RetentionDays", 3651)]
    [InlineData("BatchSize", 0)]
    [InlineData("BatchSize", 1001)]
    [InlineData("MaxBatches", 0)]
    [InlineData("MaxBatches", 101)]
    [InlineData("InitialDelay", -1)]
    [InlineData("InitialDelay", 86401)]
    [InlineData("Interval", 0)]
    [InlineData("Interval", 86401)]
    [InlineData("ExecutionTimeout", 0)]
    [InlineData("ExecutionTimeout", 301)]
    public void InvalidCleanupBoundsAreRejected(string setting, int value)
    {
        var policy = new RefreshTokenCleanupOptions();
        switch (setting)
        {
            case "RetentionDays": policy.RetentionDays = value; break;
            case "BatchSize": policy.BatchSize = value; break;
            case "MaxBatches": policy.MaxBatchesPerCycle = value; break;
            case "InitialDelay": policy.InitialDelay = TimeSpan.FromSeconds(value); break;
            case "Interval": policy.Interval = TimeSpan.FromSeconds(value); break;
            case "ExecutionTimeout": policy.ExecutionTimeout = TimeSpan.FromSeconds(value); break;
            default: throw new ArgumentException("Unknown fixture setting", nameof(setting));
        }
        Assert.NotEmpty(policy.Validate());
    }

    [Fact]
    public async Task DisabledWorkerDoesNotCreateAStoreScope()
    {
        var probe = new WorkerProbe();
        using var provider = CreateProvider(probe);
        using var worker = CreateWorker(provider, new RefreshTokenCleanupOptions { Enabled = false });
        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, probe.CreatedScopes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EachCycleGetsAFreshScopeAndFailuresRetry(bool failFirst)
    {
        var probe = new WorkerProbe { FailFirst = failFirst };
        using var provider = CreateProvider(probe);
        using var worker = CreateWorker(provider, FastPolicy());
        await worker.StartAsync(CancellationToken.None);
        await probe.SecondCall.Task.WaitAsync(TimeSpan.FromSeconds(8));
        await worker.StopAsync(CancellationToken.None);
        Assert.Equal(2, probe.CreatedScopes);
        Assert.Equal(2, probe.ScopeIds.Distinct().Count());
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ShutdownCancelsTheInFlightStoreOperation()
    {
        var probe = new WorkerProbe { BlockFirst = true };
        using var provider = CreateProvider(probe);
        using var worker = CreateWorker(provider, FastPolicy());
        await worker.StartAsync(CancellationToken.None);
        await probe.FirstCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);
        Assert.True(probe.CancellationObserved);
        Assert.Equal(1, probe.CreatedScopes);
    }

    [Fact]
    public async Task CycleDeadlineCancelsWorkAndTheNextIntervalRetries()
    {
        var probe = new WorkerProbe { BlockFirst = true };
        using var provider = CreateProvider(probe);
        var policy = FastPolicy();
        policy.ExecutionTimeout = TimeSpan.FromSeconds(1);
        using var worker = CreateWorker(provider, policy);
        await worker.StartAsync(CancellationToken.None);
        await probe.SecondCall.Task.WaitAsync(TimeSpan.FromSeconds(8));
        await worker.StopAsync(CancellationToken.None);
        Assert.True(probe.CancellationObserved);
        Assert.Equal(2, probe.CreatedScopes);
    }

    [Fact]
    public async Task ShutdownInterruptsTheInitialDelay()
    {
        var probe = new WorkerProbe();
        using var provider = CreateProvider(probe);
        var timeProvider = new InitialDelayObservingTimeProvider();
        using var worker = CreateWorker(provider, new RefreshTokenCleanupOptions(), timeProvider);
        await worker.StartAsync(CancellationToken.None);
        await timeProvider.InitialDelayCreated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, probe.CreatedScopes);
        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ImmediateShutdownCompletesWithoutOpeningAStoreScope()
    {
        var probe = new WorkerProbe();
        using var provider = CreateProvider(probe);
        using var worker = CreateWorker(provider, new RefreshTokenCleanupOptions());
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);
        var execution = worker.ExecuteTask!;
        try { await execution.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) when (execution.IsCanceled)
        {
            Assert.True(execution.IsCanceled);
        }
        Assert.True(execution.IsCompleted);
        Assert.False(execution.IsFaulted);
        Assert.True(execution.IsCompletedSuccessfully || execution.IsCanceled);
        Assert.Equal(0, probe.CreatedScopes);
    }

    private static RefreshTokenCleanupOptions FastPolicy() => new()
    {
        InitialDelay = TimeSpan.Zero, Interval = TimeSpan.FromSeconds(1), ExecutionTimeout = TimeSpan.FromSeconds(5)
    };

    private static ServiceProvider CreateProvider(WorkerProbe probe) => new ServiceCollection()
        .AddScoped<IRefreshTokenCleanupOperation>(_ => new RecordingOperation(probe, Interlocked.Increment(ref probe.CreatedScopes)))
        .BuildServiceProvider();

    private static RefreshTokenCleanupWorker CreateWorker(ServiceProvider provider, RefreshTokenCleanupOptions policy,
        TimeProvider? timeProvider = null) => new(
        provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(policy), timeProvider ?? TimeProvider.System,
        NullLogger<RefreshTokenCleanupWorker>.Instance);

    private sealed class InitialDelayObservingTimeProvider : TimeProvider
    {
        public TaskCompletionSource InitialDelayCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = TimeProvider.System.CreateTimer(callback, state, dueTime, period);
            InitialDelayCreated.TrySetResult();
            return timer;
        }
    }

    private sealed class WorkerProbe
    {
        public int CreatedScopes;
        public bool FailFirst;
        public bool BlockFirst;
        public bool CancellationObserved;
        public ConcurrentQueue<int> ScopeIds { get; } = new();
        public TaskCompletionSource FirstCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class RecordingOperation(WorkerProbe probe, int scopeId) : IRefreshTokenCleanupOperation
    {
        public async Task<RefreshTokenCleanupResult> RunAsync(CancellationToken cancellationToken)
        {
            probe.ScopeIds.Enqueue(scopeId);
            if (scopeId == 1)
            {
                probe.FirstCall.TrySetResult();
                if (probe.BlockFirst)
                {
                    try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                    catch (OperationCanceledException) { probe.CancellationObserved = true; throw; }
                }
                if (probe.FailFirst) { throw new IOException("Synthetic cleanup storage failure"); }
            }
            if (scopeId == 2) { probe.SecondCall.TrySetResult(); }
            return new RefreshTokenCleanupResult(0, 0);
        }
    }
}
