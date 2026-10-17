using System.Diagnostics.Metrics;
using GameGuild.Identity.Authentication;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

[CollectionDefinition("Session analytics metrics", DisableParallelization = true)]
public sealed class SessionAnalyticsMetricsCollection;

[Collection("Session analytics metrics")]
public sealed class SessionAnalyticsMetricsTests
{
    [Fact]
    public void CreatedCounter_EmitsOnCreation()
    {
        var values = new List<long>();
        using var listener = Listen("authentication.sessions.created", value => values.Add(value));

        SessionAnalyticsMetrics.RecordCreated();

        Assert.Equal([1L], values);
    }

    [Theory]
    [InlineData(SessionTerminationReason.UserLogout)]
    [InlineData(SessionTerminationReason.Expired)]
    [InlineData(SessionTerminationReason.MaxSessionsExceeded)]
    public void TerminatedCounter_EmitsWithBoundedReasonTag(SessionTerminationReason reason)
    {
        var measurements = new List<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == SessionAnalyticsMetrics.MeterName && instrument.Name == "authentication.sessions.terminated")
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            measurements.Add((value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value))));
        listener.Start();

        SessionAnalyticsMetrics.RecordTerminated(reason, 3);

        var measurement = Assert.Single(measurements);
        Assert.Equal(3, measurement.Value);
        Assert.Equal(reason.ToString().ToLowerInvariant(), measurement.Tags["reason"]);
        Assert.Equal(["reason"], measurement.Tags.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void TerminatedCounter_IgnoresNonPositiveAndUnknownReasons()
    {
        var terminated = 0;
        using var listener = Listen("authentication.sessions.terminated", _ => terminated++);

        SessionAnalyticsMetrics.RecordTerminated(SessionTerminationReason.UserLogout, 0);
        SessionAnalyticsMetrics.RecordTerminated((SessionTerminationReason)int.MaxValue, 2);

        Assert.Equal(0, terminated);
    }

    [Fact]
    public void ListenerFailureDoesNotEscapeIntoSessionLifecycle()
    {
        var calls = 0;
        using var listener = Listen("authentication.sessions.created", _ =>
        {
            calls++;
            throw new InvalidOperationException("Synthetic telemetry listener failure");
        });

        SessionAnalyticsMetrics.RecordCreated();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void ActiveGauge_ReflectsCreatedMinusTerminated()
    {
        var observed = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == SessionAnalyticsMetrics.MeterName && instrument.Name == "authentication.sessions.active")
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => observed.Add(value));
        listener.Start();
        listener.RecordObservableInstruments();

        SessionAnalyticsMetrics.RecordCreated();
        SessionAnalyticsMetrics.RecordCreated();
        SessionAnalyticsMetrics.RecordTerminated(SessionTerminationReason.UserLogout);
        listener.RecordObservableInstruments();

        // Static counters are shared across tests in this collection; assert the observable
        // tracked a delta of +1 across the three operations rather than an absolute value.
        Assert.NotEmpty(observed);
        var delta = observed[^1] - observed[0];
        Assert.Equal(1, delta);
    }

    private static MeterListener Listen(string name, Action<long> callback)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == SessionAnalyticsMetrics.MeterName && instrument.Name == name)
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => callback(value));
        listener.Start();
        return listener;
    }
}
