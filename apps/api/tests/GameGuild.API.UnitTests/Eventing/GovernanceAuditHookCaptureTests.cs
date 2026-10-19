using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
    [Theory]
    [InlineData(SecurityEventCaptureOutcome.PersistedToDatabase)]
    [InlineData(SecurityEventCaptureOutcome.SpooledLocally)]
    public async Task PermissionSnapshots_UseSecurityPipelineAndPreserveBulkCount(SecurityEventCaptureOutcome outcome)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var captured = new List<CreateAuditLogRequest>();
        var security = new Mock<ISecurityEventLogger>();
        security.Setup(service => service.RecordAsync(It.IsAny<CreateAuditLogRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateAuditLogRequest, CancellationToken>((request, token) =>
            {
                Assert.Equal(CancellationToken.None, token);
                captured.Add(request);
            })
            .ReturnsAsync((CreateAuditLogRequest request, CancellationToken _) => new SecurityEventCaptureResult(
                outcome,
                SecurityEventTaxonomy.Classify(request.ActionType, request.Category, request.Success, request.RiskLevel),
                Guid.NewGuid()));
        using var services = new ServiceCollection().AddSingleton(security.Object).BuildServiceProvider();
        await using var context = new ApplicationDbContext(options, null, new UseCaseOperationContextAccessor(),
            auditService: RoutingAuditService(services));
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        context.AddRange(new TenantPermission { UserId = Guid.NewGuid(), TenantId = tenant, GrantedBy = actor, Permissions = ["test:read"] },
            new TenantPermission { UserId = Guid.NewGuid(), TenantId = tenant, GrantedBy = actor, Permissions = ["test:write"] });

        await context.SaveChangesAsync();

        var request = Assert.Single(captured);
        Assert.Equal(AuditActionTypes.PermissionGranted, request.ActionType);
        Assert.Equal(tenant, request.TenantId);
        Assert.Equal(actor, request.UserId);
        var metadata = JsonSerializer.SerializeToElement(request.Metadata);
        Assert.Equal(2, metadata.GetProperty("ChangeCount").GetInt32());
        Assert.Equal(2, metadata.GetProperty("Changes").GetArrayLength());
        Assert.Equal(2, await context.Set<TenantPermission>().CountAsync());
        security.Verify(service => service.RecordAsync(It.IsAny<CreateAuditLogRequest>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PermissionSnapshots_GroupBulkEventsByActorAndTenant()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var captured = new List<CreateAuditLogRequest>();
        var security = new Mock<ISecurityEventLogger>();
        security.Setup(service => service.RecordAsync(It.IsAny<CreateAuditLogRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateAuditLogRequest, CancellationToken>((request, _) => captured.Add(request))
            .ReturnsAsync((CreateAuditLogRequest request, CancellationToken _) => new SecurityEventCaptureResult(
                SecurityEventCaptureOutcome.PersistedToDatabase,
                SecurityEventTaxonomy.Classify(request.ActionType, request.Category, request.Success, request.RiskLevel),
                Guid.NewGuid()));
        using var services = new ServiceCollection().AddSingleton(security.Object).BuildServiceProvider();
        await using var context = new ApplicationDbContext(options, null, new UseCaseOperationContextAccessor(),
            auditService: RoutingAuditService(services));
        var tenant = Guid.NewGuid();
        var foreignTenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        context.AddRange(new TenantPermission { UserId = Guid.NewGuid(), TenantId = tenant, GrantedBy = actor, Permissions = ["test:read"] },
            new TenantPermission { UserId = Guid.NewGuid(), TenantId = foreignTenant, GrantedBy = actor, Permissions = ["test:read"] });

        await context.SaveChangesAsync();

        Assert.Equal(2, captured.Count);
        Assert.Equal(new[] { tenant, foreignTenant }.OrderBy(value => value), captured.Select(request => request.TenantId!.Value).OrderBy(value => value));
        foreach (var request in captured)
        {
            var metadata = JsonSerializer.SerializeToElement(request.Metadata);
            Assert.Equal(1, metadata.GetProperty("ChangeCount").GetInt32());
            var change = Assert.Single(metadata.GetProperty("Changes").EnumerateArray());
            Assert.Equal(request.TenantId, change.GetProperty("TenantId").GetGuid());
        }
    }

    private static AuditService RoutingAuditService(ServiceProvider services) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), new HttpContextAccessor(),
            new Mock<IPiiRedactionService>().Object, NullLogger<AuditService>.Instance);

    [Theory]
    [InlineData(SecurityEventCaptureOutcome.PersistedToDatabase, true)]
    [InlineData(SecurityEventCaptureOutcome.SpooledLocally, true)]
    [InlineData(SecurityEventCaptureOutcome.SpoolingDisabled, false)]
    public async Task PermissionCapture_ReportsWhetherDeliveryIsDurable(SecurityEventCaptureOutcome outcome, bool durable)
    {
        var security = new Mock<ISecurityEventLogger>();
        security.Setup(service => service.RecordAsync(It.IsAny<CreateAuditLogRequest>(), CancellationToken.None))
            .ReturnsAsync(new SecurityEventCaptureResult(outcome,
                SecurityEventTaxonomy.Classify(AuditActionTypes.PermissionGranted, AuditCategory.Permission, true, AuditRiskLevel.Medium), Guid.NewGuid()));
        using var services = new ServiceCollection().AddSingleton(security.Object).BuildServiceProvider();
        var captured = await RoutingAuditService(services).TryLogAsync(new CreateAuditLogRequest
        {
            ActionType = AuditActionTypes.PermissionGranted,
            Category = AuditCategory.Permission,
            ResourceType = "Permission",
            Metadata = new { ChangeCount = 1 }
        });
        Assert.Equal(durable, captured);
        security.Verify(service => service.RecordAsync(It.IsAny<CreateAuditLogRequest>(), CancellationToken.None), Times.Once);
    }

}
