using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

using GameGuild;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Issue #358 gap 1 (multi-parent role inheritance with cycle detection) and
///     gap 2 (selective inheritance blocking + configurable rules) tests for
///     <see cref="RoleInheritanceEngine"/> and <see cref="RbacPermissionResolver"/>.
/// </summary>
public class RoleInheritanceEngineTests
{
    private static DynamicRole Role(
        Guid id,
        string name,
        string[] permissions,
        Guid? parent = null,
        Guid[]? additionalParents = null,
        string[]? blocked = null,
        string[]? denies = null)
        => new()
        {
            Id = id,
            Name = name,
            DisplayName = name,
            TenantId = Guid.NewGuid(),
            Permissions = permissions,
            DenyPermissions = denies ?? Array.Empty<string>(),
            ParentRoleId = parent,
            AdditionalParentRoleIds = additionalParents ?? Array.Empty<Guid>(),
            BlockedInheritedPermissions = blocked ?? Array.Empty<string>()
        };

    private static RoleInheritanceEngine CreateEngine(
        Dictionary<Guid, DynamicRole> roles,
        PermissionEngineOptions? options = null)
    {
        var repository = new Mock<IDynamicRoleRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => roles.GetValueOrDefault(id));
        repository
            .Setup(repo => repo.GetManyByIdAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                ids.Where(id => roles.ContainsKey(id)).Select(id => roles[id]).ToList());
        repository
            .Setup(repo => repo.GetByTenantAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? tenantId, bool _, CancellationToken _) =>
                roles.Values.Where(role => role.TenantId == tenantId).ToList());

        return new RoleInheritanceEngine(
            repository.Object,
            Options.Create(options ?? new PermissionEngineOptions()),
            NullLogger<RoleInheritanceEngine>.Instance);
    }

    [Fact]
    public async Task GetClosureAsync_UnionsPermissionsFromMultipleParents()
    {
        var parentA = Role(Guid.NewGuid(), "Editor", new[] { "content:create", "content:update" });
        var parentB = Role(Guid.NewGuid(), "Reviewer", new[] { "content:review" });
        var child = Role(
            Guid.NewGuid(),
            "SeniorEditor",
            new[] { "content:publish" },
            parent: parentA.Id,
            additionalParents: new[] { parentB.Id });

        var closure = await CreateEngine(new Dictionary<Guid, DynamicRole>
        {
            [child.Id] = child,
            [parentA.Id] = parentA,
            [parentB.Id] = parentB
        }).GetClosureAsync(child.Id);

        closure.Role.Should().NotBeNull();
        closure.CyclesCut.Should().Be(0);
        closure.InheritedPermissions.Should().BeEquivalentTo(new[]
        {
            "content:create", "content:update", "content:review"
        });
        closure.Ancestors.Should().HaveCount(2);
        closure.Ancestors.Should().OnlyContain(ancestor => ancestor.Depth == 1);
    }

    [Fact]
    public async Task GetClosureAsync_CutsDirectParentCycle()
    {
        var a = Role(Guid.NewGuid(), "A", new[] { "perm:a" });
        var b = Role(Guid.NewGuid(), "B", new[] { "perm:b" }, parent: a.Id);
        a.ParentRoleId = b.Id; // A -> B -> A cycle

        var closure = await CreateEngine(new Dictionary<Guid, DynamicRole>
        {
            [a.Id] = a,
            [b.Id] = b
        }).GetClosureAsync(a.Id);

        closure.CyclesCut.Should().BeGreaterThan(0);
        // The cycle edge is cut and traversal terminates: B is A's parent, so A keeps
        // inheriting perm:b from B.
        closure.InheritedPermissions.Should().Contain("perm:b");
    }

    [Fact]
    public async Task GetClosureAsync_CutsCycleAmongMultipleParents()
    {
        var root = Role(Guid.NewGuid(), "Root", new[] { "perm:root" });
        var side = Role(Guid.NewGuid(), "Side", new[] { "perm:side" }, parent: root.Id);
        var child = Role(
            Guid.NewGuid(),
            "Child",
            new[] { "perm:child" },
            parent: root.Id,
            additionalParents: new[] { side.Id });
        side.AdditionalParentRoleIds = new[] { child.Id }; // Child -> Side -> Child cycle

        var closure = await CreateEngine(new Dictionary<Guid, DynamicRole>
        {
            [child.Id] = child,
            [side.Id] = side,
            [root.Id] = root
        }).GetClosureAsync(child.Id);

        closure.CyclesCut.Should().BeGreaterThan(0);
        // Both direct parents still contribute; the cycle edge to Child is cut.
        closure.InheritedPermissions.Should().Contain("perm:root").And.Contain("perm:side");
    }

    [Fact]
    public async Task GetClosureAsync_UnknownParentContributesNothing()
    {
        var child = Role(
            Guid.NewGuid(),
            "Orphan",
            new[] { "perm:own" },
            parent: Guid.NewGuid()); // dangling reference - fail closed

        var closure = await CreateEngine(new Dictionary<Guid, DynamicRole>
        {
            [child.Id] = child
        }).GetClosureAsync(child.Id);

        closure.InheritedPermissions.Should().BeEmpty();
        closure.Ancestors.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Gap 2: selective inheritance blocking + configurable rules
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetClosureAsync_BlockedInheritedPermissions_DoNotFlowIn()
    {
        var parent = Role(Guid.NewGuid(), "Base", new[] { "content:read", "content:delete", "content:create" });
        var child = Role(
            Guid.NewGuid(),
            "ReadOnly",
            new[] { "content:read" },
            parent: parent.Id,
            blocked: new[] { "content:delete" });

        var closure = await CreateEngine(new Dictionary<Guid, DynamicRole>
        {
            [child.Id] = child,
            [parent.Id] = parent
        }).GetClosureAsync(child.Id);

        // Blocked permission does not flow in; the parent's other permissions do.
        closure.InheritedPermissions.Should().BeEquivalentTo(new[] { "content:read", "content:create" });
        closure.InheritedPermissions.Should().NotContain("content:delete");
    }

    [Fact]
    public async Task GetClosureAsync_IntermediateBlocking_StopsFlowToDescendants()
    {
        var grandparent = Role(Guid.NewGuid(), "Top", new[] { "perm:keep", "perm:blocked-at-middle" });
        var middle = Role(
            Guid.NewGuid(),
            "Middle",
            new[] { "perm:middle" },
            parent: grandparent.Id,
            blocked: new[] { "perm:blocked-at-middle" });
        var child = Role(Guid.NewGuid(), "Bottom", new[] { "perm:bottom" }, parent: middle.Id);

        var closure = await CreateEngine(new Dictionary<Guid, DynamicRole>
        {
            [child.Id] = child,
            [middle.Id] = middle,
            [grandparent.Id] = grandparent
        }).GetClosureAsync(child.Id);

        // The middle role opts out of perm:blocked-at-middle, so nothing of it flows to
        // the bottom role even though the bottom role blocks nothing itself.
        closure.InheritedPermissions.Should().BeEquivalentTo(new[] { "perm:middle", "perm:keep" });
        closure.InheritedPermissions.Should().NotContain("perm:blocked-at-middle");
    }

    [Fact]
    public async Task GetClosureAsync_DenyPermissions_FlowDownUnblocked()
    {
        var parent = Role(
            Guid.NewGuid(),
            "Base",
            new[] { "content:read" },
            denies: new[] { "content:delete" });
        var child = Role(
            Guid.NewGuid(),
            "Child",
            new[] { "content:delete" },
            parent: parent.Id,
            blocked: new[] { "content:read" }); // blocking applies to allows only

        var closure = await CreateEngine(new Dictionary<Guid, DynamicRole>
        {
            [child.Id] = child,
            [parent.Id] = parent
        }).GetClosureAsync(child.Id);

        closure.InheritedPermissions.Should().NotContain("content:read");
        closure.InheritedDenyPermissions.Should().Contain("content:delete");
    }

    [Fact]
    public async Task GetClosureAsync_InheritanceDisabled_YieldsNothing()
    {
        var parent = Role(Guid.NewGuid(), "Base", new[] { "content:read" });
        var child = Role(Guid.NewGuid(), "Child", Array.Empty<string>(), parent: parent.Id);

        var closure = await CreateEngine(
            new Dictionary<Guid, DynamicRole> { [child.Id] = child, [parent.Id] = parent },
            new PermissionEngineOptions { Inheritance = new PermissionInheritanceOptions { Enabled = false } })
            .GetClosureAsync(child.Id);

        closure.InheritedPermissions.Should().BeEmpty();
        closure.Ancestors.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClosureAsync_MaxDepth_PrunesDeepAncestors()
    {
        var top = Role(Guid.NewGuid(), "Top", new[] { "perm:top" });
        var middle = Role(Guid.NewGuid(), "Middle", new[] { "perm:middle" }, parent: top.Id);
        var bottom = Role(Guid.NewGuid(), "Bottom", new[] { "perm:bottom" }, parent: middle.Id);

        var closure = await CreateEngine(
            new Dictionary<Guid, DynamicRole>
            {
                [bottom.Id] = bottom,
                [middle.Id] = middle,
                [top.Id] = top
            },
            new PermissionEngineOptions
            {
                Inheritance = new PermissionInheritanceOptions { MaxDepth = 1 }
            })
            .GetClosureAsync(bottom.Id);

        closure.DepthLimitReached.Should().BeTrue();
        // Only the direct parent (depth 1) contributes; the grandparent is pruned.
        closure.InheritedPermissions.Should().BeEquivalentTo(new[] { "perm:middle" });
    }

    [Fact]
    public async Task GetClosureAsync_StaticRolePermissions_FlowFromParents()
    {
        var member = Role(Guid.NewGuid(), "MEMBER", Array.Empty<string>()); // static permissions apply
        var senior = Role(Guid.NewGuid(), "Senior", new[] { "content:review" }, parent: member.Id);

        var closure = await CreateEngine(new Dictionary<Guid, DynamicRole>
        {
            [senior.Id] = senior,
            [member.Id] = member
        }).GetClosureAsync(senior.Id);

        // Static MEMBER permissions flow from the parent; the child's own
        // content:review stays direct (not inherited).
        closure.InheritedPermissions.Should().Contain("tenant:read");
        closure.InheritedPermissions.Should().NotContain("content:review");
    }

    // ------------------------------------------------------------------
    // Cycle guard for parent mutations
    // ------------------------------------------------------------------

    [Fact]
    public async Task WouldCreateCycleAsync_SelfReference_IsACycle()
    {
        var role = Role(Guid.NewGuid(), "A", Array.Empty<string>());
        var engine = CreateEngine(new Dictionary<Guid, DynamicRole> { [role.Id] = role });

        (await engine.WouldCreateCycleAsync(role.Id, new[] { role.Id })).Should().BeTrue();
    }

    [Fact]
    public async Task WouldCreateCycleAsync_DownwardPathToRole_IsACycle()
    {
        // child -> parent chain: adding parent as a PARENT of child closes a cycle.
        var top = Role(Guid.NewGuid(), "Top", Array.Empty<string>());
        var middle = Role(Guid.NewGuid(), "Middle", Array.Empty<string>(), parent: top.Id);

        var engine = CreateEngine(new Dictionary<Guid, DynamicRole>
        {
            [top.Id] = top,
            [middle.Id] = middle
        });

        // middle's existing parent is top; adopting middle as top's parent closes the
        // cycle top -> middle -> top.
        (await engine.WouldCreateCycleAsync(top.Id, new[] { middle.Id })).Should().BeTrue();
    }

    [Fact]
    public async Task WouldCreateCycleAsync_UnrelatedParent_IsNotACycle()
    {
        var role = Role(Guid.NewGuid(), "A", Array.Empty<string>());
        var unrelated = Role(Guid.NewGuid(), "B", Array.Empty<string>());

        var engine = CreateEngine(new Dictionary<Guid, DynamicRole>
        {
            [role.Id] = role,
            [unrelated.Id] = unrelated
        });

        (await engine.WouldCreateCycleAsync(role.Id, new[] { unrelated.Id })).Should().BeFalse();
    }

    [Fact]
    public void GetEffectiveParentRoleIds_DeduplicatesAndOrdersPrimaryFirst()
    {
        var primary = Guid.NewGuid();
        var additional = Guid.NewGuid();
        var role = Role(
            Guid.NewGuid(),
            "A",
            Array.Empty<string>(),
            parent: primary,
            additionalParents: new[] { additional, primary, additional, Guid.Empty });

        var parents = role.GetEffectiveParentRoleIds();

        parents.Should().Equal(primary, additional);
    }
}

/// <summary>
///     <see cref="RbacPermissionResolver"/> integration with the inheritance engine.
/// </summary>
public class RbacPermissionResolverInheritanceTests
{
    [Fact]
    public async Task ResolvePermissionsAsync_UnionsDirectAndInheritedWithEngineClosure()
    {
        var assignedRole = new DynamicRole
        {
            Id = Guid.NewGuid(),
            Name = "SeniorEditor",
            DisplayName = "SeniorEditor",
            Permissions = new[] { "content:publish" },
            DenyPermissions = new[] { "billing:read" },
            IsActive = true
        };
        var assignment = new DynamicRoleAssignment
        {
            UserId = Guid.NewGuid(),
            RoleId = assignedRole.Id,
            Role = assignedRole,
            IsActive = true
        };

        var parentRole = new DynamicRole { Id = Guid.NewGuid(), Name = "Editor", DisplayName = "Editor", Permissions = new[] { "content:update" } };

        var assignmentRepository = new Mock<IDynamicRoleAssignmentRepository>();
        assignmentRepository
            .Setup(repo => repo.GetValidByUserAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });

        var closure = new RoleInheritanceClosure(
            assignedRole,
            new[]
            {
                new RoleAncestorContribution(parentRole, 1, new[] { "content:update" })
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "content:update" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "billing:write" },
            CyclesCut: 0,
            DepthLimitReached: false);

        var engine = new Mock<IRoleInheritanceEngine>();
        engine
            .Setup(eng => eng.GetClosureAsync(assignedRole.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(closure);

        var resolver = new RbacPermissionResolver(
            assignmentRepository.Object,
            engine.Object,
            NullLogger<RbacPermissionResolver>.Instance);

        var result = await resolver.ResolvePermissionsAsync(assignment.UserId, assignedRole.TenantId);

        result.Permissions.Should().BeEquivalentTo(new[] { "content:publish", "content:update" });
        result.DenyPermissions.Should().BeEquivalentTo(new[] { "billing:read", "billing:write" });
        result.RoleContributions.Should().HaveCount(2);
        result.RoleContributions.Should().Contain(contribution =>
            contribution.RoleName == "SeniorEditor" && !contribution.IsInherited);
        result.RoleContributions.Should().Contain(contribution =>
            contribution.RoleName == "Editor" && contribution.IsInherited && contribution.InheritedFromRoleId == assignedRole.Id);
    }

    [Fact]
    public async Task ResolvePermissionsAsync_SkipsInactiveRoles()
    {
        var inactiveRole = new DynamicRole
        {
            Id = Guid.NewGuid(),
            Name = "Disabled",
            DisplayName = "Disabled",
            Permissions = new[] { "content:publish" },
            IsActive = false
        };
        var assignment = new DynamicRoleAssignment
        {
            UserId = Guid.NewGuid(),
            RoleId = inactiveRole.Id,
            Role = inactiveRole,
            IsActive = true
        };

        var assignmentRepository = new Mock<IDynamicRoleAssignmentRepository>();
        assignmentRepository
            .Setup(repo => repo.GetValidByUserAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });

        var engine = new Mock<IRoleInheritanceEngine>(MockBehavior.Strict);

        var resolver = new RbacPermissionResolver(
            assignmentRepository.Object,
            engine.Object,
            NullLogger<RbacPermissionResolver>.Instance);

        var result = await resolver.ResolvePermissionsAsync(assignment.UserId, Guid.NewGuid());

        result.Permissions.Should().BeEmpty();
        result.DenyPermissions.Should().BeEmpty();
        result.RoleContributions.Should().BeEmpty();
        engine.VerifyNoOtherCalls();
    }
}
