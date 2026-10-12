using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Tenant-administration surface for the PII redaction and tokenization pipeline (issue #192):
/// status, guarded/versioned/audited rule CRUD, a dry-run test endpoint, and authorized token
/// resolution from the token vault. Mutations run through CQRS commands; every write is audited.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/audit/redaction-pipeline")]
[Route("api/audit/redaction-pipeline")]
[Tags("compliance/audit/redaction-pipeline")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class PiiRedactionPipelineController(IPiiRedactionAdminService service, ISender sender) : ControllerBase
{
    /// <summary>
    /// Pipeline status: cached rule snapshots, enabled rule counts (global and tenant), vault size,
    /// and the last rule-refresh outcome.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PiiRedactionPipelineStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PiiRedactionPipelineStatusResponse>> GetStatus(CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => await service.GetStatusAsync(cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>Lists the tenant's rules plus the platform-global rules visible to it.</summary>
    [HttpGet("rules")]
    [ProducesResponseType(typeof(IReadOnlyList<PiiRedactionRuleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PiiRedactionRuleResponse>>> ListRules(CancellationToken cancellationToken) =>
        await ExecuteAsync<IReadOnlyList<PiiRedactionRuleResponse>>(
            async () => await service.ListRulesAsync(cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>Gets one rule by id.</summary>
    [HttpGet("rules/{ruleId:guid}")]
    [ProducesResponseType(typeof(PiiRedactionRuleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PiiRedactionRuleResponse>> GetRule(Guid ruleId, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () => await service.GetRuleAsync(ruleId, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>Creates a redaction rule. Guarded to tenant administrators; audited; invalidates the rule cache.</summary>
    [HttpPost("rules")]
    [ProducesResponseType(typeof(PiiRedactionRuleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PiiRedactionRuleResponse>> CreateRule(
        [FromBody] UpsertPiiRedactionRuleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var rule = await sender.Send(new CreatePiiRedactionRuleCommand(request), cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetRule), new { ruleId = rule.Id }, rule);
        }
        catch (Exception exception)
        {
            return MapException(exception) ?? throw exception;
        }
    }

    /// <summary>
    /// Updates a rule. Versioned: the request must carry the revision observed on read; a stale
    /// revision returns 409 Conflict.
    /// </summary>
    [HttpPut("rules/{ruleId:guid}")]
    [ProducesResponseType(typeof(PiiRedactionRuleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PiiRedactionRuleResponse>> UpdateRule(
        Guid ruleId,
        [FromQuery(Name = "expectedRevision")] int expectedRevision,
        [FromBody] UpsertPiiRedactionRuleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var rule = await sender.Send(new UpdatePiiRedactionRuleCommand(ruleId, expectedRevision, request), cancellationToken).ConfigureAwait(false);
            return Ok(rule);
        }
        catch (Exception exception)
        {
            return MapException(exception) ?? throw exception;
        }
    }

    /// <summary>Deletes a rule. Versioned like the update; audited at high risk.</summary>
    [HttpDelete("rules/{ruleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteRule(
        Guid ruleId,
        [FromQuery(Name = "expectedRevision")] int expectedRevision,
        CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new DeletePiiRedactionRuleCommand(ruleId, expectedRevision), cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception exception)
        {
            return MapException(exception) ?? throw exception;
        }
    }

    /// <summary>
    /// Dry-runs the enabled rules against sample field values without persisting anything — the
    /// pattern-recognition verification surface for administrators.
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(PiiRedactionTestRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PiiRedactionTestRunResponse>> Test(
        [FromBody] PiiRedactionTestRunRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await sender.Send(new TestPiiRedactionPipelineCommand(request), cancellationToken).ConfigureAwait(false);
            return Ok(response);
        }
        catch (Exception exception)
        {
            return MapException(exception) ?? throw exception;
        }
    }

    /// <summary>
    /// Resolves a deterministic token to its original value from the token vault for an authorized
    /// investigation. Requires a reason; audited at critical risk.
    /// </summary>
    [HttpGet("tokens/{token}")]
    [ProducesResponseType(typeof(PiiTokenResolutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PiiTokenResolutionResponse>> ResolveToken(
        string token, [FromQuery(Name = "reason")] string reason, CancellationToken cancellationToken)
    {
        try
        {
            var response = await sender.Send(new ResolvePiiTokenCommand(token, reason), cancellationToken).ConfigureAwait(false);
            return Ok(response);
        }
        catch (Exception exception)
        {
            return MapException(exception) ?? throw exception;
        }
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T?>> operation) where T : class
    {
        try
        {
            var response = await operation().ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (Exception exception)
        {
            return MapException(exception) ?? throw exception;
        }
    }

    private ActionResult? MapException(Exception exception) => exception switch
    {
        PiiRedactionUnauthorizedException => Forbid(),
        KeyNotFoundException => NotFound(),
        PiiRedactionValidationException validation => BadRequest(ToValidationProblem(validation.Errors)),
        PiiRedactionConcurrencyException concurrency => Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Rule revision conflict",
            Detail = concurrency.Message
        }),
        InvalidOperationException invalid when invalid.Message.Contains("decrypted", StringComparison.Ordinal) =>
            BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Token resolution failed",
                Detail = invalid.Message
            }),
        _ => null
    };

    private static ValidationProblemDetails ToValidationProblem(IReadOnlyDictionary<string, string[]> errors)
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var (key, messages) in errors)
        {
            modelState.AddModelError(key, string.Join(" ", messages));
        }

        return new ValidationProblemDetails(modelState)
        {
            Status = StatusCodes.Status400BadRequest
        };
    }
}
