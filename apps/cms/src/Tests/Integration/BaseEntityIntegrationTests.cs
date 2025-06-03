using Microsoft.EntityFrameworkCore;
using cms.Data;
using cms.Modules.User.Models;
using cms.Modules.User.Services;
using Xunit;

namespace cms.Tests.Integration;

public class BaseEntityIntegrationTests
{
    private ApplicationDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task User_BaseEntityProperties_ShouldWork()
    {
        // Arrange
        using ApplicationDbContext context = GetInMemoryContext();
        var userService = new UserService(context);

        // Act - Create user using BaseEntity constructor pattern
        var user = new User(
            new
            {
                Name = "Test User", Email = "test@example.com"
            }
        );
        User createdUser = await userService.CreateUserAsync(user);

        // Assert
        Assert.NotEqual(Guid.Empty, createdUser.Id);
        Assert.Equal("Test User", createdUser.Name);
        Assert.Equal("test@example.com", createdUser.Email);
        Assert.True(createdUser.CreatedAt > DateTime.MinValue);
        Assert.True(createdUser.UpdatedAt > DateTime.MinValue);
        Assert.Null(createdUser.DeletedAt);
        Assert.False(createdUser.IsDeleted);
        Assert.Equal(0, createdUser.Version);
    }

    [Fact]
    public async Task User_SoftDelete_ShouldWork()
    {
        // Arrange
        using ApplicationDbContext context = GetInMemoryContext();
        var userService = new UserService(context);

        var user = new User(
            new
            {
                Name = "Delete Test", Email = "delete@example.com"
            }
        );
        User createdUser = await userService.CreateUserAsync(user);

        // Act
        await userService.SoftDeleteUserAsync(createdUser.Id);

        // Assert
        var activeUsers = await userService.GetAllUsersAsync();
        var deletedUsers = await userService.GetDeletedUsersAsync();

        Assert.DoesNotContain(activeUsers, u => u.Id == createdUser.Id);
        Assert.Contains(deletedUsers, u => u.Id == createdUser.Id);

        User deletedUser = deletedUsers.First(u => u.Id == createdUser.Id);
        Assert.True(deletedUser.IsDeleted);
        Assert.NotNull(deletedUser.DeletedAt);
    }

    [Fact]
    public async Task User_RestoreAfterSoftDelete_ShouldWork()
    {
        // Arrange
        using ApplicationDbContext context = GetInMemoryContext();
        var userService = new UserService(context);

        var user = new User(
            new
            {
                Name = "Restore Test", Email = "restore@example.com"
            }
        );
        User createdUser = await userService.CreateUserAsync(user);

        // Act
        await userService.SoftDeleteUserAsync(createdUser.Id);
        await userService.RestoreUserAsync(createdUser.Id);

        // Assert
        var activeUsers = await userService.GetAllUsersAsync();
        var deletedUsers = await userService.GetDeletedUsersAsync();

        Assert.Contains(activeUsers, u => u.Id == createdUser.Id);
        Assert.DoesNotContain(deletedUsers, u => u.Id == createdUser.Id);

        User restoredUser = activeUsers.First(u => u.Id == createdUser.Id);
        Assert.False(restoredUser.IsDeleted);
        Assert.Null(restoredUser.DeletedAt);
    }
}
