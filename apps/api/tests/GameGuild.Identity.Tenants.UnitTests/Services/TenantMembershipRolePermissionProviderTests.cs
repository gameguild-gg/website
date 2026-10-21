using FluentAssertions;
using GameGuild.Identity.Authorization;
using Moq;
using Xunit;

namespace GameGuild.Identity.Tenants.UnitTests.Services;

[Trait("Category", "Unit")]
[Trait("Security", "Authorization")]
public sealed class TenantMembershipRolePermissionProviderTests
{
    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("TenantAdmin")]
    public async Task GetPermissionsAsync_Should_GrantTenantAdmin_ForActiveAdministrativeMembership(string role)
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var repository = new Mock<ITenantMemberRepository>();
        repository
            .Setup(candidate => candidate.GetByUserAndTenantAsync(
                userId,
                tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMember
            {
                UserId = userId,
                TenantId = tenantId,
                Role = role,
                IsActive = true
            });

        var provider = new TenantMembershipRolePermissionProvider(repository.Object);

        var permissions = await provider.GetPermissionsAsync(userId, tenantId);

        // Issue #294: administrative membership roles carry the administrative permission
        // tier. Role administration (roles:manage) and audit exports (audit:export) are
        // deliberately absent - they are delegation-only capabilities.
        // Issue #375: administrative roles also carry the query-operations tier
        // (list/search/query) so audit-query, security-event and permission-query
        // surfaces keep working for tenant administrators.
        permissions.Should().BeEquivalentTo(new[]
        {
            AdminPermission.Keys.TenantAdmin,
            ManageMembersPermission.Keys.Manage,
            ManageSettingsPermission.Keys.Read,
            ManageSettingsPermission.Keys.Manage,
            AuditPermission.Keys.Read,
            QueryPermission.Keys.List,
            QueryPermission.Keys.Search,
            QueryPermission.Keys.Query
        });
    }

    [Theory]
    [InlineData("PropertyManager")]
    [InlineData("PropertyOwner")]
    [InlineData("Renter")]
    [InlineData("Member")]
    public async Task GetPermissionsAsync_Should_NotGrantTenantAdmin_ForNonAdministrativeMembership(string role)
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var repository = new Mock<ITenantMemberRepository>();
        repository
            .Setup(candidate => candidate.GetByUserAndTenantAsync(
                userId,
                tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMember
            {
                UserId = userId,
                TenantId = tenantId,
                Role = role,
                IsActive = true
            });

        var provider = new TenantMembershipRolePermissionProvider(repository.Object);

        var permissions = await provider.GetPermissionsAsync(userId, tenantId);

        permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsAsync_Should_NotGrantTenantAdmin_ForInactiveAdministrativeMembership()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var repository = new Mock<ITenantMemberRepository>();
        repository
            .Setup(candidate => candidate.GetByUserAndTenantAsync(
                userId,
                tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMember
            {
                UserId = userId,
                TenantId = tenantId,
                Role = TenantRole.Owner,
                IsActive = false
            });

        var provider = new TenantMembershipRolePermissionProvider(repository.Object);

        var permissions = await provider.GetPermissionsAsync(userId, tenantId);

        permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPermissionsAsync_Should_NotGrantTenantAdmin_ForMembershipFromAnotherTenant()
    {
        var userId = Guid.NewGuid();
        var requestedTenantId = Guid.NewGuid();
        var repository = new Mock<ITenantMemberRepository>();
        repository
            .Setup(candidate => candidate.GetByUserAndTenantAsync(
                userId,
                requestedTenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMember
            {
                UserId = userId,
                TenantId = Guid.NewGuid(),
                Role = TenantRole.Owner,
                IsActive = true
            });

        var provider = new TenantMembershipRolePermissionProvider(repository.Object);

        var permissions = await provider.GetPermissionsAsync(userId, requestedTenantId);

        permissions.Should().BeEmpty();
    }
}
