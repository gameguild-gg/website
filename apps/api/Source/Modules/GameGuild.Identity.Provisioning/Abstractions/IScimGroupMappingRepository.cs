namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Persistence for <see cref="ScimGroupMapping"/> rows plus the flattened
///     <see cref="ScimGroupView"/> projections the SCIM group surface queries.
///     Mutation methods stage changes on the tracked context; the caller decides when to
///     save so membership operations stay atomic.
/// </summary>
public interface IScimGroupMappingRepository
{
    Task<ScimGroupMapping?> GetByExternalIdAsync(Guid tenantId, string externalId, CancellationToken cancellationToken = default);

    Task<ScimGroupMapping?> GetByRoleIdAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Resolved SCIM view for one provisioned group (tenant-scoped, non-deleted).</summary>
    Task<ScimGroupView?> GetViewAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Queryable view of every non-deleted SCIM-mapped group in the tenant.</summary>
    IQueryable<ScimGroupView> QueryTenantGroups(Guid tenantId);

    void Add(ScimGroupMapping mapping);

    void Remove(ScimGroupMapping mapping);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
