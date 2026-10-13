namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Persistence for <see cref="ScimUserMapping"/> rows plus the flattened
///     <see cref="ScimUserView"/> projections the SCIM list/filter surface queries.
///     Mutation methods stage changes on the tracked context; the caller decides when to
///     save so multi-entity provisioning operations stay atomic.
/// </summary>
public interface IScimUserMappingRepository
{
    Task<ScimUserMapping?> GetByExternalIdAsync(Guid tenantId, string externalId, CancellationToken cancellationToken = default);

    Task<ScimUserMapping?> GetByUserIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Resolved SCIM view for one provisioned user (tenant-scoped, non-deleted).</summary>
    Task<ScimUserView?> GetViewAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Queryable view of every non-deleted SCIM-mapped user in the tenant.</summary>
    IQueryable<ScimUserView> QueryTenantUsers(Guid tenantId);

    void Add(ScimUserMapping mapping);

    void Remove(ScimUserMapping mapping);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
