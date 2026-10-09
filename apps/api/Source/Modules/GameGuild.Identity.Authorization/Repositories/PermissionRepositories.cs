using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Repository implementation for TenantPermission entities
/// </summary>
public class TenantPermissionRepository(IApplicationDbContext context) : ITenantPermissionRepository
{
    private DbSet<TenantPermission> TenantPermissions => context.Set<TenantPermission>();

    public async Task<TenantPermission> CreateAsync(
        TenantPermission permission,
        CancellationToken cancellationToken = default
    )
    {
        permission.Touch();

        TenantPermissions.Add(permission);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return permission;
    }

    public async Task<TenantPermission> UpdateAsync(
        TenantPermission permission,
        CancellationToken cancellationToken = default
    )
    {
        permission.Touch();
        TenantPermissions.Update(permission);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return permission;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var permission = await TenantPermissions
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        if (permission == null) return false;

        permission.SoftDelete();
        TenantPermissions.Update(permission);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<TenantPermission?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await TenantPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TenantPermission?> GetByUserAndTenantAsync(
        Guid? userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        return await TenantPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.UserId == userId && p.TenantId == tenantId && p.DeletedAt == null,
                cancellationToken
            ).ConfigureAwait(false);
    }

    public async Task<List<TenantPermission>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    )
    {
        return await TenantPermissions
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.DeletedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<TenantPermission>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        return await TenantPermissions
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.DeletedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<TenantPermission>> GetExpiredPermissionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await TenantPermissions
            .AsNoTracking()
            .Where(p => p.DeletedAt == null && p.ExpiresAt.HasValue && p.ExpiresAt < SystemClock.UtcNow)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TenantPermission?> GetDeletedByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await TenantPermissions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt != null, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Repository implementation for PermissionAuditLog entities
/// </summary>
public class PermissionAuditLogRepository(IApplicationDbContext context) : IPermissionAuditLogRepository
{
    private DbSet<PermissionAuditLog> AuditLogs => context.Set<PermissionAuditLog>();

    public async Task<PermissionAuditLog> CreateAsync(
        PermissionAuditLog auditLog,
        CancellationToken cancellationToken = default
    )
    {
        AuditLogs.Add(auditLog);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return auditLog;
    }

    public async Task<PermissionAuditLog?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionAuditLog>> GetByTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        if (tenantId == null)
        {
            return await AuditLogs
                .AsNoTracking()
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        return await AuditLogs
            .AsNoTracking()
            .Where(l => l.TenantId != null && l.TenantId.Value.Value == tenantId.Value)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionAuditLog>> GetByUserAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = AuditLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId);

        if (tenantId.HasValue)
        {
            query = query.Where(l => l.TenantId != null && l.TenantId.Value.Value == tenantId.Value);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionAuditLog>> GetByDateRangeAsync(
        DateTime fromDate,
        DateTime toDate,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = AuditLogs
            .AsNoTracking()
            .Where(l => l.Timestamp >= fromDate && l.Timestamp <= toDate);

        if (tenantId.HasValue)
        {
            query = query.Where(l => l.TenantId != null && l.TenantId.Value.Value == tenantId.Value);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionAuditLog>> GetByOperationTypeAsync(
        PermissionOperationType operationType,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var query = AuditLogs
            .AsNoTracking()
            .Where(l => l.OperationType == operationType);

        if (tenantId.HasValue)
        {
            query = query.Where(l => l.TenantId != null && l.TenantId.Value.Value == tenantId.Value);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionAuditLog>> GetByTenantAsync(
        Guid? tenantId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default
    )
    {
        var query = AuditLogs
            .AsNoTracking()
            .Where(l => l.Timestamp >= startDate && l.Timestamp <= endDate);

        if (tenantId.HasValue)
        {
            query = query.Where(l => l.TenantId != null && l.TenantId.Value.Value == tenantId.Value);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionAuditLog>> GetByUserAsync(
        Guid userId,
        Guid? tenantId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default
    )
    {
        var query = AuditLogs
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.Timestamp >= startDate && l.Timestamp <= endDate);

        if (tenantId.HasValue)
        {
            query = query.Where(l => l.TenantId != null && l.TenantId.Value.Value == tenantId.Value);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionAuditLog>> GetByPermissionAsync(
        string permission,
        Guid? tenantId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default
    )
    {
        var query = AuditLogs
            .AsNoTracking()
            .Where(l => l.PermissionType == permission && l.Timestamp >= startDate && l.Timestamp <= endDate);

        if (tenantId.HasValue)
        {
            query = query.Where(l => l.TenantId != null && l.TenantId.Value.Value == tenantId.Value);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionAuditLog>> GetByResourceTypeAsync(
        string resourceType,
        Guid? tenantId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default
    )
    {
        var query = AuditLogs
            .AsNoTracking()
            .Where(l => l.ResourceType == resourceType && l.Timestamp >= startDate && l.Timestamp <= endDate);

        if (tenantId.HasValue)
        {
            query = query.Where(l => l.TenantId != null && l.TenantId.Value.Value == tenantId.Value);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
