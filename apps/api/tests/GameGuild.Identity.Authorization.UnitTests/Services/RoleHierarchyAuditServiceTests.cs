using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
/// Cascade visibility for role-hierarchy and governance audits (issues #188/#189):
/// descendant roles, affected users and inherited-permission deltas are computed at
/// mutation time and recorded on the shared PermissionAuditLog surface.
/// </summary>
public class RoleHierarchyAuditServiceTests
{
    private static DynamicRole Role(Guid id, string name, string[] permissions, Guid? parentId = null, Guid[]? extraParents = null) =>
        new()
        {
            Id = id,
            Name = name,
            Permissions = permissions,
            ParentRoleId = parentId,
            AdditionalParentRoleIds = extraParents ?? []
        };

    private static (Mock<IDynamicRoleRepository> Roles, Mock<IDynamicRoleAssignmentRepository> Assignments, Mock<IPermissionAuditService> Audit) Deps(
        IReadOnlyList<DynamicRole> roles,
        IReadOnlyList<DynamicRoleAssignment> assignments)
    {
        var roleRepo = new Mock<IDynamicRoleRepository>(MockBehavior.Strict);
        roleRepo.Setup(r => r.GetByTenantAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles.ToList());

        var assignmentRepo = new Mock<IDynamicRoleAssignmentRepository>(MockBehavior.Strict);
        assignmentRepo.Setup(r => r.GetByTenantAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignments.ToList());

        return (roleRepo, assignmentRepo, new Mock<IPermissionAuditService>());
    }

    private static RoleHierarchyAuditService Sut(
        Mock<IDynamicRoleRepository> roles,
        Mock<IDynamicRoleAssignmentRepository> assignments,
        Mock<IPermissionAuditService> audit) =>
        new(roles.Object, assignments.Object, audit.Object, NullLogger<RoleHierarchyAuditService>.Instance);

    [Fact]
    public async Task ComputeCascade_ReportsDescendantsUsersAndPermissionDelta()
    {
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();
        var grandChild = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var tenant = Guid.NewGuid();

        var roles = new[]
        {
            Role(parent, "Parent", ["content:read"]),
            Role(child, "Child", ["content:write"], parentId: parent),
            Role(grandChild, "GrandChild", [], parentId: child)
        };
        var assignments = new[]
        {
            new DynamicRoleAssignment { UserId = userA, RoleId = child, IsActive = true },
            new DynamicRoleAssignment { UserId = userB, RoleId = grandChild, IsActive = true }
        };
        var (roleRepo, assignmentRepo, audit) = Deps(roles, assignments);

        var cascade = await Sut(roleRepo, assignmentRepo, audit).ComputeCascadeAsync(
            parent,
            tenant,
            beforeParentIds: [],
            afterParentIds: []);

        cascade.AffectedDescendantRoleIds.Should().Equal(child, grandChild);
        cascade.AffectedUserCount.Should().Be(2); // users holding affected roles
        cascade.PermissionsGained.Should().BeEmpty();
        cascade.PermissionsLost.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeCascade_ComputesInheritedDeltaBetweenParentSets()
    {
        var baseRole = Guid.NewGuid();
        var extraParent = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var tenant = Guid.NewGuid();

        var roles = new[]
        {
            Role(subject, "Subject", []),
            Role(baseRole, "Base", ["content:read"]),
            Role(extraParent, "Extra", ["billing:read"])
        };
        var (roleRepo, assignmentRepo, audit) = Deps(roles, []);

        var cascade = await Sut(roleRepo, assignmentRepo, audit).ComputeCascadeAsync(
            subject,
            tenant,
            beforeParentIds: [baseRole],
            afterParentIds: [baseRole, extraParent]);

        cascade.InheritedPermissionsBefore.Should().Equal("content:read");
        cascade.InheritedPermissionsAfter.Should().Contain("content:read").And.Contain("billing:read");
        cascade.PermissionsGained.Should().Equal("billing:read");
        cascade.PermissionsLost.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteGovernanceAuditEntries_HierarchyEdit_WritesInheritanceChangeWithCascade()
    {
        var roleId = Guid.NewGuid();
        var newParent = Guid.NewGuid();
        var roles = new[]
        {
            Role(roleId, "Editor", ["content:read"]),
            Role(newParent, "Base", ["content:write"])
        };
        var (roleRepo, assignmentRepo, audit) = Deps(roles, []);

        var before = JsonSerializer.Serialize(new { ParentRoleId = (Guid?)null });
        var after = JsonSerializer.Serialize(new { ParentRoleId = newParent });

        await Sut(roleRepo, assignmentRepo, audit).WriteGovernanceAuditEntriesAsync(
        [
            new GovernancePermissionChange(
                "DynamicRole", "Modified", roleId.ToString(), null, null, Guid.NewGuid(),
                "corr-1", "UpdateDynamicRoleCommand", before, after)
        ]);

        audit.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.InheritanceChange,
            null,
            It.IsAny<Guid>(),
            null,
            It.Is<string>(type => type.StartsWith("DynamicRole:Editor", StringComparison.Ordinal)),
            roleId,
            "RoleHierarchy",
            before,
            It.Is<string>(payload => payload.Contains("AffectedDescendantRoleIds", StringComparison.Ordinal)
                && payload.Contains("PermissionsGained", StringComparison.Ordinal)),
            It.IsAny<string?>(),
            true,
            null,
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteGovernanceAuditEntries_NonHierarchyRoleEdit_WritesNothing()
    {
        var roleId = Guid.NewGuid();
        var (roleRepo, assignmentRepo, audit) = Deps([Role(roleId, "Editor", ["content:read"])], []);

        await Sut(roleRepo, assignmentRepo, audit).WriteGovernanceAuditEntriesAsync(
        [
            new GovernancePermissionChange(
                "DynamicRole", "Modified", roleId.ToString(), null, null, Guid.NewGuid(),
                null, null,
                JsonSerializer.Serialize(new { DisplayName = "Old" }),
                JsonSerializer.Serialize(new { DisplayName = "New" }))
        ]);

        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WriteGovernanceAuditEntries_AssignmentAdded_ExpandsInheritedPermissions()
    {
        var roleId = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var roles = new[]
        {
            Role(roleId, "Editor", ["content:edit"], parentId: parent),
            Role(parent, "Base", ["content:read"])
        };
        var (roleRepo, assignmentRepo, audit) = Deps(roles, []);

        var state = JsonSerializer.Serialize(new { RoleId = roleId, UserId = subject });

        await Sut(roleRepo, assignmentRepo, audit).WriteGovernanceAuditEntriesAsync(
        [
            new GovernancePermissionChange(
                "DynamicRoleAssignment", "Added", roleId.ToString(), subject, null, Guid.NewGuid(),
                null, "AssignDynamicRoleCommand", null, state)
        ]);

        audit.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.InheritanceChange,
            subject,
            It.IsAny<Guid>(),
            null,
            $"RoleAssignment:{roleId}",
            roleId,
            "RoleAssignment",
            null,
            It.Is<string>(payload => payload.Contains("content:edit", StringComparison.Ordinal)
                && payload.Contains("content:read", StringComparison.Ordinal)),
            It.IsAny<string?>(),
            true,
            null,
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("ConditionalPolicy")]
    [InlineData("PolicyDefinitionEntity")]
    public async Task WriteGovernanceAuditEntries_PolicyMutation_WritesPolicyChangeEntry(string entityType)
    {
        var (roleRepo, assignmentRepo, audit) = Deps([], []);
        var policyId = Guid.NewGuid();

        await Sut(roleRepo, assignmentRepo, audit).WriteGovernanceAuditEntriesAsync(
        [
            new GovernancePermissionChange(
                entityType, "Modified", policyId.ToString(), null, Guid.NewGuid(), Guid.NewGuid(),
                null, null, "{\"IsEnabled\":true}", "{\"IsEnabled\":false}")
        ]);

        audit.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.PolicyChange,
            null,
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            entityType,
            policyId,
            entityType,
            It.IsAny<string?>(),
            It.Is<string>(payload => payload.Contains("IsEnabled", StringComparison.Ordinal)),
            It.IsAny<string?>(),
            true,
            null,
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteGovernanceAuditEntries_AuditFailure_IsIsolatedPerChange()
    {
        var roleA = Guid.NewGuid();
        var roleB = Guid.NewGuid();
        var (roleRepo, assignmentRepo, audit) = Deps(
        [
            Role(roleA, "A", ["p:a"]),
            Role(roleB, "B", ["p:b"])
        ], []);

        audit.SetupSequence(a => a.LogPermissionChangeAsync(
                It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit sink down"))
            .ReturnsAsync(new PermissionAuditLog());

        var state = JsonSerializer.Serialize(new { ParentRoleId = (Guid?)null });

        var act = async () => await Sut(roleRepo, assignmentRepo, audit).WriteGovernanceAuditEntriesAsync(
        [
            new GovernancePermissionChange("ConditionalPolicy", "Modified", roleA.ToString(), null, null, Guid.NewGuid(), null, null, state, state),
            new GovernancePermissionChange("ConditionalPolicy", "Modified", roleB.ToString(), null, null, Guid.NewGuid(), null, null, state, state)
        ]);

        await act.Should().NotThrowAsync();
        audit.Verify(a => a.LogPermissionChangeAsync(
            It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
