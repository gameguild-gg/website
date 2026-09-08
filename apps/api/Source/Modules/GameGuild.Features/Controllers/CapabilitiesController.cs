using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;

namespace GameGuild.Features;

/// <summary>
/// Controller for managing tenant capabilities and entitlements.
/// Provides endpoints to query and manage capability states.
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/tenants/{tenantId:guid}/capabilities")]
[Authorize]
public sealed class CapabilitiesController : BaseApiController
{
    private readonly ICapabilityService _capabilityService;
    private readonly ISender? _sender;

    public CapabilitiesController(ICapabilityService capabilityService, ISender? sender = null)
    {
        _capabilityService = capabilityService;
        _sender = sender;
    }

    /// <summary>
    /// Gets all capabilities for a tenant with their enabled states.
    /// Returns a dictionary mapping capability keys to boolean enabled states.
    /// </summary>
    /// <remarks>
    /// Example response:
    /// ```json
    /// {
    ///   "lms.courses.basic": true,
    ///   "lms.enrollments": true,
    ///   "lms.certificates": false,
    ///   "lxp.discovery": true,
    ///   "lxp.learningPaths": false,
    ///   "lxp.recommendations.basic": false,
    ///   "lxp.recommendations.ai": false,
    ///   "lxp.skills": false,
    ///   "analytics.advanced": false,
    ///   "branding.custom": false
    /// }
    /// ```
    /// </remarks>
    /// <param name="tenantId">The tenant ID to query capabilities for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of capability keys to enabled states.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IDictionary<string, bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCapabilities(Guid tenantId, CancellationToken ct)
    {
        var capabilities = await _capabilityService.GetTenantCapabilitiesAsync(tenantId, ct).ConfigureAwait(false);
        return Ok(capabilities);
    }

    /// <summary>
    /// Checks if a specific capability is enabled for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID to check.</param>
    /// <param name="capability">The capability key to check (e.g., "lxp.discovery").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Boolean indicating if the capability is enabled.</returns>
    [HttpGet("{capability}")]
    [ProducesResponseType(typeof(CapabilityCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckCapability(Guid tenantId, string capability, CancellationToken ct)
    {
        var isEnabled = await _capabilityService.IsCapabilityEnabledAsync(tenantId, capability, ct).ConfigureAwait(false);
        return Ok(new CapabilityCheckResponse(capability, isEnabled));
    }

    /// <summary>
    /// Sets or updates a capability override for a tenant.
    /// Only accessible by tenant admins or platform administrators.
    /// </summary>
    /// <param name="tenantId">The tenant ID to modify.</param>
    /// <param name="request">The capability override request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetCapabilityOverride(
        Guid tenantId,
        [FromBody] SetCapabilityOverrideRequest request,
        CancellationToken ct)
    {
        // Get current user ID from claims (fail-closed if not available)
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value;
        Guid? userId = Guid.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : null;

        await _sender!.Send(new SetCapabilityOverrideCommand(
            tenantId,
            request.Capability,
            request.IsEnabled,
            request.Source ?? "override:admin",
            userId,
            request.Reason,
            request.ExpiresAt),
            ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// Removes a capability override, reverting to the subscription plan default.
    /// </summary>
    /// <param name="tenantId">The tenant ID to modify.</param>
    /// <param name="capability">The capability to remove the override for.</param>
    /// <param name="reason">Optional reason for removing the override.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{capability}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveCapabilityOverride(
        Guid tenantId,
        string capability,
        [FromQuery] string? reason,
        CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value;
        Guid? userId = Guid.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : null;

        await _sender!.Send(
            new RemoveCapabilityOverrideCommand(tenantId, capability, userId, reason),
            ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Syncs capabilities from the tenant's current subscription plan.
    /// Useful after subscription changes or plan upgrades.
    /// </summary>
    /// <param name="tenantId">The tenant ID to sync.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SyncFromPlan(Guid tenantId, CancellationToken ct)
    {
        await _sender!.Send(new SyncCapabilitiesFromPlanCommand(tenantId), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Gets the audit log for capability changes.
    /// </summary>
    /// <param name="tenantId">The tenant ID to query.</param>
    /// <param name="capability">Optional capability filter.</param>
    /// <param name="fromDate">Optional start date filter.</param>
    /// <param name="toDate">Optional end date filter.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("audit-log")]
    [ProducesResponseType(typeof(IEnumerable<CapabilityAuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLog(
        Guid tenantId,
        [FromQuery] string? capability,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        CancellationToken ct)
    {
        var logs = await _capabilityService.GetAuditLogAsync(tenantId, capability, fromDate, toDate, ct).ConfigureAwait(false);
        var dtos = logs.Select(log => new CapabilityAuditLogDto(
            log.Id,
            log.TenantId,
            log.CapabilityKey,
            log.OldValue,
            log.NewValue,
            log.OldSource,
            log.NewSource,
            log.ChangedByUserId,
            log.ChangeReason,
            log.ChangeType.ToString(),
            log.ChangedAt));

        return Ok(dtos);
    }
}

/// <summary>
/// Response for capability check endpoint.
/// </summary>
public sealed record CapabilityCheckResponse(string Capability, bool IsEnabled);

/// <summary>
/// Request for setting a capability override.
/// </summary>
public sealed record SetCapabilityOverrideRequest(
    string Capability,
    bool IsEnabled,
    string? Source,
    string? Reason,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// DTO for capability audit log entries.
/// </summary>
public sealed record CapabilityAuditLogDto(
    Guid Id,
    Guid TenantId,
    string CapabilityKey,
    bool? OldValue,
    bool NewValue,
    string? OldSource,
    string? NewSource,
    Guid? ChangedByUserId,
    string? ChangeReason,
    string ChangeType,
    DateTimeOffset ChangedAt);
