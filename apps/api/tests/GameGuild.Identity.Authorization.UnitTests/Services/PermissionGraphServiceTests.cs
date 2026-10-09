using FluentAssertions;
using GameGuild.Identity.Authorization.Models;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the tenant permission graph builder (issue #334): node/edge
///    construction, user inclusion, tenant defaults, and data-quality findings
///    (inheritance cycles, unregistered keys, orphaned roles).
/// </summary>
public class PermissionGraphServiceTests
{
    [Fact]
    public async Task BuildGraph_EmptyTenant_ReturnsEmptyGraph()
    {
        var setup = new PermissionGraphTestSetup();
        setup.SetupData([]);
        var sut = setup.CreateGraphService();

        var graph = await sut.BuildGraphAsync(setup.TenantId, includeUsers: true);

        graph.Nodes.Should().BeEmpty();
        graph.Edges.Should().BeEmpty();
        graph.TenantId.Should().Be(setup.TenantId);
        graph.IncludesUsers.Should().BeTrue();
        graph.Summary.RoleCount.Should().Be(0);
        graph.Summary.EdgeCount.Should().Be(0);
        graph.Summary.InheritanceCycles.Should().BeEmpty();
        graph.Summary.UnregisteredPermissionKeys.Should().BeEmpty();
        graph.Summary.OrphanedRoleIds.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildGraph_RolesWithInheritance_EmitsInheritsAndGrantEdges()
    {
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
        [
            PermissionGraphTestSetup.Role(child, "Senior", ["content:write"], parentRoleId: parent),
            PermissionGraphTestSetup.Role(parent, "Base", ["content:read"])
        ]);
        var sut = setup.CreateGraphService();

        var graph = await sut.BuildGraphAsync(setup.TenantId, includeUsers: false);

        graph.Summary.RoleCount.Should().Be(2);
        graph.Summary.PermissionCount.Should().Be(2);
        graph.Summary.UserCount.Should().Be(0);
        graph.Edges.Should().Contain(e =>
            e.SourceId == $"role:{child}" && e.TargetId == $"role:{parent}" && e.Type == PermissionGraphEdgeType.RoleInheritsFrom);
        graph.Edges.Should().Contain(e =>
            e.SourceId == $"role:{parent}" && e.TargetId == "perm:content:read" && e.Type == PermissionGraphEdgeType.RoleGrantsPermission);
        graph.Nodes.Should().Contain(n => n.Id == "perm:content:read" && n.IsRegistered && n.Resource == "content");
    }

    [Fact]
    public async Task BuildGraph_DenyPermissions_EmitDenyEdges()
    {
        var roleId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData([PermissionGraphTestSetup.Role(roleId, "Restricted", ["content:read"], denyPermissions: ["content:write"])]);
        var sut = setup.CreateGraphService();

        var graph = await sut.BuildGraphAsync(setup.TenantId, includeUsers: false);

        graph.Edges.Should().Contain(e =>
            e.SourceId == $"role:{roleId}" && e.TargetId == "perm:content:write" && e.Type == PermissionGraphEdgeType.RoleDeniesPermission);
    }

    [Fact]
    public async Task BuildGraph_IncludeUsersFalse_OmitsUsersAndSkipsAssignmentLoad()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [PermissionGraphTestSetup.Role(roleId, "Member", ["content:read"])],
            [PermissionGraphTestSetup.Assignment(userId, roleId, setup.TenantId)]);
        var sut = setup.CreateGraphService();

        var graph = await sut.BuildGraphAsync(setup.TenantId, includeUsers: false);

        graph.Summary.UserCount.Should().Be(0);
        graph.Nodes.Should().NotContain(n => n.Type == PermissionGraphNodeType.User);
        graph.Edges.Should().NotContain(e => e.Type == PermissionGraphEdgeType.UserAssignedRole);
        setup.Assignments.Verify(r => r.GetByTenantAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BuildGraph_UsersWithAssignmentsAndDirectGrants_EmitsUserEdges()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [PermissionGraphTestSetup.Role(roleId, "Member", ["content:read"])],
            [PermissionGraphTestSetup.Assignment(userId, roleId, setup.TenantId)],
            [PermissionGraphTestSetup.DirectGrant(userId, setup.TenantId, ["assets:read"], denies: ["assets:delete"])]);
        var sut = setup.CreateGraphService();

        var graph = await sut.BuildGraphAsync(setup.TenantId, includeUsers: true);

        graph.Summary.UserCount.Should().Be(1);
        graph.Edges.Should().Contain(e =>
            e.SourceId == $"user:{userId}" && e.TargetId == $"role:{roleId}" && e.Type == PermissionGraphEdgeType.UserAssignedRole);
        graph.Edges.Should().Contain(e =>
            e.SourceId == $"user:{userId}" && e.TargetId == "perm:assets:read" && e.Type == PermissionGraphEdgeType.UserDirectGrant);
        graph.Edges.Should().Contain(e =>
            e.SourceId == $"user:{userId}" && e.TargetId == "perm:assets:delete" && e.Type == PermissionGraphEdgeType.UserDirectDeny);
        graph.Summary.DirectGrantCount.Should().Be(1);
    }

    [Fact]
    public async Task BuildGraph_TenantDefaultRow_AddsPseudoRoleNode()
    {
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
            [],
            tenantRows: [PermissionGraphTestSetup.TenantDefault(setup.TenantId, ["content:read"])]);
        var sut = setup.CreateGraphService();

        var graph = await sut.BuildGraphAsync(setup.TenantId, includeUsers: false);

        var pseudoId = $"role:tenant-default:{setup.TenantId}";
        graph.Nodes.Should().Contain(n => n.Id == pseudoId && n.Type == PermissionGraphNodeType.Role);
        graph.Edges.Should().Contain(e => e.SourceId == pseudoId && e.TargetId == "perm:content:read");
    }

    [Fact]
    public async Task BuildGraph_GlobalDefaultRow_IsNotPartOfTenantGraph()
    {
        var setup = new PermissionGraphTestSetup();
        setup.SetupData([], globalDefault: new TenantPermission
        {
            UserId = null,
            TenantId = null,
            Permissions = ["content:read"],
            IsActive = true
        });
        var sut = setup.CreateGraphService();

        var graph = await sut.BuildGraphAsync(setup.TenantId, includeUsers: false);

        graph.Nodes.Should().NotContain(n => n.Id == "perm:content:read");
    }

    [Fact]
    public async Task BuildGraph_InheritanceCycle_ReportedInSummary()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData(
        [
            PermissionGraphTestSetup.Role(a, "A", [], parentRoleId: b),
            PermissionGraphTestSetup.Role(b, "B", [], parentRoleId: a)
        ]);
        var sut = setup.CreateGraphService();

        var graph = await sut.BuildGraphAsync(setup.TenantId, includeUsers: false);

        graph.Summary.InheritanceCycles.Should().HaveCount(1);
        var cycle = graph.Summary.InheritanceCycles.Single();
        cycle.RoleIds.Should().BeEquivalentTo(new[] { a, b });
    }

    [Fact]
    public async Task BuildGraph_UnregisteredKeys_FlagsNodeAndSummary()
    {
        var roleId = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData([PermissionGraphTestSetup.Role(roleId, "Typo", ["content:read", "nonexistent:whatever"])]);
        var sut = setup.CreateGraphService();

        var graph = await sut.BuildGraphAsync(setup.TenantId, includeUsers: false);

        graph.Summary.UnregisteredPermissionKeys.Should().BeEquivalentTo(["nonexistent:whatever"]);
        graph.Nodes.Single(n => n.Id == "perm:nonexistent:whatever").IsRegistered.Should().BeFalse();
        graph.Nodes.Single(n => n.Id == "perm:content:read").IsRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task BuildGraph_OrphanedParent_ReportedWithoutEdge()
    {
        var roleId = Guid.NewGuid();
        var missingParent = Guid.NewGuid();
        var setup = new PermissionGraphTestSetup();
        setup.SetupData([PermissionGraphTestSetup.Role(roleId, "Orphan", ["content:read"], parentRoleId: missingParent)]);
        var sut = setup.CreateGraphService();

        var graph = await sut.BuildGraphAsync(setup.TenantId, includeUsers: false);

        graph.Summary.OrphanedRoleIds.Should().BeEquivalentTo([roleId]);
        graph.Edges.Should().NotContain(e => e.Type == PermissionGraphEdgeType.RoleInheritsFrom);
    }
}
