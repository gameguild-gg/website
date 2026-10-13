using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GameGuild.API.UnitTests.Database;

/// <summary>
///     Model-level tests for the unified <see cref="PermissionBase"/> permission-entity
///     hierarchy (#352/#357), asserted against the real application EF model:
///     every mapped permission table carries the common inherited column block (the
///     schema-level expression of permission inheritance), the divergent
///     subject/activity storage on <see cref="ResourceUserPermission"/> maps to a single
///     column per name, and the previously-unmapped <see cref="GenericResourcePermission"/>
///     is now part of the runtime model.
/// </summary>
public sealed class PermissionBaseModelTests
{
    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .Options);

    private static IModel BuildModel()
    {
        using var context = CreateContext();

        return context.Model;
    }

    [Fact]
    public void ModelContext_RequiresNoCredentialsAndKeepsConnectionClosed()
    {
        using var context = CreateContext();
        var connection = context.Database.GetDbConnection();
        context.Database.GetConnectionString().Should().Be("Host=localhost;Database=model_only");
        connection.State.Should().Be(System.Data.ConnectionState.Closed);

        context.Model.GetEntityTypes().Should().NotBeEmpty();

        connection.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public void EveryPermissionEntity_InheritsFromPermissionBase_InTheRuntimeModel()
    {
        var model = BuildModel();

        var permissionEntities = new[]
        {
            typeof(TenantPermission),
            typeof(ContentTypePermission),
            typeof(GenericResourcePermission),
            typeof(ResourceUserPermission)
        };

        foreach (var entity in permissionEntities)
        {
            model.FindEntityType(entity).Should().NotBeNull(
                $"{entity.Name} must be part of the runtime EF model");
            entity.IsAssignableTo(typeof(PermissionBase)).Should().BeTrue(
                $"{entity.Name} must inherit from PermissionBase");
        }
    }

    [Fact]
    public void TenantPermission_TableCarriesTheCommonInheritedColumnBlock()
    {
        var entityType = BuildModel().FindEntityType(typeof(TenantPermission));

        foreach (var column in new[] { "Id", "UserId", "TenantId", "ExpiresAt", "IsActive", "GrantedAt", "CreatedAt", "UpdatedAt", "DeletedAt", "Version" })
        {
            entityType!.FindProperty(column).Should().NotBeNull(
                $"TenantPermissions must keep carrying the inherited '{column}' column of the permission hierarchy");
        }
    }

    [Fact]
    public void ContentTypePermission_TableCarriesTheCommonInheritedColumnBlock()
    {
        var entityType = BuildModel().FindEntityType(typeof(ContentTypePermission));

        foreach (var column in new[] { "Id", "UserId", "TenantId", "ExpiresAt", "IsActive", "GrantedAt", "CreatedAt", "UpdatedAt", "DeletedAt", "Version" })
        {
            entityType!.FindProperty(column).Should().NotBeNull(
                $"contenttypepermission must keep carrying the inherited '{column}' column of the permission hierarchy");
        }
    }

    [Fact]
    public void ResourceUserPermission_TableCarriesTheCommonInheritedColumnBlock()
    {
        var entityType = BuildModel().FindEntityType(typeof(ResourceUserPermission));

        // Activity is computed from the revocation state on this entity (IsActive stays
        // unmapped), so the common block here is the universal set without IsActive.
        foreach (var column in new[] { "Id", "UserId", "TenantId", "ExpiresAt", "GrantedAt", "CreatedAt", "UpdatedAt", "DeletedAt", "Version" })
        {
            entityType!.FindProperty(column).Should().NotBeNull(
                $"ResourceUserPermission must keep carrying the inherited '{column}' column of the permission hierarchy");
        }

        // The `new`-hidden subject property must not duplicate the base column.
        entityType!.GetProperties().Should().ContainSingle(p => p.Name == "UserId");
        entityType.GetProperties().Where(p => p.Name == "IsActive").Should().BeEmpty();
    }

    [Fact]
    public void GenericResourcePermission_IsMapped_AndCarriesTheCommonColumnBlock()
    {
        var entityType = BuildModel().FindEntityType(typeof(GenericResourcePermission));

        entityType.Should().NotBeNull(
            "GenericResourcePermission must be mapped so Set<GenericResourcePermission>() works at runtime (bulk resource grants)");

        foreach (var column in new[] { "Id", "UserId", "TenantId", "ExpiresAt", "IsActive", "GrantedAt", "CreatedAt", "UpdatedAt", "DeletedAt", "Version" })
        {
            entityType!.FindProperty(column).Should().NotBeNull(
                $"genericresourcepermission must carry the inherited '{column}' column of the permission hierarchy");
        }

        entityType!.FindProperty(nameof(GenericResourcePermission.ResourceId)).Should().NotBeNull();
        entityType.FindProperty(nameof(GenericResourcePermission.ResourceType)).Should().NotBeNull();
        entityType.GetIndexes().Should().NotBeEmpty();
    }
}
