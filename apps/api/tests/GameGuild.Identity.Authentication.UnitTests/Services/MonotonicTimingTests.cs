using System.Text.Json;
using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class MonotonicTimingTests
{
    [Fact]
    public void ElapsedTimeUsesMonotonicTimestampEvenWhenUtcMovesBackward()
    {
        var clock = new MonotonicClock();
        var origin = AuthenticationTimingOrigin.Start(clock);
        clock.Advance(TimeSpan.FromMilliseconds(410));
        clock.Utc = clock.Utc.AddDays(-10);
        Assert.Equal(TimeSpan.FromMilliseconds(410), origin.Elapsed);
    }

    [Fact]
    public async Task CompletedWorkAlreadyAboveFloorDoesNotAddAnotherDelay()
    {
        var clock = new MonotonicClock();
        var origin = AuthenticationTimingOrigin.Start(clock);
        clock.Advance(TimeSpan.FromMilliseconds(410));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new UserEnumerationProtectionService(NullLogger<UserEnumerationProtectionService>.Instance, cache);
        await service.CompleteAuthenticationTimingAsync(origin, true);
        Assert.Equal(0, clock.TimersCreated);
    }

    [Fact]
    public async Task ExistingElapsedWorkIsSubtractedFromTheSameFloor()
    {
        var clock = new MonotonicClock();
        var origin = AuthenticationTimingOrigin.Start(clock);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new UserEnumerationProtectionService(NullLogger<UserEnumerationProtectionService>.Instance, cache);
        using var cancellation = new CancellationTokenSource();
        var compensation = service.CompleteAuthenticationTimingAsync(origin, true, cancellation.Token);
        Assert.Equal(TimeSpan.FromMilliseconds(150), clock.LastDueTime);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => compensation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlreadyCancelledAttemptDoesNotHashOrCreateTimers(bool workCompleted)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new MonotonicClock();
        var logger = new CostLogger();
        var service = new UserEnumerationProtectionService(logger, cache);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CompleteAuthenticationTimingAsync(
            AuthenticationTimingOrigin.Start(clock), workCompleted, new CancellationToken(true)));
        Assert.Empty(logger.Costs);
        Assert.Equal(0, clock.TimersCreated);
    }

    [Fact]
    public async Task DummyWorkUsesReloadedRealHashPolicy()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PasswordPolicy:BCryptWorkFactor"] = "10"
        }).Build();
        var logger = new CostLogger();
        var service = new UserEnumerationProtectionService(logger, cache, configuration);
        await service.PerformDummyPasswordHashAsync("synthetic", CancellationToken.None);
        configuration["PasswordPolicy:BCryptWorkFactor"] = "11";
        configuration.Reload();
        await service.PerformDummyPasswordHashAsync("synthetic", CancellationToken.None);
        Assert.Equal(new[] {10, 11}, logger.Costs);
    }

    [Fact]
    public async Task DummyConfigurationFailureIsPropagatedWithoutClaimingCompletedWork()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PasswordPolicy:BCryptWorkFactor"] = "17"
        }).Build();
        var logger = new CostLogger();
        var service = new UserEnumerationProtectionService(logger, cache, configuration);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PerformDummyPasswordHashAsync("synthetic", CancellationToken.None));
        Assert.Empty(logger.Costs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SimulationPerformsTheSameCredentialWorkForEitherAccountClass(bool accountExists)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PasswordPolicy:BCryptWorkFactor"] = "10"
        }).Build();
        var logger = new CostLogger();
        var service = new UserEnumerationProtectionService(logger, cache, configuration);

        await service.SimulateAuthenticationDelayAsync("synthetic@example.test", accountExists);

        Assert.Equal(new[] {10}, logger.Costs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledSimulationDoesNotClaimCompletedCredentialWork(bool accountExists)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = new CostLogger();
        var service = new UserEnumerationProtectionService(logger, cache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SimulateAuthenticationDelayAsync("synthetic@example.test", accountExists, new CancellationToken(true)));

        Assert.Empty(logger.Costs);
    }

    [Fact]
    public void AnonymousJsonCannotSupplyOrObserveTheTimingOrigin()
    {
        var request = JsonSerializer.Deserialize<LocalSignInRequest>("""{"Email":"synthetic@example.test","Password":"synthetic","TimingOrigin":{"StartedAtUtc":"2099-01-01"}}""")!;
        Assert.Null(request.TimingOrigin);
        request = new LocalSignInRequest { Email = "synthetic@example.test", Password = "synthetic", TimingOrigin = AuthenticationTimingOrigin.Start() };
        Assert.DoesNotContain("TimingOrigin", JsonSerializer.Serialize(request));
        Assert.DoesNotContain("StartedAtUtc", JsonSerializer.Serialize(request));
    }

    [Fact]
    public async Task FractionalRemainingBudgetCannotBypassTheMonotonicFloor()
    {
        var clock = new ScheduledClock();
        var origin = AuthenticationTimingOrigin.Start(clock);
        clock.Advance(TimeSpan.FromMilliseconds(399.5));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new UserEnumerationProtectionService(NullLogger<UserEnumerationProtectionService>.Instance, cache);
        await service.CompleteAuthenticationTimingAsync(origin, true);
        Assert.True(origin.Elapsed >= TimeSpan.FromMilliseconds(400));
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(1) }, clock.Delays);
    }

    [Fact]
    public async Task EarlyTimerCompletionRechecksTheRemainingMonotonicBudget()
    {
        var clock = new ScheduledClock { CompleteFirstTimerEarly = true };
        var origin = AuthenticationTimingOrigin.Start(clock);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new UserEnumerationProtectionService(NullLogger<UserEnumerationProtectionService>.Instance, cache);
        await service.CompleteAuthenticationTimingAsync(origin, true);
        Assert.Equal(TimeSpan.FromMilliseconds(400), origin.Elapsed);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(1) }, clock.Delays);
    }

    private sealed class ScheduledClock : TimeProvider
    {
        private long timestamp;
        public bool CompleteFirstTimerEarly { get; set; }
        public List<TimeSpan> Delays { get; } = [];
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref timestamp);
        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref timestamp, elapsed.Ticks);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            var elapsed = dueTime;
            if (CompleteFirstTimerEarly)
            {
                elapsed -= TimeSpan.FromMilliseconds(1);
                CompleteFirstTimerEarly = false;
            }
            Advance(elapsed);
            return TimeProvider.System.CreateTimer(callback, state, TimeSpan.Zero, period);
        }
    }

    private sealed class MonotonicClock : TimeProvider
    {
        private long timestamp;
        public DateTimeOffset Utc { get; set; } = new(2026,10,7,0,0,0,TimeSpan.Zero);
        public int TimersCreated { get; private set; }
        public TimeSpan LastDueTime { get; private set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => Utc;
        public void Advance(TimeSpan elapsed) => timestamp += elapsed.Ticks;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            TimersCreated++;
            LastDueTime = dueTime;
            return TimeProvider.System.CreateTimer(callback, state, Timeout.InfiniteTimeSpan, period);
        }
    }

    private sealed class CostLogger : ILogger<UserEnumerationProtectionService>
    {
        public List<int> Costs { get; } = [];
        IDisposable? ILogger.BeginScope<TState>(TState state)
        {
            _ = state;
            return null;
        }
        bool ILogger.IsEnabled(LogLevel logLevel)
        {
            _ = logLevel;
            return true;
        }
        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState,Exception?,string> formatter)
        {
            // The interface requires these arguments; this recorder observes only structured state.
            _ = logLevel;
            _ = eventId;
            _ = exception;
            _ = formatter;
            if (state is IEnumerable<KeyValuePair<string,object?>> values)
            {
                foreach (var pair in values)
                {
                    if (pair.Key == "WorkFactor" && pair.Value is int cost) { Costs.Add(cost); }
                }
            }
        }
    }
}
