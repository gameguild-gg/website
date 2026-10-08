using GameGuild.Identity.Context.Actors;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Tests for the moderation permission scope (moderate, flag, ban, warn).
///     Verifies typed definitions, registry discovery, convenience constants,
///     static role wiring, and fail-closed permission checks.
/// </summary>
public class ModerationPermissionsTests
{
    public static TheoryData<string> ModerationKeys => new()
    {
        ModerationPermission.Keys.Moderate,
        ModerationPermission.Keys.Flag,
        ModerationPermission.Keys.Ban,
        ModerationPermission.Keys.Warn
    };

    [Theory]
    [InlineData(ModerationPermission.Keys.Moderate, "moderation:moderate")]
    [InlineData(ModerationPermission.Keys.Flag, "moderation:flag")]
    [InlineData(ModerationPermission.Keys.Ban, "moderation:ban")]
    [InlineData(ModerationPermission.Keys.Warn, "moderation:warn")]
    public void Keys_ShouldUseExpectedResourceActionFormat(string key, string expected)
    {
        Assert.Equal(expected, key);
    }

    [Fact]
    public void Keys_ShouldBeDistinct()
    {
        var keys = new[] { ModerationPermission.Keys.Moderate, ModerationPermission.Keys.Flag, ModerationPermission.Keys.Ban, ModerationPermission.Keys.Warn };
        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(ModerationKeys))]
    public void Registry_ShouldContainModerationPermission(string key)
    {
        // The registry auto-discovers typed permissions; every moderation key must be valid.
        Assert.True(PermissionRegistry.IsValidKey(key), $"{key} should be a registered permission");
        Assert.Contains(key, PermissionRegistry.Keys);
    }

    [Fact]
    public void Registry_GetByKey_ShouldReturnModerateWithCorrectMetadata()
    {
        var permission = PermissionRegistry.GetByKey(ModerationPermission.Keys.Moderate);

        Assert.NotNull(permission);
        Assert.Equal("moderation", permission.Resource);
        Assert.Equal("moderate", permission.Action);
        Assert.Null(permission.Scope);
        Assert.NotEmpty(permission.Description);
    }

    [Fact]
    public void Registry_GetByResource_ShouldReturnAllFourModerationPermissions()
    {
        var permissions = PermissionRegistry.GetByResource("moderation").Select(p => p.Key).ToList();

        Assert.Contains(ModerationPermission.Keys.Moderate, permissions);
        Assert.Contains(ModerationPermission.Keys.Flag, permissions);
        Assert.Contains(ModerationPermission.Keys.Ban, permissions);
        Assert.Contains(ModerationPermission.Keys.Warn, permissions);
    }

    [Fact]
    public void Registry_IsValidKey_WithModerationWildcard_ShouldReturnTrue()
    {
        // Wildcards for a registered resource are valid (used by Owner/Admin role grants).
        Assert.True(PermissionRegistry.IsValidKey("moderation:*"));
    }

    [Fact]
    public void Registry_IsValidKey_WithUnknownModerationAction_ShouldReturnFalse()
    {
        // Fail closed: unregistered moderation actions must not validate.
        Assert.False(PermissionRegistry.IsValidKey("moderation:nonexistent"));
    }

    [Fact]
    public void TypedInstances_ShouldCarryExpectedKeys()
    {
        Assert.Equal("moderation:moderate", ModerationPermission.Moderate.Key);
        Assert.Equal("moderation:flag", ModerationPermission.Flag.Key);
        Assert.Equal("moderation:ban", ModerationPermission.Ban.Key);
        Assert.Equal("moderation:warn", ModerationPermission.Warn.Key);
    }

    [Fact]
    public void Permission_ShouldImplicitlyConvertToKey()
    {
        string key = ModerationPermission.Ban;
        Assert.Equal(ModerationPermission.Keys.Ban, key);
    }

    [Fact]
    public void ConvenienceConstants_ShouldMatchTypedKeys()
    {
        Assert.Equal(ModerationPermission.Keys.Moderate, Permissions.ModerationModerate);
        Assert.Equal(ModerationPermission.Keys.Flag, Permissions.ModerationFlag);
        Assert.Equal(ModerationPermission.Keys.Ban, Permissions.ModerationBan);
        Assert.Equal(ModerationPermission.Keys.Warn, Permissions.ModerationWarn);
    }

    [Fact]
    public void ModeratorRole_ShouldHaveAllModerationPermissions()
    {
        Assert.Contains(ModerationPermission.Keys.Moderate, StaticRolePermissions.ModeratorPermissions);
        Assert.Contains(ModerationPermission.Keys.Flag, StaticRolePermissions.ModeratorPermissions);
        Assert.Contains(ModerationPermission.Keys.Ban, StaticRolePermissions.ModeratorPermissions);
        Assert.Contains(ModerationPermission.Keys.Warn, StaticRolePermissions.ModeratorPermissions);
    }

    [Fact]
    public void GetStaticPermissions_ForModerator_ShouldReturnModerationPermissions()
    {
        var permissions = StaticRolePermissions.GetStaticPermissions("Moderator");

        Assert.Contains(ModerationPermission.Keys.Moderate, permissions);
        Assert.Contains(ModerationPermission.Keys.Flag, permissions);
        Assert.Contains(ModerationPermission.Keys.Ban, permissions);
        Assert.Contains(ModerationPermission.Keys.Warn, permissions);
    }

    [Fact]
    public void GetStaticPermissions_ForModeratorCaseInsensitive_ShouldReturnModerationPermissions()
    {
        var permissions = StaticRolePermissions.GetStaticPermissions("MODERATOR");

        Assert.Same(StaticRolePermissions.ModeratorPermissions, permissions);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    public void AdministrativeRoles_ShouldHaveModerationWildcard(string role)
    {
        var permissions = StaticRolePermissions.GetStaticPermissions(role);

        Assert.Contains("moderation:*", permissions);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Contributor")]
    [InlineData("Viewer")]
    [InlineData("Guest")]
    public void NonModeratorRoles_ShouldNotHaveModerationPermissions(string role)
    {
        var permissions = StaticRolePermissions.GetStaticPermissions(role);

        Assert.DoesNotContain(permissions, p => p.StartsWith("moderation:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Actor_WithModerationPermissions_ShouldPassChecks()
    {
        var actor = ActorContextBuilder.ForUser(Guid.NewGuid())
            .WithRole("Moderator")
            .WithPermission(ModerationPermission.Keys.Moderate)
            .WithPermission(ModerationPermission.Keys.Flag)
            .WithPermission(ModerationPermission.Keys.Ban)
            .WithPermission(ModerationPermission.Keys.Warn)
            .Build();

        Assert.True(actor.HasPermission(ModerationPermission.Moderate));
        Assert.True(actor.HasPermission(ModerationPermission.Flag));
        Assert.True(actor.HasPermission(ModerationPermission.Ban));
        Assert.True(actor.HasPermission(ModerationPermission.Warn));
    }

    [Fact]
    public void Actor_WithoutModerationPermissions_ShouldFailClosed()
    {
        var actor = ActorContextBuilder.ForUser(Guid.NewGuid())
            .WithRole("Member")
            .WithPermission("content:read")
            .Build();

        Assert.False(actor.HasPermission(ModerationPermission.Keys.Moderate));
        Assert.False(actor.HasPermission(ModerationPermission.Keys.Flag));
        Assert.False(actor.HasPermission(ModerationPermission.Keys.Ban));
        Assert.False(actor.HasPermission(ModerationPermission.Keys.Warn));
    }

    [Fact]
    public void Actor_WithModerationPermissions_HasAnyPermission_ShouldReturnTrue()
    {
        var actor = ActorContextBuilder.ForUser(Guid.NewGuid())
            .WithPermission(ModerationPermission.Keys.Warn)
            .Build();

        Assert.True(actor.HasAnyPermission(ModerationPermission.Keys.Moderate, ModerationPermission.Keys.Warn));
    }

    [Fact]
    public void SystemAdmin_ShouldBypassModerationChecks()
    {
        var actor = ActorContextBuilder.ForSystem("moderation-permission-tests").Build();

        Assert.True(actor.HasPermission(ModerationPermission.Keys.Ban));
    }
}
