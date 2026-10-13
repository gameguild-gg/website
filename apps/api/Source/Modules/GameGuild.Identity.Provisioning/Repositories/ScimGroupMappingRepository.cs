using GameGuild.Identity.Authentication;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Repository implementation for SCIM group mappings (tenant roles) and their
///     read-side projections.
/// </summary>
public class ScimGroupMappingRepository(IApplicationDbContext context) : IScimGroupMappingRepository
{
    private DbSet<ScimGroupMapping> Mappings => context.Set<ScimGroupMapping>();
    private DbSet<Role> Roles => context.Set<Role>();

    public async Task<ScimGroupMapping?> GetByExternalIdAsync(Guid tenantId, string externalId, CancellationToken cancellationToken = default)
    {
        return await Mappings
            .FirstOrDefaultAsync(mapping => mapping.TenantId == tenantId
                                            && mapping.ExternalId == externalId
                                            && mapping.DeletedAt == null,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScimGroupMapping?> GetByRoleIdAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        return await Mappings
            .FirstOrDefaultAsync(mapping => mapping.TenantId == tenantId
                                            && mapping.RoleId == roleId
                                            && mapping.DeletedAt == null,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScimGroupView?> GetViewAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        return await QueryTenantGroups(tenantId)
            .FirstOrDefaultAsync(view => view.RoleId == roleId, cancellationToken).ConfigureAwait(false);
    }

    public IQueryable<ScimGroupView> QueryTenantGroups(Guid tenantId)
    {
        // Same pair-then-Select shape as ScimUserMappingRepository: keeps downstream
        // Where/OrderBy/Skip/Take translatable by EF Core on relational providers.
        return Mappings
            .AsNoTracking()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DeletedAt == null)
            .Join(Roles.AsNoTracking(),
                mapping => mapping.RoleId,
                role => role.Id,
                (mapping, role) => new { Mapping = mapping, Role = role })
            .Select(joined => new ScimGroupView
            {
                RoleId = joined.Role.Id,
                ExternalId = joined.Mapping.ExternalId,
                DisplayName = joined.Role.Name,
                Description = joined.Role.Description,
                Active = joined.Role.IsActive && joined.Role.DeletedAt == null,
                CreatedAt = joined.Role.CreatedAt,
                UpdatedAt = joined.Role.UpdatedAt
            });
    }

    public void Add(ScimGroupMapping mapping) => Mappings.Add(mapping);

    public void Remove(ScimGroupMapping mapping) => Mappings.Remove(mapping);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
