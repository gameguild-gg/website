using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.UnitTests.Core;

public sealed class UsernameModelContractTests
{
    [Fact]
    public void GeneratedHandleUsesExistingColumnAndDoesNotPublishTheAllocationHint()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"username-contract-{Guid.NewGuid():N}").Options);
        var model = context.Model.FindEntityType(typeof(User))!;
        var property = model.FindProperty(nameof(User.Username))!;
        Assert.Equal(256, property.GetMaxLength());
        Assert.True(property.IsNullable); // Existing accounts with null handles remain supported.
        var index = Assert.Single(model.GetIndexes(), candidate =>
            candidate.Properties.Count == 1 && candidate.Properties[0].Name == nameof(User.Username));
        Assert.True(index.IsUnique);
        Assert.Null(index.GetFilter()); // Deleted users retain their handles under the existing index.
        Assert.DoesNotContain(model.GetProperties(), candidate => candidate.Name.Contains("Generated", StringComparison.OrdinalIgnoreCase));

        var user = User.CreateWithPassword("synthetic-model@example.test", "MátHeus Martíns", "synthetic-hash");
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(user));
        Assert.Equal("matheus-martins", json.RootElement.GetProperty(nameof(User.Username)).GetString());
        Assert.DoesNotContain(json.RootElement.EnumerateObject(), member => member.Name.Contains("Generated", StringComparison.OrdinalIgnoreCase));
        Assert.False(json.RootElement.TryGetProperty(nameof(User.PasswordHash), out _));
    }
}
