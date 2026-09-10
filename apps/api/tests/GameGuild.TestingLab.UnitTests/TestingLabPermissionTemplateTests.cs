using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Configuration;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.TestingLab;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.TestingLab.UnitTests;

public sealed class TestingLabPermissionTemplateTests
{
    [Fact]
    public void SettingsController_UsesTestingLabScopedAuthorizationOnEveryEndpoint()
    {
        var endpointMethods = typeof(TestingLabSettingsController).GetMethods()
            .Where(method => method.DeclaringType == typeof(TestingLabSettingsController) &&
                             method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), true).Length > 0)
            .ToArray();

        endpointMethods.Should().NotBeEmpty();
        endpointMethods.Should().OnlyContain(method =>
            method.GetCustomAttributes(typeof(RequireTestingLabPermissionAttribute), true).Length == 1);
        TestingLabResourceTypes.All.Should().Contain(TestingLabResourceTypes.Settings);
    }

    [Fact]
    public async Task Service_Should_Create_Update_List_And_Delete_TestingLab_Role_Templates()
    {
        await using var context = CreateContext();
        var service = new TestingLabPermissionService(context);

        var created = await service.CreateRoleTemplateAsync(
            "TestingLab Reviewer",
            "Can review testing feedback",
            [
                new GameGuild.TestingLab.PermissionTemplate { Action = TestingLabActions.Read, ResourceType = TestingLabResourceTypes.Feedback },
                new GameGuild.TestingLab.PermissionTemplate { Action = TestingLabActions.Moderate, ResourceType = TestingLabResourceTypes.Feedback }
            ]);

        created.Name.Should().Be("TestingLab Reviewer");
        created.PermissionTemplates.Should().NotBeNull();
        created.PermissionTemplates!.Select(ToPermissionString).Should().Contain([
            $"{TestingLabResourceTypes.Feedback}:{TestingLabActions.Read}",
            $"{TestingLabResourceTypes.Feedback}:{TestingLabActions.Moderate}"
        ]);

        var updated = await service.UpdateRoleTemplateAsync(
            created.Id.ToString(),
            "TestingLab Moderator",
            "Moderates sessions and feedback",
            [
                new GameGuild.TestingLab.PermissionTemplate { Action = TestingLabActions.Moderate, ResourceType = TestingLabResourceTypes.Feedback },
                new GameGuild.TestingLab.PermissionTemplate { Action = TestingLabActions.Approve, ResourceType = TestingLabResourceTypes.Request }
            ]);

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("TestingLab Moderator");
        updated.PermissionTemplates.Should().NotBeNull();
        updated.PermissionTemplates!.Select(ToPermissionString).Should().BeEquivalentTo([
            $"{TestingLabResourceTypes.Feedback}:{TestingLabActions.Moderate}",
            $"{TestingLabResourceTypes.Request}:{TestingLabActions.Approve}"
        ]);

        var templates = await service.GetRoleTemplatesAsync();
        templates.Should().ContainSingle(template => template.Name == "TestingLab Moderator");

        var deleted = await service.DeleteRoleTemplateAsync("TestingLab Moderator");

        deleted.Should().BeTrue();
        (await service.GetRoleTemplatesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Controller_Should_Return_Role_Template_Results_Instead_Of_Disabled_Responses()
    {
        await using var context = CreateContext();
        var service = new TestingLabPermissionService(context);
        var actorContextAccessor = new Mock<IActorContextAccessor>();
        actorContextAccessor
            .SetupGet(accessor => accessor.ActorContext)
            .Returns(ActorContextBuilder.ForUser(Guid.NewGuid()).WithRole("SystemAdmin").Build());

        var sender = new Mock<GameGuild.CQRS.ISender>();
        sender
            .Setup(candidate => candidate.Send(
                It.IsAny<CreateTestingLabRoleTemplateEndpointCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns((CreateTestingLabRoleTemplateEndpointCommand command, CancellationToken _) =>
                service.CreateRoleTemplateAsync(command.Name, command.Description, command.Permissions));
        var controller = new TestingLabPermissionController(
            service,
            actorContextAccessor.Object,
            sender.Object,
            NullLogger<TestingLabPermissionController>.Instance);

        var created = await controller.CreateTestingLabRoleTemplate(new CreateTestingLabRoleRequest
        {
            Name = "TestingLab Facilitator",
            Description = "Runs sessions",
            Permissions = new TestingLabPermissionsDto
            {
                CanCreateSessions = true,
                CanViewParticipants = true
            }
        });

        created.Result.Should().BeOfType<OkObjectResult>();

        var listed = await controller.GetRoleTemplates();

        var ok = listed.Result.Should().BeOfType<OkObjectResult>().Subject;
        var templates = ok.Value.Should().BeAssignableTo<IEnumerable<TestingLabRoleTemplate>>().Subject;
        templates.Should().ContainSingle(template => template.Name == "TestingLab Facilitator");
    }

    [Fact]
    public void Permission_Controller_Should_Require_Admin_Policy()
    {
        var authorize = typeof(TestingLabPermissionController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        authorize.Policy.Should().Be("Users.Admin");
    }

    [Theory]
    [InlineData(nameof(TestingLabPermissionController.CreateTestingLabRoleTemplate))]
    [InlineData(nameof(TestingLabPermissionController.UpdateTestingLabRoleTemplate))]
    [InlineData(nameof(TestingLabPermissionController.DeleteTestingLabRoleTemplate))]
    [InlineData(nameof(TestingLabPermissionController.DeleteTestingLabRoleTemplateByName))]
    public void Global_Role_Template_Mutations_Should_Require_SystemAdmin(string methodName)
    {
        var method = typeof(TestingLabPermissionController).GetMethod(methodName);

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Should().ContainSingle(attribute => attribute.Policy == Policies.SystemAdmin);
    }

    [Fact]
    public async Task SystemAdmin_Should_Assign_Role_In_An_Explicit_Target_Tenant()
    {
        var contextTenantId = Guid.NewGuid();
        var requestedTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var permissionService = new Mock<ITestingLabPermissionService>();
        var actorContextAccessor = new Mock<IActorContextAccessor>();
        actorContextAccessor
            .SetupGet(accessor => accessor.ActorContext)
            .Returns(ActorContextBuilder.ForUser(Guid.NewGuid())
                .WithTenantId(contextTenantId)
                .WithRole("SystemAdmin")
                .Build());
        var sender = new Mock<GameGuild.CQRS.ISender>();
        var controller = new TestingLabPermissionController(
            permissionService.Object,
            actorContextAccessor.Object,
            sender.Object,
            NullLogger<TestingLabPermissionController>.Instance);

        var result = await controller.AssignTestingLabRole(userId, new AssignTestingLabRoleRequest
        {
            TenantId = requestedTenantId,
            RoleName = "TestingLab Reviewer"
        });

        result.Should().BeOfType<OkResult>();
        sender.Verify(candidate => candidate.Send(
            It.Is<AssignTestingLabRoleEndpointCommand>(command =>
                command.UserId == userId &&
                command.TenantId == requestedTenantId &&
                command.RoleName == "TestingLab Reviewer" &&
                command.ExpiresAt == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TenantAdmin_Should_Not_Assign_Role_Outside_Current_Tenant()
    {
        var permissionService = new Mock<ITestingLabPermissionService>();
        var actorContextAccessor = new Mock<IActorContextAccessor>();
        actorContextAccessor
            .SetupGet(accessor => accessor.ActorContext)
            .Returns(ActorContextBuilder.ForUser(Guid.NewGuid())
                .WithTenantId(Guid.NewGuid())
                .WithRole("Admin")
                .Build());
        var controller = new TestingLabPermissionController(
            permissionService.Object,
            actorContextAccessor.Object,
            Mock.Of<GameGuild.CQRS.ISender>(),
            NullLogger<TestingLabPermissionController>.Instance);

        var result = await controller.AssignTestingLabRole(Guid.NewGuid(), new AssignTestingLabRoleRequest
        {
            TenantId = Guid.NewGuid(),
            RoleName = "TestingLab Reviewer"
        });

        result.Should().BeOfType<ForbidResult>();
        permissionService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    public async Task Service_Should_Reject_Invalid_TestingLab_Template_Names(string name)
    {
        await using var context = CreateContext();
        var service = new TestingLabPermissionService(context);

        var action = () => service.CreateRoleTemplateAsync(
            name,
            "Invalid template",
            [new GameGuild.TestingLab.PermissionTemplate { ResourceType = TestingLabResourceTypes.Request, Action = TestingLabActions.Read }]);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Service_Should_Accept_Human_Readable_Role_Template_Name()
    {
        await using var context = CreateContext();
        var service = new TestingLabPermissionService(context);

        var created = await service.CreateRoleTemplateAsync(
            "Event Reviewer",
            "Reviews project applications.",
            [new GameGuild.TestingLab.PermissionTemplate { ResourceType = TestingLabResourceTypes.Request, Action = TestingLabActions.Read }]);

        created.Name.Should().Be("Event Reviewer");
    }

    [Fact]
    public async Task Service_Should_Reject_Overlong_Role_Template_Name()
    {
        await using var context = CreateContext();
        var service = new TestingLabPermissionService(context);

        var action = () => service.CreateRoleTemplateAsync(
            new string('R', 101),
            "Too long.",
            []);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Role template name cannot exceed 100 characters.");
    }

    [Fact]
    public async Task Service_Should_Map_Malformed_Persisted_Template_Permissions_To_Empty_Parts()
    {
        await using var context = CreateContext();
        context.PermissionTemplates.Add(new GameGuild.Identity.Authorization.PermissionTemplate
        {
            Name = "TestingLab Imported",
            Description = "Imported legacy template",
            Category = "TestingLab",
            Permissions = ["feedback", ""],
            IsActive = true
        });
        await context.SaveChangesAsync();
        var service = new TestingLabPermissionService(context);

        var templates = await service.GetRoleTemplatesAsync();

        var imported = templates.Should().ContainSingle(template => template.Name == "TestingLab Imported").Subject;
        imported.PermissionTemplates.Should().Contain(template => template.ResourceType == "feedback" && template.Action == string.Empty);
        imported.PermissionTemplates.Should().Contain(template => template.ResourceType == string.Empty && template.Action == string.Empty);
    }

    [Fact]
    public async Task AssignedRole_ShouldExpandItsEffectivePermissions()
    {
        await using var context = CreateContext();
        var service = new TestingLabPermissionService(context);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        AddActiveTenantMember(context, userId, tenantId);
        await service.CreateRoleTemplateAsync(
            "Session facilitator",
            "Runs testing sessions.",
            [
                new GameGuild.TestingLab.PermissionTemplate { ResourceType = TestingLabResourceTypes.Session, Action = TestingLabActions.Create },
                new GameGuild.TestingLab.PermissionTemplate { ResourceType = TestingLabResourceTypes.Session, Action = TestingLabActions.Read }
            ]);

        await service.AssignRoleToUserAsync(userId, tenantId, "Session facilitator");

        var roles = await service.GetUserRolesAsync(userId, tenantId);
        var permissions = await service.GetUserPermissionsAsync(userId, tenantId);

        roles.Should().ContainSingle(role => role.RoleName == "Session facilitator");
        permissions.Should().Contain(permission =>
            permission.ResourceType == TestingLabResourceTypes.Session &&
            permission.Action == TestingLabActions.Create &&
            permission.ResourceId == null);
    }

    [Fact]
    public async Task AssignedRoleTemplate_ShouldNotBeDeleted()
    {
        await using var context = CreateContext();
        var service = new TestingLabPermissionService(context);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        AddActiveTenantMember(context, userId, tenantId);
        await service.CreateRoleTemplateAsync("Event reviewer", "Reviews applications.", []);
        await service.AssignRoleToUserAsync(userId, tenantId, "Event reviewer");

        var action = () => service.DeleteRoleTemplateAsync("Event reviewer");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*assigned to one or more members*");
    }

    [Fact]
    public async Task AssignedRole_ShouldRejectAUserWithoutAnActiveTargetTenantMembership()
    {
        await using var context = CreateContext();
        var service = new TestingLabPermissionService(context);
        await service.CreateRoleTemplateAsync("Event manager", "Manages events.", []);

        var action = () => service.AssignRoleToUserAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Event manager");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*active member of the target tenant*");
    }

    [Fact]
    public async Task ResourcePermission_ShouldTrackExpiryAndPreserveOtherActionsWhenOneIsRevoked()
    {
        await using var context = CreateContext();
        var service = new TestingLabPermissionService(context);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        AddActiveTenantMember(context, userId, tenantId);
        var expiry = SystemClock.UtcNow.AddDays(2);

        await service.GrantPermissionAsync(userId, tenantId, TestingLabActions.Read, TestingLabResourceTypes.Request, resourceId, expiresAt: expiry);
        await service.GrantPermissionAsync(userId, tenantId, TestingLabActions.Edit, TestingLabResourceTypes.Request, resourceId, expiresAt: expiry);
        await service.RevokePermissionAsync(userId, tenantId, TestingLabActions.Edit, TestingLabResourceTypes.Request, resourceId);

        var permissions = await service.GetUserPermissionsAsync(userId, tenantId);

        permissions.Should().ContainSingle(permission =>
            permission.ResourceType == TestingLabResourceTypes.Request &&
            permission.ResourceId == resourceId &&
            permission.Action == TestingLabActions.Read &&
            permission.ExpiresAt == expiry);
        permissions.Should().NotContain(permission => permission.Action == TestingLabActions.Edit);
    }

    [Fact]
    public async Task ExpiredResourcePermission_ShouldNotBeEffective()
    {
        await using var context = CreateContext();
        var service = new TestingLabPermissionService(context);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        AddActiveTenantMember(context, userId, tenantId);

        await service.GrantPermissionAsync(
            userId,
            tenantId,
            TestingLabActions.Read,
            TestingLabResourceTypes.Location,
            resourceId,
            expiresAt: SystemClock.UtcNow.AddMinutes(-1));

        var permissions = await service.GetUserPermissionsAsync(userId, tenantId);

        permissions.Should().BeEmpty();
        (await service.HasPermissionAsync(userId, tenantId, TestingLabActions.Read, TestingLabResourceTypes.Location, resourceId)).Should().BeFalse();
    }

    [Fact]
    public async Task ResourceSpecificPermission_ShouldNotAuthorizeACollectionOperation()
    {
        await using var context = CreateContext();
        var service = new TestingLabPermissionService(context);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        AddActiveTenantMember(context, userId, tenantId);
        await service.GrantPermissionAsync(
            userId,
            tenantId,
            TestingLabActions.Read,
            TestingLabResourceTypes.Request,
            requestId);

        var collectionAllowed = await service.HasPermissionAsync(
            userId,
            tenantId,
            TestingLabActions.Read,
            TestingLabResourceTypes.Request);
        var resourceAllowed = await service.HasPermissionAsync(
            userId,
            tenantId,
            TestingLabActions.Read,
            TestingLabResourceTypes.Request,
            requestId);

        collectionAllowed.Should().BeFalse();
        resourceAllowed.Should().BeTrue();
    }

    [Fact]
    public void TestingLab_Model_Configuration_Should_Register_Runtime_Entities()
    {
        var modelBuilder = new ModelBuilder();

        new TestingLabModelConfiguration().Configure(modelBuilder);

        modelBuilder.Model.FindEntityType(typeof(TestingRequest))!.GetTableName().Should().Be("testing_requests");
        modelBuilder.Model.FindEntityType(typeof(TestingSession))!.GetTableName().Should().Be("testing_sessions");
        modelBuilder.Model.FindEntityType(typeof(TestingLocation))!.GetTableName().Should().Be("testing_locations");
        modelBuilder.Model.FindEntityType(typeof(TestingFeedback))!.GetTableName().Should().Be("testing_feedback");
        modelBuilder.Model.FindEntityType(typeof(TestingLabSettings))!.GetTableName().Should().Be("testing_lab_settings");
    }

    private static TestingLabPermissionDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestingLabPermissionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestingLabPermissionDbContext(options);
    }

    private static void AddActiveTenantMember(
        TestingLabPermissionDbContext context,
        Guid userId,
        Guid tenantId)
    {
        context.Set<TenantMember>().Add(new TenantMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Role = "Member",
            IsActive = true,
        });
        context.SaveChanges();
    }

    private static string ToPermissionString(GameGuild.TestingLab.PermissionTemplate template)
        => $"{template.ResourceType}:{template.Action}";

    private sealed class TestingLabPermissionDbContext(DbContextOptions<TestingLabPermissionDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<TenantPermission> TenantPermissions => Set<TenantPermission>();

        public DbSet<GameGuild.Identity.Authorization.PermissionTemplate> PermissionTemplates => Set<GameGuild.Identity.Authorization.PermissionTemplate>();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TenantPermissionConfiguration());
            modelBuilder.ApplyConfiguration(new PermissionTemplateConfiguration());
            modelBuilder.ApplyConfiguration(new ResourceUserPermissionConfiguration());
            modelBuilder.ApplyConfiguration(new TenantMemberConfiguration());
        }
    }
}
