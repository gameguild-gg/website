using Asp.Versioning;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Controller for audit log management (admin only)
/// </summary>
[Microsoft.AspNetCore.Http.Tags("compliance/audit")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/admin/audit-logs")]
[Authorize(Policy = Policies.SystemAdmin)]
public class AuditController(
    IAuditService auditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<AuditController> _logger,
    ISender sender) : BaseApiController
{
    /// <summary>
    /// Gets the current user ID from the actor context
    /// </summary>
    protected Guid? GetCurrentUserId()
    {
        return actorContextAccessor.ActorContext.SubjectIdAsGuid;
    }

    /// <summary>
    /// Get audit logs with filtering and pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AuditLogResponse>> GetAuditLogs([FromQuery] AuditLogQueryRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) throw new UnauthorizedAccessException("User not authenticated");

        _logger.LogInformation("Admin {AdminUserId} querying audit logs: ActionType={ActionType}, RiskLevel={RiskLevel}", adminUserId.Value, request.ActionType, request.RiskLevel);

        // Log admin access to audit logs
        await auditService.LogAdminActionAsync(adminUserId.Value, "ViewAuditLogs", "Admin accessed audit logs", new { Filters = request, RequestedBy = adminUserId.Value }).ConfigureAwait(false);

        var query = new AuditLogQuery
        {
            UserId = request.UserId,
            TenantId = request.TenantId,
            ActionType = request.ActionType,
            ResourceType = request.ResourceType,
            Category = request.Category,
            RiskLevel = request.RiskLevel,
            Success = request.Success,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IpAddress = request.IpAddress,
            Skip = request.Skip,
            Take = Math.Min(request.Take, 1000) // Cap at 1000 records
        };

        var logs = await auditService.GetAuditLogsAsync(query).ConfigureAwait(false);
        var totalCount = await auditService.GetAuditLogCountAsync(query).ConfigureAwait(false);

        var response = new AuditLogResponse { Logs = logs.Select(MapToDto).ToList(), TotalCount = totalCount, Skip = request.Skip, Take = request.Take };

        return Ok(response);
    }

    /// <summary>
    /// Get audit log statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<AuditStatisticsResponse>> GetAuditStatistics([FromQuery] AuditStatisticsRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) throw new UnauthorizedAccessException("User not authenticated");

        await auditService.LogAdminActionAsync(adminUserId.Value, "ViewAuditStatistics", "Admin accessed audit statistics").ConfigureAwait(false);

        var startDate = request.StartDate ?? SystemClock.UtcNow.AddDays(-30);
        var endDate = request.EndDate ?? SystemClock.UtcNow;

        // Get statistics for different categories
        var authenticationQuery = new AuditLogQuery { Category = AuditCategory.Authentication, StartDate = startDate, EndDate = endDate };

        var permissionQuery = new AuditLogQuery { Category = AuditCategory.Permission, StartDate = startDate, EndDate = endDate };

        var securityQuery = new AuditLogQuery { Category = AuditCategory.Security, StartDate = startDate, EndDate = endDate };

        var failedQuery = new AuditLogQuery { Success = false, StartDate = startDate, EndDate = endDate };

        var highRiskQuery = new AuditLogQuery { RiskLevel = AuditRiskLevel.High, StartDate = startDate, EndDate = endDate };

        var totalEvents = await auditService.GetAuditLogCountAsync(new AuditLogQuery { StartDate = startDate, EndDate = endDate }).ConfigureAwait(false);
        var authenticationEvents = await auditService.GetAuditLogCountAsync(authenticationQuery).ConfigureAwait(false);
        var permissionEvents = await auditService.GetAuditLogCountAsync(permissionQuery).ConfigureAwait(false);
        var securityEvents = await auditService.GetAuditLogCountAsync(securityQuery).ConfigureAwait(false);
        var failedEvents = await auditService.GetAuditLogCountAsync(failedQuery).ConfigureAwait(false);
        var highRiskEvents = await auditService.GetAuditLogCountAsync(highRiskQuery).ConfigureAwait(false);

        var response = new AuditStatisticsResponse
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalEvents = totalEvents,
            AuthenticationEvents = authenticationEvents,
            PermissionEvents = permissionEvents,
            SecurityEvents = securityEvents,
            FailedEvents = failedEvents,
            HighRiskEvents = highRiskEvents
        };

        return Ok(response);
    }

    /// <summary>
    /// Export audit logs (admin only)
    /// </summary>
    [HttpPost(":export")]
    public async Task<ActionResult> ExportAuditLogs([FromBody] AuditExportRequest request)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue) throw new UnauthorizedAccessException("User not authenticated");

        var logs = await sender.Send(
            new ExportAuditLogsCommand(adminUserId.Value, request),
            HttpContext.RequestAborted).ConfigureAwait(false);

        // Convert to CSV format
        var csv = GenerateCsv(logs);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);

        var fileName = $"audit-logs-{SystemClock.UtcNow:yyyy-MM-dd-HH-mm-ss}.csv";

        return File(bytes, "text/csv", fileName);
    }

    private AuditLogDto MapToDto(AuditLog log)
    {
        return new AuditLogDto
        {
            Id = log.Id,
            ActionType = log.ActionType,
            ResourceType = log.ResourceType,
            ResourceId = log.ResourceId,
            UserId = log.UserId,
            TenantId = log.TenantId,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            SessionId = log.SessionId,
            Description = log.Description,
            Success = log.Success,
            ErrorMessage = log.ErrorMessage,
            RiskLevel = log.RiskLevel,
            Category = log.Category,
            CorrelationId = log.CorrelationId,
            CreatedAt = log.CreatedAt
        };
    }

    private string GenerateCsv(List<AuditLog> logs)
    {
        var csv = new System.Text.StringBuilder();

        // Header
        csv.AppendLine("Id,ActionType,ResourceType,ResourceId,UserId,TenantId,IpAddress,Description,Success,RiskLevel,Category,CreatedAt");

        // Data rows
        foreach (var log in logs)
        {
            csv.AppendLine(
                $"{log.Id},{log.ActionType},{log.ResourceType},{log.ResourceId},{log.UserId},{log.TenantId},{log.IpAddress},\"{log.Description}\",{log.Success},{log.RiskLevel},{log.Category},{log.CreatedAt:yyyy-MM-dd HH:mm:ss}"
            );
        }

        return csv.ToString();
    }
}

// Request/Response DTOs
