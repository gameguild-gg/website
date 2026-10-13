using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authorization.Controllers;

/// <summary>
///     API controller for multi-stage permission approval workflows (issue #188). Every
///     stage decision is audited individually; the permission grant only executes through
///     the guarded-mutation flow after the final stage approves.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/permission-approvals")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/authorization/permission-approvals")]
[Authorize]
[Produces("application/json")]
public sealed class PermissionApprovalsController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Creates a pending multi-stage approval workflow for a tenant permission grant.
    /// </summary>
    /// <param name="command">The workflow request (subject, permissions, ordered stages).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Workflow created; the id of the created workflow.</response>
    /// <response code="400">Invalid request (e.g. no stages).</response>
    /// <response code="401">User not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePermissionApprovalCommand command,
        CancellationToken cancellationToken)
    {
        var workflowId = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = workflowId, version = "1" }, workflowId);
    }

    /// <summary>
    ///     Loads a workflow with its stages and decisions.
    /// </summary>
    /// <param name="id">The workflow id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The workflow.</response>
    /// <response code="404">Workflow not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PermissionApprovalWorkflow), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var workflow = await sender.Send(new GetPermissionApprovalQuery(id), cancellationToken).ConfigureAwait(false);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    /// <summary>
    ///     Decides the workflow's current stage. On the final approval the guarded grant
    ///     executes; a rejection terminates the workflow without granting.
    /// </summary>
    /// <param name="id">The workflow id.</param>
    /// <param name="request">The decision body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The decision result (stage decided, workflow status, grant applied).</response>
    /// <response code="400">The workflow is not pending or has no stage left (fail closed).</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="403">A different approver is required for this stage.</response>
    /// <response code="404">Workflow not found.</response>
    [HttpPost("{id:guid}/decisions")]
    [ProducesResponseType(typeof(PermissionApprovalDecisionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Decide(
        Guid id,
        [FromBody] DecidePermissionApprovalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new DecidePermissionApprovalCommand { WorkflowId = id, Approve = request.Approve, Notes = request.Notes },
                cancellationToken).ConfigureAwait(false);

            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { exception.Message });
        }
    }
}

/// <summary>
///     Body for deciding the current stage of an approval workflow. The approver
///     identity is never taken from the body — it comes from the actor context.
/// </summary>
public sealed record DecidePermissionApprovalRequest
{
    /// <summary>
    ///     Gets the decision for the current stage.
    /// </summary>
    public required bool Approve { get; init; }

    /// <summary>
    ///     Gets optional notes recorded with the decision.
    /// </summary>
    public string? Notes { get; init; }
}
