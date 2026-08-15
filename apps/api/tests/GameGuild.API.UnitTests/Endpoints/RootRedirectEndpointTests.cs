using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using GameGuild.API.Endpoints;

namespace GameGuild.API.UnitTests.Endpoints;

public sealed class RootRedirectEndpointTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public async Task Root_ShouldRedirectToDocumentation_InNonProductionEnvironments(string environmentName)
    {
        await using var app = CreateApp(environmentName);
        var response = await ExecuteRootAsync(app);

        response.StatusCode.Should().Be((int)HttpStatusCode.Redirect);
        response.Headers.Location.ToString().Should().Be("/documentation");
    }

    [Fact]
    public async Task Root_ShouldReturnApiMetadata_InProduction()
    {
        await using var app = CreateApp("Production");
        var response = await ExecuteRootAsync(app);

        response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body);
        var payload = await reader.ReadToEndAsync();
        payload.Should().Contain("GameGuild API");
        payload.Should().Contain("Healthy");
        payload.Should().Contain("Production");
    }

    private static WebApplication CreateApp(string environmentName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });
        var app = builder.Build();
        new RootRedirectEndpoint().MapEndpoint(app);
        return app;
    }

    private static async Task<HttpResponse> ExecuteRootAsync(WebApplication app)
    {
        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == "/");
        var context = new DefaultHttpContext
        {
            RequestServices = app.Services
        };
        context.Response.Body = new MemoryStream();

        await (endpoint.RequestDelegate ?? throw new InvalidOperationException("Root request delegate was not built."))
            .Invoke(context);

        return context.Response;
    }
}
