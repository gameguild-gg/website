using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Security event pipeline: the published security event taxonomy, the durable delivery status of
///     security logging, the security alert queue, and the security log retention policy.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/audit/security-events")]
[Route("api/audit/security-events")]
[Tags("compliance/audit/security-events")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class SecurityEventController(
    ISecurityEventQueryService queryService,
    ISecurityLogRetentionService retentionService,
    ISender sender) : ControllerBase
{
    /// <summary>Returns the complete security event taxonomy used by the security event pipeline.</summary>
    [HttpGet("taxonomy")]
    [ProducesResponseType(typeof(SecurityEventTaxonomyResponse), StatusCodes.Status200OK)]
    public ActionResult<SecurityEventTaxonomyResponse> GetTaxonomy() => Ok(queryService.GetTaxonomy());

    /// <summary>Returns the durable delivery status of the security event pipeline for this instance.</summary>
    [HttpGet("delivery-status")]
    [ProducesResponseType(typeof(SecurityEventDeliveryStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SecurityEventDeliveryStatusResponse>> GetDeliveryStatus(CancellationToken cancellationToken) =>
        Ok(await queryService.GetDeliveryStatusAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Returns the security alert queue for the current tenant.</summary>
    [HttpGet("alerts")]
    [ProducesResponseType(typeof(IReadOnlyList<SecurityAlertResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SecurityAlertResponse>>> GetAlerts(
        [FromQuery] SecurityAlertListRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await queryService.GetAlertsAsync(request, cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (SecurityLogRetentionValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(exception.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
                { Status = StatusCodes.Status400BadRequest });
        }
    }

    /// <summary>Acknowledges an open security alert. The acting administrator is derived from the request context.</summary>
    [HttpPost("alerts/{alertId:guid}/acknowledge")]
    [ProducesResponseType(typeof(SecurityAlertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SecurityAlertResponse>> AcknowledgeAlert(
        Guid alertId,
        [FromBody] AcknowledgeSecurityAlertRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await sender.Send(new AcknowledgeSecurityAlertCommand(alertId, request?.Notes), cancellationToken).ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Returns the security log retention policy for the current tenant, when configured.</summary>
    [HttpGet("retention/policy")]
    [ProducesResponseType(typeof(SecurityLogRetentionPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SecurityLogRetentionPolicyResponse>> GetRetentionPolicy(CancellationToken cancellationToken)
    {
        try
        {
            var response = await retentionService.GetPolicyAsync(cancellationToken).ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Creates or updates the security log retention policy for the current tenant.</summary>
    [HttpPut("retention/policy")]
    [ProducesResponseType(typeof(SecurityLogRetentionPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SecurityLogRetentionPolicyResponse>> ConfigureRetentionPolicy(
        [FromBody] ConfigureSecurityLogRetentionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(new ConfigureSecurityLogRetentionCommand(request), cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (SecurityLogRetentionValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(exception.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
                { Status = StatusCodes.Status400BadRequest });
        }
    }

    /// <summary>
    ///     Runs one retention enforcement pass for the current tenant now. Passes respect legal holds
    ///     and are recorded in the execution history; use <c>DryRun</c> to preview deletions.
    /// </summary>
    [HttpPost("retention/enforce")]
    [ProducesResponseType(typeof(SecurityLogRetentionExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SecurityLogRetentionExecutionResponse>> EnforceRetention(
        [FromBody] EnforceSecurityLogRetentionRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await sender.Send(new EnforceSecurityLogRetentionCommand(request ?? new EnforceSecurityLogRetentionRequest()), cancellationToken)
                .ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Returns the retention execution history for the current tenant.</summary>
    [HttpGet("retention/executions")]
    [ProducesResponseType(typeof(IReadOnlyList<SecurityLogRetentionExecutionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SecurityLogRetentionExecutionResponse>>> GetRetentionExecutions(
        [FromQuery] SecurityLogRetentionExecutionListRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await retentionService.GetExecutionsAsync(request.Skip, request.Take, cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (SecurityLogRetentionValidationException exception)
        {
            return BadRequest(new ValidationProblemDetails(exception.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
                { Status = StatusCodes.Status400BadRequest });
        }
    }
}
