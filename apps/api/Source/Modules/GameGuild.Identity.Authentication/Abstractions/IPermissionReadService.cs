using GameGuild.Identity.Authorization;

namespace GameGuild.Identity.Authentication;

/// <summary>Validated, actor-scoped permission records and audit history (issue #340).</summary>
/// <remarks>
/// These operations return direct grant records, not inferred effective permissions. Existing
/// permission evaluation remains authoritative. Validation and authorization failures return a
/// typed <see cref="Result{TValue}"/>; cancellation and persistence failures propagate.
/// Reads are untracked and page in the database. A count and page describe the current dataset,
/// not a frozen snapshot shared across requests. A null query is a caller programming error
/// and throws <see cref="ArgumentNullException"/> rather than returning a data-access result.
/// </remarks>
public interface IPermissionReadService
{
    /// <summary>List tenant grants in descending grant-time and ascending identifier order.</summary>
    /// <param name="query">Explicit subject, tenant, lifetime and page filters.</param>
    /// <param name="cancellationToken">Cancels every database operation.</param>
    /// <returns>The selected records and the filtered total, or a typed validation/access error.</returns>
    Task<Result<PagedResult<TenantPermission>>> QueryTenantPermissionsAsync(
        TenantPermissionListQuery query, CancellationToken cancellationToken = default);

    /// <summary>List content-type grants, optionally restricted to an exact registered name.</summary>
    /// <param name="query">Scope, exact content-type name, lifetime and page filters.</param>
    /// <param name="cancellationToken">Cancels every database operation.</param>
    /// <returns>The selected records and the filtered total, or a typed validation/access error.</returns>
    Task<Result<PagedResult<ContentTypePermission>>> QueryContentTypePermissionsAsync(
        ContentTypePermissionListQuery query, CancellationToken cancellationToken = default);

    /// <summary>List grants for an explicitly mapped resource permission type.</summary>
    /// <typeparam name="TPermission">The resource permission entity already registered in the model.</typeparam>
    /// <typeparam name="TResource">Its resource type; no caller-supplied type name is executed.</typeparam>
    /// <param name="query">Scope, exact resource identifier/type, lifetime and page filters.</param>
    /// <param name="cancellationToken">Cancels every database operation.</param>
    /// <returns>The selected records and the filtered total, or a typed validation/access error.</returns>
    Task<Result<PagedResult<TPermission>>> QueryResourcePermissionsAsync<TPermission, TResource>(
        ResourcePermissionListQuery query, CancellationToken cancellationToken = default)
        where TPermission : ResourcePermission<TResource>
        where TResource : EntityBase;

    /// <summary>List stored audit records in descending event-time and ascending identifier order.</summary>
    /// <param name="query">Scope, inclusive UTC bounds, stored event filters and page limits.</param>
    /// <param name="cancellationToken">Cancels every database operation.</param>
    /// <returns>Exact stored evidence without inventing a single permission for a bulk event.</returns>
    /// <remarks>Another subject's history, default-subject history and all-subject history require SystemAdmin.</remarks>
    Task<Result<PagedResult<PermissionAuditLog>>> QueryPermissionHistoryAsync(
        PermissionAuditHistoryQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Additive version of the three-layer permission contract. Existing providers and callers of
/// <see cref="IPermissionService"/> keep their original source and return-type contract.
/// </summary>
public interface IPermissionServiceV2 : IPermissionService, IPermissionReadService
{
}
