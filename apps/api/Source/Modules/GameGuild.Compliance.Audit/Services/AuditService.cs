using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

public sealed class AuditService(IServiceScopeFactory scopeFactory, IHttpContextAccessor httpContextAccessor, ILogger<AuditService> logger) : IAuditService
{
    public async Task LogAsync(CreateAuditLogRequest request)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var httpContext = httpContextAccessor.HttpContext;

            var auditLog = AuditLogEntryFactory.Create(request, httpContext);

            context.Set<AuditLog>().Add(auditLog);
            await context.SaveChangesAsync().ConfigureAwait(false);

            // Log to structured logging as well for real-time monitoring
            var logLevel = GetLogLevel(request.RiskLevel, request.Success);

            logger.Log(
                logLevel,
                "Audit: {ActionType} on {ResourceType} {ResourceId} by User {UserId} - {Success}",
                request.ActionType,
                request.ResourceType,
                request.ResourceId,
                request.UserId,
                request.Success ? "Success" : "Failed"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create audit log for action {ActionType}", request.ActionType);
            // Don't throw - audit logging should not break business operations
        }
    }

    public async Task LogPermissionGrantAsync(Guid userId, string permissionName, string resourceType, string? resourceId, Guid? tenantId = null)
    {
        await LogAsync(
            new CreateAuditLogRequest
            {
                ActionType = AuditActionTypes.PermissionGranted,
                ResourceType = resourceType,
                ResourceId = resourceId,
                UserId = userId,
                TenantId = tenantId,
                Description = $"Permission '{permissionName}' granted to user {userId}",
                Metadata = new { PermissionName = permissionName },
                Success = true,
                RiskLevel = AuditRiskLevel.Medium,
                Category = AuditCategory.Permission
            }
        );
    }

    public async Task LogPermissionDenyAsync(Guid? userId, string permissionName, string resourceType, string? resourceId, string reason, Guid? tenantId = null)
    {
        await LogAsync(
            new CreateAuditLogRequest
            {
                ActionType = AuditActionTypes.PermissionDenied,
                ResourceType = resourceType,
                ResourceId = resourceId,
                UserId = userId,
                TenantId = tenantId,
                Description = $"Permission '{permissionName}' denied: {reason}",
                Metadata = new { PermissionName = permissionName, Reason = reason },
                Success = false,
                RiskLevel = AuditRiskLevel.High,
                Category = AuditCategory.Permission
            }
        );
    }

    public async Task LogAuthenticationAsync(string actionType, Guid? userId, bool success, string? errorMessage = null)
    {
        await LogAsync(
            new CreateAuditLogRequest
            {
                ActionType = actionType,
                ResourceType = "User",
                ResourceId = userId?.ToString(),
                UserId = userId,
                Description = $"Authentication {actionType}: {(success ? "Success" : "Failed")}",
                Success = success,
                ErrorMessage = errorMessage,
                RiskLevel = success ? AuditRiskLevel.Low : AuditRiskLevel.High,
                Category = AuditCategory.Authentication
            }
        );
    }

    public async Task LogAdminActionAsync(Guid userId, string actionType, string description, object? metadata = null)
    {
        await LogAsync(
            new CreateAuditLogRequest
            {
                ActionType = actionType, ResourceType = "System", UserId = userId, Description = description, Metadata = metadata, Success = true, RiskLevel = AuditRiskLevel.High, Category = AuditCategory.Admin
            }
        ).ConfigureAwait(false);
    }

    public async Task LogSecurityViolationAsync(string violationType, string description, Guid? userId = null, object? metadata = null)
    {
        await LogAsync(
            new CreateAuditLogRequest
            {
                ActionType = AuditActionTypes.SecurityViolation,
                ResourceType = "Security",
                UserId = userId,
                Description = $"{violationType}: {description}",
                Metadata = metadata,
                Success = false,
                RiskLevel = AuditRiskLevel.Critical,
                Category = AuditCategory.Security
            }
        );
    }

    public async Task LogTenantOperationAsync(string actionType, Guid tenantId, Guid? userId = null, string? description = null, object? metadata = null, bool success = true)
    {
        await LogAsync(
            new CreateAuditLogRequest
            {
                ActionType = actionType,
                ResourceType = "Tenant",
                ResourceId = tenantId.ToString(),
                UserId = userId,
                TenantId = tenantId,
                Description = description ?? $"Tenant operation: {actionType}",
                Metadata = metadata,
                Success = success,
                RiskLevel = AuditRiskLevel.Medium,
                Category = AuditCategory.Tenant
            }
        );
    }

    public async Task LogTenantIsolationBypassAsync(Guid userId, string reason, object? metadata = null)
    {
        await LogAsync(
            new CreateAuditLogRequest
            {
                ActionType = AuditActionTypes.TenantIsolationBypassed,
                ResourceType = "System",
                UserId = userId,
                Description = $"Tenant isolation bypassed: {reason}",
                Metadata = metadata,
                Success = true,
                RiskLevel = AuditRiskLevel.High,
                Category = AuditCategory.Security
            }
        );
    }

    public async Task LogPrivacyOperationAsync(string actionType, Guid userId, string? settingName = null, string? oldValue = null, string? newValue = null, Guid? tenantId = null, object? metadata = null)
    {
        await LogAsync(
            new CreateAuditLogRequest
            {
                ActionType = actionType,
                ResourceType = "UserPrivacy",
                ResourceId = userId.ToString(),
                UserId = userId,
                TenantId = tenantId,
                Description = settingName != null ? $"Privacy setting '{settingName}' changed from '{oldValue}' to '{newValue}'" : $"Privacy operation: {actionType}",
                Metadata = new { SettingName = settingName, OldValue = oldValue, NewValue = newValue, AdditionalMetadata = metadata },
                Success = true,
                RiskLevel = AuditRiskLevel.Low,
                Category = AuditCategory.Privacy
            }
        );
    }

    public async Task LogPrivacyViolationAsync(Guid? requestingUserId, Guid targetUserId, string attemptedField, string reason, Guid? tenantId = null)
    {
        await LogAsync(
            new CreateAuditLogRequest
            {
                ActionType = AuditActionTypes.PrivacyViolationAttempt,
                ResourceType = "UserPrivacy",
                ResourceId = targetUserId.ToString(),
                UserId = requestingUserId,
                TenantId = tenantId,
                Description = $"Privacy violation attempt: {reason} - attempted to access '{attemptedField}' of user {targetUserId}",
                Metadata = new { TargetUserId = targetUserId, AttemptedField = attemptedField, Reason = reason },
                Success = false,
                RiskLevel = AuditRiskLevel.High,
                Category = AuditCategory.Privacy
            }
        );
    }

    public async Task LogUsernameOperationAsync(string actionType, Guid userId, string? oldUsername = null, string? newUsername = null, string? reason = null, object? metadata = null)
    {
        await LogAsync(
            new CreateAuditLogRequest
            {
                ActionType = actionType,
                ResourceType = "User",
                ResourceId = userId.ToString(),
                UserId = userId,
                Description = oldUsername != null && newUsername != null
                    ? $"Username changed from '{oldUsername}' to '{newUsername}'{(reason != null ? $" - Reason: {reason}" : "")}"
                    : $"Username operation: {actionType}{(reason != null ? $" - Reason: {reason}" : "")}",
                Metadata = new { OldUsername = oldUsername, NewUsername = newUsername, Reason = reason, AdditionalMetadata = metadata },
                Success = true,
                RiskLevel = AuditRiskLevel.Low,
                Category = AuditCategory.User
            }
        );
    }

    public async Task<List<AuditLog>> GetAuditLogsAsync(AuditLogQuery query)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        IQueryable<AuditLog> queryable = ApplyFilters(context.Set<AuditLog>().AsQueryable(), query)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id);

        queryable = ApplyPagination(queryable, query);

        var logs = await queryable.ToListAsync().ConfigureAwait(false);
        var recorder = scope.ServiceProvider.GetService<IAuditDataAccessRecorder>();
        if (recorder is not null)
        {
            await recorder.RecordAsync(logs.Select(log => new AuditAccessedRecord(log.TenantId, log.CreatedAt))).ConfigureAwait(false);
        }
        return logs;
    }

    public async IAsyncEnumerable<AuditLog> StreamAuditLogsAsync(
        AuditLogQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        IQueryable<AuditLog> queryable = ApplyFilters(context.Set<AuditLog>().AsQueryable(), query)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id);

        queryable = ApplyPagination(queryable, query);

        var recorder = scope.ServiceProvider.GetService<IAuditDataAccessRecorder>();
        var observed = new List<AuditAccessedRecord>(1000);
        try
        {
            await foreach (var auditLog in queryable.AsAsyncEnumerable().WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                observed.Add(new(auditLog.TenantId, auditLog.CreatedAt));
                yield return auditLog;
                if (observed.Count == 1000)
                {
                    if (recorder is not null) { await recorder.RecordAsync(observed, cancellationToken).ConfigureAwait(false); }
                    observed.Clear();
                }
            }
        }
        finally
        {
            // Count only yielded rows even if the consumer stops early. The recorder uses a separate connection scope.
            if (recorder is not null && observed.Count > 0)
            {
                await recorder.RecordAsync(observed, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    public async Task<int> GetAuditLogCountAsync(AuditLogQuery query)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await ApplyFilters(context.Set<AuditLog>().AsQueryable(), query)
            .CountAsync().ConfigureAwait(false);
    }

    public async Task<List<AuditActivityBucket>> GetAuditActivityAsync(
        AuditLogQuery query,
        AuditActivityBucketSize bucketSize,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var filtered = ApplyFilters(context.Set<AuditLog>().AsNoTracking(), query);

        if (bucketSize == AuditActivityBucketSize.Hourly)
        {
            var hourlyGroups = await filtered
                .GroupBy(log => new { log.CreatedAt.Year, log.CreatedAt.Month, log.CreatedAt.Day, log.CreatedAt.Hour })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    group.Key.Hour,
                    EventCount = group.Count()
                })
                .OrderBy(group => group.Year)
                .ThenBy(group => group.Month)
                .ThenBy(group => group.Day)
                .ThenBy(group => group.Hour)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return hourlyGroups
                .Select(group => new AuditActivityBucket(
                    new DateTime(group.Year, group.Month, group.Day, group.Hour, 0, 0, DateTimeKind.Utc),
                    group.EventCount))
                .ToList();
        }

        if (bucketSize != AuditActivityBucketSize.Daily)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketSize), bucketSize, "Unsupported audit activity bucket size.");
        }

        var dailyGroups = await filtered
            .GroupBy(log => new { log.CreatedAt.Year, log.CreatedAt.Month, log.CreatedAt.Day })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                EventCount = group.Count()
            })
            .OrderBy(group => group.Year)
            .ThenBy(group => group.Month)
            .ThenBy(group => group.Day)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return dailyGroups
            .Select(group => new AuditActivityBucket(
                new DateTime(group.Year, group.Month, group.Day, 0, 0, 0, DateTimeKind.Utc),
                group.EventCount))
            .ToList();
    }

    private static IQueryable<AuditLog> ApplyFilters(IQueryable<AuditLog> queryable, AuditLogQuery query)
    {
        if (query.UserId.HasValue) { queryable = queryable.Where(a => a.UserId == query.UserId.Value); }
        if (query.TenantId.HasValue) { queryable = queryable.Where(a => a.TenantId == query.TenantId.Value); }
        if (!string.IsNullOrEmpty(query.ActionType)) { queryable = queryable.Where(a => a.ActionType == query.ActionType); }
        if (!string.IsNullOrEmpty(query.ResourceType)) { queryable = queryable.Where(a => a.ResourceType == query.ResourceType); }
        if (query.Category.HasValue) { queryable = queryable.Where(a => a.Category == query.Category.Value); }
        if (query.RiskLevel.HasValue) { queryable = queryable.Where(a => a.RiskLevel >= query.RiskLevel.Value); }
        if (query.Success.HasValue) { queryable = queryable.Where(a => a.Success == query.Success.Value); }
        if (query.StartDate.HasValue) { queryable = queryable.Where(a => a.CreatedAt >= query.StartDate.Value); }
        if (query.EndDate.HasValue) { queryable = queryable.Where(a => a.CreatedAt <= query.EndDate.Value); }
        if (!string.IsNullOrEmpty(query.IpAddress)) { queryable = queryable.Where(a => a.IpAddress == query.IpAddress); }

        return queryable;
    }

    private static IQueryable<AuditLog> ApplyPagination(IQueryable<AuditLog> queryable, AuditLogQuery query)
    {
        if (query.Skip > 0) { queryable = queryable.Skip(query.Skip); }
        if (query.Take > 0) { queryable = queryable.Take(Math.Min(query.Take, 1000)); }

        return queryable;
    }

    private LogLevel GetLogLevel(AuditRiskLevel riskLevel, bool success)
    {
        return riskLevel switch
        {
            AuditRiskLevel.Critical => LogLevel.Critical,
            AuditRiskLevel.High => success ? LogLevel.Warning : LogLevel.Error,
            AuditRiskLevel.Medium => LogLevel.Information,
            AuditRiskLevel.Low => LogLevel.Debug,
            _ => LogLevel.Information
        };
    }
}
