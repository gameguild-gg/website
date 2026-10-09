using AutoFixture;
using FluentAssertions;
using GameGuild;

using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Repositories;

public class RoleRepositoryTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<DbSet<Role>> _mockRoleSet;
    private readonly Mock<DbSet<UserRole>> _mockUserRoleSet;
    private readonly RoleRepository _repository;
    private readonly Fixture _fixture;

    public RoleRepositoryTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockRoleSet = new Mock<DbSet<Role>>();
        _mockUserRoleSet = new Mock<DbSet<UserRole>>();
        _fixture = new Fixture();

        _mockContext.Setup(c => c.Set<Role>()).Returns(_mockRoleSet.Object);
        _mockContext.Setup(c => c.Set<UserRole>()).Returns(_mockUserRoleSet.Object);

        _repository = new RoleRepository(_mockContext.Object);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRoleExists_ReturnsRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new Role("Admin", "Administrator role", null) { Id = roleId };
        var roles = new List<Role> { role }.AsQueryable();

        var mockSet = CreateMockDbSet(roles);
        _mockContext.Setup(c => c.Set<Role>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.GetByIdAsync(roleId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(roleId);
        result.Name.Should().Be("Admin");
    }

    [Fact]
    public async Task GetByIdAsync_WhenRoleDoesNotExist_ReturnsNull()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var roles = new List<Role>().AsQueryable();

        var mockSet = CreateMockDbSet(roles);
        _mockContext.Setup(c => c.Set<Role>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.GetByIdAsync(roleId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_WithoutFilters_ReturnsAllActiveRoles()
    {
        // Arrange
        var roles = new List<Role>
        {
            new Role("Admin", "Admin role", null) { IsActive = true },
            new Role("User", "User role", null) { IsActive = true },
            new Role("Disabled", "Disabled role", null) { IsActive = false }
        }.AsQueryable();

        var mockSet = CreateMockDbSet(roles);
        _mockContext.Setup(c => c.Set<Role>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.IsActive);
    }

    [Fact]
    public async Task GetAllAsync_WithTenantFilter_ReturnsOnlyTenantRoles()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var roles = new List<Role>
        {
            new Role("TenantAdmin", "Tenant admin", tenantId) { IsActive = true },
            new Role("GlobalAdmin", "Global admin", null) { IsActive = true }
        }.AsQueryable();

        var mockSet = CreateMockDbSet(roles);
        _mockContext.Setup(c => c.Set<Role>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.GetAllAsync(tenantId);

        // Assert
        result.Should().HaveCount(1);
        result.First().TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task GetAllAsync_WithIncludeInactive_ReturnsAllRoles()
    {
        // Arrange
        var roles = new List<Role>
        {
            new Role("Active", "Active role", null) { IsActive = true },
            new Role("Inactive", "Inactive role", null) { IsActive = false }
        }.AsQueryable();

        var mockSet = CreateMockDbSet(roles);
        _mockContext.Setup(c => c.Set<Role>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.GetAllAsync(includeInactive: true);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByNameAsync_WithMatchingName_ReturnsRole()
    {
        // Arrange
        var roles = new List<Role>
        {
            new Role("Admin", "Admin role", null)
        }.AsQueryable();

        var mockSet = CreateMockDbSet(roles);
        _mockContext.Setup(c => c.Set<Role>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.GetByNameAsync("admin"); // Case-insensitive

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Admin");
    }

    [Fact]
    public async Task GetByNameAsync_WithTenantId_ReturnsOnlyTenantRole()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var roles = new List<Role>
        {
            new Role("Admin", "Tenant admin", tenantId),
            new Role("Admin", "Global admin", null)
        }.AsQueryable();

        var mockSet = CreateMockDbSet(roles);
        _mockContext.Setup(c => c.Set<Role>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.GetByNameAsync("Admin", tenantId);

        // Assert
        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task AddAsync_CreatesNewRole_AndSetsTimestamps()
    {
        // Arrange
        var role = new Role("NewRole", "New role", null);

        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _repository.AddAsync(role);

        // Assert
        result.Should().NotBeNull();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        _mockRoleSet.Verify(s => s.Add(role), Times.Once);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesRole_AndSetsUpdatedTimestamp()
    {
        // Arrange
        var role = new Role("UpdatedRole", "Updated description", null);
        typeof(EntityBase).GetProperty(nameof(EntityBase.CreatedAt))!.SetValue(role, DateTime.UtcNow.AddDays(-1));
        typeof(EntityBase).GetProperty(nameof(EntityBase.UpdatedAt))!.SetValue(role, DateTime.UtcNow.AddDays(-1));

        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _repository.UpdateAsync(role);

        // Assert
        role.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        _mockRoleSet.Verify(s => s.Update(role), Times.Once);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenRoleExists_DeletesRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new Role("ToDelete", "Role to delete", null) { Id = roleId };
        var roles = new List<Role> { role }.AsQueryable();

        var mockSet = CreateMockDbSet(roles);
        _mockContext.Setup(c => c.Set<Role>()).Returns(mockSet.Object);
        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        await repository.DeleteAsync(roleId);

        // Assert
        mockSet.Verify(s => s.Remove(role), Times.Once);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenRoleExists_ReturnsTrue()
    {
        // Arrange
        var roles = new List<Role>
        {
            new Role("ExistingRole", "Existing role", null)
        }.AsQueryable();

        var mockSet = CreateMockDbSet(roles);
        _mockContext.Setup(c => c.Set<Role>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.ExistsByNameAsync("existingrole"); // Case-insensitive

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByNameAsync_WithExcludeRoleId_ExcludesSpecifiedRole()
    {
        // Arrange
        var excludeId = Guid.NewGuid();
        var roles = new List<Role>
        {
            new Role("RoleName", "Role", null) { Id = excludeId }
        }.AsQueryable();

        var mockSet = CreateMockDbSet(roles);
        _mockContext.Setup(c => c.Set<Role>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.ExistsByNameAsync("RoleName", null, excludeId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserRolesAsync_ReturnsActiveNonExpiredRoles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeRole = new Role("ActiveRole", "Active", null) { Id = Guid.NewGuid(), IsActive = true };
        var inactiveRole = new Role("InactiveRole", "Inactive", null) { Id = Guid.NewGuid(), IsActive = false };

        var userRoles = new List<UserRole>
        {
            new UserRole(userId, activeRole.Id, null) { Role = activeRole },
            new UserRole(userId, inactiveRole.Id, null) { Role = inactiveRole }
        }.AsQueryable();

        var mockUserRoleSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(c => c.Set<UserRole>()).Returns(mockUserRoleSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.GetUserRolesAsync(userId);

        // Assert
        result.Should().HaveCount(1);
        result.First().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AssignRoleToUserAsync_CreatesUserRoleAssignment()
    {
        // Arrange
        var userRole = new UserRole(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Setup empty UserRole collection (no existing assignments)
        var userRoles = new List<UserRole>().AsQueryable();
        var mockSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(c => c.Set<UserRole>()).Returns(mockSet.Object);

        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.AssignRoleToUserAsync(userRole);

        // Assert
        result.Should().NotBeNull();
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_WhenRoleAlreadyAssigned_ReturnExistingAssignment()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var assignedBy = Guid.NewGuid();
        
        var existingUserRole = new UserRole(userId, roleId, assignedBy) 
        { 
            Id = Guid.NewGuid(),
            AssignedAt = DateTime.UtcNow.AddDays(-1)
        };
        typeof(EntityBase).GetProperty(nameof(EntityBase.CreatedAt))!.SetValue(existingUserRole, DateTime.UtcNow.AddDays(-1));
        typeof(EntityBase).GetProperty(nameof(EntityBase.UpdatedAt))!.SetValue(existingUserRole, DateTime.UtcNow.AddDays(-1));

        // Setup UserRole collection with existing assignment
        var userRoles = new List<UserRole> { existingUserRole }.AsQueryable();
        var mockSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(c => c.Set<UserRole>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        var duplicateUserRole = new UserRole(userId, roleId, assignedBy);

        // Act
        var result = await repository.AssignRoleToUserAsync(duplicateUserRole);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(existingUserRole.Id);
        // SaveChanges should NOT be called since no new record is created
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_WhenExistingAssignmentExpired_ReactivatesAssignment()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var assignedBy = Guid.NewGuid();
        var existingUserRole = new UserRole(userId, roleId, null)
        {
            Id = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        var userRoles = new List<UserRole> { existingUserRole }.AsQueryable();
        var mockSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(c => c.Set<UserRole>()).Returns(mockSet.Object);
        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var repository = new RoleRepository(_mockContext.Object);
        var renewed = new UserRole(userId, roleId, assignedBy) { ExpiresAt = DateTime.UtcNow.AddDays(30) };

        var result = await repository.AssignRoleToUserAsync(renewed);

        result.Id.Should().Be(existingUserRole.Id);
        result.AssignedBy.Should().Be(assignedBy);
        result.ExpiresAt.Should().Be(renewed.ExpiresAt);
        result.IsExpired().Should().BeFalse();
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkAssignRoleToUsersAsync_ReturnsOutcomesAndPersistsAllChangesOnce()
    {
        var roleId = Guid.NewGuid();
        var assignedBy = Guid.NewGuid();
        var activeUserId = Guid.NewGuid();
        var expiredUserId = Guid.NewGuid();
        var newUserId = Guid.NewGuid();
        var activeAssignment = new UserRole(activeUserId, roleId, Guid.NewGuid())
        {
            Id = Guid.NewGuid(),
            AssignedAt = SystemClock.UtcNow.AddDays(-2)
        };
        var expiredAssignment = new UserRole(expiredUserId, roleId, Guid.NewGuid())
        {
            Id = Guid.NewGuid(),
            AssignedAt = SystemClock.UtcNow.AddDays(-4),
            ExpiresAt = SystemClock.UtcNow.AddDays(-1)
        };
        var userRoles = new List<UserRole> { activeAssignment, expiredAssignment }.AsQueryable();
        var mockSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(context => context.Set<UserRole>()).Returns(mockSet.Object);
        _mockContext.Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var repository = new RoleRepository(_mockContext.Object);
        var expiresAt = SystemClock.UtcNow.AddDays(15);

        var result = await repository.BulkAssignRoleToUsersAsync(
            roleId,
            [activeUserId, expiredUserId, newUserId, newUserId],
            assignedBy,
            expiresAt,
            CancellationToken.None);

        result.Should().HaveCount(3);
        result.Select(item => item.Status).Should().Equal(
            BulkRoleAssignmentStatus.AlreadyAssigned,
            BulkRoleAssignmentStatus.Reactivated,
            BulkRoleAssignmentStatus.Assigned);
        result[0].AssignmentId.Should().Be(activeAssignment.Id);
        result[1].AssignmentId.Should().Be(expiredAssignment.Id);
        result[2].AssignmentId.Should().NotBeEmpty();
        expiredAssignment.AssignedBy.Should().Be(assignedBy);
        expiredAssignment.ExpiresAt.Should().Be(expiresAt);
        mockSet.Verify(set => set.Add(It.Is<UserRole>(assignment => assignment.UserId == newUserId && assignment.RoleId == roleId)), Times.Once);
        _mockContext.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkAssignRoleToUsersAsync_OnlyActiveAssignments_DoesNotSave()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userRoles = new List<UserRole>
        {
            new(userId, roleId, null) { Id = Guid.NewGuid() }
        }.AsQueryable();
        var mockSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(context => context.Set<UserRole>()).Returns(mockSet.Object);
        var repository = new RoleRepository(_mockContext.Object);

        var result = await repository.BulkAssignRoleToUsersAsync(roleId, [userId], null, null, CancellationToken.None);

        result.Should().ContainSingle().Which.Status.Should().Be(BulkRoleAssignmentStatus.AlreadyAssigned);
        _mockContext.Verify(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UserHasRoleAsync_WhenUserHasRole_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userRoles = new List<UserRole>
        {
            new UserRole(userId, roleId, null)
        }.AsQueryable();

        var mockSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(c => c.Set<UserRole>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.UserHasRoleAsync(userId, roleId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_WhenAssignmentExists_SoftDeletesInsteadOfPhysicalDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userRole = new UserRole(userId, roleId, Guid.NewGuid()) { Version = 1 };
        var userRoles = new List<UserRole> { userRole }.AsQueryable();

        var mockSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(c => c.Set<UserRole>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        await repository.RemoveRoleFromUserAsync(userId, roleId);

        // Assert - the row is soft-deleted (history preserved per #357), never removed.
        userRole.DeletedAt.Should().NotBeNull();
        userRole.IsDeleted.Should().BeTrue();
        mockSet.Verify(s => s.Remove(It.IsAny<UserRole>()), Times.Never);
        mockSet.Verify(s => s.Update(userRole), Times.Once);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_WhenAssignmentAlreadySoftDeleted_IsANoOp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userRole = new UserRole(userId, roleId, Guid.NewGuid()) { Version = 1 };
        userRole.SoftDelete();
        var userRoles = new List<UserRole> { userRole }.AsQueryable();

        var mockSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(c => c.Set<UserRole>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        await repository.RemoveRoleFromUserAsync(userId, roleId);

        // Assert - the query excludes soft-deleted rows, so nothing is touched.
        mockSet.Verify(s => s.Update(It.IsAny<UserRole>()), Times.Never);
        mockSet.Verify(s => s.Remove(It.IsAny<UserRole>()), Times.Never);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UserHasRoleAsync_WhenAssignmentIsSoftDeleted_ReturnsFalse_FailClosed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userRole = new UserRole(userId, roleId, null) { Version = 1 };
        userRole.SoftDelete();
        var userRoles = new List<UserRole> { userRole }.AsQueryable();

        var mockSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(c => c.Set<UserRole>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.UserHasRoleAsync(userId, roleId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserRolesAsync_ExcludesSoftDeletedAssignments_FailClosed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var role = new Role("KeptRole", "kept", null) { Id = Guid.NewGuid(), IsActive = true };
        var removedRole = new Role("RemovedRole", "removed", null) { Id = Guid.NewGuid(), IsActive = true };

        var activeAssignment = new UserRole(userId, role.Id, null) { Version = 1, Role = role };
        var softDeletedAssignment = new UserRole(userId, removedRole.Id, null) { Version = 1, Role = removedRole };
        softDeletedAssignment.SoftDelete();

        var userRoles = new List<UserRole> { activeAssignment, softDeletedAssignment }.AsQueryable();

        var mockSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(c => c.Set<UserRole>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        var result = await repository.GetUserRolesAsync(userId);

        // Assert
        result.Should().ContainSingle(r => r.Id == role.Id);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_WhenAssignmentIsSoftDeleted_RestoresIt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var assignedBy = Guid.NewGuid();
        var existing = new UserRole(userId, roleId, null) { Version = 1 };
        existing.SoftDelete();
        var userRoles = new List<UserRole> { existing }.AsQueryable();

        var mockSet = CreateMockDbSet(userRoles);
        _mockContext.Setup(c => c.Set<UserRole>()).Returns(mockSet.Object);

        var repository = new RoleRepository(_mockContext.Object);

        // Act
        await repository.AssignRoleToUserAsync(new UserRole(userId, roleId, assignedBy));

        // Assert - the soft-deleted assignment is restored, not duplicated
        // (protecting the unique (UserId, RoleId) index).
        existing.IsDeleted.Should().BeFalse();
        existing.DeletedAt.Should().BeNull();
        existing.AssignedBy.Should().Be(assignedBy);
        mockSet.Verify(s => s.Add(It.IsAny<UserRole>()), Times.Never);
        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // Helper method to create mock DbSet with async support
    private Mock<DbSet<T>> CreateMockDbSet<T>(IQueryable<T> data) where T : class
    {
        return data.BuildMockDbSet();
    }
}
