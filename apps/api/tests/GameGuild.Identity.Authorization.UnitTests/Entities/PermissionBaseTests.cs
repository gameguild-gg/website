using FluentAssertions;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authentication;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Entities;

/// <summary>
///     Unit tests for the unified <see cref="PermissionBase"/> permission-entity contract (#352):
///     common properties, shared validation, expiration, audit views, tenant isolation and the
///     fail-closed permission-type mapping layer shared by every permission entity.
/// </summary>
public class PermissionBaseTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid Grantor = Guid.NewGuid();

    [Fact]
    public void AllPermissionEntities_InheritFromPermissionBase()
    {
        typeof(TenantPermission).Should().BeDerivedFrom<PermissionBase>();
        typeof(ResourceUserPermission).Should().BeDerivedFrom<PermissionBase>();
        typeof(WithPermissions).Should().BeDerivedFrom<PermissionBase>();
        typeof(ContentTypePermission).Should().BeDerivedFrom<PermissionBase>();
        typeof(GenericResourcePermission).Should().BeDerivedFrom<PermissionBase>();
    }

    [Fact]
    public void PermissionBase_DefinesTheCommonGrantProperties()
    {
        var baseType = typeof(PermissionBase);

        baseType.GetProperty(nameof(PermissionBase.UserId)).Should().NotBeNull();
        baseType.GetProperty(nameof(PermissionBase.ExpiresAt)).Should().NotBeNull();
        baseType.GetProperty(nameof(PermissionBase.IsActive)).Should().NotBeNull();
        baseType.GetProperty(nameof(PermissionBase.GrantedAt)).Should().NotBeNull();
        baseType.GetProperty(nameof(PermissionBase.CreatedBy)).Should().NotBeNull();
        baseType.GetProperty(nameof(PermissionBase.PermissionTenantId)).Should().NotBeNull();

        // Inherited platform fields every permission table carries.
        baseType.GetProperty(nameof(PermissionBase.Id)).Should().NotBeNull();
        baseType.GetProperty(nameof(PermissionBase.CreatedAt)).Should().NotBeNull();
        baseType.GetProperty(nameof(PermissionBase.UpdatedAt)).Should().NotBeNull();
        baseType.GetProperty(nameof(PermissionBase.DeletedAt)).Should().NotBeNull();
        baseType.GetProperty(nameof(PermissionBase.Version)).Should().NotBeNull();
        baseType.GetProperty(nameof(PermissionBase.TenantId)).Should().NotBeNull();
    }

    [Fact]
    public void TenantPermission_CommonDefaults_ComputeFromBase()
    {
        var permission = new TenantPermission();

        permission.Id.Should().NotBe(Guid.Empty);
        permission.UserId.Should().BeNull();
        permission.ExpiresAt.Should().BeNull();
        permission.IsActive.Should().BeTrue();
        permission.GrantedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        permission.IsEffective().Should().BeTrue();
    }

    [Fact]
    public void ResourceUserPermission_CommonDefaults_ComputeFromBase()
    {
        var permission = new ResourceUserPermission
        {
            UserId = UserA,
            TenantId = new TenantId(TenantA),
            ResourceType = "Project",
            ResourceId = Guid.NewGuid().ToString(),
            Permissions = ["read"],
            GrantedByUserId = Grantor
        };

        permission.Id.Should().NotBe(Guid.Empty);
        permission.ExpiresAt.Should().BeNull();
        permission.IsActive.Should().BeTrue();
        permission.GrantedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ContentTypePermission_CommonDefaults_ComputeFromBase()
    {
        var permission = new ContentTypePermission(UserA, TenantA, "Document");

        permission.Id.Should().NotBe(Guid.Empty);
        permission.ExpiresAt.Should().BeNull();
        permission.IsActive.Should().BeTrue();
        permission.GrantedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        permission.IsEffective().Should().BeTrue();
    }

    [Fact]
    public void IsExpired_UsesInclusiveBoundary_FailClosed()
    {
        var permission = new TenantPermission { ExpiresAt = SystemClock.UtcNow };

        permission.IsExpired().Should().BeTrue();
        permission.IsEffective().Should().BeFalse();
    }

    [Fact]
    public void Expire_Deactivates_StampsExpiration_AndTouchesAuditTimestamp()
    {
        var permission = new TenantPermission { IsActive = true };
        var updatedAtBefore = permission.UpdatedAt;

        permission.Expire();

        permission.IsActive.Should().BeFalse();
        permission.ExpiresAt.Should().NotBeNull();
        permission.IsExpired().Should().BeTrue();
        permission.UpdatedAt.Should().BeOnOrAfter(updatedAtBefore);
    }

    [Fact]
    public void ExtendExpiration_UpdatesExpirationAndTouches()
    {
        var permission = new ContentTypePermission(UserA, TenantA, "Document");
        var newExpiry = DateTime.UtcNow.AddDays(30);

        permission.ExtendExpiration(newExpiry);

        permission.ExpiresAt.Should().Be(newExpiry);
        permission.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void IsValid_RejectsExpirationBeforeGrantInstant()
    {
        var permission = new TenantPermission
        {
            GrantedAt = SystemClock.UtcNow,
            ExpiresAt = SystemClock.UtcNow.AddDays(-1)
        };

        permission.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_RejectsEmptySubject()
    {
        var permission = new TenantPermission { UserId = Guid.Empty };

        permission.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_RejectsSoftDeletedButActiveGrants()
    {
        var permission = new TenantPermission { Version = 1 };

        permission.SoftDelete();

        permission.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_AcceptsWellFormedGrants()
    {
        var permission = new TenantPermission
        {
            UserId = UserA,
            GrantedBy = Grantor,
            ExpiresAt = SystemClock.UtcNow.AddDays(1)
        };

        permission.IsValid().Should().BeTrue();
    }

    [Fact]
    public void CreatedBy_ExposesUnifiedGrantorView_AcrossEntities()
    {
        new TenantPermission { GrantedBy = Grantor }.CreatedBy.Should().Be(Grantor);

        new ContentTypePermission(UserA, TenantA, "Document") { GrantedBy = Grantor }.CreatedBy.Should().Be(Grantor);

        new ResourceUserPermission
        {
            UserId = UserA,
            TenantId = new TenantId(TenantA),
            ResourceType = "Project",
            ResourceId = Guid.NewGuid().ToString(),
            Permissions = ["read"],
            GrantedByUserId = Grantor
        }.CreatedBy.Should().Be(Grantor);
    }

    [Fact]
    public void PermissionTenantId_ExposesUnifiedTenantScope_AcrossEntities()
    {
        new TenantPermission { TenantId = TenantA }.PermissionTenantId.Should().Be(TenantA);

        new ContentTypePermission(UserA, TenantA, "Document").PermissionTenantId.Should().Be(TenantA);

        new ResourceUserPermission
        {
            UserId = UserA,
            TenantId = new TenantId(TenantA),
            ResourceType = "Project",
            ResourceId = Guid.NewGuid().ToString(),
            Permissions = ["read"],
            GrantedByUserId = Grantor
        }.PermissionTenantId.Should().Be(TenantA);
    }

    [Fact]
    public void IsInTenantScope_MatchesOnlyTheOwningScope()
    {
        var permission = new TenantPermission { TenantId = TenantA };

        permission.IsInTenantScope(TenantA).Should().BeTrue();
        permission.IsInTenantScope(TenantB).Should().BeFalse();
        permission.IsInTenantScope(null).Should().BeFalse();
    }

    [Fact]
    public void EnsureTenantScope_ThrowsOnScopeMismatch_FailClosed()
    {
        var permission = new TenantPermission { TenantId = TenantA };

        var act = () => permission.EnsureTenantScope(TenantB);

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void EnsureTenantScope_AcceptsMatchingScope()
    {
        var permission = new TenantPermission { TenantId = TenantA };

        var act = () => permission.EnsureTenantScope(TenantA);

        act.Should().NotThrow();
    }

    [Fact]
    public void GetGrantedPermissionTypes_TenantPermission_MapsNamesAndNumbers_FailClosed()
    {
        var permission = new TenantPermission
        {
            Permissions = ["Read", "delete", "26", "9999", "not-a-permission", ""],
            DenyPermissions = []
        };

        var granted = permission.GetGrantedPermissionTypes();

        granted.Should().BeEquivalentTo(new[] { PermissionType.Read, PermissionType.Delete, PermissionType.Delete });
    }

    [Fact]
    public void GetGrantedPermissionTypes_ResourceUserPermission_MapsStrings_FailClosed()
    {
        var permission = new ResourceUserPermission
        {
            UserId = UserA,
            TenantId = new TenantId(TenantA),
            ResourceType = "Project",
            ResourceId = Guid.NewGuid().ToString(),
            Permissions = ["read", "Delete", "bogus"],
            GrantedByUserId = Grantor
        };

        permission.GetGrantedPermissionTypes()
            .Should().BeEquivalentTo(new[] { PermissionType.Read, PermissionType.Delete });
    }

    [Fact]
    public void Grants_RequiresEffectiveGrant_FailClosed()
    {
        var active = new TenantPermission { Permissions = ["Read"] };
        active.Grants(PermissionType.Read).Should().BeTrue();

        var inactive = new TenantPermission { Permissions = ["Read"], IsActive = false };
        inactive.Grants(PermissionType.Read).Should().BeFalse();

        var expired = new ContentTypePermission(UserA, TenantA, "Document");
        expired.SetPermissions(new[] { PermissionType.Read });
        expired.ExpiresAt = SystemClock.UtcNow.AddSeconds(-1);
        expired.Grants(PermissionType.Read).Should().BeFalse();

        var unmapped = new TenantPermission { Permissions = ["not-a-permission"] };
        unmapped.Grants(PermissionType.Read).Should().BeFalse();
    }

    [Fact]
    public void ResourceUserPermission_ComputedActivity_HidesStoredBaseState()
    {
        var permission = new ResourceUserPermission
        {
            UserId = UserA,
            TenantId = new TenantId(TenantA),
            ResourceType = "Project",
            ResourceId = Guid.NewGuid().ToString(),
            Permissions = ["read"],
            GrantedByUserId = Grantor
        };

        permission.IsActive.Should().BeTrue();

        permission.Revoke(Grantor, "rotated");

        permission.IsActive.Should().BeFalse();
        permission.IsEffective().Should().BeFalse();
    }
}
