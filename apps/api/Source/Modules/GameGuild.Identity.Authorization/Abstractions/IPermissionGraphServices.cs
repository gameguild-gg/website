namespace GameGuild.Identity.Authorization;

/// <summary>
///     Builds a read-only, graph-shaped view of a tenant's permission structure
///     (users, dynamic roles, permission keys, inheritance, denies, direct grants)
///     for visualization and data-quality inspection.
/// </summary>
public interface IPermissionGraphService
{
    /// <summary>
    ///     Builds the permission graph of a tenant scope.
    /// </summary>
    /// <param name="tenantId">Tenant scope (null = global scope).</param>
    /// <param name="includeUsers">Whether to include user nodes and their edges.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The permission graph with nodes, edges, and a data-quality summary.</returns>
    Task<Models.PermissionGraph> BuildGraphAsync(
        Guid? tenantId,
        bool includeUsers,
        CancellationToken ct = default);
}

/// <summary>
///     Simulates permission changes (role deletion, permission-key removal) and reports
///     which users would lose or retain access. Purely read-only: nothing is persisted.
/// </summary>
public interface IPermissionImpactAnalysisService
{
    /// <summary>
    ///     Simulates the deletion of a dynamic role and reports its impact.
    /// </summary>
    /// <param name="tenantId">Tenant scope (null = global scope).</param>
    /// <param name="roleId">The role to simulate deleting.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The simulated impact, including affected users and severity.</returns>
    Task<Models.RoleDeletionImpact> AnalyzeRoleDeletionAsync(
        Guid? tenantId,
        Guid roleId,
        CancellationToken ct = default);

    /// <summary>
    ///     Simulates removing a permission key from a role and reports its impact.
    /// </summary>
    /// <param name="tenantId">Tenant scope (null = global scope).</param>
    /// <param name="roleId">The role the key would be removed from.</param>
    /// <param name="permissionKey">The concrete permission key under analysis.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The simulated impact, including affected users and severity.</returns>
    Task<Models.PermissionRemovalImpact> AnalyzePermissionRemovalAsync(
        Guid? tenantId,
        Guid roleId,
        string permissionKey,
        CancellationToken ct = default);
}
