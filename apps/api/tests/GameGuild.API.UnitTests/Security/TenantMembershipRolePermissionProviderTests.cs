using FluentAssertions;
using TenantMembershipRolePermissionProvider = GameGuild.API.Security.TenantMembershipRolePermissionProvider;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using Moq;
using Xunit;

namespace GameGuild.API.UnitTests.Security;

/// <summary>
///     Tests for the composition-root provider that projects this product's built-in
///     tenant membership roles into the dynamic permission pipeline.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Security", "TenantAuthorization")]
public sealed class TenantMembershipRolePermissionProviderTests
{
    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("TenantAdmin")]
    public async Task GetPermissionsAsync_ShouldGrantTenantAdminPermission_ForAdministrativeMembership(string role)
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var repository = new Mock<ITenantMemberRepository>();
        repository
            .Setup(candidate => candidate.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMember
            {
                UserId = userId,
                TenantId = tenantId,
                Role = role,
                IsActive = true
            });

        var permissions = await new TenantMembershipRolePermissionProvider(repository.Object)
            .GetPermissionsAsync(userId, tenantId);

        permissions.Should().Contain(AdminPermission.Keys.TenantAdmin);
        permissions.Should().Contain(UsersPermission.Keys.Manage);

        // Issue #294: the administrative tier is granted to administrative membership roles...
        permissions.Should().Contain([
            ManageMembersPermission.Keys.Manage,
            ManageSettingsPermission.Keys.Read,
            ManageSettingsPermission.Keys.Manage,
            AuditPermission.Keys.Read
        ]);

        // ...while delegation-only capabilities stay absent.
        permissions.Should().NotContain([
            ManageRolesPermission.Keys.Manage,
            AuditPermission.Keys.Export
        ]);
    }

    [Fact]
    public async Task GetPermissionsAsync_ShouldNotGrantTenantAdminPermission_ForNonAdministrativeMembership()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var repository = new Mock<ITenantMemberRepository>();
        repository
            .Setup(candidate => candidate.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMember
            {
                UserId = userId,
                TenantId = tenantId,
                Role = TenantRole.Member,
                IsActive = true
            });

        var permissions = await new TenantMembershipRolePermissionProvider(repository.Object)
            .GetPermissionsAsync(userId, tenantId);

        permissions.Should().Contain([
            UsersPermission.Keys.ReadSelf,
            UsersPermission.Keys.EditSelf,
            UsersPermission.Keys.DeleteSelf
        ]);
        permissions.Should().NotContain([
            AdminPermission.Keys.TenantAdmin,
            UsersPermission.Keys.Manage,
            ManageMembersPermission.Keys.Manage,
            ManageRolesPermission.Keys.Manage,
            ManageSettingsPermission.Keys.Manage,
            AuditPermission.Keys.Read,
            AuditPermission.Keys.Export
        ]);
    }

    [Fact]
    public async Task GetPermissionsAsync_ShouldGrantNoPermissions_ForInactiveMembership()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var repository = new Mock<ITenantMemberRepository>();
        repository
            .Setup(candidate => candidate.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMember
            {
                UserId = userId,
                TenantId = tenantId,
                Role = TenantRole.Owner,
                IsActive = false
            });

        var permissions = await new TenantMembershipRolePermissionProvider(repository.Object)
            .GetPermissionsAsync(userId, tenantId);

        permissions.Should().BeEmpty();
    }
}
