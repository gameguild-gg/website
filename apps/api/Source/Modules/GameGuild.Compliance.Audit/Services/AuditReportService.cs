using System.Text;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Generates security audit dashboards with aggregated statistics
///     and exports audit data to CSV format.
/// </summary>
public class AuditReportService(
    IApplicationDbContext context,
    IPermissionAuditLogRepository permissionAuditRepository,
    IAuditLogQueryService auditLogQueryService,
    ILogger<AuditReportService> _logger) : IAuditReportService
{
    public async Task<SecurityAuditDashboard> GetSecurityDashboardAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating security dashboard for period {StartDate} to {EndDate}, TenantId={TenantId}",
            startDate, endDate, LogRedaction.RedactId(tenantId, "tid"));

        // Authentication statistics
        var authQuery = context.Set<AuthenticationAttempt>().AsNoTracking()
            .Where(a => a.AttemptedAt >= startDate && a.AttemptedAt <= endDate);

        var totalLoginAttempts = await authQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var successfulLogins = await authQuery.CountAsync(a => a.IsSuccessful, cancellationToken);
        var failedLogins = totalLoginAttempts - successfulLogins;
        var loginSuccessRate = totalLoginAttempts > 0 ? (successfulLogins * 100.0 / totalLoginAttempts) : 0;
        var uniqueUsersAuth = await authQuery.Where(a => a.UserId.HasValue).Select(a => a.UserId).Distinct().CountAsync(cancellationToken);

        // Permission statistics
        var permLogs = await permissionAuditRepository.GetByDateRangeAsync(startDate, endDate, tenantId, cancellationToken).ConfigureAwait(false);
        var totalPermissionChanges = permLogs.Count;
        var permissionGrants = permLogs.Count(l => l.OperationType == PermissionOperationType.Grant);
        var permissionRevokes = permLogs.Count(l => l.OperationType == PermissionOperationType.Revoke);
        var permissionDenials = permLogs.Count(l => !l.Success);

        // General audit statistics
        var auditQuery = context.Set<AuditLog>().AsNoTracking()
            .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate);

        if (tenantId.HasValue)
            auditQuery = auditQuery.Where(a => a.TenantId == tenantId.Value);

        var securityViolations = await auditQuery
            .CountAsync(a => a.RiskLevel >= AuditRiskLevel.High || !a.Success, cancellationToken).ConfigureAwait(false);
        var highRiskEvents = await auditQuery
            .CountAsync(a => a.RiskLevel >= AuditRiskLevel.High, cancellationToken).ConfigureAwait(false);

        // Top users by activity
        var topUsers = await authQuery
            .Where(a => a.UserId.HasValue)
            .GroupBy(a => a.UserId!.Value)
            .Select(g => new TopUserActivity
            {
                UserId = g.Key,
                Email = g.Select(a => a.Email).FirstOrDefault(),
                EventCount = g.Count(),
                FailedAttempts = g.Count(a => !a.IsSuccessful)
            })
            .OrderByDescending(u => u.EventCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        // Top IPs by activity
        var topIps = await authQuery
            .Where(a => !string.IsNullOrEmpty(a.IpAddress))
            .GroupBy(a => a.IpAddress!)
            .Select(g => new TopIpActivity
            {
                IpAddress = g.Key,
                EventCount = g.Count(),
                FailedAttempts = g.Count(a => !a.IsSuccessful),
                UniqueUsers = g.Where(a => a.UserId.HasValue).Select(a => a.UserId).Distinct().Count()
            })
            .OrderByDescending(ip => ip.EventCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        // Failure reasons breakdown
        var failureReasons = await authQuery
            .Where(a => !a.IsSuccessful && !string.IsNullOrEmpty(a.FailureReason))
            .GroupBy(a => a.FailureReason!)
            .Select(g => new FailureReasonCount
            {
                Reason = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(f => f.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        // Daily trends
        var dailyTrends = await authQuery
            .GroupBy(a => a.AttemptedAt.Date)
            .Select(g => new DailyActivityTrend
            {
                Date = g.Key,
                AuthenticationEvents = g.Count(),
                TotalEvents = g.Count(),
                PermissionEvents = 0,
                SecurityViolations = g.Count(a => !a.IsSuccessful)
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);

        return new SecurityAuditDashboard
        {
            StartDate = startDate,
            EndDate = endDate,
            TenantId = tenantId,

            // Authentication
            TotalAuthenticationAttempts = totalLoginAttempts,
            SuccessfulLogins = successfulLogins,
            FailedLogins = failedLogins,
            LoginSuccessRate = Math.Round(loginSuccessRate, 2),
            UniqueUsersAuthenticated = uniqueUsersAuth,
            SuspiciousLoginAttempts = 0,

            // Permissions
            TotalPermissionChanges = totalPermissionChanges,
            PermissionsGranted = permissionGrants,
            PermissionsRevoked = permissionRevokes,
            PermissionDenials = permissionDenials,

            // General
            TotalSecurityViolations = securityViolations,
            HighRiskEvents = highRiskEvents,
            CrossTenantAttempts = 0,

            // Breakdowns
            TopActiveUsers = topUsers,
            TopIpAddresses = topIps,
            TopFailureReasons = failureReasons,
            DailyTrends = dailyTrends
        };
    }

    public async Task<byte[]> ExportAuditLogsAsync(
        UnifiedSecurityAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        // Override pagination to get all records for export
        request.Skip = 0;
        request.Take = 10000; // Max export limit

        var response = await auditLogQueryService.GetUnifiedAuditLogsAsync(request, cancellationToken).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,SourceType,ActionType,ResourceType,ResourceId,UserId,IpAddress,Success,Description");

        foreach (var entry in response.Entries)
        {
            sb.AppendLine($"\"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}\"," +
                         $"\"{entry.SourceType}\"," +
                         $"\"{EscapeCsv(entry.ActionType)}\"," +
                         $"\"{EscapeCsv(entry.ResourceType)}\"," +
                         $"\"{EscapeCsv(entry.ResourceId)}\"," +
                         $"\"{entry.UserId}\"," +
                         $"\"{EscapeCsv(entry.IpAddress)}\"," +
                         $"\"{entry.Success}\"," +
                         $"\"{EscapeCsv(entry.Description)}\"");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string? EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Replace("\"", "\"\"");
    }
}
