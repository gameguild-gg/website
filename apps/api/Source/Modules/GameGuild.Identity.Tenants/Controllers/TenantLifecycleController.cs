using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Tenants;

/// <summary>
///     Tenant Lifecycle API Controller - handles individual tenant state transitions and audit
/// </summary>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("tenants")]
[Authorize]
public sealed class TenantLifecycleController(ISender sender) : BaseApiController
{
    #region Individual Tenant Actions - /v1/tenants/{tenantId}:action

    /// <summary>
    ///     Activate tenant account
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/tenants/{tenantId:guid}:activate")]
    [EndpointSummary("Activate tenant account")]
    [EndpointDescription("Activates a tenant organization by ID.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateTenant(Guid tenantId, CancellationToken ct)
    {
        await sender.Send(new ActivateTenantCommand(tenantId), ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Deactivate tenant account
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/tenants/{tenantId:guid}:deactivate")]
    [EndpointSummary("Deactivate tenant account")]
    [EndpointDescription("Deactivates a tenant organization by ID.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateTenant(Guid tenantId, CancellationToken ct)
    {
        await sender.Send(new DeactivateTenantCommand(tenantId), ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Archive (soft delete) tenant account
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="body">Archive request containing reason and optional metadata</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/tenants/{tenantId:guid}:archive")]
    [EndpointSummary("Archive (soft delete) tenant account")]
    [EndpointDescription("Archives a tenant organization by ID.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ArchiveTenant(Guid tenantId, [FromBody] ArchiveRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new ArchiveTenantCommand(tenantId, body.Reason), ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Undelete a soft-deleted tenant account
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="body">Recovery request containing reason and optional restoration settings</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/tenants/{tenantId:guid}:undelete")]
    [EndpointSummary("Undelete a soft-deleted tenant account")]
    [EndpointDescription("Undeletes a previously soft-deleted (archived) tenant organization.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UndeleteTenant(Guid tenantId, [FromBody] RecoverRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new RecoverTenantCommand(tenantId, body.Reason), ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Permanently delete (hard delete) tenant account
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/tenants/{tenantId:guid}:purge")]
    [EndpointSummary("Permanently delete (hard delete) tenant account")]
    [EndpointDescription("Permanently and irreversibly deletes a tenant organization. Admin operation requiring proper authorization.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PurgeTenant(Guid tenantId, CancellationToken ct)
    {
        await sender.Send(new DeleteTenantCommand(tenantId), ct).ConfigureAwait(false);

        return NoContent();
    }

    #endregion

    #region Tenant Audit - /v1/tenants/{tenantId}/audit-log

    /// <summary>
    ///     Get tenant audit log
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="startDate">Optional start date filter</param>
    /// <param name="endDate">Optional end date filter</param>
    /// <param name="action">Optional action type filter (e.g., 'create', 'update', 'delete', 'settings_change')</param>
    /// <param name="actorId">Optional filter by actor (user who performed the action)</param>
    /// <param name="page">Page number for pagination (default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 200)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of audit log entries</returns>
    /// <remarks>
    ///     Retrieves the audit log for a specific tenant, showing all changes and actions performed.
    ///     Audit entries include:
    ///     - Timestamp of the action
    ///     - Action type (create, update, delete, settings change, etc.)
    ///     - Actor who performed the action
    ///     - Before and after values for changes
    ///     - IP address and user agent (when available)
    ///     - Correlation ID for request tracking
    /// </remarks>
    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/audit-log")]
    [EndpointSummary("Get tenant audit log")]
    [EndpointDescription("Retrieves the audit log for a tenant showing all changes, actions, and who performed them.")]
    [ProducesResponseType<PagedResult<TenantAuditLogEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantAuditLog(
        Guid tenantId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? action = null,
        [FromQuery] Guid? actorId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default
    )
    {
        // Validate pagination parameters
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 50;
        }

        if (pageSize > 200)
        {
            pageSize = 200;
        }

        var auditLog = await sender.Send(
            new GetTenantAuditLogQuery(tenantId, startDate, endDate, action, actorId, page, pageSize),
            ct
        ).ConfigureAwait(false);

        return Ok(auditLog);
    }

    #endregion
}
