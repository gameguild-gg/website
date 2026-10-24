using GameGuild.CQRS.Models;
using GameGuild.Identity.Authentication;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Entities;

public sealed class PermissionSoftDeleteTests
{
    [Fact]
    public void SharedDeactivationRevokesDirectResourceAccessWithoutChangingTheGrantPayload()
    {
        var direct = Assert.IsType<ResourceUserPermission>(Create("direct-resource"));
        PermissionBase shared = direct;
        var grantor = shared.CreatedBy;
        var expiration = DateTime.UtcNow.AddDays(1);
        shared.ExpiresAt = expiration;
        shared.UpdatedAt = DateTime.UtcNow.AddDays(-1);
        var previousUpdate = shared.UpdatedAt;

        shared.IsActive = false;

        Assert.False(shared.IsActive);
        AssertAccess(shared, false);
        Assert.NotNull(direct.RevokedAt);
        Assert.True(shared.UpdatedAt > previousUpdate);
        Assert.Equal(expiration, shared.ExpiresAt);
        Assert.Equal(grantor, shared.CreatedBy);
        Assert.Equal(["Read"], direct.Permissions);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("deleted")]
    [InlineData("expired")]
    public void SharedActivityCannotReactivateAnIneffectiveResourceGrant(string limitation)
    {
        var direct = Assert.IsType<ResourceUserPermission>(Create("direct-resource"));
        if (limitation == "revoked") { direct.Revoke(Guid.NewGuid(), "Access withdrawn"); }
        else if (limitation == "deleted") { direct.SoftDelete(); }
        else { direct.ExpiresAt = DateTime.UtcNow.AddDays(-1); }
        PermissionBase shared = direct;
        var revokedAt = direct.RevokedAt;
        var revokedBy = direct.RevokedByUserId;
        var reason = direct.RevocationReason;
        var deletedAt = shared.DeletedAt;
        var expiresAt = shared.ExpiresAt;

        Assert.False(shared.IsActive);
        Assert.Throws<InvalidOperationException>(() => shared.IsActive = true);

        AssertAccess(shared, false);
        Assert.Equal(revokedAt, direct.RevokedAt);
        Assert.Equal(revokedBy, direct.RevokedByUserId);
        Assert.Equal(reason, direct.RevocationReason);
        Assert.Equal(deletedAt, shared.DeletedAt);
        Assert.Equal(expiresAt, shared.ExpiresAt);
    }

    [Fact]
    public void SettingAnAlreadyActiveResourceGrantActiveDoesNotChangeItsHistory()
    {
        var direct = Assert.IsType<ResourceUserPermission>(Create("direct-resource"));
        PermissionBase shared = direct;
        var updatedAt = shared.UpdatedAt;

        shared.IsActive = true;

        Assert.True(shared.IsActive);
        AssertAccess(shared, true);
        Assert.Null(direct.RevokedAt);
        Assert.Equal(updatedAt, shared.UpdatedAt);
    }

    [Fact]
    public void SharedDeactivationPreservesExistingRevocationAuditMetadata()
    {
        var direct = Assert.IsType<ResourceUserPermission>(Create("direct-resource"));
        var actor = Guid.NewGuid();
        direct.Revoke(actor, "Access withdrawn");
        var revokedAt = direct.RevokedAt;
        var updatedAt = direct.UpdatedAt;
        PermissionBase shared = direct;

        shared.IsActive = false;

        AssertAccess(shared, false);
        Assert.Equal(revokedAt, direct.RevokedAt);
        Assert.Equal(actor, direct.RevokedByUserId);
        Assert.Equal("Access withdrawn", direct.RevocationReason);
        Assert.Equal(updatedAt, direct.UpdatedAt);
    }

    [Fact]
    public void DirectResourceSubjectIsConsistentAcrossTheSharedAndConcreteContracts()
    {
        var direct = Assert.IsType<ResourceUserPermission>(Create("direct-resource"));
        PermissionBase shared = direct;
        Assert.NotEqual(Guid.Empty, direct.UserId);
        Assert.Equal(direct.UserId, shared.UserId);

        var replacement = Guid.NewGuid();
        shared.UserId = replacement;
        Assert.Equal(replacement, direct.UserId);
        Assert.Equal(replacement, ((IDacAttribute)direct).Owner);

        replacement = Guid.NewGuid();
        direct.UserId = replacement;
        Assert.Equal(replacement, shared.UserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClearingTheSharedResourceSubjectCannotLeaveAValidConcreteGrant(bool useEmptyId)
    {
        var direct = Assert.IsType<ResourceUserPermission>(Create("direct-resource"));
        PermissionBase shared = direct;

        shared.UserId = useEmptyId ? Guid.Empty : null;

        Assert.Equal(Guid.Empty, direct.UserId);
        Assert.False(shared.IsValid());
        Assert.False(direct.IsValid());
    }

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
    [InlineData("direct-resource")]
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
            Assert.Equal(expected, grant.IsActive);
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
