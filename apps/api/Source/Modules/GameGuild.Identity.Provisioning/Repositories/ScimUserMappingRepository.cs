using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Repository implementation for SCIM user mappings and their read-side projections.
/// </summary>
public class ScimUserMappingRepository(IApplicationDbContext context) : IScimUserMappingRepository
{
    private DbSet<ScimUserMapping> Mappings => context.Set<ScimUserMapping>();
    private DbSet<User> Users => context.Set<User>();

    public async Task<ScimUserMapping?> GetByExternalIdAsync(Guid tenantId, string externalId, CancellationToken cancellationToken = default)
    {
        return await Mappings
            .FirstOrDefaultAsync(mapping => mapping.TenantId == tenantId
                                            && mapping.ExternalId == externalId
                                            && mapping.DeletedAt == null,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScimUserMapping?> GetByUserIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await Mappings
            .FirstOrDefaultAsync(mapping => mapping.TenantId == tenantId
                                            && mapping.UserId == userId
                                            && mapping.DeletedAt == null,
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScimUserView?> GetViewAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await QueryTenantUsers(tenantId)
            .FirstOrDefaultAsync(view => view.UserId == userId, cancellationToken).ConfigureAwait(false);
    }

    public IQueryable<ScimUserView> QueryTenantUsers(Guid tenantId)
    {
        // Join to an intermediate pair and project through an explicit Select node:
        // projecting straight from the Join resultSelector leaves downstream operators
        // (Where/OrderBy/Skip/Take) binding members through the constructor inside the
        // join, which EF Core cannot translate ("The LINQ expression ... could not be
        // translated") — every /scim/v2/Users request failed with 500 on PostgreSQL.
        return Mappings
            .AsNoTracking()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DeletedAt == null)
            .Join(Users.AsNoTracking(),
                mapping => mapping.UserId,
                user => user.Id,
                (mapping, user) => new { Mapping = mapping, User = user })
            .Select(joined => new ScimUserView
            {
                UserId = joined.User.Id,
                ExternalId = joined.Mapping.ExternalId,
                UserName = joined.User.Username,
                Email = joined.User.Email,
                DisplayName = joined.User.Name,
                PhoneNumber = joined.User.PhoneNumber,
                Active = joined.User.IsActive && !joined.User.IsSuspended && joined.User.DeletedAt == null,
                CreatedAt = joined.User.CreatedAt,
                UpdatedAt = joined.User.UpdatedAt
            });
    }

    public void Add(ScimUserMapping mapping) => Mappings.Add(mapping);

    public void Remove(ScimUserMapping mapping) => Mappings.Remove(mapping);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
