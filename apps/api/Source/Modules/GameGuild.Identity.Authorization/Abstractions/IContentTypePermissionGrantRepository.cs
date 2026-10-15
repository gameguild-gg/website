namespace GameGuild.Identity.Authorization;

/// <summary>
///     Persistence contract for <see cref="ContentTypePermissionGrant"/> rows — the
///     content-type layer's scoped grant store (issues #360/#365). Implementations must be
///     tenant-isolated and must never return soft-deleted rows on the read paths.
/// </summary>
public interface IContentTypePermissionGrantRepository
{
    /// <summary>Creates a new grant row.</summary>
    Task<ContentTypePermissionGrant> CreateAsync(
        ContentTypePermissionGrant grant,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing grant row.</summary>
    Task<ContentTypePermissionGrant> UpdateAsync(
        ContentTypePermissionGrant grant,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Soft-deletes a grant row (permission history preservation). Returns false when
    ///     the id is unknown or the row is already deleted.
    /// </summary>
    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a live grant by id.</summary>
    Task<ContentTypePermissionGrant?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads the exact grant row for one subject scope (user id or null for the tenant
    ///     default), tenant and content type. Null when no such row exists. This is the
    ///     effective-resolution lookup: callers apply activity/expiration themselves
    ///     (fail-closed).
    /// </summary>
    Task<ContentTypePermissionGrant?> GetBySubjectAndContentTypeAsync(
        Guid? userId,
        Guid? tenantId,
        string contentTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads every live grant row for one tenant (administration and listing).
    /// </summary>
    Task<List<ContentTypePermissionGrant>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads every live grant row for one tenant and content type (both user-specific
    ///     and tenant-default rows).
    /// </summary>
    Task<List<ContentTypePermissionGrant>> GetByContentTypeAsync(
        Guid tenantId,
        string contentTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads every live grant row for one user across tenants (cross-tenant callers
    ///     must re-check tenant scope before acting on a row).
    /// </summary>
    Task<List<ContentTypePermissionGrant>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
