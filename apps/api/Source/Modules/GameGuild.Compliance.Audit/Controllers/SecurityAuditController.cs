using Asp.Versioning;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Unified security audit log viewer for administrators.
///     Provides a centralized view of all security-related audit events including:
///     - Authentication attempts (login/logout, MFA, failures)
///     - Permission audit logs (grants, revokes, changes)
///     - General audit logs (admin actions, security violations)
/// </summary>
[Microsoft.AspNetCore.Http.Tags("compliance/audit/security")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/admin/security-audit")]
[Authorize(Policy = Policies.SystemAdmin)]
public class SecurityAuditController(
    ISecurityAuditAggregator auditAggregator,
    IAuditService auditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<SecurityAuditController> _logger,
    ISender sender) : BaseApiController
{
    /// <summary>
    ///     Get unified security audit logs from all sources with filtering and pagination.
    /// </summary>
    /// <param name="request">Query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of unified security audit events.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(UnifiedSecurityAuditResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UnifiedSecurityAuditResponse>> GetSecurityAuditLogs(
        [FromQuery] UnifiedSecurityAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized("User not authenticated");

        _logger.LogInformation("Admin {AdminUserId} accessing security audit logs: SourceType={SourceType}", adminUserId.Value, request.SourceType);

        // Log this admin access
        await auditService.LogAdminActionAsync(
            adminUserId.Value,
            "ViewSecurityAuditLogs",
            "Admin accessed unified security audit logs",
            new { Filters = request }).ConfigureAwait(false);

        var result = await auditAggregator.GetUnifiedAuditLogsAsync(request, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get authentication attempt logs specifically.
    /// </summary>
    [HttpGet("authentication")]
    [ProducesResponseType(typeof(AuthenticationAuditResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthenticationAuditResponse>> GetAuthenticationLogs(
        [FromQuery] AuthenticationAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized("User not authenticated");

        await auditService.LogAdminActionAsync(
            adminUserId.Value,
            "ViewAuthenticationLogs",
            "Admin accessed authentication audit logs").ConfigureAwait(false);

        var result = await auditAggregator.GetAuthenticationLogsAsync(request, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get permission audit logs specifically.
    /// </summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(PermissionAuditResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionAuditResponse>> GetPermissionLogs(
        [FromQuery] PermissionAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized("User not authenticated");

        await auditService.LogAdminActionAsync(
            adminUserId.Value,
            "ViewPermissionLogs",
            "Admin accessed permission audit logs").ConfigureAwait(false);

        var result = await auditAggregator.GetPermissionLogsAsync(request, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get security audit dashboard with aggregated statistics.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(SecurityAuditDashboard), StatusCodes.Status200OK)]
    public async Task<ActionResult<SecurityAuditDashboard>> GetSecurityDashboard(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized("User not authenticated");

        await auditService.LogAdminActionAsync(
            adminUserId.Value,
            "ViewSecurityDashboard",
            "Admin accessed security audit dashboard").ConfigureAwait(false);

        var result = await auditAggregator.GetSecurityDashboardAsync(
            startDate ?? SystemClock.UtcNow.AddDays(-30),
            endDate ?? SystemClock.UtcNow,
            tenantId,
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Export unified security audit logs to CSV.
    /// </summary>
    [HttpPost(":export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> ExportSecurityAuditLogs(
        [FromBody] UnifiedSecurityAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized("User not authenticated");

        var exportData = await sender.Send(
            new ExportSecurityAuditLogsCommand(adminUserId.Value, request),
            cancellationToken).ConfigureAwait(false);
        var fileName = $"security-audit-{SystemClock.UtcNow:yyyy-MM-dd-HH-mm-ss}.csv";

        return File(exportData, "text/csv", fileName);
    }

    private Guid? GetCurrentUserId()
    {
        return actorContextAccessor.ActorContext.SubjectIdAsGuid;
    }
}
