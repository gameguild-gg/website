using Asp.Versioning;
using GameGuild.Identity.Authorization.Commands;
using GameGuild.Identity.Authorization.Queries;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authorization.Controllers;

/// <summary>
///     API controller for Separation of Duties (SoD) operations
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/separation-of-duties")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/sod")]
[Authorize]
[Produces("application/json")]
public class SoDController(ISender sender) : BaseApiController
{
    // =========================================================================
    // SoD Rules
    // =========================================================================

    /// <summary>
    ///     Create a new SoD rule
    /// </summary>
    [HttpPost("rules")]
    [ProducesResponseType(typeof(SoDRule), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRule(
        [FromBody] CreateSoDRuleCommand command,
        CancellationToken cancellationToken
    )
    {
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetRuleById), new { id = result.Id }, result);
    }

    /// <summary>
    ///     Update an existing SoD rule
    /// </summary>
    [HttpPut("rules/{id:guid}")]
    [ProducesResponseType(typeof(SoDRule), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRule(
        Guid id,
        [FromBody] UpdateSoDRuleRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new UpdateSoDRuleCommand(
            id,
            request.Name,
            request.Description,
            request.ConflictingPermissions,
            request.RuleType,
            request.IsEnabled
        );

        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    ///     Delete a SoD rule
    /// </summary>
    [HttpDelete("rules/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteSoDRuleCommand(id);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    ///     Get a SoD rule by ID
    /// </summary>
    [HttpGet("rules/{id:guid}")]
    [ProducesResponseType(typeof(SoDRule), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRuleById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetSoDRuleByIdQuery(id);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    ///     Get all SoD rules for a tenant
    /// </summary>
    [HttpGet("rules")]
    [ProducesResponseType(typeof(List<SoDRule>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRules(
        [FromQuery] Guid? tenantId,
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default
    )
    {
        if (activeOnly)
        {
            var activeQuery = new GetActiveSoDRulesQuery(tenantId);
            var activeResult = await sender.Send(activeQuery, cancellationToken).ConfigureAwait(false);
            return Ok(activeResult);
        }

        var query = new GetSoDRulesQuery(tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    // =========================================================================
    // SoD Violations
    // =========================================================================

    /// <summary>
    ///     Detect SoD violations for a user
    /// </summary>
    [HttpGet("violations/detect/{userId:guid}")]
    [ProducesResponseType(typeof(List<SoDViolation>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetectViolations(
        Guid userId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new DetectSoDViolationsQuery(userId, tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Get SoD violations for a user
    /// </summary>
    [HttpGet("violations/user/{userId:guid}")]
    [ProducesResponseType(typeof(List<SoDViolation>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserViolations(
        Guid userId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new GetUserSoDViolationsQuery(userId, tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Get active SoD violations
    /// </summary>
    [HttpGet("violations/active")]
    [ProducesResponseType(typeof(List<SoDViolation>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveViolations(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var query = new GetActiveSoDViolationsQuery(tenantId);
        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Resolve a SoD violation
    /// </summary>
    [HttpPost("violations/{id:guid}:resolve")]
    [ProducesResponseType(typeof(SoDViolation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveViolation(
        Guid id,
        [FromBody] ResolveViolationRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new ResolveSoDViolationCommand(id, request.ResolvedBy, request.Action, request.Notes);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Grant an exception for a SoD violation
    /// </summary>
    [HttpPost("violations/{id:guid}:exception")]
    [ProducesResponseType(typeof(SoDViolation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GrantException(
        Guid id,
        [FromBody] GrantExceptionRequest request,
        CancellationToken cancellationToken
    )
    {
        var command = new GrantSoDExceptionCommand(id, request.ApprovedBy, request.Justification);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Scan for SoD violations (admin only)
    /// </summary>
    [HttpPost("violations:scan")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> ScanViolations(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken
    )
    {
        var command = new ScanSoDViolationsCommand(tenantId);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new { ViolationsFound = result });
    }
}

// Request DTOs
public sealed record UpdateSoDRuleRequest(
    string Name,
    string Description,
    string[] ConflictingPermissions,
    SoDRuleType RuleType,
    bool IsEnabled
);

public sealed record ResolveViolationRequest(Guid ResolvedBy, SoDResolutionAction Action, string Notes);
public sealed record GrantExceptionRequest(Guid ApprovedBy, string Justification);
