using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Moq;

namespace GameGuild.API.UnitTests.Eventing;

/// <summary>
/// Governance entity capture (issues #188/#189): role-hierarchy mutations, dynamic role
/// assignments and permission-policy mutations flow through the EF permission-audit hook
/// with before/after state, where they previously escaped every audit path.
/// </summary>
public sealed class GovernanceAuditHookCaptureTests
{
    private static (ApplicationDbContext Context, Mock<IAuditService> Audit) CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Returns(Task.CompletedTask);
        var context = new ApplicationDbContext(options, null, operationAccessor, audit.Object);
        return (context, audit);
    }

    private static List<JsonElement> ChangesOf(CreateAuditLogRequest request)
    {
        var metadata = JsonDocument.Parse(JsonSerializer.Serialize(request.Metadata)).RootElement;
        return metadata.GetProperty("Changes").EnumerateArray().ToList();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenRoleHierarchyEdgeChanges_CapturesBeforeAndAfter()
    {
        var (context, audit) = CreateContext();
        var parentId = Guid.NewGuid();
        var parent = new DynamicRole { Id = parentId, Name = "base", DisplayName = "Base", Permissions = ["content:read"] };
        var child = new DynamicRole { Id = Guid.NewGuid(), Name = "child", DisplayName = "Child", Permissions = [] };
        context.Add(parent);
        context.Add(child);
        await context.SaveChangesAsync();
        audit.Invocations.Clear();

        child.ParentRoleId = parentId;
        await context.SaveChangesAsync();

        audit.Invocations.Should().HaveCount(1);
        var request = (CreateAuditLogRequest)audit.Invocations.Single().Arguments[0]!;
        request.ResourceType.Should().Be("Permission");
        var change = ChangesOf(request).Single();
        change.GetProperty("EntityType").GetString().Should().Be("DynamicRole");
        change.GetProperty("BeforeState").GetString().Should().Contain("ParentRoleId");
        change.GetProperty("AfterState").GetString().Should().Contain(parentId.ToString());
    }

    [Fact]
    public async Task SaveChangesAsync_WhenDynamicRoleAssignmentAdded_IsCapturedAsGrant()
    {
        var (context, audit) = CreateContext();
        var role = new DynamicRole { Id = Guid.NewGuid(), Name = "editor", DisplayName = "Editor", Permissions = ["content:edit"] };
        context.Add(role);
        await context.SaveChangesAsync();
        audit.Invocations.Clear();

        context.Add(new DynamicRoleAssignment { UserId = Guid.NewGuid(), RoleId = role.Id });
        await context.SaveChangesAsync();

        var request = (CreateAuditLogRequest)audit.Invocations.Single().Arguments[0]!;
        request.ActionType.Should().Be(AuditActionTypes.PermissionGranted);
        ChangesOf(request).Single().GetProperty("EntityType").GetString().Should().Be("DynamicRoleAssignment");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenDynamicRoleDisplayNameOnlyChanges_NoPermissionAudit()
    {
        var (context, audit) = CreateContext();
        var role = new DynamicRole { Id = Guid.NewGuid(), Name = "editor", DisplayName = "Editor", Permissions = [] };
        context.Add(role);
        await context.SaveChangesAsync();
        audit.Invocations.Clear();

        role.DisplayName = "Senior Editor";
        await context.SaveChangesAsync();

        // Pure display metadata is not a permission/hierarchy mutation.
        audit.Verify(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()), Times.Never);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenConditionalPolicyToggled_IsCapturedWithState()
    {
        var (context, audit) = CreateContext();
        var policy = new ConditionalPolicy
        {
            Name = "business-hours",
            PermissionType = "billing:charge",
            IsEnabled = true
        };
        context.Add(policy);
        await context.SaveChangesAsync();
        audit.Invocations.Clear();

        policy.Disable();
        await context.SaveChangesAsync();

        var request = (CreateAuditLogRequest)audit.Invocations.Single().Arguments[0]!;
        var change = ChangesOf(request).Single();
        change.GetProperty("EntityType").GetString().Should().Be("ConditionalPolicy");
        change.GetProperty("AfterState").GetString().Should().Contain("IsEnabled");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenPolicyDefinitionMutated_IsCaptured()
    {
        var (context, audit) = CreateContext();
        var policy = new PolicyDefinitionEntity
        {
            Id = Guid.NewGuid(),
            PolicyName = "RequireEditor",
            RequiredPermissionsJson = "[]"
        };
        context.Add(policy);
        await context.SaveChangesAsync();
        audit.Invocations.Clear();

        policy.RequireAuthentication = false;
        await context.SaveChangesAsync();

        var request = (CreateAuditLogRequest)audit.Invocations.Single().Arguments[0]!;
        ChangesOf(request).Single().GetProperty("EntityType").GetString().Should().Be("PolicyDefinitionEntity");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenPolicyVersionHistoryAppended_IsNotRecaptured()
    {
        var (context, audit) = CreateContext();
        var policy = new ConditionalPolicy { Name = "hours", PermissionType = "billing:charge" };
        context.Add(policy);
        await context.SaveChangesAsync();
        audit.Invocations.Clear();

        context.Set<PolicyVersionHistory>().Add(new PolicyVersionHistory
        {
            PolicyId = policy.Id,
            Kind = PolicyVersionKind.ConditionalPolicy,
            Version = 1,
            ChangeType = PolicyVersionChangeType.Created,
            SnapshotJson = "{}"
        });
        await context.SaveChangesAsync();

        // Snapshots are history, not permission changes: no re-audit, no recursion.
        audit.Verify(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()), Times.Never);
    }
}
