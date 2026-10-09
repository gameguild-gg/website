using System.Net;
using System.Security.Claims;
using FluentAssertions;
using GameGuild.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class AdRewardRequestRiskContextResolverTests
{
    private const string Key = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    [Fact]
    public async Task ResolvesStablePseudonymousHashesFromTrustedServerContext()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var context = Context("session-1", IPAddress.Parse("203.0.113.8"), "AS64512");
        context.Request.Headers["X-Verified-ASN"] = "attacker-controlled";
        var resolver = CreateResolver(context, Key);

        var first = await resolver.ResolveAsync(tenantId, actorId);
        var second = await resolver.ResolveAsync(tenantId, actorId);

        first.Should().Be(second);
        new[] { first.DeviceRiskHash, first.IpRiskHash, first.AsnRiskHash }
            .Should().OnlyContain(hash => hash.Length == 64);
        first.ToString().Should().NotContain("session-1").And.NotContain("203.0.113.8")
            .And.NotContain("AS64512").And.NotContain("attacker-controlled");
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public async Task FailsClosedWhenAnyTrustedInputIsUnavailable(
        bool hasKey,
        bool hasSession,
        bool hasIp,
        bool hasAsn)
    {
        var context = Context(
            hasSession ? "session-1" : null,
            hasIp ? IPAddress.Loopback : null,
            hasAsn ? "AS64512" : null);
        var resolver = CreateResolver(context, hasKey ? Key : null);

        var act = () => resolver.ResolveAsync(Guid.NewGuid(), Guid.NewGuid()).AsTask();

        await act.Should().ThrowAsync<AdRewardRiskContextUnavailableException>();
    }

    private static DefaultHttpContext Context(string? sessionId, IPAddress? address, string? asn)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = address;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            sessionId is null ? [] : [new Claim("sid", sessionId)], "test"));
        if (asn is not null)
        {
            context.Items[AdRewardRequestRiskContextResolver.VerifiedAsnItemKey] = asn;
        }

        return context;
    }

    private static AdRewardRequestRiskContextResolver CreateResolver(HttpContext context, string? key)
    {
        var values = new Dictionary<string, string?>();
        if (key is not null)
        {
            values[AdRewardRequestRiskContextResolver.HmacKeyConfiguration] = key;
        }

        return new AdRewardRequestRiskContextResolver(
            new HttpContextAccessor { HttpContext = context },
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }
}
