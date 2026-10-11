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

public sealed class CommonOpenApiIntegrationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public CommonOpenApiIntegrationTests()
    {
        _factory = new ConfiguredApiWebApplicationFactory(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Database:RunStartupInitialization"] = "false" }));
            builder.ConfigureTestServices(services =>
            {
                var descriptorsToRemove = services
                    .Where(descriptor => descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                                         descriptor.ServiceType == typeof(ApplicationDbContext) ||
                                         descriptor.ServiceType.FullName?.Contains("EntityFramework") == true ||
                                         descriptor.ImplementationType?.FullName?.Contains("Npgsql") == true)
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase($"CommonOpenApiTestDb_{Guid.NewGuid()}"));
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
            });
        });
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Swagger_ShouldDocumentRefreshTokenRevocationOwnershipDenial()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var responses = document["paths"]!["/v1/auth/tokens:revoke"]!["post"]!["responses"]!.AsObject();

        responses.Should().ContainKey("204");
        responses.Should().ContainKey("400");
        responses.Should().ContainKey("401");
        responses.Should().ContainKey("403");
    }

    [Fact]
    public async Task Swagger_ShouldExposeSharedContractsWithoutSensitiveIdentityFields()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var paths = document["paths"]!.AsObject();
        var schemas = document["components"]!["schemas"]!.AsObject();

        paths.Should().ContainKey("/v1/ai/status");
        paths.Should().ContainKey("/v1/orders/{orderId}");
        var publishedPropertyNames = schemas
            .SelectMany(schema => schema.Value?["properties"] is JsonObject properties
                ? properties.Select(property => property.Key)
                : [])
            .ToArray();
        publishedPropertyNames.Should().NotContain("passwordHash");
    }

    [Fact]
    public async Task Swagger_ShouldServeDescriptionsAndExamplesForEverySchema()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var schemas = document["components"]!["schemas"]!.AsObject();

        schemas.Should().NotBeEmpty();
        var schemasWithoutDescriptions = schemas
            .Where(pair => string.IsNullOrWhiteSpace(pair.Value?["description"]?.GetValue<string>()))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var schemasWithoutExamples = schemas
            .Where(pair => pair.Value?["example"] is null)
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        schemasWithoutDescriptions.Should().BeEmpty("missing descriptions: {0}",
            string.Join(", ", schemasWithoutDescriptions));
        schemasWithoutExamples.Should().BeEmpty("missing examples: {0}; ActivitySettings schema: {1}",
            string.Join(", ", schemasWithoutExamples),
            schemas["Learning_Courses_ActivitySettings"]?.ToJsonString());
        schemas["Identity_Authentication_CreateApiKeyResponse"]!["example"]!["apiKey"]!
            .GetValue<string>().Should().Be("gg_example_not-a-valid-secret");
        var activitySettings = schemas["Learning_Courses_ActivitySettings"]!.AsObject();
        activitySettings["oneOf"]!.AsArray().Should().HaveCount(3);
        activitySettings["discriminator"]!["propertyName"]!.GetValue<string>().Should().Be("kind");
        activitySettings["example"]!["kind"]!.GetValue<string>().Should().Be("discussion");

        foreach (var (schemaId, discriminatorValue) in new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Learning_Courses_DiscussionActivitySettings"] = "discussion",
            ["Learning_Courses_ReflectionActivitySettings"] = "reflection",
            ["Learning_Courses_SurveyActivitySettings"] = "survey"
        })
        {
            var variant = schemas[schemaId]!.AsObject();
            variant["required"]!.AsArray().Select(value => value!.GetValue<string>())
                .Should().Contain("kind");
            variant["properties"]!["kind"]!["enum"]!.AsArray()
                .Select(value => value!.GetValue<string>()).Should().ContainSingle()
                .Which.Should().Be(discriminatorValue);
        }
    }
}
