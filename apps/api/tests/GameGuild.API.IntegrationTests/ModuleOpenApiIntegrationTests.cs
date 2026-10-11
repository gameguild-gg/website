using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

public sealed class ModuleOpenApiIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public ModuleOpenApiIntegrationTests()
    {
        _factory = new ConfiguredApiWebApplicationFactory(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Database:RunStartupInitialization"] = "false" }));
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpLogging(_ => { });

                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                                d.ServiceType == typeof(ApplicationDbContext) ||
                                d.ServiceType.FullName?.Contains("EntityFramework") == true ||
                                d.ImplementationType?.FullName?.Contains("Npgsql") == true)
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase($"ModuleOpenApiTestDb_{Guid.NewGuid()}");
                });
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
            });
        });
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Swagger_ShouldExposeImplementedModuleRoutes()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var paths = document["paths"]!.AsObject();

        paths.Should().ContainKey("/v1/ai/status");
        paths.Should().ContainKey("/v1/ai/chat");
        paths.Should().ContainKey("/v1/ai/prompt-templates");
        paths.Should().ContainKey("/api/compliance/ferpa/students/{studentUserId}/records");
        paths.Should().ContainKey("/api/social/profiles/users/{userId}");
        paths.Should().ContainKey("/api/game-jams");
        paths.Should().ContainKey("/api/learning/enrollments");
        paths.Should().ContainKey("/api/social/blog/public/posts");
        paths.Should().ContainKey("/api/social/blog/posts/mine");
        paths.Should().NotContainKey("/api/social/blog");
        paths.Should().ContainKey("/api/social/feed/users/{userId}");
        paths.Should().ContainKey("/api/social/groups");
        paths.Should().ContainKey("/api/social/reactions");
    }

    [Fact]
    public async Task Swagger_ShouldExposeOnlyTheVerifiedOrderSurface()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var orderOperations = document["paths"]!.AsObject()
            .Where(path => path.Key.StartsWith("/v1/orders", StringComparison.Ordinal))
            .SelectMany(path => path.Value!.AsObject().Select(operation => $"{operation.Key} {path.Key}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        orderOperations.Should().Equal(
            "get /v1/orders",
            "get /v1/orders/{orderId}",
            "post /v1/orders",
            "post /v1/orders/{orderId}/items",
            "post /v1/orders/{orderId}:capture",
            "post /v1/orders/{orderId}:complete",
            "post /v1/orders/{orderId}:payment-intent");
    }

    [Fact]
    public async Task Swagger_ShouldDescribeFlagsEnumsAsComposableStrings()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var schema = document["components"]!["schemas"]!["Learning_Assessments_SubmissionModality"]!.AsObject();

        schema["type"]!.GetValue<string>().Should().Be("string");
        schema["enum"].Should().BeNull();
        schema["description"]!.GetValue<string>().Should().Contain("comma-separated");
    }

    [Fact]
    public async Task Swagger_ShouldHideLegacyProgramContentTypes()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var schema = document["components"]!["schemas"]!["Learning_Courses_ProgramContentType"]!.AsObject();
        var values = schema["enum"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();

        values.Should().NotContain(["Page", "Challenge"]);
        values.Should().Contain(["Lesson", "Assignment", "Questionnaire", "Module"]);
    }
    [Fact]
    public async Task Swagger_ShouldHideLegacyAssessmentExamTypeAndExposeDefinitionRoutes()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var types = document["components"]!["schemas"]!["Learning_Assessments_AssessmentType"]!["enum"]!
            .AsArray()
            .Select(value => value!.GetValue<string>())
            .ToArray();
        var paths = document["paths"]!.AsObject();

        types.Should().NotContain("Exam");
        types.Should().Contain(["Quiz", "Assignment", "Project", "PeerReview", "SelfAssessment"]);
        paths.Should().ContainKey("/v1/assessments/course/{courseId}/content/{contentId}/draft");
        paths.Should().ContainKey("/v1/assessments/{id}/authoring-state");
        paths.Should().ContainKey("/v1/assessments/{id}/revisions/prepare");
        paths.Should().ContainKey("/v1/assessments/{id}/revisions/publish");
        paths.Should().NotContainKey("/v1/assessments/{id}/definition");
    }

    [Fact]
    public async Task Swagger_ShouldExposeStructuredLessonBodies()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var schemas = document["components"]!["schemas"]!.AsObject();

        foreach (var name in
                 new[]
                 {
                     "Learning_Courses_ProgramContentDto",
                     "Learning_Courses_CreateProgramContentDto",
                     "Learning_Courses_UpdateProgramContentDto",
                 })
        {
            schemas[name]!["properties"]!.AsObject().Should().ContainKey("jsonBody");
        }
    }

    [Fact]
    public async Task Swagger_ShouldNotExposePasswordHashes()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var userProperties = document["components"]!["schemas"]!["Identity_Users_User"]!["properties"]!.AsObject();

        userProperties.Should().NotContainKey("passwordHash");
    }

    [Fact]
    public async Task Runtime_ShouldRouteOnlyVerifiedOrderOperations()
    {
        using var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();

        var listResponse = await client.GetAsync("/v1/orders");
        var cancelResponse = await client.PostAsync($"/v1/orders/{orderId}:cancel", content: null);
        var verifiedResponse = await client.GetAsync($"/v1/orders/{orderId}");
        var paymentIntentResponse = await client.PostAsync(
            $"/v1/orders/{orderId}:payment-intent", content: null);

        listResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        verifiedResponse.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        verifiedResponse.StatusCode.Should().NotBe(HttpStatusCode.MethodNotAllowed);
        paymentIntentResponse.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        paymentIntentResponse.StatusCode.Should().NotBe(HttpStatusCode.MethodNotAllowed);
    }
    [Fact]
    public async Task Swagger_ShouldExposeAuthenticatedEconomyWalletReadRoutes()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var paths = document["paths"]!.AsObject().Select(path => path.Key).ToArray();

        paths.Should().Contain(path => path.EndsWith("/economy/wallet", StringComparison.Ordinal));
        paths.Should().Contain(path => path.EndsWith("/economy/wallet/transactions", StringComparison.Ordinal));
        paths.Should().Contain(path => path.EndsWith("/economy/capabilities", StringComparison.Ordinal));
    }

}
