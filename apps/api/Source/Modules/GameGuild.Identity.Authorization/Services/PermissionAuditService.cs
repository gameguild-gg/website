using GameGuild.CQRS.Models;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Service for auditing permission changes
/// </summary>
public class PermissionAuditService(
    IPermissionAuditLogRepository repository,
    ILogger<PermissionAuditService> logger
) : IPermissionAuditService
{
    private readonly ILogger<PermissionAuditService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IPermissionAuditLogRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<PermissionAuditLog> LogPermissionChangeAsync(
        PermissionOperationType operationType,
        Guid? userId,
        Guid performedBy,
        Guid? tenantId,
        string? permissionType = null,
        Guid? resourceId = null,
        string? resourceType = null,
        string? oldValue = null,
        string? newValue = null,
        string? reason = null,
        bool success = true,
        string? errorMessage = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default
    )
    {
        var auditLog = new PermissionAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId.HasValue ? new TenantId(tenantId.Value) : null,
            OperationType = operationType,
            ResourceType = resourceType ?? "Permission",
            ResourceId = resourceId,
            PermissionType = permissionType,
            Success = success,
            Reason = reason,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            OldValue = oldValue,
            NewValue = newValue,
            PerformedBy = performedBy,
            PermissionDetails = errorMessage,
            ErrorMessage = errorMessage,
            Timestamp = SystemClock.UtcNow
        };

        _logger.LogInformation(
            "Logging permission change: {OperationType} for user {UserId}",
            operationType,
            userId
        );

        return await _repository.CreateAsync(auditLog, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<PermissionAuditLog>> GetAuditLogsAsync(
        Guid? tenantId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? limit = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Retrieving audit logs: tenantId={TenantId}", tenantId);

        if (fromDate.HasValue && toDate.HasValue)
        {
            var logs = await _repository.GetByDateRangeAsync(
                fromDate.Value,
                toDate.Value,
                tenantId,
                cancellationToken
            ).ConfigureAwait(false);
            return limit.HasValue ? logs.Take(limit.Value).ToList() : logs;
        }

        var allLogs = await _repository.GetByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return limit.HasValue ? allLogs.Take(limit.Value).ToList() : allLogs;
    }

    public async Task<List<PermissionAuditLog>> GetUserAuditHistoryAsync(
        Guid userId,
        Guid? tenantId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        var logs = await _repository.GetByUserAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);

        if (fromDate.HasValue)
        {
            logs = logs.Where(l => l.Timestamp >= fromDate.Value).ToList();
        }

        if (toDate.HasValue)
        {
            logs = logs.Where(l => l.Timestamp <= toDate.Value).ToList();
        }

        return logs;
    }

    public async Task<List<PermissionAuditLog>> GetResourceAuditHistoryAsync(
        Guid resourceId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        var logs = await _repository.GetByTenantAsync(null, cancellationToken).ConfigureAwait(false);
        logs = logs.Where(l => l.ResourceId == resourceId).ToList();

        if (fromDate.HasValue)
        {
            logs = logs.Where(l => l.Timestamp >= fromDate.Value).ToList();
        }

        if (toDate.HasValue)
        {
            logs = logs.Where(l => l.Timestamp <= toDate.Value).ToList();
        }

        return logs;
    }

    public async Task<List<PermissionAuditLog>> GetAuditLogsByOperationAsync(
        PermissionOperationType operationType,
        Guid? tenantId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        var logs = await _repository.GetByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        logs = logs.Where(l => l.OperationType == operationType).ToList();

        if (fromDate.HasValue)
        {
            logs = logs.Where(l => l.Timestamp >= fromDate.Value).ToList();
        }

        if (toDate.HasValue)
        {
            logs = logs.Where(l => l.Timestamp <= toDate.Value).ToList();
        }

        return logs;
    }

    public async Task<List<PermissionAuditLog>> GetFailedOperationsAsync(
        Guid? tenantId,
        DateTime? fromDate = null,
        CancellationToken cancellationToken = default
    )
    {
        var logs = await _repository.GetByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        logs = logs.Where(l => !l.Success).ToList();

        if (fromDate.HasValue)
        {
            logs = logs.Where(l => l.Timestamp >= fromDate.Value).ToList();
        }

        return logs;
    }
}
