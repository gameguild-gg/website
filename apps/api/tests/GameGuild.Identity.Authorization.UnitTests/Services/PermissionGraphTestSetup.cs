using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Shared repository-mock plumbing for the permission graph and impact analysis
///     tests (issue #334). Mirrors the exact loader call surface of
///     <c>TenantAuthorizationSnapshotLoader</c>.
/// </summary>
public sealed class PermissionGraphTestSetup
{
    public readonly Mock<IDynamicRoleRepository> Roles = new();
    public readonly Mock<IDynamicRoleAssignmentRepository> Assignments = new();
    public readonly Mock<ITenantPermissionRepository> TenantPermissions = new();

    public Guid TenantId { get; } = Guid.NewGuid();

    public PermissionGraphService CreateGraphService()
        => new(Roles.Object, Assignments.Object, TenantPermissions.Object);

    public PermissionImpactAnalysisService CreateImpactService()
        => new(Roles.Object, Assignments.Object, TenantPermissions.Object);

    public void SetupData(
        IReadOnlyList<DynamicRole> roles,
        IReadOnlyList<DynamicRoleAssignment>? assignments = null,
        List<TenantPermission>? tenantRows = null,
        TenantPermission? globalDefault = null)
    {
        Roles.Setup(r => r.GetByTenantAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);

        Assignments.Setup(r => r.GetByTenantAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignments ?? []);

        TenantPermissions.Setup(r => r.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(globalDefault);

        TenantPermissions.Setup(r => r.GetByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantRows ?? []);
    }

    public static DynamicRole Role(
        Guid id,
        string name,
        string[] permissions,
        Guid? parentRoleId = null,
        bool isActive = true,
        bool isSystem = false,
        string[]? denyPermissions = null) => new()
    {
        Id = id,
        Name = name,
        DisplayName = name,
        Permissions = permissions,
        DenyPermissions = denyPermissions ?? [],
        ParentRoleId = parentRoleId,
        IsActive = isActive,
        IsSystem = isSystem
    };

    public static DynamicRoleAssignment Assignment(Guid userId, Guid roleId, Guid? tenantId) => new()
    {
        UserId = userId,
        RoleId = roleId,
        TenantId = tenantId,
        IsActive = true
    };

    public static TenantPermission DirectGrant(Guid userId, Guid tenantId, string[] permissions, string[]? denies = null) => new()
    {
        UserId = userId,
        TenantId = tenantId,
        Permissions = permissions,
        DenyPermissions = denies ?? [],
        IsActive = true
    };

    public static TenantPermission TenantDefault(Guid tenantId, string[] permissions, string[]? denies = null) => new()
    {
        UserId = null,
        TenantId = tenantId,
        Permissions = permissions,
        DenyPermissions = denies ?? [],
        IsActive = true
    };
}
