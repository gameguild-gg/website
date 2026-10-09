using System.Text.Json;
using FluentAssertions;
using GameGuild;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GameGuild.API.UnitTests.Database;

/// <summary>
///     DbContext-level audit tests for the soft-delete flow of role-assignment removal (#357):
///     <see cref="RoleRepository.RemoveRoleFromUserAsync"/> must preserve the <see cref="UserRole"/>
///     row for permission history and the centralized permission-audit hook in
///     <see cref="ApplicationDbContext"/> must classify the resulting Modified entry as a
///     permission revocation.
/// </summary>
public sealed class UserRoleSoftDeleteAuditTests
{
    [Fact]
    public async Task RemoveRoleFromUserAsync_SoftDeletesAssignment_AndAuditsRevocation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Returns(Task.CompletedTask);
        await using var context = new ApplicationDbContext(options, null, operationAccessor, auditService.Object);

        var targetUserId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var role = new Role("AuditableRole", "Auditable role", null) { Id = Guid.NewGuid() };
        var assignment = new UserRole(targetUserId, role.Id, actorId) { Id = Guid.NewGuid(), Role = role };
        context.Add(role);
        context.Add(assignment);
        await context.SaveChangesAsync();
        auditService.Invocations.Clear();

        var repository = new RoleRepository(context);
        await repository.RemoveRoleFromUserAsync(targetUserId, role.Id);

        // The row is preserved (soft-deleted), never physically removed.
        var persisted = await context.Set<UserRole>().SingleAsync();
        persisted.DeletedAt.Should().NotBeNull();
        persisted.IsDeleted.Should().BeTrue();

        // The centralized permission-audit hook classified the soft delete as a revocation.
        var revokeRequest = (CreateAuditLogRequest)auditService.Invocations.Single().Arguments[0]!;
        revokeRequest.ActionType.Should().Be(AuditActionTypes.PermissionRevoked);
        revokeRequest.ResourceType.Should().Be("Permission");
        revokeRequest.Category.Should().Be(AuditCategory.Permission);

        var metadata = JsonDocument.Parse(JsonSerializer.Serialize(revokeRequest.Metadata)).RootElement;
        var change = metadata.GetProperty("Changes")[0];
        change.GetProperty("EntityType").GetString().Should().Be("UserRole");
        change.GetProperty("Operation").GetString().Should().Be("Modified");
        change.GetProperty("TargetUserId").GetGuid().Should().Be(targetUserId);
        change.GetProperty("BeforeState").GetString().Should().NotContain("DeletedAt");
        change.GetProperty("AfterState").GetString().Should().Contain("DeletedAt");
    }

    [Fact]
    public async Task Reassigning_ASoftDeletedRoleAssignment_IsAuditedAsAGrant()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Returns(Task.CompletedTask);
        await using var context = new ApplicationDbContext(options, null, operationAccessor, auditService.Object);

        var targetUserId = Guid.NewGuid();
        var role = new Role("RestorableRole", "Restorable role", null) { Id = Guid.NewGuid() };
        var assignment = new UserRole(targetUserId, role.Id, null) { Id = Guid.NewGuid(), Role = role };
        context.Add(role);
        context.Add(assignment);
        await context.SaveChangesAsync();

        var repository = new RoleRepository(context);
        await repository.RemoveRoleFromUserAsync(targetUserId, role.Id);
        auditService.Invocations.Clear();

        // Re-assignment restores the soft-deleted row instead of duplicating it.
        await repository.AssignRoleToUserAsync(new UserRole(targetUserId, role.Id, null));

        var persistedAssignments = await context.Set<UserRole>().ToListAsync();
        persistedAssignments.Should().ContainSingle();
        persistedAssignments[0].IsDeleted.Should().BeFalse();
        persistedAssignments[0].DeletedAt.Should().BeNull();

        // Restoring a soft-deleted grant is a permission change (not a fresh grant):
        // the centralized hook observes the Modified UserRole entry.
        var restoreRequest = (CreateAuditLogRequest)auditService.Invocations.Single().Arguments[0]!;
        restoreRequest.ActionType.Should().Be(AuditActionTypes.PermissionChanged);
        restoreRequest.Category.Should().Be(AuditCategory.Permission);
    }
}
