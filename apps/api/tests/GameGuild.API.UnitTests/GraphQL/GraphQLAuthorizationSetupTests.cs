using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using GameGuild.API;
using GameGuild.Configuration.PresentationLayer.GraphQL;
using FluentAssertions;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.API.UnitTests.GraphQL;

public sealed class GraphQLAuthorizationSetupTests
{
    [Fact]
    public async Task SetupGraphQL_WhenEnabled_RegistersProjectSchemaAndAuthorizationDirective()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.SetupGraphQL(
            new ConfigurationBuilder().Build(),
            new GraphQLOptions { EnableGraphQL = true });

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequiredService<IRequestExecutorResolver>()
            .GetRequestExecutorAsync();

        var queryType = executor.Schema.GetType<ObjectType>("Query");
        queryType.Fields.Select(field => field.Name).Should().Contain("projects");
        executor.Schema.DirectiveTypes.Select(directive => directive.Name).Should().Contain("authorize");
        queryType.Fields["projectById"].Directives.ContainsDirective("projectAuthorize").Should().BeTrue();
        executor.Schema.DirectiveTypes.Select(directive => directive.Name)
            .Should().Contain("projectAuthorize");
    }

    [Fact]
    public async Task GraphQLRoute_RequiresAuthentication_AndEnforcesFieldPolicy()
    {
        await using var app = await CreateApplicationAsync();
        using var client = app.GetTestClient();

        using var anonymousResponse = await client.PostAsJsonAsync(
            "/graphql",
            new { query = "{ __typename }" });
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var userRequest = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(new { query = "{ deletedProjects { id } }" })
        };
        userRequest.Headers.Add("Authorization", "Bearer user");
        using var userResponse = await client.SendAsync(userRequest);

        // The operation-planning authorization gate hides the protected root operation from
        // callers without a resolvable permission context: the request fails closed with the
        // nonexistent-field error shape (a request error → 400), revealing neither the
        // operation's existence, an authorization code, nor a response path.
        var responseBody = await userResponse.Content.ReadAsStringAsync();
        userResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, responseBody);
        using var responseDocument = JsonDocument.Parse(responseBody);
        responseDocument.RootElement.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
        responseDocument.RootElement.TryGetProperty("data", out _).Should().BeFalse(responseBody);
        responseDocument.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Select(error => error.GetProperty("message").GetString())
            .Should().Contain("The field `deletedProjects` does not exist on the type `Query`.");
        responseBody.Should().NotContain("AUTHORIZATION");
        responseBody.ToLowerInvariant().Should().NotContain("authorized");
    }

    private static async Task<WebApplication> CreateApplicationAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, GraphQLTestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
        });
        builder.Services.AddHttpContextAccessor();
        builder.Services.SetupGraphQL(
            new ConfigurationBuilder().Build(),
            new GraphQLOptions { EnableGraphQL = true });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGraphQL("/graphql").RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private sealed class GraphQLTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authorization))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var role = authorization.ToString().EndsWith("admin", StringComparison.OrdinalIgnoreCase)
                ? "Admin"
                : "User";
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6"), new Claim(ClaimTypes.Role, role)],
                Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
