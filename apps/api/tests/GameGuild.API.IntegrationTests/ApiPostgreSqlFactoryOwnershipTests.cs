using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class ApiPostgreSqlFactoryOwnershipTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(typeof(ApplicationStartupIntegrationTests))]
    [InlineData(typeof(CommonOpenApiIntegrationTests))]
    [InlineData(typeof(ModuleOpenApiIntegrationTests))]
    public void PerTestHostOwnersDisposeTheirFactoryWithoutAParentFixture(Type ownerType)
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(ownerType),
            $"{ownerType.Name} must release its per-test host.");
        Assert.DoesNotContain(typeof(IClassFixture<WebApplicationFactory<Program>>), ownerType.GetInterfaces());
    }

    [Theory]
    [InlineData(typeof(ApplicationStartupIntegrationTests))]
    [InlineData(typeof(CommonOpenApiIntegrationTests))]
    [InlineData(typeof(ModuleOpenApiIntegrationTests))]
    public void DisposingPerTestHostOwnerReleasesItsServices(Type ownerType)
    {
        var constructor = Assert.Single(ownerType.GetConstructors());
        var arguments = constructor.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(ApiPostgreSqlFixture) ? (object)fixture :
            parameter.ParameterType == typeof(WebApplicationFactory<Program>) ? fixture.Factory :
            throw new InvalidOperationException($"Unexpected fixture: {parameter.ParameterType}")).ToArray();
        var instance = constructor.Invoke(arguments);
        var owner = Assert.IsAssignableFrom<IDisposable>(instance);
        var field = ownerType.GetField("_factory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        var factory = Assert.IsAssignableFrom<WebApplicationFactory<Program>>(field.GetValue(instance));
        IServiceProvider services;
        try
        {
            services = factory.Services;
            using var scope = services.CreateScope();
            Assert.True(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.IsInMemory());
            Assert.Empty(factory.Factories);
        }
        finally
        {
            owner.Dispose();
        }

        Assert.Throws<ObjectDisposedException>(() => services.CreateScope());
    }

    [Fact]
    public async Task DisposedConfiguredFactoriesAreNotRetainedByTheSharedFixture()
    {
        var initialCount = fixture.Factory.Factories.Count;
        for (var iteration = 0; iteration < 3; iteration++)
        {
            await using (var factory = fixture.CreateFactory(_ => { }))
            {
                Assert.NotSame(fixture.Factory, factory);
            }

            Assert.Equal(initialCount, fixture.Factory.Factories.Count);
        }
    }

    [Fact]
    public async Task IndependentFactoryRetainsDatabaseAndConfigurationAndDisposesItsHost()
    {
        var marker = Guid.NewGuid().ToString("N");
        var initialCount = fixture.Factory.Factories.Count;
        var factory = fixture.CreateFactory(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FactoryOwnership:Marker"] = marker
            })));
        IServiceProvider services;
        await using (factory)
        {
            services = factory.Services;
            Assert.Equal(marker, services.GetRequiredService<IConfiguration>()["FactoryOwnership:Marker"]);
            using var scope = services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(fixture.ConnectionString, database.Database.GetConnectionString());
            Assert.True(await database.Database.CanConnectAsync());
        }

        Assert.Equal(initialCount, fixture.Factory.Factories.Count);
        Assert.Throws<ObjectDisposedException>(() => services.CreateScope());
    }
}
