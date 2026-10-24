using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class PermissionSoftDeletePostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData("tenant", 0)]
    [InlineData("tenant", 1)]
    [InlineData("tenant", 2)]
    [InlineData("content-grant", 0)]
    [InlineData("content-grant", 1)]
    [InlineData("content-grant", 2)]
    [InlineData("legacy-content", 0)]
    [InlineData("legacy-content", 1)]
    [InlineData("legacy-content", 2)]
    [InlineData("resource", 0)]
    [InlineData("resource", 1)]
    [InlineData("resource", 2)]
    [InlineData("direct-resource", 0)]
    [InlineData("direct-resource", 1)]
    [InlineData("direct-resource", 2)]
    public async Task MaterializedDeletedGrantsDenyAccessAndRestoreRetainsOtherLifecycleLimits(string family, int limitation)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var subject = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var grantor = Guid.NewGuid();
        PermissionBase grant = family switch
        {
            "tenant" => new TenantPermission { UserId = subject, TenantId = tenant, Permissions = ["Read"], GrantedBy = grantor },
            "content-grant" => new ContentTypePermissionGrant { UserId = subject, TenantId = tenant, ContentTypeName = "Document", Permissions = ["Read"], GrantedBy = grantor },
            "legacy-content" => new ContentTypePermission(subject, tenant, "Document") { Permissions = "1", GrantedBy = grantor },
            "resource" => new GenericResourcePermission(subject, tenant, Guid.NewGuid(), "Document") { Permissions = "1", GrantedBy = grantor },
            "direct-resource" => new ResourceUserPermission { UserId = subject, TenantId = new TenantId(tenant), ResourceType = "Document", ResourceId = Guid.NewGuid().ToString("D"), Permissions = ["Read"], GrantedByUserId = grantor },
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        grant.GrantedAt = DateTime.UtcNow.AddDays(-2);
        if (limitation == 1)
        {
            grant.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        }
        else if (limitation == 2)
        {
            if (grant is ResourceUserPermission direct)
            {
                direct.Revoke(grantor, "Revocation survives restoration");
            }
            else
            {
                grant.IsActive = false;
            }
        }

        database.Add((object)grant);
        await database.SaveChangesAsync();
        grant.SoftDelete();
        await database.SaveChangesAsync();
        var id = grant.Id;
        database.ChangeTracker.Clear();

        // Read the persisted historical row deliberately bypassing query filters.
        // Access must fail on the entity itself even when that row is materialized.
        var loaded = await LoadAsync(database, family, id);
        Assert.NotSame(grant, loaded);
        Assert.True(loaded.IsDeleted);
        AssertAccess(loaded, false);
        Assert.Equal(subject, loaded.UserId);
        Assert.Equal(tenant, loaded.PermissionTenantId);
        Assert.Equal(grantor, loaded.CreatedBy);
        Assert.Contains(PermissionType.Read, loaded.GetGrantedPermissionTypes());

        loaded.Restore();
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var restored = await LoadAsync(database, family, id);
        Assert.NotSame(loaded, restored);
        Assert.False(restored.IsDeleted);
        AssertAccess(restored, limitation == 0);
        Assert.Equal(subject, restored.UserId);
        Assert.Equal(loaded.ExpiresAt, restored.ExpiresAt);
        if (restored is ResourceUserPermission directRestored && limitation == 2)
        {
            Assert.NotNull(directRestored.RevokedAt);
            Assert.Equal(grantor, directRestored.RevokedByUserId);
            Assert.Equal("Revocation survives restoration", directRestored.RevocationReason);
        }
        else if (limitation == 2)
        {
            Assert.False(restored.IsActive);
        }
    }

    [Fact]
    public async Task DirectResourceSubjectWrittenThroughTheBaseContractPersistsWithoutAShadowSubject()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var originalSubject = Guid.NewGuid();
        var updatedSubject = Guid.NewGuid();
        var grant = new ResourceUserPermission
        {
            UserId = originalSubject,
            TenantId = new TenantId(Guid.NewGuid()),
            ResourceType = "Document",
            ResourceId = Guid.NewGuid().ToString("D"),
            Permissions = ["Read"],
            GrantedByUserId = Guid.NewGuid()
        };
        database.Add(grant);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        PermissionBase loaded = await database.Set<ResourceUserPermission>().SingleAsync(row => row.Id == grant.Id);
        Assert.Equal(originalSubject, loaded.UserId);
        loaded.UserId = updatedSubject;
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var updated = await database.Set<ResourceUserPermission>().SingleAsync(row => row.Id == grant.Id);
        Assert.Equal(updatedSubject, updated.UserId);
        Assert.Equal(updatedSubject, ((PermissionBase)updated).UserId);
        Assert.Equal(updatedSubject, ((IDacAttribute)updated).Owner);
        Assert.Single(database.Model.FindEntityType(typeof(ResourceUserPermission))!.GetProperties(), property => property.Name == "UserId");
    }

    private static void AssertAccess(PermissionBase grant, bool expected)
    {
        Assert.Equal(expected, grant.IsEffective());
        Assert.Equal(expected, grant.Grants(PermissionType.Read));
        if (grant is TenantPermission tenant)
        {
            Assert.Equal(expected, tenant.HasEffectivePermission("Read"));
        }
        if (grant is ContentTypePermissionGrant content)
        {
            Assert.Equal(expected, content.HasEffectivePermission("Read"));
        }
        if (grant is ResourceUserPermission direct)
        {
            Assert.Equal(expected, direct.IsActive);
            Assert.Equal(expected, direct.CanAccess);
            Assert.Equal(expected, direct.HasPermission("Read"));
            Assert.Equal(expected, direct.HasAnyPermission(["Read"]));
            Assert.Equal(expected, direct.HasAllPermissions(["Read"]));
        }
    }

    private static async Task<PermissionBase> LoadAsync(ApplicationDbContext database, string family, Guid id)
        => family switch
        {
            "tenant" => await database.Set<TenantPermission>().IgnoreQueryFilters().SingleAsync(row => row.Id == id),
            "content-grant" => await database.Set<ContentTypePermissionGrant>().IgnoreQueryFilters().SingleAsync(row => row.Id == id),
            "legacy-content" => await database.Set<ContentTypePermission>().IgnoreQueryFilters().SingleAsync(row => row.Id == id),
            "resource" => await database.Set<GenericResourcePermission>().IgnoreQueryFilters().SingleAsync(row => row.Id == id),
            "direct-resource" => await database.Set<ResourceUserPermission>().IgnoreQueryFilters().SingleAsync(row => row.Id == id),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
}
