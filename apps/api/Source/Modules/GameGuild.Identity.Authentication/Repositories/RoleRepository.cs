using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository implementation for Role entities
/// </summary>
public class RoleRepository(IApplicationDbContext context) : IRoleRepository
{
    private DbSet<Role> Roles => context.Set<Role>();
    private DbSet<UserRole> UserRoles => context.Set<UserRole>();

    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Role>> GetAllAsync(Guid? tenantId = null, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = Roles.AsNoTracking();

        if (tenantId.HasValue)
        {
            query = query.Where(r => r.TenantId == tenantId.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Role?> GetByNameAsync(string name, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var query = Roles.AsNoTracking();

        if (tenantId.HasValue)
        {
            query = query.Where(r => r.Name.ToLower() == name.ToLower() && r.TenantId == tenantId.Value);
        }
        else
        {
            query = query.Where(r => r.Name.ToLower() == name.ToLower() && r.TenantId == null);
        }

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Role> AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        role.Touch();

        Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return role;
    }

    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        role.Touch();

        Roles.Update(role);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role != null)
        {
            Roles.Remove(role);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> ExistsByNameAsync(string name, Guid? tenantId = null, Guid? excludeRoleId = null, CancellationToken cancellationToken = default)
    {
        var query = Roles.AsNoTracking();

        if (tenantId.HasValue)
        {
            query = query.Where(r => r.Name.ToLower() == name.ToLower() && r.TenantId == tenantId.Value);
        }
        else
        {
            query = query.Where(r => r.Name.ToLower() == name.ToLower() && r.TenantId == null);
        }

        if (excludeRoleId.HasValue)
        {
            query = query.Where(r => r.Id != excludeRoleId.Value);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<Role>> GetUserRolesAsync(Guid userId, bool includeExpired = false, CancellationToken cancellationToken = default)
    {
        var query = UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.DeletedAt == null);

        if (!includeExpired)
        {
            var now = SystemClock.UtcNow;
            query = query.Where(ur => ur.ExpiresAt == null || ur.ExpiresAt > now);
        }

        return await query
            .Include(ur => ur.Role)
            .Where(ur => ur.Role != null && ur.Role.IsActive)
            .Select(ur => ur.Role!)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserRole> AssignRoleToUserAsync(UserRole userRole, CancellationToken cancellationToken = default)
    {
        // Check if the role is already assigned to the user (soft-deleted assignments are
        // included so they can be restored instead of hitting the unique index).
        var existingUserRole = await UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userRole.UserId && ur.RoleId == userRole.RoleId, cancellationToken).ConfigureAwait(false);

        if (existingUserRole != null)
        {
            if (existingUserRole.IsExpired() || existingUserRole.IsDeleted)
            {
                existingUserRole.AssignedBy = userRole.AssignedBy;
                existingUserRole.AssignedAt = SystemClock.UtcNow;
                existingUserRole.ExpiresAt = userRole.ExpiresAt;

                // Restore soft-deleted assignments (#357): permission history is preserved,
                // the row is reactivated and audited through the normal update path.
                if (existingUserRole.IsDeleted)
                {
                    existingUserRole.Restore();
                }

                existingUserRole.Touch();
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return existingUserRole;
        }

        userRole.Touch();
        userRole.AssignedAt = SystemClock.UtcNow;

        UserRoles.Add(userRole);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return userRole;
    }

    public async Task<IReadOnlyList<BulkRoleAssignmentItemResult>> BulkAssignRoleToUsersAsync(
        Guid roleId,
        IReadOnlyCollection<Guid> userIds,
        Guid? assignedBy,
        DateTime? expiresAt,
        CancellationToken cancellationToken)
    {
        var uniqueUserIds = userIds.Distinct().ToArray();
        if (uniqueUserIds.Length == 0)
        {
            return Array.Empty<BulkRoleAssignmentItemResult>();
        }

        var existingAssignments = await UserRoles
            .Where(userRole => userRole.RoleId == roleId && uniqueUserIds.Contains(userRole.UserId))
            .ToDictionaryAsync(userRole => userRole.UserId, cancellationToken)
            .ConfigureAwait(false);

        var now = SystemClock.UtcNow;
        var results = new List<BulkRoleAssignmentItemResult>(uniqueUserIds.Length);
        var hasChanges = false;

        foreach (var userId in uniqueUserIds)
        {
            if (existingAssignments.TryGetValue(userId, out var existing))
            {
                if (!existing.IsDeleted && (!existing.ExpiresAt.HasValue || existing.ExpiresAt.Value > now))
                {
                    results.Add(new BulkRoleAssignmentItemResult(
                        userId, existing.Id, BulkRoleAssignmentStatus.AlreadyAssigned, existing.AssignedAt, existing.ExpiresAt));
                    continue;
                }

                existing.AssignedBy = assignedBy;
                existing.AssignedAt = now;
                existing.ExpiresAt = expiresAt;

                // Restore soft-deleted assignments (#357) instead of failing the unique index.
                if (existing.IsDeleted)
                {
                    existing.Restore();
                }

                existing.Touch();
                hasChanges = true;
                results.Add(new BulkRoleAssignmentItemResult(
                    userId, existing.Id, BulkRoleAssignmentStatus.Reactivated, existing.AssignedAt, existing.ExpiresAt));
                continue;
            }

            var assignment = new UserRole(userId, roleId, assignedBy)
            {
                Id = Guid.NewGuid(),
                ExpiresAt = expiresAt
            };
            assignment.Touch();
            UserRoles.Add(assignment);
            hasChanges = true;
            results.Add(new BulkRoleAssignmentItemResult(
                userId, assignment.Id, BulkRoleAssignmentStatus.Assigned, assignment.AssignedAt, assignment.ExpiresAt));
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    /// <summary>
    ///     Removes a role assignment from a user using a soft delete (#357): the
    ///     <c>UserRole</c> row is preserved for permission-history tracking instead of being
    ///     physically deleted. The centralized permission-audit hook in
    ///     <c>ApplicationDbContext</c> captures the resulting Modified entry as a permission
    ///     change, and re-assignment restores the soft-deleted row (see
    ///     <see cref="AssignRoleToUserAsync"/> and <see cref="BulkAssignRoleToUsersAsync"/>).
    /// </summary>
    public async Task RemoveRoleFromUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var userRole = await UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (userRole != null)
        {
            userRole.SoftDelete();
            UserRoles.Update(userRole);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> UserHasRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        return await UserRoles
            .AsNoTracking()
            .AnyAsync(ur => ur.UserId == userId
                && ur.RoleId == roleId
                && ur.DeletedAt == null
                && (ur.ExpiresAt == null || ur.ExpiresAt > now),
                cancellationToken).ConfigureAwait(false);
    }
}
