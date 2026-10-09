using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GameGuild.Identity.Authorization.Caching;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Repository for dynamic roles.
/// </summary>
public interface IDynamicRoleRepository
{
    Task<DynamicRole?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DynamicRole?> GetByNameAsync(string name, Guid? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<DynamicRole>> GetByTenantAsync(Guid? tenantId, bool includeGlobal = true, CancellationToken ct = default);
    Task<IReadOnlyList<DynamicRole>> GetActiveByTenantAsync(Guid? tenantId, bool includeGlobal = true, CancellationToken ct = default);
    Task<DynamicRole> CreateAsync(DynamicRole role, CancellationToken ct = default);
    Task UpdateAsync(DynamicRole role, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DynamicRole>> GetRoleHierarchyAsync(Guid roleId, CancellationToken ct = default);
    Task<IReadOnlyList<DynamicRole>> GetManyByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}

/// <summary>
///     Repository for role assignments.
/// </summary>
public interface IDynamicRoleAssignmentRepository
{
    Task<IReadOnlyList<DynamicRoleAssignment>> GetByUserAsync(Guid userId, Guid? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<DynamicRoleAssignment>> GetValidByUserAsync(Guid userId, Guid? tenantId, CancellationToken ct = default);

    /// <summary>
    ///     Gets every role assignment stored in a tenant scope (null = global scope),
    ///     regardless of validity. Used by read-only analysis such as the permission
    ///     graph and impact simulation, which filter validity themselves.
    /// </summary>
    Task<IReadOnlyList<DynamicRoleAssignment>> GetByTenantAsync(Guid? tenantId, CancellationToken ct = default);

    Task<DynamicRoleAssignment> CreateAsync(DynamicRoleAssignment assignment, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task<int> CountByRoleAsync(Guid roleId, CancellationToken ct = default);
}

/// <summary>
///     Database implementation of dynamic role repository.
/// </summary>
public class DynamicRoleRepository(
    IApplicationDbContext context,
    ICacheInvalidationService invalidationService) : IDynamicRoleRepository
{
    private DbSet<DynamicRole> DbSet => context.Set<DynamicRole>();

    public async Task<DynamicRole?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet.Include(r => r.ParentRole).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<DynamicRole?> GetByNameAsync(string name, Guid? tenantId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(r => r.Name == name && r.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<DynamicRole>> GetByTenantAsync(Guid? tenantId, bool includeGlobal = true, CancellationToken ct = default)
    {
        var query = DbSet.AsQueryable();
        if (includeGlobal)
        {
            query = query.Where(r => r.TenantId == tenantId || r.TenantId == null);
        }
        else
        {
            query = query.Where(r => r.TenantId == tenantId);
        }

        return await query.Include(r => r.ParentRole).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DynamicRole>> GetActiveByTenantAsync(Guid? tenantId, bool includeGlobal = true, CancellationToken ct = default)
    {
        var query = DbSet.Where(r => r.IsActive);
        if (includeGlobal)
        {
            query = query.Where(r => r.TenantId == tenantId || r.TenantId == null);
        }
        else
        {
            query = query.Where(r => r.TenantId == tenantId);
        }

        return await query.Include(r => r.ParentRole).ToListAsync(ct);
    }

    public async Task<DynamicRole> CreateAsync(DynamicRole role, CancellationToken ct = default)
    {
        DbSet.Add(role);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        await InvalidateRoleScopesAsync(ct, role.TenantId).ConfigureAwait(false);
        return role;
    }

    public async Task UpdateAsync(DynamicRole role, CancellationToken ct = default)
    {
        // Read the persisted scope before attaching the updated role. A role can move
        // between tenants, and a global role change affects permission caches in every tenant.
        var existingRole = await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(existing => existing.Id == role.Id, ct)
            .ConfigureAwait(false);

        DbSet.Update(role);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        if (existingRole is null)
        {
            await InvalidateRoleScopesAsync(ct, role.TenantId).ConfigureAwait(false);
        }
        else
        {
            await InvalidateRoleScopesAsync(ct, existingRole.TenantId, role.TenantId).ConfigureAwait(false);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await DbSet.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role != null)
        {
            DbSet.Remove(role);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await InvalidateRoleScopesAsync(ct, role.TenantId).ConfigureAwait(false);
        }
    }

    private async Task InvalidateRoleScopesAsync(CancellationToken ct, params Guid?[] tenantIds)
    {
        if (tenantIds.Length == 0 || tenantIds.Any(tenantId => tenantId is null))
        {
            await invalidationService.InvalidateGlobalAsync(ct).ConfigureAwait(false);
            return;
        }

        foreach (var tenantId in tenantIds.Select(id => id!.Value).Distinct())
        {
            await invalidationService.InvalidateTenantAsync(tenantId, ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<DynamicRole>> GetRoleHierarchyAsync(Guid roleId, CancellationToken ct = default)
    {
        var hierarchy = new List<DynamicRole>();
        var currentRole = await GetByIdAsync(roleId, ct).ConfigureAwait(false);

        while (currentRole != null)
        {
            hierarchy.Add(currentRole);
            if (currentRole.ParentRoleId.HasValue)
            {
                currentRole = await GetByIdAsync(currentRole.ParentRoleId.Value, ct).ConfigureAwait(false);
            }
            else
            {
                currentRole = null;
            }

            // Prevent infinite loops
            if (hierarchy.Count > 20)
            {
                break;
            }
        }

        return hierarchy;
    }

    /// <summary>
    ///     Loads multiple roles by id in one query (multi-parent inheritance traversal).
    ///     Unknown ids are simply absent from the result (fail-closed).
    /// </summary>
    public async Task<IReadOnlyList<DynamicRole>> GetManyByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<DynamicRole>();
        }

        return await DbSet
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}

/// <summary>
///     Database implementation of role assignment repository.
/// </summary>
public class DynamicRoleAssignmentRepository(
    IApplicationDbContext context,
    ICacheInvalidationService invalidationService) : IDynamicRoleAssignmentRepository
{
    private DbSet<DynamicRoleAssignment> DbSet => context.Set<DynamicRoleAssignment>();

    public async Task<IReadOnlyList<DynamicRoleAssignment>> GetByUserAsync(Guid userId, Guid? tenantId, CancellationToken ct = default)
        => await DbSet
            .Include(a => a.Role)
            .Where(a => a.UserId == userId && a.TenantId == tenantId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DynamicRoleAssignment>> GetValidByUserAsync(Guid userId, Guid? tenantId, CancellationToken ct = default)
    {
        var now = SystemClock.UtcNow;
        return await DbSet
            .Include(a => a.Role)
            .Where(a => a.UserId == userId && a.TenantId == tenantId && a.IsActive)
            .Where(a => !a.StartsAt.HasValue || a.StartsAt.Value <= now)
            .Where(a => !a.ExpiresAt.HasValue || a.ExpiresAt.Value > now)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DynamicRoleAssignment>> GetByTenantAsync(Guid? tenantId, CancellationToken ct = default)
        => await DbSet
            .Include(a => a.Role)
            .Where(a => a.TenantId == tenantId)
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<DynamicRoleAssignment> CreateAsync(DynamicRoleAssignment assignment, CancellationToken ct = default)
    {
        DbSet.Add(assignment);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        await InvalidateAssignmentAsync(assignment.UserId, assignment.TenantId, ct).ConfigureAwait(false);
        return assignment;
    }

    public async Task DeleteAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var assignment = await DbSet.FirstOrDefaultAsync(a => a.UserId == userId && a.RoleId == roleId, ct);
        if (assignment != null)
        {
            DbSet.Remove(assignment);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            await InvalidateAssignmentAsync(assignment.UserId, assignment.TenantId, ct).ConfigureAwait(false);
        }
    }

    private async Task InvalidateAssignmentAsync(Guid userId, Guid? tenantId, CancellationToken ct)
    {
        if (tenantId is Guid tenant)
        {
            await invalidationService.InvalidateUserAsync(userId, tenant, ct).ConfigureAwait(false);
        }
        else
        {
            await invalidationService.InvalidateGlobalAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<int> CountByRoleAsync(Guid roleId, CancellationToken ct = default)
        => await DbSet.CountAsync(a => a.RoleId == roleId && a.IsActive, ct);
}

/// <summary>
///     Service for RBAC permission resolution.
/// </summary>
public interface IRbacPermissionResolver
{
    /// <summary>
    ///     Gets all permissions for a user based on their roles.
    /// </summary>
    Task<RbacResolutionResult> ResolvePermissionsAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken ct = default);
}

/// <summary>
///     Result of RBAC permission resolution.
/// </summary>
/// <remarks>
///     Contains both allowed and denied permissions from all resolved roles.
///     Caller should apply DENY-WINS semantics: EffectivePermissions = Permissions - DenyPermissions
/// </remarks>
public sealed record RbacResolutionResult(
    IReadOnlySet<string> Permissions,
    IReadOnlySet<string> DenyPermissions,
    IReadOnlyList<RoleContribution> RoleContributions);

/// <summary>
///     Implementation of RBAC permission resolver. Uses <see cref="IRoleInheritanceEngine"/>
///     for multi-parent hierarchy traversal with cycle detection and selective
///     inheritance blocking (issue #358).
/// </summary>
public class RbacPermissionResolver(
    IDynamicRoleAssignmentRepository assignmentRepository,
    IRoleInheritanceEngine inheritanceEngine,
    ILogger<RbacPermissionResolver> logger
) : IRbacPermissionResolver
{
    public async Task<RbacResolutionResult> ResolvePermissionsAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        var allPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allDenyPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contributions = new List<RoleContribution>();

        // Get valid role assignments for the user
        var assignments = await assignmentRepository.GetValidByUserAsync(userId, tenantId, ct).ConfigureAwait(false);

        foreach (var assignment in assignments)
        {
            if (assignment.Role == null || !assignment.Role.IsActive)
            {
                continue;
            }

            // Direct contribution of the assigned role (static + dynamic permissions).
            var directPermissions = new List<string>();
            directPermissions.AddRange(StaticRolePermissions.GetStaticPermissions(assignment.Role.Name));
            directPermissions.AddRange(assignment.Role.Permissions);

            foreach (var perm in directPermissions)
            {
                allPermissions.Add(perm);
            }

            foreach (var perm in assignment.Role.DenyPermissions)
            {
                allDenyPermissions.Add(perm);
            }

            contributions.Add(new RoleContribution(
                assignment.Role.Id,
                assignment.Role.Name,
                directPermissions,
                IsInherited: false,
                InheritedFromRoleId: null));

            // Inherited contribution: multi-parent closure with cycle detection and
            // selective blocking, computed by the inheritance engine (issue #358).
            var closure = await inheritanceEngine.GetClosureAsync(assignment.RoleId, ct).ConfigureAwait(false);

            foreach (var perm in closure.InheritedPermissions)
            {
                allPermissions.Add(perm);
            }

            // Ancestor denies always flow down unblocked (DENY-WINS is global).
            foreach (var perm in closure.InheritedDenyPermissions)
            {
                allDenyPermissions.Add(perm);
            }

            foreach (var ancestor in closure.Ancestors)
            {
                if (ancestor.Permissions.Count == 0) continue;

                foreach (var perm in ancestor.Permissions)
                {
                    allPermissions.Add(perm);
                }

                contributions.Add(new RoleContribution(
                    ancestor.Role.Id,
                    ancestor.Role.Name,
                    ancestor.Permissions.ToList(),
                    IsInherited: true,
                    InheritedFromRoleId: assignment.RoleId));
            }

            logger.LogDebug(
                "Resolved {DirectCount} direct permissions, {InheritedCount} inherited permissions (from {AncestorCount} ancestors, {CyclesCut} cycle edges cut) and {DenyCount} denies from role {RoleName}",
                directPermissions.Count,
                closure.InheritedPermissions.Count,
                closure.Ancestors.Count,
                closure.CyclesCut,
                closure.InheritedDenyPermissions.Count,
                assignment.Role.Name);
        }

        return new RbacResolutionResult(allPermissions, allDenyPermissions, contributions);
    }
}
