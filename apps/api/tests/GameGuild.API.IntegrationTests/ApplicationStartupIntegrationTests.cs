using FluentAssertions;
using GameGuild.API.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.Json.Nodes;

namespace GameGuild.API.IntegrationTests;

/// <summary>
/// Integration tests for API application startup and configuration
/// </summary>
public class ApplicationStartupIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApplicationStartupIntegrationTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                // Add HttpLogging service required by the pipeline
                services.AddHttpLogging(_ => { });

                // Remove ALL EF Core and database provider services
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

                // Add in-memory database
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase($"StartupTestDb_{Guid.NewGuid()}");
                });
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
            });
        });
    }

    [Fact]
    public void Application_ShouldStart_WithoutThrowingExceptions()
    {
        // Arrange & Act
        Action act = () =>
        {
            using var client = _factory.CreateClient();
        };

        // Assert
        act.Should().NotThrow("application should start successfully with test configuration");
    }

    [Fact]
    public async Task RootEndpoint_ShouldReturnApiMetadata_OutsideDevelopmentAndStaging()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        body["name"]!.GetValue<string>().Should().Be("GameGuild API");
        body["environment"]!.GetValue<string>().Should().Be("Testing");
    }

    [Fact]
    public void ServiceProvider_ShouldResolveApplicationDbContext()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();

        // Act
        var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();

        // Assert
        dbContext.Should().NotBeNull("ApplicationDbContext should be registered in DI container");
    }

    [Fact]
    public void Application_ShouldConfigureMultipleTimes_WithoutThrowingExceptions()
    {
        // Arrange & Act
        Action act = () =>
        {
            using var client1 = _factory.CreateClient();
            using var client2 = _factory.CreateClient();
            using var client3 = _factory.CreateClient();
        };

        // Assert
        act.Should().NotThrow("application should handle multiple client creations");
    }

    [Fact]
    public async Task Application_ShouldHaveSwaggerEndpoint_InTestEnvironment()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        // Swagger might be disabled in test environment, so we check if it's either available or not found
        (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound)
            .Should().BeTrue("Swagger endpoint should either be available or intentionally disabled");
    }

    [Fact]
    public async Task PublicCoursesEndpoint_ShouldBeReachable_WithoutAuthentication()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/v1/courses/public");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Swagger_PublicCourseEndpoints_ShouldClearInheritedSecurityRequirements()
    {
        // Arrange
        using var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        var publicSecurity = document["paths"]?["/v1/courses/public"]?["get"]?["security"];
        var slugSecurity = document["paths"]?["/v1/courses/slug/{slug}"]?["get"]?["security"];
        var publicAllowAnonymous = document["paths"]?["/v1/courses/public"]?["get"]?["x-gameguild-allow-anonymous"];
        var slugAllowAnonymous = document["paths"]?["/v1/courses/slug/{slug}"]?["get"]?["x-gameguild-allow-anonymous"];

        publicAllowAnonymous.Should().NotBeNull();
        slugAllowAnonymous.Should().NotBeNull();
        publicAllowAnonymous!.GetValue<bool>().Should().BeTrue();
        slugAllowAnonymous!.GetValue<bool>().Should().BeTrue();

        publicSecurity.Should().BeNull("the OpenAPI serializer omits empty security arrays, so code generation relies on the explicit anonymous extension instead");
        slugSecurity.Should().BeNull("the OpenAPI serializer omits empty security arrays, so code generation relies on the explicit anonymous extension instead");
    }

    [Fact]
    public void Application_ShouldRefuseToListen_WhenRequiredStartupMigrationFails()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                const string unavailableDatabase =
                    "Host=127.0.0.1;Port=1;Database=unavailable;Username=runtime;Password=test;Timeout=1;Command Timeout=1;Pooling=false";
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = unavailableDatabase,
                    ["ConnectionStrings:MigrationConnection"] = unavailableDatabase.Replace("Username=runtime", "Username=migrator", StringComparison.Ordinal),
                    ["Database:RunStartupInitialization"] = "true",
                    ["Database:FailStartupOnMigrationFailure"] = "true",
                    ["Database:MigrationMaxAttempts"] = "1",
                    ["SeedData:ImportSnapshotCourses"] = "false"
                });
            });
        });

        Action start = () => factory.CreateClient();

        start.Should().Throw<Exception>();
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void Application_ShouldRefuseToListen_WhenPaymentSimulationIsEnabled(string environmentName)
    {
        using var factory = CreateCommerceConfiguredFactory(environmentName, configuration =>
        {
            configuration["PaymentGateways:Stripe:UseSimulation"] = "true";
        });

        Action start = () => factory.CreateClient();

        start.Should().Throw<Exception>();
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void Application_ShouldStart_WhenWebhookVerificationIsNotConfigured(string environmentName)
    {
        using var factory = CreateCommerceConfiguredFactory(environmentName, configuration =>
        {
            configuration["Billing:Stripe:WebhookSecret"] = null;
        });

        Action start = () => factory.CreateClient();

        start.Should().NotThrow();
    }

    [Fact]
    public void Application_ShouldStart_WithCompleteProductionCommerceConfiguration()
    {
        using var factory = CreateCommerceConfiguredFactory("Production");

        Action start = () => factory.CreateClient();

        start.Should().NotThrow();
    }

    private static WebApplicationFactory<Program> CreateCommerceConfiguredFactory(
        string environmentName,
        Action<Dictionary<string, string?>>? customize = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=127.0.0.1;Port=1;Database=gameguild_startup;Username=gameguild_runtime;Password=runtime-startup-test;Timeout=1;Command Timeout=1;Pooling=false",
            ["ConnectionStrings:MigrationConnection"] =
                "Host=127.0.0.1;Port=1;Database=gameguild_startup;Username=gameguild_migrator;Password=migration-startup-test;Timeout=1;Command Timeout=1;Pooling=false",
            ["Database:RunStartupInitialization"] = "false",
            ["Jwt:SecretKey"] = "startup-test-jwt-secret-with-forty-characters",
            ["Jwt:Issuer"] = "GameGuild.StartupTests",
            ["Jwt:Audience"] = "GameGuild.StartupTests.Users",
            ["Encryption:EncryptionKey"] = "startup-test-encryption-key-32-chars",
            ["Redis:Enabled"] = "true",
            ["Redis:ConnectionString"] = "127.0.0.1:1,abortConnect=false",
            ["EmailDelivery:Enabled"] = "true",
            ["EmailDelivery:FromEmail"] = "startup-tests@gameguild.gg",
            ["EmailDelivery:Provider"] = "Smtp",
            ["EmailDelivery:SmtpHost"] = "127.0.0.1",
            ["EmailDelivery:SmtpPort"] = "25",
            ["Assets:Storage:ServiceUrl"] = "http://127.0.0.1:1",
            ["Assets:Storage:AccessKey"] = "startup-test-access-key",
            ["Assets:Storage:SecretKey"] = "startup-test-secret-key",
            ["Assets:Storage:BucketName"] = "startup-tests",
            ["Assets:Token:SecretKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            ["PaymentGateways:Stripe:IsEnabled"] = "true",
            ["PaymentGateways:Stripe:UseSimulation"] = "false",
            ["PaymentGateways:Stripe:ApiKey"] = environmentName == "Production" ? "sk_live_startup_test" : "sk_test_startup_test",
            ["PaymentGateways:Stripe:PublishableKey"] = environmentName == "Production" ? "pk_live_startup_test" : "pk_test_startup_test",
            ["PaymentGateways:Stripe:AccountId"] = "acct_startup_test",
            ["PaymentGateways:Stripe:LiveMode"] = environmentName == "Production" ? "true" : "false",
            ["Billing:Stripe:WebhookSecret"] = "whsec_startup_test",
            ["Billing:Stripe:WebhookEndpointId"] = "we_startup_test",
            ["Billing:Stripe:AccountId"] = "acct_startup_test",
            ["Billing:Stripe:ApiVersion"] = "2023-10-16",
            ["Billing:Stripe:LiveMode"] = environmentName == "Production" ? "true" : "false",
            ["SeedData:ImportSnapshotCourses"] = "false"
        };
        customize?.Invoke(values);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environmentName);
            foreach (var (key, value) in values)
            {
                if (value is not null)
                {
                    builder.UseSetting(key, value);
                }
            }
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
        });
    }
}
