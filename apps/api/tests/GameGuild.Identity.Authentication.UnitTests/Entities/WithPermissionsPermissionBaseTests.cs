using FluentAssertions;
using GameGuild.Identity.Authorization;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Entities;

/// <summary>
///     Unit tests for the <see cref="WithPermissions"/> permission-entity family after the
///     unified <see cref="PermissionBase"/> refactor (#352) and the fail-closed
/// <see cref="PermissionType"/> validation hardening (#357): stored numeric values are
/// validated with Enum.IsDefined and undefined values are dropped instead of being cast.
/// </summary>
public class WithPermissionsPermissionBaseTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static ContentTypePermission NewGrant(string rawPermissions)
    {
        return new ContentTypePermission(UserId, TenantId, "Document") { Permissions = rawPermissions };
    }

    [Fact]
    public void ContentTypePermission_InheritsFromPermissionBase()
    {
        var grant = NewGrant(string.Empty);

        grant.Should().BeAssignableTo<PermissionBase>();
        grant.GrantedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        grant.IsActive.Should().BeTrue();
        grant.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void GetPermissionsAsEnum_WithDefinedValues_ReturnsThem()
    {
        var grant = NewGrant("1,26,31");

        grant.GetPermissionsAsEnum()
            .Should().BeEquivalentTo(new[] { PermissionType.Read, PermissionType.Delete, PermissionType.Edit });
    }

    [Fact]
    public void GetPermissionsAsEnum_WithUndefinedNumericValue_FailsClosedAndDropsIt()
    {
        // 9999 is not a defined PermissionType member — it must never be cast into the enum.
        var grant = NewGrant("1,9999");

        var parsed = grant.GetPermissionsAsEnum().ToList();

        parsed.Should().Contain(PermissionType.Read);
        parsed.Should().HaveCount(1);
        parsed.Should().OnlyContain(p => Enum.IsDefined(p));
    }

    [Fact]
    public void GetPermissionsAsEnum_WithZeroValue_FailsClosed()
    {
        // PermissionType has no member with value 0.
        var grant = NewGrant("0");

        grant.GetPermissionsAsEnum().Should().BeEmpty();
    }

    [Fact]
    public void GetPermissionsAsEnum_WithGarbageText_FailsClosed()
    {
        var grant = NewGrant("abc, ,;read;,,31");

        grant.GetPermissionsAsEnum()
            .Should().BeEquivalentTo(new[] { PermissionType.Edit });
    }

    [Fact]
    public void GetPermissionsAsEnum_WithEmptyOrNullPayload_ReturnsEmpty()
    {
        NewGrant(string.Empty).GetPermissionsAsEnum().Should().BeEmpty();
        NewGrant("   ").GetPermissionsAsEnum().Should().BeEmpty();
        NewGrant(",,,").GetPermissionsAsEnum().Should().BeEmpty();
    }

    [Fact]
    public void HasPermission_AfterUndefinedValueWasStored_StillFailsClosed()
    {
        var grant = NewGrant("1,9999");

        grant.HasPermission(PermissionType.Read).Should().BeTrue();

        // The undefined raw value must not grant anything.
        grant.GetPermissionsAsEnum().Should().HaveCount(1);
    }

    [Fact]
    public void AddPermission_AroundUndefinedValue_PreservesOnlyDefinedPermissions()
    {
        var grant = NewGrant("1,9999");

        grant.AddPermission(PermissionType.Edit);

        grant.Permissions.Should().Be("1,31");
    }

    [Fact]
    public void RemovePermission_ReSerializesWithoutUndefinedValues()
    {
        var grant = NewGrant("1,26,9999");

        grant.RemovePermission(PermissionType.Delete);

        grant.Permissions.Should().Be("1");
    }

    [Fact]
    public void GetGrantedPermissionTypes_BridgesTheStoredPayload()
    {
        var grant = NewGrant("1,26");

        grant.GetGrantedPermissionTypes()
            .Should().BeEquivalentTo(new[] { PermissionType.Read, PermissionType.Delete });
    }

    [Fact]
    public void Grants_RespectsBaseContract_FailClosed()
    {
        var effective = NewGrant("1");
        effective.Grants(PermissionType.Read).Should().BeTrue();

        var inactive = NewGrant("1");
        inactive.IsActive = false;
        inactive.Grants(PermissionType.Read).Should().BeFalse();

        var expired = NewGrant("1");
        expired.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        expired.Grants(PermissionType.Read).Should().BeFalse();
    }

    [Fact]
    public void BaseLifecycle_ExpireAndExtend_AppliesToDerivedGrant()
    {
        var grant = NewGrant("1");

        grant.Expire();
        grant.IsActive.Should().BeFalse();
        grant.ExpiresAt.Should().NotBeNull();
        grant.IsEffective().Should().BeFalse();

        grant.IsActive = true;
        grant.ExtendExpiration(DateTime.UtcNow.AddDays(1));
        grant.IsEffective().Should().BeTrue();
    }

    [Fact]
    public void UnifiedAuditViews_ExposeGrantorAndTenant()
    {
        var grantor = Guid.NewGuid();
        var grant = NewGrant("1");
        grant.GrantedBy = grantor;

        grant.CreatedBy.Should().Be(grantor);
        grant.PermissionTenantId.Should().Be(TenantId);
        grant.PermissionTenantId.Should().NotBe(Guid.NewGuid());
    }

    [Fact]
    public void GenericResourcePermission_InheritsFromPermissionBase_AndMapsPayload()
    {
        var grant = new GenericResourcePermission(UserId, TenantId, Guid.NewGuid(), "Project") { Permissions = "1,9999" };

        grant.Should().BeAssignableTo<PermissionBase>();
        grant.GetGrantedPermissionTypes().Should().BeEquivalentTo(new[] { PermissionType.Read });
        grant.GrantedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
