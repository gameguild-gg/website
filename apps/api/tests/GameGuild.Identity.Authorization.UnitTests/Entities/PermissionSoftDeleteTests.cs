using GameGuild.CQRS.Models;
using GameGuild.Identity.Authentication;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Entities;

public sealed class PermissionSoftDeleteTests
{
    [Theory]
    [InlineData("tenant", false)]
    [InlineData("tenant", true)]
    [InlineData("content-grant", false)]
    [InlineData("content-grant", true)]
    [InlineData("legacy-content", false)]
    [InlineData("legacy-content", true)]
    [InlineData("resource", false)]
    [InlineData("resource", true)]
    [InlineData("direct-resource", false)]
    [InlineData("direct-resource", true)]
    public void DeletedGrantCannotConveyAccessAndRestorePreservesItsPayload(string family, bool materialized)
    {
        PermissionBase grant = Create(family);
        var tenant = grant.PermissionTenantId;
        var grantor = grant.CreatedBy;
        AssertAccess(grant, true);

        if (materialized)
        {
            // Simulate a previously persisted row read without a query filter.
            grant.DeletedAt = DateTime.UtcNow.AddMinutes(-1);
        }
        else
        {
            grant.SoftDelete();
        }

        Assert.True(grant.IsDeleted);
        AssertAccess(grant, false);
        Assert.Contains(PermissionType.Read, grant.GetGrantedPermissionTypes());
        Assert.Equal(tenant, grant.PermissionTenantId);
        Assert.Equal(grantor, grant.CreatedBy);
        if (grant is ResourceUserPermission direct)
        {
            Assert.False(direct.UpdatePermissions(["Delete"], Guid.NewGuid()));
            Assert.Equal(["Read"], direct.Permissions);
        }

        grant.Restore();

        Assert.False(grant.IsDeleted);
        AssertAccess(grant, true);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("content-grant")]
    [InlineData("legacy-content")]
    [InlineData("resource")]
    [InlineData("direct-resource")]
    public void RestoreCannotReactivateAnExpiredGrant(string family)
    {
        var grant = Create(family);
        grant.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        grant.SoftDelete();

        grant.Restore();

        AssertAccess(grant, false);
        Assert.NotNull(grant.ExpiresAt);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("content-grant")]
    [InlineData("legacy-content")]
    [InlineData("resource")]
    public void RestoreCannotReactivateAnInactiveGrant(string family)
    {
        var grant = Create(family);
        grant.IsActive = false;
        grant.SoftDelete();

        grant.Restore();

        AssertAccess(grant, false);
        Assert.False(grant.IsActive);
    }

    [Fact]
    public void RestoreCannotUndoResourceRevocation()
    {
        var grant = Assert.IsType<ResourceUserPermission>(Create("direct-resource"));
        var actor = Guid.NewGuid();
        Assert.True(grant.Revoke(actor, "Access withdrawn"));
        var revokedAt = grant.RevokedAt;
        grant.SoftDelete();

        grant.Restore();

        AssertAccess(grant, false);
        Assert.Equal(revokedAt, grant.RevokedAt);
        Assert.Equal(actor, grant.RevokedByUserId);
        Assert.Equal("Access withdrawn", grant.RevocationReason);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("content-grant")]
    public void EffectiveLookupStillHonorsExplicitDenial(string family)
    {
        if (Create(family) is TenantPermission tenant)
        {
            tenant.DenyPermissions = ["read"];
            Assert.False(tenant.HasEffectivePermission("READ"));
            Assert.True(tenant.HasPermission("READ"));
            Assert.True(tenant.HasDenyPermission("READ"));
        }
        else
        {
            var content = Assert.IsType<ContentTypePermissionGrant>(Create(family));
            content.DenyPermissions = ["read"];
            Assert.False(content.HasEffectivePermission("READ"));
            Assert.True(content.HasPermission("READ"));
            Assert.True(content.HasDenyPermission("READ"));
        }
    }

    private static void AssertAccess(PermissionBase grant, bool expected)
    {
        // Exercise the polymorphic shared contract, not only concrete properties.
        Assert.Equal(expected, grant.IsEffective());
        Assert.Equal(expected, grant.Grants(PermissionType.Read));
        if (grant is TenantPermission tenant)
        {
            Assert.Equal(expected, tenant.HasEffectivePermission("READ"));
        }
        if (grant is ContentTypePermissionGrant content)
        {
            Assert.Equal(expected, content.HasEffectivePermission("READ"));
        }
        if (grant is ResourceUserPermission direct)
        {
            Assert.Equal(expected, direct.IsActive);
            Assert.Equal(expected, direct.CanAccess);
            Assert.Equal(expected, direct.HasPermission("READ"));
            Assert.Equal(expected, direct.HasAnyPermission(["read", "delete"]));
            Assert.Equal(expected, direct.HasAllPermissions(["Read"]));
            Assert.Equal(expected, direct.HasAllPermissions([]));
        }
    }

    private static PermissionBase Create(string family)
    {
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var grantor = Guid.NewGuid();
        PermissionBase grant = family switch
        {
            "tenant" => new TenantPermission { UserId = user, TenantId = tenant, Permissions = ["Read"], GrantedBy = grantor },
            "content-grant" => new ContentTypePermissionGrant { UserId = user, TenantId = tenant, ContentTypeName = "Document", Permissions = ["Read"], GrantedBy = grantor },
            "legacy-content" => new ContentTypePermission(user, tenant, "Document") { Permissions = "1", GrantedBy = grantor },
            "resource" => new GenericResourcePermission(user, tenant, Guid.NewGuid(), "Document") { Permissions = "1", GrantedBy = grantor },
            "direct-resource" => new ResourceUserPermission { UserId = user, TenantId = new TenantId(tenant), ResourceType = "Document", ResourceId = Guid.NewGuid().ToString("D"), Permissions = ["Read"], GrantedByUserId = grantor },
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        grant.Version = 1;
        return grant;
    }
}
