using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.GatewayRouting;

/// <summary>
///     Circuit-breaker transition tests (issue #413): Closed → Open at the consecutive-failure
///     threshold, Open rejecting attempts until the open duration elapses, the single
///     half-open probe, recovery to Closed and reopening on a failed probe.
/// </summary>
/// <remarks>
///     The clock provider is <see cref="AsyncLocal{T}" />-backed, so each test sets it inside
///     the test method (a write made in a constructor never flows into the test invocation).
/// </remarks>
public class GatewayCircuitBreakerTests : IDisposable
{
    private readonly MutableTimeProvider _timeProvider = new();

    private readonly InMemoryGatewayCircuitBreaker _breaker = new(new GatewayFailoverOptions
    {
        CircuitFailureThreshold = 3,
        CircuitOpenSeconds = 60
    });

    public void Dispose() => SystemClock.Reset();

    [Fact]
    public void Circuit_StaysClosed_BelowTheFailureThreshold()
    {
        SystemClock.SetProvider(_timeProvider);
        _breaker.RecordFailure("stripe");
        _breaker.RecordFailure("stripe");

        var snapshot = _breaker.GetSnapshot("stripe");
        snapshot.State.Should().Be(GatewayCircuitState.Closed);
        snapshot.ConsecutiveFailures.Should().Be(2);
        _breaker.TryAcquireAttempt("stripe").Should().BeTrue();
    }

    [Fact]
    public void Circuit_Opens_AtTheConsecutiveFailureThreshold()
    {
        SystemClock.SetProvider(_timeProvider);
        for (var i = 0; i < 3; i++)
        {
            _breaker.RecordFailure("stripe");
        }

        var snapshot = _breaker.GetSnapshot("stripe");
        snapshot.State.Should().Be(GatewayCircuitState.Open);
        snapshot.OpenedAtUtc.Should().NotBeNull();
        _breaker.TryAcquireAttempt("stripe").Should().BeFalse("an open circuit rejects attempts");
    }

    [Fact]
    public void Success_ResetsTheCircuitToClosed()
    {
        SystemClock.SetProvider(_timeProvider);
        _breaker.RecordFailure("stripe");
        _breaker.RecordFailure("stripe");

        _breaker.RecordSuccess("stripe");

        var snapshot = _breaker.GetSnapshot("stripe");
        snapshot.State.Should().Be(GatewayCircuitState.Closed);
        snapshot.ConsecutiveFailures.Should().Be(0);
        snapshot.OpenedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Circuit_GrantsASingleHalfOpenProbe_AfterTheOpenDurationElapses()
    {
        SystemClock.SetProvider(_timeProvider);
        Open();

        _timeProvider.Advance(TimeSpan.FromSeconds(59));
        _breaker.TryAcquireAttempt("stripe").Should().BeFalse("still inside the open duration");

        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        _breaker.GetSnapshot("stripe").State.Should().Be(GatewayCircuitState.Open);

        _breaker.TryAcquireAttempt("stripe").Should().BeTrue("the elapsed circuit grants the probe");
        _breaker.GetSnapshot("stripe").State.Should().Be(GatewayCircuitState.HalfOpen);
        _breaker.TryAcquireAttempt("stripe").Should().BeFalse("only one half-open probe may run");
    }

    [Fact]
    public void HalfOpenProbe_Success_ClosesTheCircuit()
    {
        SystemClock.SetProvider(_timeProvider);
        Open();
        _timeProvider.Advance(TimeSpan.FromSeconds(61));
        _breaker.TryAcquireAttempt("stripe").Should().BeTrue();

        _breaker.RecordSuccess("stripe");

        var snapshot = _breaker.GetSnapshot("stripe");
        snapshot.State.Should().Be(GatewayCircuitState.Closed);
        snapshot.ConsecutiveFailures.Should().Be(0);
        _breaker.TryAcquireAttempt("stripe").Should().BeTrue();
    }

    [Fact]
    public void HalfOpenProbe_Failure_ReopensTheCircuitImmediately()
    {
        SystemClock.SetProvider(_timeProvider);
        Open();
        _timeProvider.Advance(TimeSpan.FromSeconds(61));
        _breaker.TryAcquireAttempt("stripe").Should().BeTrue();

        _breaker.RecordFailure("stripe");

        var snapshot = _breaker.GetSnapshot("stripe");
        snapshot.State.Should().Be(GatewayCircuitState.Open);
        _breaker.TryAcquireAttempt("stripe").Should().BeFalse();
    }

    [Fact]
    public void Circuits_AreTrackedPerGateway()
    {
        SystemClock.SetProvider(_timeProvider);
        for (var i = 0; i < 3; i++)
        {
            _breaker.RecordFailure("stripe");
        }

        _breaker.GetSnapshot("paypal").State.Should().Be(GatewayCircuitState.Closed);
        _breaker.TryAcquireAttempt("paypal").Should().BeTrue();

        _breaker.GetAllSnapshots().Select(snapshot => snapshot.ProviderId)
            .Should().Equal("paypal", "stripe");
    }

    [Fact]
    public void Snapshots_CarryTheConfiguredThreshold()
    {
        _breaker.GetSnapshot("stripe").FailureThreshold.Should().Be(3);
    }

    private void Open()
    {
        for (var i = 0; i < 3; i++)
        {
            _breaker.RecordFailure("stripe");
        }

        _breaker.GetSnapshot("stripe").State.Should().Be(GatewayCircuitState.Open);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTime _utcNow = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        public override DateTimeOffset GetUtcNow() => new(_utcNow);

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
