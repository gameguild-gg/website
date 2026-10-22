using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class PermissionAuditTenantQueryPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData("Tenant")]
    [InlineData("User")]
    [InlineData("DateRange")]
    [InlineData("Operation")]
    [InlineData("TenantDateRange")]
    [InlineData("UserDateRange")]
    [InlineData("Permission")]
    [InlineData("ResourceType")]
    public async Task ConvertedTenantFilterExecutesOnPostgreSqlAndPreservesScope(string query)
    {
        await using var scope = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IPermissionAuditLogRepository>();
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);

        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var marker = "query-" + Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var own = Entry(new TenantId(tenantId), userId, marker, now);
        var foreign = Entry(new TenantId(foreignTenantId), userId, marker, now);
        var unscoped = Entry(null, userId, marker, now);
        db.AddRange(own, foreign, unscoped);
        await db.SaveChangesAsync();

        var scoped = await QueryAsync(repository, query, tenantId, userId, marker, now);
        Assert.Equal(own.Id, Assert.Single(scoped).Id);
        Assert.All(scoped, entry => Assert.Equal<TenantId?>(new TenantId(tenantId), entry.TenantId));

        var unknown = await QueryAsync(repository, query, Guid.NewGuid(), userId, marker, now);
        Assert.Empty(unknown);

        // A null scope intentionally retains the repository's platform-wide contract.
        // Authorization of that request remains the caller's responsibility.
        var global = await QueryAsync(repository, query, null, userId, marker, now);
        foreach (var expected in new[] { own, foreign, unscoped })
        {
            Assert.Contains(global, entry => entry.Id == expected.Id);
        }
    }

    private static PermissionAuditLog Entry(TenantId? tenantId, Guid userId, string marker, DateTime timestamp) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        UserId = userId,
        PerformedBy = userId,
        OperationType = PermissionOperationType.Grant,
        PermissionType = marker,
        ResourceType = marker,
        Reason = "Synthetic query scope regression",
        Timestamp = timestamp,
        Success = true
    };

    private static Task<List<PermissionAuditLog>> QueryAsync(
        IPermissionAuditLogRepository repository,
        string query,
        Guid? tenantId,
        Guid userId,
        string marker,
        DateTime timestamp) => query switch
    {
        "Tenant" => repository.GetByTenantAsync(tenantId),
        "User" => repository.GetByUserAsync(userId, tenantId),
        "DateRange" => repository.GetByDateRangeAsync(timestamp.AddMinutes(-1), timestamp.AddMinutes(1), tenantId),
        "Operation" => repository.GetByOperationTypeAsync(PermissionOperationType.Grant, tenantId),
        "TenantDateRange" => repository.GetByTenantAsync(tenantId, timestamp.AddMinutes(-1), timestamp.AddMinutes(1)),
        "UserDateRange" => repository.GetByUserAsync(userId, tenantId, timestamp.AddMinutes(-1), timestamp.AddMinutes(1)),
        "Permission" => repository.GetByPermissionAsync(marker, tenantId, timestamp.AddMinutes(-1), timestamp.AddMinutes(1)),
        "ResourceType" => repository.GetByResourceTypeAsync(marker, tenantId, timestamp.AddMinutes(-1), timestamp.AddMinutes(1)),
        _ => throw new ArgumentOutOfRangeException(nameof(query))
    };
}
