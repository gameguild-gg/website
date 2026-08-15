using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using GameGuild.API.Setup;
using Xunit;

namespace GameGuild.API.UnitTests.Core;

public sealed class PipelineExtensionsTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/ready")]
    [InlineData("/live")]
    public void ShouldRedirectToHttps_DoesNotRedirectHealthProbes(string path)
    {
        var context = CreateContext(path, IPAddress.Parse("203.0.113.10"));

        PipelineExtensions.ShouldRedirectToHttps(context).Should().BeFalse();
    }

    [Fact]
    public void ShouldRedirectToHttps_DoesNotRedirectLoopbackTraffic()
    {
        var context = CreateContext("/api/users", IPAddress.Loopback);

        PipelineExtensions.ShouldRedirectToHttps(context).Should().BeFalse();
    }

    [Fact]
    public void ShouldRedirectToHttps_RedirectsExternalApplicationTraffic()
    {
        var context = CreateContext("/api/users", IPAddress.Parse("203.0.113.10"));

        PipelineExtensions.ShouldRedirectToHttps(context).Should().BeTrue();
    }

    [Fact]
    public void ShouldRedirectToHttps_RedirectsTrafficWithoutRemoteAddress()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/users";

        PipelineExtensions.ShouldRedirectToHttps(context).Should().BeTrue();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task ConfigureHsts_HandlesProductionAndNonProductionEnvironments(string environmentName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });
        await using var app = builder.Build();

        var action = () => PipelineExtensions.ConfigureHsts(app);

        action.Should().NotThrow();
    }

    private static DefaultHttpContext CreateContext(string path, IPAddress remoteAddress)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = remoteAddress;
        return context;
    }
}
