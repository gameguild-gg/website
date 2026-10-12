using System.Diagnostics.Metrics;
using GameGuild.Identity.Authentication;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

[CollectionDefinition("Refresh token lifecycle metrics", DisableParallelization = true)]
public sealed class RefreshTokenLifecycleMetricsCollection;

[Collection("Refresh token lifecycle metrics")]
public sealed class RefreshTokenLifecycleMetricsTests
{
    [Theory]
    [InlineData(RefreshTokenLifecycleOperation.Issued, "committed")]
    [InlineData(RefreshTokenLifecycleOperation.Rotated, "committed")]
    [InlineData(RefreshTokenLifecycleOperation.Rejected, "rejected")]
    [InlineData(RefreshTokenLifecycleOperation.ReplayContained, "contained")]
    [InlineData(RefreshTokenLifecycleOperation.Revoked, "committed")]
    [InlineData(RefreshTokenLifecycleOperation.AllRevoked, "committed")]
    public void PersistedMetricHasOnlyBoundedClassifications(RefreshTokenLifecycleOperation operation, string outcome)
    {
        var values = new List<Dictionary<string, object?>>();
        using var listener = Listen("authentication.refresh_token.operations", (_, tags) => values.Add(tags));
        var userId = Guid.NewGuid();
        RefreshTokenLifecycleMetrics.RecordPersisted(new RefreshTokenLifecycleEvent(operation, userId,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RefreshTokenLifecycleReason.ConcurrentRotation));
        var tags = Assert.Single(values);
        Assert.Equal(new[] { "operation", "outcome", "reason" }, tags.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(operation.ToString().ToLowerInvariant(), tags["operation"]);
        Assert.Equal(outcome, tags["outcome"]);
        Assert.Equal("concurrentrotation", tags["reason"]);
        Assert.DoesNotContain(userId.ToString(), tags.Values);
    }

    [Fact]
    public void AttemptMetricDoesNotPretendTheOperationCommitted()
    {
        var attempts = new List<Dictionary<string, object?>>();
        var persisted = 0;
        using var attemptListener = Listen("authentication.refresh_token.attempts", (_, tags) => attempts.Add(tags));
        using var operationListener = Listen("authentication.refresh_token.operations", (_, _) => persisted++);
        RefreshTokenLifecycleMetrics.RecordAttempt(RefreshTokenLifecycleOperation.Rotated);
        Assert.Equal("rotated", Assert.Single(attempts)["operation"]);
        Assert.Single(attempts[0]);
        Assert.Equal(0, persisted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ListenerFailureDoesNotEscapeIntoAuthentication(bool attempt)
    {
        var calls = 0;
        using var listener = Listen(attempt ? "authentication.refresh_token.attempts" : "authentication.refresh_token.operations", (_, _) =>
        {
            calls++;
            throw new InvalidOperationException("Synthetic telemetry listener failure");
        });
        if (attempt) { RefreshTokenLifecycleMetrics.RecordAttempt(RefreshTokenLifecycleOperation.Rotated); }
        else { RefreshTokenLifecycleMetrics.RecordPersisted(new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.Rotated)); }
        Assert.Equal(1, calls);
    }

    [Fact]
    public void UnknownOperationCannotBecomeAnUnboundedTag() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RefreshTokenLifecycleMetrics.RecordAttempt((RefreshTokenLifecycleOperation)int.MaxValue));

    [Fact]
    public void UnknownReasonCannotBecomeAnUnboundedTag() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RefreshTokenLifecycleMetrics.RecordPersisted(
            new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.Rejected, Reason: (RefreshTokenLifecycleReason)int.MaxValue)));

    private static MeterListener Listen(string name, Action<long, Dictionary<string, object?>> callback)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == RefreshTokenLifecycleMetrics.MeterName && instrument.Name == name)
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) => callback(value,
            tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
        listener.Start();
        return listener;
    }
}
