using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using GameGuild.API.Database;
using GameGuild.Resources.IntegrationTests.Infrastructure;

namespace GameGuild.Resources.IntegrationTests;

[Collection("PostgreSql")]
public sealed class QuotaLedgerPostgreSqlTests(PostgreSqlTestFixture fixture)
{
    [Fact]
    public async Task DuplicateDeleteDelivery_AppendsAndDecrementsExactlyOnce()
    {
        await using var database = await fixture.CreateDatabaseAsync("quota_ledger");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync();

        var tenantId = Guid.NewGuid();
        context.Add(new ResourceQuota
        {
            TenantId = tenantId,
            Type = ResourceUsageType.Properties,
            CurrentUsage = 1,
            HardLimit = 10
        });
        await context.SaveChangesAsync();
        var lifecycleEvent = new PropertyDeletedEvent(Guid.NewGuid())
        {
            TenantId = tenantId,
            ActorId = Guid.NewGuid(),
            AggregateType = "Property",
            AggregateId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid()
        };
        var handler = new QuotaLedgerEventHandler(context);

        await handler.HandleAsync(lifecycleEvent);
        await handler.HandleAsync(lifecycleEvent);

        (await context.Set<QuotaLedgerEntry>().CountAsync()).Should().Be(1);
        (await context.Set<ResourceQuota>().SingleAsync()).CurrentUsage.Should().Be(0);
    }
}
