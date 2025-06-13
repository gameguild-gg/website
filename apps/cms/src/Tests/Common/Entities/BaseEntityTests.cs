using Xunit;
using Microsoft.EntityFrameworkCore;
using GameGuild.Common.Entities;
using GameGuild.Data;
using GameGuild.Modules.User.Models;

namespace GameGuild.Tests.Common.Entities;

public class BaseEntityTests
{
    [Fact]
    public void BaseEntity_Constructor_SetsTimestampsAndGuid()
    {
        // Arrange & Act
        var user = new User();
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(0, user.Version);
        Assert.True(user.IsNew);
        Assert.True(user.CreatedAt <= DateTime.UtcNow);
        Assert.True(user.UpdatedAt <= DateTime.UtcNow);
        Assert.Equal(user.CreatedAt.Ticks, user.UpdatedAt.Ticks, TimeSpan.FromMilliseconds(10).Ticks);
        Assert.Null(user.DeletedAt);
        Assert.False(user.IsDeleted);
    }

    [Fact]
    public void BaseEntity_PartialConstructor_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var user = new User(
            new
            {
                Name = "John Doe", Email = "john@example.com", IsActive = false
            }
        );

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("John Doe", user.Name);
        Assert.Equal("john@example.com", user.Email);
        Assert.False(user.IsActive);
        Assert.True(user.CreatedAt <= DateTime.UtcNow);
        Assert.True(user.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void BaseEntity_Touch_UpdatesTimestamp()
    {
        // Arrange
        var user = new User();
        DateTime originalUpdatedAt = user.UpdatedAt;

        // Wait a bit to ensure timestamp difference
        Thread.Sleep(10);

        // Act
        user.Touch();

        // Assert
        Assert.True(user.UpdatedAt > originalUpdatedAt);
        Assert.Equal(user.CreatedAt.Ticks, originalUpdatedAt.Ticks, TimeSpan.FromMilliseconds(10).Ticks); // CreatedAt should not change
    }

    [Fact]
    public void BaseEntity_SetProperties_UpdatesPropertiesAndTimestamp()
    {
        // Arrange
        var user = new User();
        DateTime originalUpdatedAt = user.UpdatedAt;
        Thread.Sleep(10);

        var properties = new Dictionary<string, object?>
        {
            {
                nameof(User.Name), "John Doe"
            },
            {
                nameof(User.Email), "john@example.com"
            },
            {
                nameof(User.IsActive), false
            }
        };

        // Act
        user.SetProperties(properties);

        // Assert
        Assert.Equal("John Doe", user.Name);
        Assert.Equal("john@example.com", user.Email);
        Assert.False(user.IsActive);
        Assert.True(user.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void BaseEntity_Create_WithProperties_CreatesInstanceCorrectly()
    {
        // Arrange
        var properties = new Dictionary<string, object?>
        {
            {
                nameof(User.Name), "Jane Doe"
            },
            {
                nameof(User.Email), "jane@example.com"
            }
        };

        // Act
        var user = BaseEntity.Create<User>(properties);

        // Assert
        Assert.Equal("Jane Doe", user.Name);
        Assert.Equal("jane@example.com", user.Email);
        Assert.True(user.IsActive); // Default value
        Assert.True(user.IsNew);
    }

    [Fact]
    public void BaseEntity_Create_WithoutProperties_CreatesInstanceWithDefaults()
    {
        // Act
        var user = BaseEntity.Create<User>();

        // Assert
        Assert.Equal(string.Empty, user.Name);
        Assert.Equal(string.Empty, user.Email);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void BaseEntity_SoftDelete_SetsDeletedAtAndIsDeleted()
    {
        // Arrange
        var user = new User();

        // Act
        user.SoftDelete();

        // Assert
        Assert.True(user.IsDeleted);
        Assert.NotNull(user.DeletedAt);
        Assert.True(user.DeletedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void BaseEntity_Restore_ClearsDeletedAtAndIsDeleted()
    {
        // Arrange
        var user = new User();
        user.SoftDelete();

        // Act
        user.Restore();

        // Assert
        Assert.False(user.IsDeleted);
        Assert.Null(user.DeletedAt);
    }

    [Fact]
    public void BaseEntity_ToDictionary_ReturnsAllProperties()
    {
        // Arrange
        var user = new User
        {
            Name = "Test User", Email = "test@example.com", IsActive = false
        };

        // Act
        var dictionary = user.ToDictionary();

        // Assert
        Assert.True(dictionary.Count >= 8); // Id, Name, Email, IsActive, CreatedAt, UpdatedAt, DeletedAt, Version
        Assert.NotNull(dictionary[nameof(User.Id)]);
        Assert.Equal("Test User", dictionary[nameof(User.Name)]);
        Assert.Equal("test@example.com", dictionary[nameof(User.Email)]);
        Assert.Equal(false, dictionary[nameof(User.IsActive)]);
        Assert.NotNull(dictionary[nameof(User.CreatedAt)]);
        Assert.NotNull(dictionary[nameof(User.UpdatedAt)]);
    }

    [Fact]
    public void BaseEntity_ToString_ReturnsFormattedString()
    {
        // Arrange
        var user = new User();

        // Act
        var result = user.ToString();

        // Assert
        Assert.Contains("User", result);
        Assert.Contains("Id =", result);
        Assert.Contains("CreatedAt", result);
        Assert.Contains("UpdatedAt", result);
    }

    [Fact]
    public void BaseEntity_IsNew_ReturnsTrueForEmptyGuid()
    {
        // Arrange
        var user = new User
        {
            // Manually set to empty GUID to test the IsNew logic
            Id = Guid.Empty
        };

        // Act & Assert
        Assert.True(user.IsNew);
    }

    [Fact]
    public async Task BaseEntity_SaveToDatabase_IncrementsVersion()
    {
        // Arrange - Create in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        
        // Create new entity
        var user = new User 
        { 
            Name = "Test User", 
            Email = "test@example.com" 
        };
        
        // Assert - Before saving to database
        Assert.Equal(0, user.Version); // New entity starts with version 0
        Assert.True(user.IsNew); // Should be new
        Assert.NotEqual(Guid.Empty, user.Id); // Should have generated GUID
        
        // Act - Save to database
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        // Assert - After saving to database
        Assert.NotEqual(0, user.Version); // Version should be incremented by ApplicationDbContext.UpdateTimestamps()
        Assert.False(user.IsNew); // Still not new 
    }

    [Fact]
    public async Task BaseEntity_UpdateEntity_IncrementsVersionAgain()
    {
        // Arrange - Create in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        
        // Create and save entity
        var user = new User 
        { 
            Name = "Test User", 
            Email = "test@example.com" 
        };

        // should be new
        Assert.True(user.IsNew);
        
        context.Users.Add(user);
        await context.SaveChangesAsync();

        int version = user.Version; // Capture initial version
        Assert.NotEqual(0, user.Version); // After first save
        
        // Act - Update and save again
        user.Name = "Updated User";
        await context.SaveChangesAsync();
        
        // Assert - Version should increment again
        Assert.NotEqual(version, user.Version); // After second save
    }

    [Fact]
    public async Task BaseEntity_MultipleEntities_EachHasOwnVersion()
    {
        // Arrange - Create in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        
        // Create multiple entities
        var user1 = new User { Name = "User 1", Email = "user1@example.com" };
        var user2 = new User { Name = "User 2", Email = "user2@example.com" };
        
        // Both start with version 0
        Assert.Equal(0, user1.Version);
        Assert.Equal(0, user2.Version);
        
        // Act - Save both
        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();
        
        // Assert - Both should have version 1
        Assert.NotEqual(0, user1.Version);
        Assert.NotEqual(0, user2.Version);

        int user1Version = user1.Version;
        int user2Version = user2.Version;
        
        // Update only user1
        user1.Name = "Updated User 1";
        await context.SaveChangesAsync();
        
        // Assert - Only user1's version should increment
        Assert.NotEqual(user1Version, user1.Version);
        Assert.Equal(user2Version, user2.Version);
    }
}
