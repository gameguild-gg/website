using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Queries authentication attempts, permission audit logs, and general audit logs
///     to provide filtered, paginated, and unified security audit data.
/// </summary>
public class AuditLogQueryService(
    IApplicationDbContext context,
    IPermissionAuditLogRepository permissionAuditRepository,
    ILogger<AuditLogQueryService> _logger,
    IAuditDataAccessRecorder? accessRecorder = null) : IAuditLogQueryService
{
    public async Task<UnifiedSecurityAuditResponse> GetUnifiedAuditLogsAsync(
        UnifiedSecurityAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Querying unified audit logs: SourceType={SourceType}, StartDate={StartDate}, EndDate={EndDate}", request.SourceType, request.StartDate, request.EndDate);

        var entries = new List<UnifiedSecurityAuditEntry>();

        // Determine date range
        var startDate = request.StartDate ?? SystemClock.UtcNow.AddDays(-30);
        var endDate = request.EndDate ?? SystemClock.UtcNow;

        // Fetch from different sources based on SourceType filter
        if (request.SourceType is null or SecurityAuditSourceType.All or SecurityAuditSourceType.Authentication)
        {
            var authLogs = await GetAuthenticationEntriesAsync(startDate, endDate, request.UserId, request.Success, request.IpAddress, cancellationToken).ConfigureAwait(false);
            entries.AddRange(authLogs);
        }

        if (request.SourceType is null or SecurityAuditSourceType.All or SecurityAuditSourceType.Permission)
        {
            var permLogs = await GetPermissionEntriesAsync(startDate, endDate, request.TenantId, request.UserId, request.Success, cancellationToken).ConfigureAwait(false);
            entries.AddRange(permLogs);
        }

        if (request.SourceType is null or SecurityAuditSourceType.All or SecurityAuditSourceType.General)
        {
            var generalLogs = await GetGeneralEntriesAsync(startDate, endDate, request.TenantId, request.UserId, request.Success, request.ActionType, request.RiskLevel, cancellationToken).ConfigureAwait(false);
            entries.AddRange(generalLogs);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            entries = entries.Where(e =>
                (e.Description?.Contains(request.SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.ActionType.Contains(request.SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (e.ResourceType?.Contains(request.SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.IpAddress?.Contains(request.SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        // Sort
        entries = request.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? entries.OrderBy(e => e.Timestamp).ToList()
            : entries.OrderByDescending(e => e.Timestamp).ToList();

        // Calculate source breakdown before pagination
        var sourceBreakdown = entries
            .GroupBy(e => e.SourceType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Pagination
        var totalCount = entries.Count;
        var pagedEntries = entries
            .Skip(request.Skip)
            .Take(request.Take)
            .ToList();

        if (accessRecorder is not null)
        {
            await accessRecorder.RecordAsync(pagedEntries.Where(entry => entry.SourceType == SecurityAuditSourceType.General)
                .Select(entry => new AuditAccessedRecord(entry.TenantId, entry.Timestamp)), cancellationToken).ConfigureAwait(false);
        }
        return new UnifiedSecurityAuditResponse
        {
            Entries = pagedEntries,
            TotalCount = totalCount,
            Skip = request.Skip,
            Take = pagedEntries.Count,
            SourceBreakdown = sourceBreakdown
        };
    }

    public async Task<AuthenticationAuditResponse> GetAuthenticationLogsAsync(
        AuthenticationAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        var startDate = request.StartDate ?? SystemClock.UtcNow.AddDays(-30);
        var endDate = request.EndDate ?? SystemClock.UtcNow;

        var query = context.Set<AuthenticationAttempt>().AsNoTracking()
            .Where(a => a.AttemptedAt >= startDate && a.AttemptedAt <= endDate);

        if (request.UserId.HasValue)
            query = query.Where(a => a.UserId == request.UserId.Value);

        if (!string.IsNullOrEmpty(request.Email))
            query = query.Where(a => a.Email == request.Email);

        if (!string.IsNullOrEmpty(request.IpAddress))
            query = query.Where(a => a.IpAddress == request.IpAddress);

        if (request.Success.HasValue)
            query = query.Where(a => a.IsSuccessful == request.Success.Value);

        if (!string.IsNullOrEmpty(request.FailureReason))
            query = query.Where(a => a.FailureReason != null && a.FailureReason.Contains(request.FailureReason));

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var successfulLogins = await query.CountAsync(a => a.IsSuccessful, cancellationToken);
        var uniqueIps = await query.Select(a => a.IpAddress).Distinct().CountAsync(cancellationToken);

        var attempts = await query
            .OrderByDescending(a => a.AttemptedAt)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new AuthenticationAuditResponse
        {
            Entries = attempts.Select(a => new AuthenticationAuditEntry
            {
                Id = a.Id,
                Email = a.Email ?? string.Empty,
                UserId = a.UserId,
                IpAddress = a.IpAddress ?? string.Empty,
                UserAgent = a.UserAgent,
                IsSuccessful = a.IsSuccessful,
                FailureReason = a.FailureReason,
                AttemptedAt = a.AttemptedAt,
                ProcessingTime = a.ProcessingTime,
                GeoLocation = null,
                IsSuspicious = false
            }).ToList(),
            TotalCount = totalCount,
            Skip = request.Skip,
            Take = attempts.Count,
            SuccessfulLogins = successfulLogins,
            FailedLogins = totalCount - successfulLogins,
            UniqueIpAddresses = uniqueIps
        };
    }

    public async Task<PermissionAuditResponse> GetPermissionLogsAsync(
        PermissionAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        var startDate = request.StartDate ?? SystemClock.UtcNow.AddDays(-30);
        var endDate = request.EndDate ?? SystemClock.UtcNow;

        var logs = await permissionAuditRepository.GetByDateRangeAsync(
            startDate, endDate, request.TenantId, cancellationToken).ConfigureAwait(false);

        // Apply additional filters
        if (request.UserId.HasValue)
            logs = logs.Where(l => l.UserId == request.UserId.Value).ToList();

        if (!string.IsNullOrEmpty(request.OperationType) && Enum.TryParse<PermissionOperationType>(request.OperationType, true, out var opType))
            logs = logs.Where(l => l.OperationType == opType).ToList();

        if (!string.IsNullOrEmpty(request.PermissionType))
            logs = logs.Where(l => l.PermissionType == request.PermissionType).ToList();

        if (!string.IsNullOrEmpty(request.ResourceType))
            logs = logs.Where(l => l.ResourceType == request.ResourceType).ToList();

        if (request.Success.HasValue)
            logs = logs.Where(l => l.Success == request.Success.Value).ToList();

        var totalCount = logs.Count;
        var grantOps = logs.Count(l => l.OperationType == PermissionOperationType.Grant);
        var revokeOps = logs.Count(l => l.OperationType == PermissionOperationType.Revoke);
        var denyOps = logs.Count(l => !l.Success);

        var pagedLogs = logs
            .Skip(request.Skip)
            .Take(request.Take)
            .ToList();

        return new PermissionAuditResponse
        {
            Entries = pagedLogs.Select(l => new PermissionAuditEntry
            {
                Id = l.Id,
                TenantId = l.TenantId?.Value,
                OperationType = l.OperationType.ToString(),
                UserId = l.UserId,
                ResourceId = l.ResourceId,
                ResourceType = l.ResourceType,
                PermissionType = l.PermissionType,
                OldValue = l.OldValue,
                NewValue = l.NewValue,
                PerformedBy = l.PerformedBy,
                IpAddress = null,
                Reason = null,
                Success = l.Success,
                ErrorMessage = null,
                Timestamp = l.Timestamp
            }).ToList(),
            TotalCount = totalCount,
            Skip = request.Skip,
            Take = pagedLogs.Count,
            GrantOperations = grantOps,
            RevokeOperations = revokeOps,
            DenyOperations = denyOps
        };
    }

    #region Private Data Collection Helpers

    internal async Task<List<UnifiedSecurityAuditEntry>> GetAuthenticationEntriesAsync(
        DateTime startDate, DateTime endDate, Guid? userId, bool? successOnly, string? ipAddress,
        CancellationToken cancellationToken)
    {
        var query = context.Set<AuthenticationAttempt>().AsNoTracking()
            .Where(a => a.AttemptedAt >= startDate && a.AttemptedAt <= endDate);

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        if (successOnly.HasValue)
            query = query.Where(a => a.IsSuccessful == successOnly.Value);

        if (!string.IsNullOrEmpty(ipAddress))
            query = query.Where(a => a.IpAddress == ipAddress);

        var attempts = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return attempts.Select(a => new UnifiedSecurityAuditEntry
        {
            Id = a.Id,
            Timestamp = a.AttemptedAt,
            SourceType = SecurityAuditSourceType.Authentication,
            SourceEntity = "AuthenticationAttempt",
            ActionType = a.IsSuccessful ? "Login" : "LoginFailed",
            ResourceType = "User",
            ResourceId = a.UserId?.ToString(),
            UserId = a.UserId,
            UserEmail = a.Email,
            TenantId = null,
            IpAddress = a.IpAddress,
            UserAgent = a.UserAgent,
            Success = a.IsSuccessful,
            ErrorMessage = a.FailureReason,
            Description = a.IsSuccessful
                ? $"Successful login for {a.Email}"
                : $"Failed login for {a.Email}: {a.FailureReason}",
            RiskLevel = a.IsSuccessful ? AuditRiskLevel.Low : AuditRiskLevel.Medium,
            Metadata = null
        }).ToList();
    }

    internal async Task<List<UnifiedSecurityAuditEntry>> GetPermissionEntriesAsync(
        DateTime startDate, DateTime endDate, Guid? tenantId, Guid? userId, bool? successOnly,
        CancellationToken cancellationToken)
    {
        var logs = await permissionAuditRepository.GetByDateRangeAsync(startDate, endDate, tenantId, cancellationToken).ConfigureAwait(false);

        if (userId.HasValue)
            logs = logs.Where(l => l.UserId == userId.Value).ToList();

        if (successOnly.HasValue)
            logs = logs.Where(l => l.Success == successOnly.Value).ToList();

        return logs.Select(l => new UnifiedSecurityAuditEntry
        {
            Id = l.Id,
            Timestamp = l.Timestamp,
            SourceType = SecurityAuditSourceType.Permission,
            SourceEntity = "PermissionAuditLog",
            ActionType = l.OperationType.ToString(),
            ResourceType = l.ResourceType ?? "Permission",
            ResourceId = l.ResourceId?.ToString(),
            UserId = l.UserId,
            UserEmail = null,
            TenantId = l.TenantId?.Value,
            IpAddress = null,
            UserAgent = null,
            Success = l.Success,
            ErrorMessage = null,
            Description = $"{l.OperationType} permission '{l.PermissionType}' for user {l.UserId}",
            RiskLevel = l.Success ? AuditRiskLevel.Low : AuditRiskLevel.Medium,
            Metadata = null
        }).ToList();
    }

    internal async Task<List<UnifiedSecurityAuditEntry>> GetGeneralEntriesAsync(
        DateTime startDate, DateTime endDate, Guid? tenantId, Guid? userId, bool? successOnly,
        string? actionType, AuditRiskLevel? riskLevel,
        CancellationToken cancellationToken)
    {
        var query = context.Set<AuditLog>().AsNoTracking()
            .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate);

        if (tenantId.HasValue)
            query = query.Where(a => a.TenantId == tenantId.Value);

        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        if (successOnly.HasValue)
            query = query.Where(a => a.Success == successOnly.Value);

        if (!string.IsNullOrEmpty(actionType))
            query = query.Where(a => a.ActionType == actionType);

        if (riskLevel.HasValue)
            query = query.Where(a => a.RiskLevel == riskLevel.Value);

        var logs = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        return logs.Select(l => new UnifiedSecurityAuditEntry
        {
            Id = l.Id,
            Timestamp = l.CreatedAt,
            SourceType = SecurityAuditSourceType.General,
            SourceEntity = "AuditLog",
            ActionType = l.ActionType,
            ResourceType = l.ResourceType,
            ResourceId = l.ResourceId,
            UserId = l.UserId,
            UserEmail = null,
            TenantId = l.TenantId,
            IpAddress = l.IpAddress,
            UserAgent = l.UserAgent,
            Success = l.Success,
            ErrorMessage = l.ErrorMessage,
            Description = l.Description,
            RiskLevel = l.RiskLevel,
            Metadata = l.Metadata
        }).ToList();
    }

    #endregion
}
