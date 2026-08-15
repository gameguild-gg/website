using System.Net;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GameGuild.API.Setup;
using Moq;
using Xunit;

namespace GameGuild.API.UnitTests.Core;

public sealed class PipelineExtensionsTests
{
    [Fact]
    public void ConfigurePipeline_WhenApplicationIsNull_Throws()
    {
        WebApplication app = null!;

        var action = () => app.ConfigurePipeline();

        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Development", true, true)]
    [InlineData("Staging", true, false)]
    [InlineData("Production", false, true)]
    public async Task ConfigurePipeline_CoversEnvironmentAndOptionalMiddleware(
        string environmentName,
        bool enableOptionalMiddleware,
        bool registerVersionProvider)
    {
        var builder = CreateBuilder(environmentName, enableOptionalMiddleware, registerVersionProvider);
        await using var app = builder.Build();

        var result = app.ConfigurePipeline();

        result.Should().BeSameAs(app);
        ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .Should().Contain(endpoint =>
                endpoint.DisplayName != null && endpoint.DisplayName.Contains("/openapi/{documentName}.json"));
    }

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

    private static WebApplicationBuilder CreateBuilder(
        string environmentName,
        bool enableOptionalMiddleware,
        bool registerVersionProvider)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PresentationLayer:EnableHttpLogging"] = enableOptionalMiddleware.ToString(),
            ["PresentationLayer:EnableRateLimiting"] = enableOptionalMiddleware.ToString()
        });
        builder.Services.AddControllers();
        builder.Services.AddRouting();
        builder.Services.AddLocalization();
        builder.Services.AddCors();
        builder.Services.AddResponseCaching();
        builder.Services.AddResponseCompression();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(_ => { });
        builder.Services.AddHttpLogging(_ => { });
        builder.Services.AddSwaggerGen();

        if (registerVersionProvider)
        {
            var provider = new Mock<IApiVersionDescriptionProvider>();
            provider.SetupGet(value => value.ApiVersionDescriptions).Returns(
            [
                new ApiVersionDescription(new ApiVersion(1, 0), "v1", false)
            ]);
            builder.Services.AddSingleton(provider.Object);
        }

        return builder;
    }
}
