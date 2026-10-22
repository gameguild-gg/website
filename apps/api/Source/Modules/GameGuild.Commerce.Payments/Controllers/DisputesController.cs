using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Dispute management surface (issue #419): exposes the dispute CQRS stack — list/get,
///     create, evidence, respond, accept, challenge, cancel and resolve — over
///     <c>api/v{version}/disputes</c>. All endpoints require authentication; mutations that
///     move money (accept, challenge) are additionally permission-guarded inside their
///     commands, and every dispute access is tenant-validated through its payment.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Microsoft.AspNetCore.Http.Tags("disputes")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
public sealed class DisputesController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     List disputes with optional filters, newest first.
    /// </summary>
    /// <param name="status">Optional dispute-status filter.</param>
    /// <param name="paymentId">Optional payment filter.</param>
    /// <param name="userId">Optional user filter.</param>
    /// <param name="page">Page number for pagination (1-based).</param>
    /// <param name="pageSize">Items per page (1-100).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("List payment disputes")]
    [EndpointDescription("Lists payment disputes with optional status, payment and user filters, newest first. Non-admin actors only see their own tenant's disputes.")]
    [ProducesResponseType<DisputeListPage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] DisputeStatus? status = null,
        [FromQuery] Guid? paymentId = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await sender.Send(new ListDisputesQuery(status, paymentId, userId, page, pageSize), ct).ConfigureAwait(false);

        // Non-admin actors are scoped to their own tenant: drop disputes whose payment belongs
        // to another tenant rather than leaking their existence.
        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated && !actorContext.IsSystemAdmin)
        {
            var tenant = actorContext.TenantId;
            var filtered = new List<DisputeRecord>(result.Items.Count);
            foreach (var item in result.Items)
            {
                var payment = await sender.Send(new GetPaymentByIdQuery(item.PaymentId), ct).ConfigureAwait(false);
                if (payment is not null && payment.TenantId == tenant)
                {
                    filtered.Add(item);
                }
            }

            return Ok(new DisputeListPage
            {
                Items = filtered,
                TotalCount = filtered.Count,
                Page = result.Page,
                PageSize = result.PageSize
            });
        }

        return Ok(result);
    }

    /// <summary>
    ///     Retrieve a single dispute by id.
    /// </summary>
    /// <param name="disputeId">Dispute identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{disputeId:guid}", Name = "GetDisputeById")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("Retrieve a payment dispute")]
    [EndpointDescription("Retrieves a single payment dispute with its attached evidence.")]
    [ProducesResponseType<PaymentDispute>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid disputeId, CancellationToken ct)
    {
        var dispute = await sender.Send(new GetDisputeByIdQuery(disputeId), ct).ConfigureAwait(false);
        if (dispute == null) return NotFound();

        var validationError = await ValidateDisputeTenantAccessAsync(dispute.PaymentId, "read dispute", ct).ConfigureAwait(false);
        if (validationError != null) return validationError;

        return Ok(dispute);
    }

    /// <summary>
    ///     List the evidence attached to a dispute.
    /// </summary>
    /// <param name="disputeId">Dispute identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{disputeId:guid}/evidence")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("List dispute evidence")]
    [EndpointDescription("Lists the evidence files attached to a payment dispute, newest first.")]
    [ProducesResponseType<IEnumerable<DisputeEvidence>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvidence(Guid disputeId, CancellationToken ct)
    {
        var dispute = await sender.Send(new GetDisputeByIdQuery(disputeId), ct).ConfigureAwait(false);
        if (dispute == null) return NotFound();

        var validationError = await ValidateDisputeTenantAccessAsync(dispute.PaymentId, "read dispute evidence", ct).ConfigureAwait(false);
        if (validationError != null) return validationError;

        return Ok(await sender.Send(new GetDisputeEvidenceQuery(disputeId), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Attach evidence to a dispute. The submitting user is the authenticated actor.
    /// </summary>
    /// <param name="disputeId">Dispute identifier.</param>
    /// <param name="body">Evidence payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{disputeId:guid}/evidence")]
    [EndpointSummary("Attach evidence to a dispute")]
    [EndpointDescription("Attaches an evidence entry to a payment dispute. The submitting identity comes from the authenticated actor, never the request body.")]
    [ProducesResponseType<DisputeEvidence>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEvidence(Guid disputeId, [FromBody] AddDisputeEvidenceRequest body, CancellationToken ct)
    {
        var dispute = await sender.Send(new GetDisputeByIdQuery(disputeId), ct).ConfigureAwait(false);
        if (dispute == null) return NotFound();

        var validationError = await ValidateDisputeTenantAccessAsync(dispute.PaymentId, "add dispute evidence", ct).ConfigureAwait(false);
        if (validationError != null) return validationError;

        var submittedBy = actorContextAccessor.ActorContext.SubjectIdAsGuid
            ?? Guid.Empty; // gateway/system-initiated evidence

        var evidence = await sender.Send(new AddDisputeEvidenceCommand(
            disputeId,
            body.EvidenceType,
            body.Title,
            body.Description,
            submittedBy,
            IsFromMerchant: true,
            body.FileUrl,
            body.FileName,
            body.FileSize,
            body.MimeType), ct).ConfigureAwait(false);

        return Ok(evidence);
    }

    /// <summary>
    ///     Create a dispute for a payment. The disputing user is the authenticated actor.
    /// </summary>
    /// <param name="body">Dispute payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [EndpointSummary("Create a payment dispute")]
    [EndpointDescription("Creates a dispute against a payment. The disputing identity comes from the authenticated actor, never the request body.")]
    [ProducesResponseType<PaymentDispute>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateDisputeRequest body, CancellationToken ct)
    {
        var payment = await sender.Send(new GetPaymentByIdQuery(body.PaymentId), ct).ConfigureAwait(false);
        if (payment == null) return NotFound();

        var validationError = ValidateTenantAccess(payment.TenantId, "create dispute");
        if (validationError != null) return validationError;

        if (body.Amount <= 0)
        {
            return BadRequest(new { error = "Dispute amount must be greater than zero" });
        }

        var userId = actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty;

        var dispute = await sender.Send(new CreateDisputeCommand(
            body.PaymentId,
            userId,
            body.Type,
            body.Amount,
            body.Reason,
            body.Description ?? string.Empty), ct).ConfigureAwait(false);

        return CreatedAtRoute("GetDisputeById", new { disputeId = dispute.Id }, dispute);
    }

    /// <summary>
    ///     Respond to a dispute by advancing its status (review, customer or merchant response).
    /// </summary>
    /// <param name="disputeId">Dispute identifier.</param>
    /// <param name="body">Status transition request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{disputeId:guid}:respond")]
    [EndpointSummary("Respond to a dispute")]
    [EndpointDescription("Advances a dispute's status (under review, pending customer or merchant response). Custom action per Google API guidelines.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Respond(Guid disputeId, [FromBody] RespondDisputeRequest body, CancellationToken ct)
    {
        var validationError = await ValidateDisputeExistsForTenantAsync(disputeId, "respond to dispute", ct).ConfigureAwait(false);
        if (validationError is NotFoundResult) return NotFound();
        if (validationError != null) return validationError;

        await sender.Send(new UpdateDisputeStatusCommand(disputeId, body.Status, body.DueDateUtc), ct).ConfigureAwait(false);

        return Ok();
    }

    /// <summary>
    ///     Accept a dispute: closes it as lost at the gateway and records the refund through the
    ///     approval-aware refund path. Requires the <c>payments:admin</c> permission.
    /// </summary>
    /// <param name="disputeId">Dispute identifier.</param>
    /// <param name="body">Optional acceptance notes and refund flag.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{disputeId:guid}:accept")]
    [EndpointSummary("Accept a dispute and issue the refund")]
    [EndpointDescription("Accepts the dispute (lost) and records the refund through the refund approval workflow. Requires the payments:admin permission.")]
    [ProducesResponseType<DisputeActionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Accept(Guid disputeId, [FromBody] AcceptDisputeRequest? body, CancellationToken ct)
    {
        var validationError = await ValidateDisputeExistsForTenantAsync(disputeId, "accept dispute", ct).ConfigureAwait(false);
        if (validationError is NotFoundResult) return NotFound();
        if (validationError != null) return validationError;

        var result = await sender.Send(new AcceptDisputeCommand(disputeId, body?.Notes, body?.IssueRefund ?? true), ct).ConfigureAwait(false);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    ///     Challenge a dispute by submitting the merchant evidence to the gateway. Requires the
    ///     <c>payments:admin</c> permission.
    /// </summary>
    /// <param name="disputeId">Dispute identifier.</param>
    /// <param name="body">Optional challenge evidence summary.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{disputeId:guid}:challenge")]
    [EndpointSummary("Challenge a dispute")]
    [EndpointDescription("Submits the merchant evidence to the gateway to contest the chargeback. Requires the payments:admin permission.")]
    [ProducesResponseType<DisputeActionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Challenge(Guid disputeId, [FromBody] ChallengeDisputeRequest? body, CancellationToken ct)
    {
        var validationError = await ValidateDisputeExistsForTenantAsync(disputeId, "challenge dispute", ct).ConfigureAwait(false);
        if (validationError is NotFoundResult) return NotFound();
        if (validationError != null) return validationError;

        var result = await sender.Send(new ChallengeDisputeCommand(disputeId, body?.EvidenceSummary), ct).ConfigureAwait(false);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    ///     Submit the dispute evidence bundle to the gateway: assembles every attached evidence
    ///     row into one bundle, delivers it through the gateway and records the submission status
    ///     on the dispute and each evidence item. Requires the <c>payments:admin</c> permission.
    /// </summary>
    /// <param name="disputeId">Dispute identifier.</param>
    /// <param name="body">Optional bundle summary prepended to the assembled evidence.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{disputeId:guid}:submit-evidence")]
    [EndpointSummary("Submit the evidence bundle to the gateway")]
    [EndpointDescription("Assembles every evidence row attached to the dispute into one bundle, submits it to the gateway and records the submission status. Requires the payments:admin permission.")]
    [ProducesResponseType<DisputeActionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitEvidence(Guid disputeId, [FromBody] SubmitDisputeEvidenceRequest? body, CancellationToken ct)
    {
        var validationError = await ValidateDisputeExistsForTenantAsync(disputeId, "submit dispute evidence", ct).ConfigureAwait(false);
        if (validationError is NotFoundResult) return NotFound();
        if (validationError != null) return validationError;

        var result = await sender.Send(new SubmitDisputeEvidenceCommand(disputeId, body?.Summary), ct).ConfigureAwait(false);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    ///     Cancel a dispute.
    /// </summary>
    /// <param name="disputeId">Dispute identifier.</param>
    /// <param name="body">Cancellation reason.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{disputeId:guid}:cancel")]
    [EndpointSummary("Cancel a dispute")]
    [EndpointDescription("Cancels an open dispute with a reason. Custom action per Google API guidelines.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid disputeId, [FromBody] CancelDisputeRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Reason))
        {
            return BadRequest(new { error = "A cancellation reason is required" });
        }

        var validationError = await ValidateDisputeExistsForTenantAsync(disputeId, "cancel dispute", ct).ConfigureAwait(false);
        if (validationError is NotFoundResult) return NotFound();
        if (validationError != null) return validationError;

        await sender.Send(new CancelDisputeCommand(disputeId, body.Reason), ct).ConfigureAwait(false);

        return Ok();
    }

    /// <summary>
    ///     Resolve a dispute with an explicit outcome.
    /// </summary>
    /// <param name="disputeId">Dispute identifier.</param>
    /// <param name="body">Resolution outcome and notes; resolved-by comes from the actor context.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{disputeId:guid}:resolve")]
    [EndpointSummary("Resolve a dispute")]
    [EndpointDescription("Resolves a dispute with an explicit outcome (won, lost, partial refund, ...). Custom action per Google API guidelines.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(Guid disputeId, [FromBody] ResolveDisputeRequest body, CancellationToken ct)
    {
        var validationError = await ValidateDisputeExistsForTenantAsync(disputeId, "resolve dispute", ct).ConfigureAwait(false);
        if (validationError is NotFoundResult) return NotFound();
        if (validationError != null) return validationError;

        var resolvedBy = actorContextAccessor.ActorContext.SubjectIdAsGuid;

        await sender.Send(new ResolveDisputeCommand(disputeId, body.Resolution, body.Notes, resolvedBy), ct).ConfigureAwait(false);

        return Ok();
    }

    /// <summary>
    ///     Dispute statistics for an inclusive window.
    /// </summary>
    /// <param name="fromUtc">Inclusive window start (UTC).</param>
    /// <param name="toUtc">Inclusive window end (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("statistics")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("Get dispute statistics for a period")]
    [EndpointDescription("Returns dispute counts by status, win/loss ratio for decided disputes and the average time to resolution for an inclusive window.")]
    [ProducesResponseType<DisputeStatistics>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTimeOffset fromUtc,
        [FromQuery] DateTimeOffset toUtc,
        CancellationToken ct = default)
    {
        if (fromUtc > toUtc)
        {
            return BadRequest(new { error = "fromUtc must be earlier than or equal to toUtc" });
        }

        // Dispute statistics are an administration view: system-admins see the platform,
        // everyone else is tenant-scoped through the dispute's payment.
        Guid? tenantId = null;
        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated && !actorContext.IsSystemAdmin)
        {
            if (actorContext.TenantId is null)
            {
                return Forbid();
            }

            tenantId = actorContext.TenantId;
        }

        return Ok(await sender.Send(new GetDisputeStatisticsQuery(fromUtc.UtcDateTime, toUtc.UtcDateTime, tenantId), ct).ConfigureAwait(false));
    }

    /// <summary>Request body for creating a dispute.</summary>
    public sealed record CreateDisputeRequest(Guid PaymentId, DisputeType Type, decimal Amount, string Reason, string? Description = null);

    /// <summary>Request body for attaching evidence.</summary>
    public sealed record AddDisputeEvidenceRequest(
        EvidenceType EvidenceType,
        string Title,
        string Description,
        string? FileUrl = null,
        string? FileName = null,
        long? FileSize = null,
        string? MimeType = null);

    /// <summary>Request body for responding to a dispute.</summary>
    public sealed record RespondDisputeRequest(DisputeStatus Status, DateTime? DueDateUtc = null);

    /// <summary>Request body for accepting a dispute.</summary>
    public sealed record AcceptDisputeRequest(string? Notes = null, bool IssueRefund = true);

    /// <summary>Request body for challenging a dispute.</summary>
    public sealed record ChallengeDisputeRequest(string? EvidenceSummary = null);

    /// <summary>Request body for submitting the evidence bundle to the gateway.</summary>
    public sealed record SubmitDisputeEvidenceRequest(string? Summary = null);

    /// <summary>Request body for cancelling a dispute.</summary>
    public sealed record CancelDisputeRequest(string Reason);

    /// <summary>Request body for resolving a dispute.</summary>
    public sealed record ResolveDisputeRequest(DisputeResolution Resolution, string? Notes = null);

    private IActionResult? ValidateTenantAccess(Guid requestedTenantId, string operation)
        => actorContextAccessor.ActorContext.IsSystemAdmin
            ? null
            : actorContextAccessor.ValidateTenantAccessAsActionResult(requestedTenantId, operation);

    private async Task<IActionResult?> ValidateDisputeTenantAccessAsync(Guid paymentId, string operation, CancellationToken ct)
    {
        var payment = await sender.Send(new GetPaymentByIdQuery(paymentId), ct).ConfigureAwait(false);
        if (payment == null) return NotFound();

        return ValidateTenantAccess(payment.TenantId, operation);
    }

    private async Task<IActionResult?> ValidateDisputeExistsForTenantAsync(Guid disputeId, string operation, CancellationToken ct)
    {
        var dispute = await sender.Send(new GetDisputeByIdQuery(disputeId), ct).ConfigureAwait(false);
        if (dispute == null) return NotFound();

        return await ValidateDisputeTenantAccessAsync(dispute.PaymentId, operation, ct).ConfigureAwait(false);
    }
}
