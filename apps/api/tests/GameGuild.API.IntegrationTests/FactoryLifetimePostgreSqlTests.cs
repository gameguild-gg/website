using GameGuild.API.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class FactoryLifetimePostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task Independent_factory_stops_its_host_before_disposal_returns()
    {
        IHostApplicationLifetime ownedLifetime;
        await using (var factory = fixture.CreateFactory(_ => { }))
        {
            using var client = factory.CreateClient();
            ownedLifetime = factory.Services.GetRequiredService<IHostApplicationLifetime>();

            Assert.False(ownedLifetime.ApplicationStopped.IsCancellationRequested);
            Assert.DoesNotContain(factory, fixture.Factory.Factories);
        }

        Assert.True(ownedLifetime.ApplicationStopped.IsCancellationRequested);
    }
}
