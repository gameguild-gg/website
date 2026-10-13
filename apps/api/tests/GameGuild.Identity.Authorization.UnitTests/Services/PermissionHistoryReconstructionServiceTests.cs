using FluentAssertions;
using GameGuild.CQRS.Models;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.Moq;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
/// Point-in-time permission reconstruction (issue #189): replay of the audit stream and
/// grant tables with fail-closed behavior whenever the requested instant cannot be
/// proven from the available data.
/// </summary>
public class PermissionHistoryReconstructionServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private sealed class Store(
        List<PermissionAuditLog> auditLogs,
        List<TenantPermission> tenantPermissions,
        List<ResourceUserPermission> resourcePermissions,
        List<DynamicRole> roles,
        List<DynamicRoleAssignment> assignments)
    {
        public List<PermissionAuditLog> AuditLogs { get; } = auditLogs;
        public List<TenantPermission> TenantPermissions { get; } = tenantPermissions;
        public List<ResourceUserPermission> ResourcePermissions { get; } = resourcePermissions;
        public List<DynamicRole> Roles { get; } = roles;
        public List<DynamicRoleAssignment> Assignments { get; } = assignments;

        public IApplicationDbContext Context()
        {
            var context = new Mock<IApplicationDbContext>();
            context.Setup(c => c.Set<PermissionAuditLog>()).Returns(AuditLogs.AsQueryable().BuildMockDbSet().Object);
            context.Setup(c => c.Set<TenantPermission>()).Returns(TenantPermissions.AsQueryable().BuildMockDbSet().Object);
            context.Setup(c => c.Set<ResourceUserPermission>()).Returns(ResourcePermissions.AsQueryable().BuildMockDbSet().Object);
            context.Setup(c => c.Set<DynamicRole>()).Returns(Roles.AsQueryable().BuildMockDbSet().Object);
            context.Setup(c => c.Set<DynamicRoleAssignment>()).Returns(Assignments.AsQueryable().BuildMockDbSet().Object);
            return context.Object;
        }
    }

    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Store EmptyStore() => new([], [], [], [], []);

    private PermissionAuditLog TenantAudit(DateTime at, PermissionOperationType op, string? oldValue, string? newValue) =>
        new()
        {
            Timestamp = at,
            TenantId = new TenantId(_tenantId),
            UserId = _userId,
            ResourceType = "TenantPermission",
            OperationType = op,
            OldValue = oldValue,
            NewValue = newValue,
            PerformedBy = Guid.NewGuid(),
            Success = true
        };

    private PermissionHistoryReconstructionService Sut(Store store) =>
        new(store.Context(), NullLogger<PermissionHistoryReconstructionService>.Instance);

    [Fact]
    public async Task EmptySubject_ReconstructsToEmptySnapshot_WithoutError()
    {
        var snapshot = await Sut(EmptyStore()).ReconstructAtAsync(_tenantId, _userId, T2);

        snapshot.TenantGrants.Should().BeEmpty();
        snapshot.EffectivePermissions.Should().BeEmpty();
        snapshot.RoleAssignments.Should().BeEmpty();
        snapshot.ResourceGrants.Should().BeEmpty();
    }

    [Fact]
    public async Task TenantGrants_ReplayGrantThenRevokeBetweenInstants()
    {
        var store = EmptyStore();
        store.AuditLogs.Add(TenantAudit(T0, PermissionOperationType.Grant, null, "content:read,content:write"));
        store.AuditLogs.Add(TenantAudit(T1, PermissionOperationType.Revoke, "content:read,content:write", "content:read"));

        var afterGrant = await Sut(store).ReconstructAtAsync(_tenantId, _userId, T0.AddMinutes(1));
        var afterRevoke = await Sut(store).ReconstructAtAsync(_tenantId, _userId, T2);

        afterGrant.TenantGrants.Should().Equal("content:read", "content:write");
        afterRevoke.TenantGrants.Should().Equal("content:read");
    }

    [Fact]
    public async Task TenantDenies_SubtractFromEffectivePermissions()
    {
        var store = EmptyStore();
        store.AuditLogs.Add(TenantAudit(T0, PermissionOperationType.Grant, null, "content:read,content:write"));
        store.AuditLogs.Add(TenantAudit(T1, PermissionOperationType.Deny, null, "content:write"));

        var snapshot = await Sut(store).ReconstructAtAsync(_tenantId, _userId, T2);

        snapshot.TenantDenies.Should().Equal("content:write");
        snapshot.EffectivePermissions.Should().Equal("content:read");
    }

    [Fact]
    public async Task PreAuditGrantRow_FailsClosedWithExplicitGap()
    {
        var store = EmptyStore();
        store.TenantPermissions.Add(new TenantPermission
        {
            UserId = _userId,
            TenantId = _tenantId,
            Permissions = ["content:read"],
            GrantedAt = T0
        });

        var act = () => Sut(store).ReconstructAtAsync(_tenantId, _userId, T1);

        var failure = await act.Should().ThrowAsync<PermissionHistoryIncompleteException>();
        failure.Which.Gaps.Should().ContainSingle().Which.Should().Contain("no audit coverage");
    }

    [Fact]
    public async Task NonEmptyInitialAuditState_FailsClosedWhenRowPredatesStream()
    {
        var store = EmptyStore();
        store.TenantPermissions.Add(new TenantPermission
        {
            UserId = _userId,
            TenantId = _tenantId,
            Permissions = ["content:read"],
            GrantedAt = T0
        });
        store.AuditLogs.Add(TenantAudit(T1, PermissionOperationType.Update, "legacy:perm,content:read", "content:read"));

        var act = () => Sut(store).ReconstructAtAsync(_tenantId, _userId, T2);

        var failure = await act.Should().ThrowAsync<PermissionHistoryIncompleteException>();
        failure.Which.Gaps.Should().ContainSingle().Which.Should().Contain("non-empty prior state");
    }

    [Fact]
    public async Task ResourceGrants_AreTimeFilteredByRevocationAndExpiry()
    {
        var store = EmptyStore();
        var now = SystemClock.UtcNow;
        store.ResourcePermissions.Add(new ResourceUserPermission
        {
            UserId = _userId,
            TenantId = new TenantId(_tenantId),
            ResourceType = "Project",
            ResourceId = "p1",
            Permissions = ["read"],
            GrantedByUserId = Guid.NewGuid(),
            GrantedAt = T0
        });
        store.ResourcePermissions.Add(new ResourceUserPermission
        {
            UserId = _userId,
            TenantId = new TenantId(_tenantId),
            ResourceType = "Project",
            ResourceId = "p2",
            Permissions = ["read"],
            GrantedByUserId = Guid.NewGuid(),
            GrantedAt = T0,
            RevokedAt = T1
        });
        store.ResourcePermissions.Add(new ResourceUserPermission
        {
            UserId = _userId,
            TenantId = new TenantId(_tenantId),
            ResourceType = "Project",
            ResourceId = "p3",
            Permissions = ["read"],
            GrantedByUserId = Guid.NewGuid(),
            GrantedAt = T0,
            ExpiresAt = T1.AddMonths(1)
        });

        var betweenGrantAndRevoke = await Sut(store).ReconstructAtAsync(_tenantId, _userId, T0.AddMonths(1));
        var afterEverything = await Sut(store).ReconstructAtAsync(_tenantId, _userId, now.AddMinutes(-1));

        betweenGrantAndRevoke.ResourceGrants.Select(grant => grant.ResourceId).Should().Equal("p1", "p2", "p3");
        afterEverything.ResourceGrants.Select(grant => grant.ResourceId).Should().Equal("p1");
    }

    [Fact]
    public async Task RoleAssignmentWithoutAuditCoverage_FailsClosedWhenSubjectHoldsRoles()
    {
        var store = EmptyStore();
        var role = new DynamicRole { Id = Guid.NewGuid(), Name = "Editor", Permissions = ["content:edit"] };
        store.Roles.Add(role);
        store.Assignments.Add(new DynamicRoleAssignment
        {
            UserId = _userId,
            RoleId = role.Id,
            TenantId = _tenantId,
            IsActive = true
        });

        var act = () => Sut(store).ReconstructAtAsync(_tenantId, _userId, T1);

        var failure = await act.Should().ThrowAsync<PermissionHistoryIncompleteException>();
        failure.Which.Gaps.Should().ContainSingle().Which.Should().Contain("Role-assignment audit coverage");
    }

    [Fact]
    public async Task AuditedAssignment_ExpandsRolePermissionsIntoEffectiveSet()
    {
        var parentRole = new DynamicRole { Id = Guid.NewGuid(), Name = "Base", Permissions = ["content:read"] };
        var editorRole = new DynamicRole
        {
            Id = Guid.NewGuid(),
            Name = "Editor",
            Permissions = ["content:edit"],
            ParentRoleId = parentRole.Id
        };
        var coverageStart = T0;
        var store = EmptyStore();
        store.Roles.AddRange([parentRole, editorRole]);

        // Assignment audit coverage: granted at T0, no later removal.
        store.AuditLogs.Add(new PermissionAuditLog
        {
            Timestamp = coverageStart,
            TenantId = new TenantId(_tenantId),
            UserId = _userId,
            ResourceType = "RoleAssignment",
            OperationType = PermissionOperationType.InheritanceChange,
            ResourceId = editorRole.Id,
            Reason = "Role assigned (command: direct); inherited permissions expanded.",
            PerformedBy = Guid.NewGuid(),
            Success = true
        });
        // Hierarchy audit coverage predates the instant so the edges are provable.
        store.AuditLogs.Add(new PermissionAuditLog
        {
            Timestamp = coverageStart.AddMinutes(-1),
            TenantId = new TenantId(_tenantId),
            ResourceType = "RoleHierarchy",
            OperationType = PermissionOperationType.InheritanceChange,
            ResourceId = editorRole.Id,
            OldValue = System.Text.Json.JsonSerializer.Serialize(new { }),
            NewValue = "{}",
            PerformedBy = Guid.NewGuid(),
            Success = true
        });

        var snapshot = await Sut(store).ReconstructAtAsync(_tenantId, _userId, T2);

        snapshot.RoleAssignments.Should().ContainSingle()
            .Which.DirectPermissions.Should().Equal("content:edit");
        snapshot.EffectivePermissions.Should().Contain("content:edit").And.Contain("content:read");
        snapshot.InheritanceEdges.Should().ContainSingle()
            .Which.ParentRoleName.Should().Be("Base");
    }

    [Fact]
    public async Task AuditedRemoval_ExcludesRoleFromLaterInstants()
    {
        var role = new DynamicRole { Id = Guid.NewGuid(), Name = "Editor", Permissions = ["content:edit"] };
        var store = EmptyStore();
        store.Roles.Add(role);
        store.AuditLogs.Add(new PermissionAuditLog
        {
            Timestamp = T0,
            TenantId = new TenantId(_tenantId),
            UserId = _userId,
            ResourceType = "RoleAssignment",
            OperationType = PermissionOperationType.InheritanceChange,
            ResourceId = role.Id,
            Reason = "Role assigned (command: direct); inherited permissions expanded.",
            PerformedBy = Guid.NewGuid(),
            Success = true
        });
        store.AuditLogs.Add(new PermissionAuditLog
        {
            Timestamp = T1,
            TenantId = new TenantId(_tenantId),
            UserId = _userId,
            ResourceType = "RoleAssignment",
            OperationType = PermissionOperationType.InheritanceChange,
            ResourceId = role.Id,
            Reason = "Role assignment removed (command: direct).",
            PerformedBy = Guid.NewGuid(),
            Success = true
        });
        store.AuditLogs.Add(new PermissionAuditLog
        {
            Timestamp = T0.AddMinutes(-1),
            TenantId = new TenantId(_tenantId),
            ResourceType = "RoleHierarchy",
            OperationType = PermissionOperationType.InheritanceChange,
            ResourceId = role.Id,
            PerformedBy = Guid.NewGuid(),
            Success = true
        });

        var beforeRemoval = await Sut(store).ReconstructAtAsync(_tenantId, _userId, T1.AddMinutes(-1));
        var afterRemoval = await Sut(store).ReconstructAtAsync(_tenantId, _userId, T2);

        beforeRemoval.EffectivePermissions.Should().Contain("content:edit");
        afterRemoval.EffectivePermissions.Should().NotContain("content:edit");
    }

    [Fact]
    public async Task FutureTimestamp_IsRejected()
    {
        var act = () => Sut(EmptyStore()).ReconstructAtAsync(_tenantId, _userId, SystemClock.UtcNow.AddYears(1));
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task InheritanceHistory_ListsHierarchyEventsAscending()
    {
        var roleId = Guid.NewGuid();
        var store = EmptyStore();
        store.AuditLogs.AddRange(
        [
            new PermissionAuditLog
            {
                Timestamp = T1,
                TenantId = new TenantId(_tenantId),
                ResourceType = "RoleHierarchy",
                OperationType = PermissionOperationType.InheritanceChange,
                ResourceId = roleId,
                Reason = "Role hierarchy modified",
                PerformedBy = Guid.NewGuid()
            },
            new PermissionAuditLog
            {
                Timestamp = T0,
                TenantId = new TenantId(_tenantId),
                ResourceType = "RoleHierarchy",
                OperationType = PermissionOperationType.InheritanceChange,
                ResourceId = roleId,
                Reason = "Role hierarchy added",
                PerformedBy = Guid.NewGuid()
            }
        ]);

        var events = await Sut(store).GetInheritanceHistoryAsync(_tenantId);

        events.Should().HaveCount(2);
        events[0].Description.Should().Contain("added");
        events[1].Description.Should().Contain("modified");
    }
}
