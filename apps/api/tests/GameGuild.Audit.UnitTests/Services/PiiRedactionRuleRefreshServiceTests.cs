using FluentAssertions;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

// =====================================================================================
// Background refresh loop for the PII redaction rule provider (issue #192): the global
// snapshot is warmed immediately on start, and refresh failures are swallowed so the
// host keeps retrying while the provider serves stale-if-error snapshots.
// =====================================================================================

public class PiiRedactionRuleRefreshServiceTests
{
    [Fact]
    public async Task StartAsync_WarmsTheGlobalSnapshotImmediately()
    {
        var provider = new SequencedProvider();
        using var service = new PiiRedactionRuleRefreshService(provider, TimeSpan.FromSeconds(30));

        await service.StartAsync(CancellationToken.None);
        await provider.GlobalLoadSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        provider.GlobalLoadCount.Should().BeGreaterThanOrEqualTo(1);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_RefreshFailureIsSwallowedAndRetriedNextCycle()
    {
        var provider = new SequencedProvider { FailFirstTwoLoads = true };
        using var service = new PiiRedactionRuleRefreshService(provider, TimeSpan.FromMilliseconds(20));

        await service.StartAsync(CancellationToken.None);
        await provider.SuccessSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        provider.GlobalLoadCount.Should().BeGreaterThanOrEqualTo(3);
        await service.StopAsync(CancellationToken.None);
    }

    private sealed class SequencedProvider : IPiiRedactionRuleProvider
    {
        private readonly TaskCompletionSource _globalLoadSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _successSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _globalLoadCount;

        public bool FailFirstTwoLoads { get; set; }

        public TaskCompletionSource GlobalLoadSignal => _globalLoadSignal;

        public TaskCompletionSource SuccessSignal => _successSignal;

        public int GlobalLoadCount => Volatile.Read(ref _globalLoadCount);

        public Task<PiiRuleSet> GetGlobalRuleSetAsync(CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _globalLoadCount);
            _globalLoadSignal.TrySetResult();
            if (FailFirstTwoLoads && count <= 2)
            {
                return Task.FromException<PiiRuleSet>(
                    new PiiRedactionUnavailableException(new InvalidOperationException("rule store down")));
            }

            _successSignal.TrySetResult();
            return Task.FromResult(PiiRedactionRuleProvider.Compile([]));
        }

        public Task<PiiRuleSet> GetTenantRuleSetAsync(Guid? tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(PiiRedactionRuleProvider.Compile([]));

        public PiiRuleSet? TryGetGlobalSnapshot() => null;

        public PiiRedactionCacheStatus GetCacheStatus() => new(false, null, 0, 0, null, null);

        public void Invalidate(Guid? tenantId) { }
    }
}
