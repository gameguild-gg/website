using Xunit;
using cms.Common.Entities;
using cms.Modules.User.Models;

namespace cms.Tests.Common.Entities;

public class BaseEntityTests
{
    [Fact]
    public void BaseEntity_Constructor_SetsTimestampsAndGuid()
    {
        // Arrange & Act
        var user = new User();

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id); // Should have a generated GUID
        Assert.True(user.IsNew); // New entities should return true for IsNew
        Assert.True(user.CreatedAt <= DateTime.UtcNow);
        Assert.True(user.UpdatedAt <= DateTime.UtcNow);
        Assert.Equal(user.CreatedAt.Ticks, user.UpdatedAt.Ticks, TimeSpan.FromMilliseconds(10).Ticks);
        Assert.Null(user.DeletedAt);
        Assert.False(user.IsDeleted);
        Assert.Equal(0, user.Version);
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
        Assert.True(user.IsNew);
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
    public void BaseEntity_IsNew_WorksCorrectlyWithGeneratedGuids()
    {
        // Arrange
        var user = new User();

        // Act & Assert
        // Since BaseEntity generates a GUID in constructor, IsNew will be false
        // This is the intended behavior - entities with generated IDs are not considered "new"
        // in the sense of needing ID generation
        Assert.False(user.IsNew);
        Assert.NotEqual(Guid.Empty, user.Id);
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
}
