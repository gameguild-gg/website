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
/// <remarks>
///     <para>
///         Issue #294: reads are gated by <see cref="Policies.AuditRead"/> — the
///         tenant-administered self-service tier — and exports by
///         <see cref="Policies.AuditExport"/>. Non-system callers are clamped to their
///         own tenant's security events (fail closed on missing tenant context or an
///         explicit cross-tenant request).
///     </para>
/// </remarks>
[Microsoft.AspNetCore.Http.Tags("compliance/audit/security")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/admin/security-audit")]
[Authorize]
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
    [Authorize(Policy = Policies.AuditRead)]
    [RequireDacPermission(QueryPermission.Keys.List)]
    [RequireSearchPermission(nameof(UnifiedSecurityAuditRequest.SearchText))]
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

        if (!TryClampToTenantScope(request.TenantId, out var tenantScope))
            return Forbid();
        request.TenantId = tenantScope;

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
    /// <remarks>
    ///     Deliberately kept on the SystemAdmin policy: the authentication-attempt surface
    ///     has no tenant-scoped filter today (the request and the underlying query are
    ///     platform-wide), so opening it to the Audit.Read self-service tier would leak
    ///     cross-tenant authentication data. Revisit together with a tenant filter on
    ///     <see cref="AuthenticationAuditRequest"/>.
    /// </remarks>
    [HttpGet("authentication")]
    [Authorize(Policy = Policies.SystemAdmin)]
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
    [Authorize(Policy = Policies.AuditRead)]
    [RequireDacPermission(QueryPermission.Keys.List)]
    [ProducesResponseType(typeof(PermissionAuditResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionAuditResponse>> GetPermissionLogs(
        [FromQuery] PermissionAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized("User not authenticated");

        if (!TryClampToTenantScope(request.TenantId, out var tenantScope))
            return Forbid();
        request.TenantId = tenantScope;

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
    [Authorize(Policy = Policies.AuditRead)]
    [RequireDacPermission(QueryPermission.Keys.Query)]
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

        if (!TryClampToTenantScope(tenantId, out var tenantScope))
            return Forbid();

        await auditService.LogAdminActionAsync(
            adminUserId.Value,
            "ViewSecurityDashboard",
            "Admin accessed security audit dashboard").ConfigureAwait(false);

        var result = await auditAggregator.GetSecurityDashboardAsync(
            startDate ?? SystemClock.UtcNow.AddDays(-30),
            endDate ?? SystemClock.UtcNow,
            tenantScope,
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Export unified security audit logs to CSV.
    /// </summary>
    [HttpPost(":export")]
    [Authorize(Policy = Policies.AuditExport)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> ExportSecurityAuditLogs(
        [FromBody] UnifiedSecurityAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = GetCurrentUserId();
        if (!adminUserId.HasValue)
            return Unauthorized("User not authenticated");

        if (!TryClampToTenantScope(request.TenantId, out var tenantScope))
            return Forbid();
        request.TenantId = tenantScope;

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

    /// <summary>
    ///     Issue #294 tenant clamp for the audit self-service tier: system administrators
    ///     keep their requested (platform-wide or any-tenant) scope; every other caller is
    ///     pinned to their own tenant. An explicit cross-tenant request or a missing tenant
    ///     context fails closed with 403.
    /// </summary>
    private bool TryClampToTenantScope(Guid? requestedTenantId, out Guid? tenantScope)
    {
        var actor = actorContextAccessor.ActorContext;
        if (actor.IsSystemAdmin)
        {
            tenantScope = requestedTenantId;
            return true;
        }

        if (!actor.TenantId.HasValue)
        {
            tenantScope = null;
            return false;
        }

        if (requestedTenantId.HasValue && requestedTenantId.Value != actor.TenantId.Value)
        {
            tenantScope = null;
            return false;
        }

        tenantScope = actor.TenantId.Value;
        return true;
    }
}
