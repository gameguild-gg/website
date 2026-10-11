using GameGuild.Identity.Authentication;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class RequestTimingOriginTests
{
    [Fact]
    public void AdmissionAndCommandDispatchReuseOneMonotonicRequestOrigin()
    {
        var clock = new RequestClock();
        var context = new DefaultHttpContext();
        var admissionOrigin = AuthenticationTimingOrigin.GetOrStartForRequest(context, clock);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        clock.Utc = clock.Utc.AddDays(-10);
        var commandOrigin = AuthenticationTimingOrigin.GetOrStartForRequest(context, TimeProvider.System);
        Assert.Same(admissionOrigin, commandOrigin);
        Assert.Equal(TimeSpan.FromMilliseconds(250), commandOrigin.Elapsed);
    }

    [Fact]
    public void DifferentRequestsCannotShareAnOrigin()
    {
        Assert.NotSame(
            AuthenticationTimingOrigin.GetOrStartForRequest(new DefaultHttpContext()),
            AuthenticationTimingOrigin.GetOrStartForRequest(new DefaultHttpContext()));
    }

    [Fact]
    public void StringKeysCannotSupplyThePrivateServerOrigin()
    {
        var context = new DefaultHttpContext();
        var supplied = AuthenticationTimingOrigin.Start(new RequestClock());
        context.Items["TimingOrigin"] = supplied;
        context.Items[nameof(AuthenticationTimingOrigin)] = supplied;
        Assert.NotSame(supplied, AuthenticationTimingOrigin.GetOrStartForRequest(context));
    }

    [Fact]
    public void NonHttpCallsReceiveAnIndependentOrigin()
    {
        Assert.NotSame(AuthenticationTimingOrigin.GetOrStartForRequest(null), AuthenticationTimingOrigin.GetOrStartForRequest(null));
    }

    private sealed class RequestClock : TimeProvider
    {
        private long timestamp;
        public DateTimeOffset Utc { get; set; } = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => Utc;
        public void Advance(TimeSpan elapsed) => timestamp += elapsed.Ticks;
    }
}
