using FluentAssertions;
using GameGuild.Identity.Authorization.Models;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the read-only impact analysis simulations (issue #334): role-deletion
///     impact and permission-key-removal impact, including retention paths, severity
///     classification, and warnings.
/// </summary>
public class PermissionImpactAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeRoleDeletion_UnknownRole_ReturnsNotFound()
    {
        var setup = new PermissionGraphTestSetup();
        setup.SetupData([]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzeRoleDeletionAsync(setup.TenantId, Guid.NewGuid());

        impact.RoleFound.Should().BeFalse();
        impact.Severity.Should().Be(ImpactSeverity.Low);
        impact.UsersLosingPermissions.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeRoleDeletion_UserLosesExclusiveKey_ReportsLossWithMediumSeverity()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [PermissionGraphTestSetup.Role(roleId, "Editor", ["content:write"])],
            [PermissionGraphTestSetup.Assignment(userId, roleId, setup.TenantId)]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzeRoleDeletionAsync(setup.TenantId, roleId);

        impact.RoleFound.Should().BeTrue();
        impact.DirectAssignmentCount.Should().Be(1);
        impact.AssignedUserIds.Should().BeEquivalentTo([userId]);
        var impacted = impact.UsersLosingPermissions.Single(u => u.UserId == userId);
        impacted.LostPermissionKeys.Should().BeEquivalentTo(["content:write"]);
        impact.Severity.Should().Be(ImpactSeverity.Medium);
    }

    [Fact]
    public async Task AnalyzeRoleDeletion_SecondRoleAlsoGrantsKey_UserNotImpacted()
    {
        var editor = Guid.NewGuid();
        var backup = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [
                PermissionGraphTestSetup.Role(editor, "Editor", ["content:write"]),
                PermissionGraphTestSetup.Role(backup, "Backup", ["content:write"])
            ],
            [
                PermissionGraphTestSetup.Assignment(userId, editor, setup.TenantId),
                PermissionGraphTestSetup.Assignment(userId, backup, setup.TenantId)
            ]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzeRoleDeletionAsync(setup.TenantId, editor);

        impact.UsersLosingPermissions.Should().BeEmpty();
        impact.Severity.Should().Be(ImpactSeverity.Low);
    }

    [Fact]
    public async Task AnalyzeRoleDeletion_DirectGrantRetainsKey_UserNotImpacted()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [PermissionGraphTestSetup.Role(roleId, "Editor", ["content:write"])],
            [PermissionGraphTestSetup.Assignment(userId, roleId, setup.TenantId)],
            [PermissionGraphTestSetup.DirectGrant(userId, setup.TenantId, ["content:write"])]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzeRoleDeletionAsync(setup.TenantId, roleId);

        impact.UsersLosingPermissions.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeRoleDeletion_DescendantAssignment_TruncatesChainAboveDeletedRole()
    {
        var baseRole = Guid.NewGuid();
        var derived = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [
                PermissionGraphTestSetup.Role(derived, "Derived", ["assets:read"], parentRoleId: baseRole),
                PermissionGraphTestSetup.Role(baseRole, "Base", ["content:write"])
            ],
            [PermissionGraphTestSetup.Assignment(userId, derived, setup.TenantId)]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzeRoleDeletionAsync(setup.TenantId, baseRole);

        // The user keeps "assets:read" (stored on the surviving descendant) but loses
        // "content:write" (stored on the deleted ancestor reached via inheritance).
        var impacted = impact.UsersLosingPermissions.Single(u => u.UserId == userId);
        impacted.LostPermissionKeys.Should().BeEquivalentTo(["content:write"]);
        impact.ChildRoleIds.Should().BeEquivalentTo([derived]);
        // 1 losing user + 1 child role => High
        impact.Severity.Should().Be(ImpactSeverity.High);
    }

    [Fact]
    public async Task AnalyzeRoleDeletion_SystemRole_IsCriticalWithRefusalWarning()
    {
        var roleId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData([PermissionGraphTestSetup.Role(roleId, "Core", ["content:read"], isSystem: true)]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzeRoleDeletionAsync(setup.TenantId, roleId);

        impact.IsSystemRole.Should().BeTrue();
        impact.Severity.Should().Be(ImpactSeverity.Critical);
        impact.Warnings.Should().Contain(w => w.Contains("system role"));
    }

    [Fact]
    public async Task AnalyzeRoleDeletion_WildcardGrant_ProducesWildcardWarning()
    {
        var roleId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData([PermissionGraphTestSetup.Role(roleId, "Broad", ["content:*"])]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzeRoleDeletionAsync(setup.TenantId, roleId);

        impact.Warnings.Should().Contain(w => w.Contains("wildcard"));
    }

    [Fact]
    public async Task AnalyzePermissionRemoval_UnknownRole_ReturnsNotFound()
    {
        var setup = new PermissionGraphTestSetup();
        setup.SetupData([]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzePermissionRemovalAsync(setup.TenantId, Guid.NewGuid(), "content:read");

        impact.RoleFound.Should().BeFalse();
        impact.Severity.Should().Be(ImpactSeverity.Low);
    }

    [Fact]
    public async Task AnalyzePermissionRemoval_VerbatimKey_UserLosesIt()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [PermissionGraphTestSetup.Role(roleId, "Editor", ["content:read", "content:write"])],
            [PermissionGraphTestSetup.Assignment(userId, roleId, setup.TenantId)]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzePermissionRemovalAsync(setup.TenantId, roleId, "content:read");

        impact.IsGranted.Should().BeTrue();
        impact.RemovableDirectly.Should().BeTrue();
        impact.GrantedViaInheritance.Should().BeFalse();
        impact.UsersLosingPermission.Single(u => u.UserId == userId)
            .LostPermissionKeys.Should().BeEquivalentTo(["content:read"]);
        impact.UsersRetainingPermission.Should().BeEmpty();
        impact.Severity.Should().Be(ImpactSeverity.Medium);
    }

    [Fact]
    public async Task AnalyzePermissionRemoval_OtherRoleRetains_ReportsRetentionPath()
    {
        var editor = Guid.NewGuid();
        var backup = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [
                PermissionGraphTestSetup.Role(editor, "Editor", ["content:read"]),
                PermissionGraphTestSetup.Role(backup, "Backup", ["content:read"])
            ],
            [
                PermissionGraphTestSetup.Assignment(userId, editor, setup.TenantId),
                PermissionGraphTestSetup.Assignment(userId, backup, setup.TenantId)
            ]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzePermissionRemovalAsync(setup.TenantId, editor, "content:read");

        impact.UsersLosingPermission.Should().BeEmpty();
        var retaining = impact.UsersRetainingPermission.Single(u => u.UserId == userId);
        retaining.RetainedViaRoleIds.Should().BeEquivalentTo([backup]);
        retaining.RetainedViaDirectGrant.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzePermissionRemoval_DirectGrantRetains_ReportsDirectPath()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [PermissionGraphTestSetup.Role(roleId, "Editor", ["content:read"])],
            [PermissionGraphTestSetup.Assignment(userId, roleId, setup.TenantId)],
            [PermissionGraphTestSetup.DirectGrant(userId, setup.TenantId, ["content:read"])]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzePermissionRemovalAsync(setup.TenantId, roleId, "content:read");

        impact.UsersLosingPermission.Should().BeEmpty();
        impact.UsersRetainingPermission.Single(u => u.UserId == userId).RetainedViaDirectGrant.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzePermissionRemoval_KeyOnlyViaInheritance_FlagsAncestorWarning()
    {
        var baseRole = Guid.NewGuid();
        var derived = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [
                PermissionGraphTestSetup.Role(derived, "Derived", [], parentRoleId: baseRole),
                PermissionGraphTestSetup.Role(baseRole, "Base", ["content:read"])
            ],
            [PermissionGraphTestSetup.Assignment(userId, derived, setup.TenantId)]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzePermissionRemovalAsync(setup.TenantId, derived, "content:read");

        impact.IsGranted.Should().BeTrue();
        impact.RemovableDirectly.Should().BeFalse();
        impact.GrantedViaInheritance.Should().BeTrue();
        impact.Warnings.Should().Contain(w => w.Contains("inheritance"));
        impact.DownstreamRoleIds.Should().BeEmpty();
        // Removing the key from the derived role is a no-op (the key lives on the ancestor),
        // so the user keeps the key through the derived -> ancestor chain.
        var retaining = impact.UsersRetainingPermission.Single(u => u.UserId == userId);
        retaining.RetainedViaRoleIds.Should().BeEquivalentTo([derived]);
        impact.UsersLosingPermission.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzePermissionRemoval_RoleDoesNotGrantKey_IsNoOp()
    {
        var roleId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData([PermissionGraphTestSetup.Role(roleId, "Editor", ["assets:read"])]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzePermissionRemovalAsync(setup.TenantId, roleId, "content:read");

        impact.IsGranted.Should().BeFalse();
        impact.Severity.Should().Be(ImpactSeverity.Low);
        impact.Warnings.Should().Contain(w => w.Contains("no-op"));
    }

    [Fact]
    public async Task AnalyzePermissionRemoval_WildcardCoversKey_WarnsAboutSiblingKeys()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [PermissionGraphTestSetup.Role(roleId, "Broad", ["content:*"])],
            [PermissionGraphTestSetup.Assignment(userId, roleId, setup.TenantId)]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzePermissionRemovalAsync(setup.TenantId, roleId, "content:read");

        impact.IsGranted.Should().BeTrue();
        impact.RemovableDirectly.Should().BeTrue();
        impact.Warnings.Should().Contain(w => w.Contains("wildcard"));
        // Removing the stored wildcard drops every concrete key it covered for this user.
        impact.UsersLosingPermission.Single(u => u.UserId == userId)
            .LostPermissionKeys.Should().BeEquivalentTo(["content:read"]);
        impact.UsersRetainingPermission.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzePermissionRemoval_TenantDefaultDeny_UserDoesNotHoldKey()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [PermissionGraphTestSetup.Role(roleId, "Editor", ["content:read"])],
            [PermissionGraphTestSetup.Assignment(userId, roleId, setup.TenantId)],
            [PermissionGraphTestSetup.TenantDefault(setup.TenantId, [], denies: ["content:read"])]);
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzePermissionRemovalAsync(setup.TenantId, roleId, "content:read");

        // The role grants the key, but the tenant default denies it exactly, so no user
        // currently holds it and nobody is impacted by removing it from the role.
        impact.IsGranted.Should().BeTrue();
        impact.UsersLosingPermission.Should().BeEmpty();
        impact.UsersRetainingPermission.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzePermissionRemoval_GlobalDefaultRetains_UserNotImpacted()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [PermissionGraphTestSetup.Role(roleId, "Editor", ["content:read"])],
            [PermissionGraphTestSetup.Assignment(userId, roleId, setup.TenantId)],
            globalDefault: new TenantPermission
            {
                UserId = null,
                TenantId = null,
                Permissions = ["content:read"],
                IsActive = true
            });
        var sut = setup.CreateImpactService();

        var impact = await sut.AnalyzePermissionRemovalAsync(setup.TenantId, roleId, "content:read");

        // The data-driven global-default row keeps providing the key after the removal.
        impact.UsersLosingPermission.Should().BeEmpty();
        var retaining = impact.UsersRetainingPermission.Single(u => u.UserId == userId);
        retaining.RetainedViaRoleIds.Should().BeEmpty();
        retaining.RetainedViaDirectGrant.Should().BeFalse();
        retaining.Reason.Should().Contain("default");
    }
}
