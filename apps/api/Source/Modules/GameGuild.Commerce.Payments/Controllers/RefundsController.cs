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
///     Refund request and approval surface (issue #419) over <c>api/v{version}/refunds</c>:
///     request a refund (queued for manual approval at or above the configured threshold),
///     list refunds, browse the pending-approval queue, approve or reject queued refunds and
///     read refund statistics. The immediate synchronous refund path remains on
///     <c>POST /payments/{paymentId}:refund</c>. Approval decisions are permission-guarded
///     (<c>payments:refunds:approve</c>) inside their commands.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Microsoft.AspNetCore.Http.Tags("refunds")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
public sealed class RefundsController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     Request a refund for a payment. Amounts at or above the manual-approval threshold
    ///     are queued as pending; amounts below it (or all refunds when no threshold is
    ///     configured) execute immediately.
    /// </summary>
    /// <param name="body">Refund request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [EndpointSummary("Request a refund for a payment")]
    [EndpointDescription("Records a refund request. Amounts at or above the configured manual-approval threshold enter the pending-approval queue; smaller amounts (or all refunds when no threshold is configured) execute immediately through the gateway. Use kind=prorated to refund the unused time of the subscription billing period (subscription-linked payments only).")]
    [ProducesResponseType<RequestRefundResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestRefund([FromBody] RequestRefundHttpRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Reason))
        {
            return BadRequest(new { error = "A refund reason is required" });
        }

        // SECURITY: the refund target must belong to the caller's tenant (validated through the payment).
        var payment = await sender.Send(new GetPaymentByIdQuery(body.PaymentId), ct).ConfigureAwait(false);
        if (payment == null) return NotFound();

        var validationError = ValidateTenantAccess(payment.TenantId, "request refund");
        if (validationError != null) return validationError;

        var result = await sender.Send(new RequestRefundCommand(
            body.PaymentId,
            body.Amount,
            body.Reason,
            body.ReasonDetails,
            body.IdempotencyKey,
            body.Kind), ct).ConfigureAwait(false);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    ///     List refunds with optional filters, newest first.
    /// </summary>
    /// <param name="status">Optional refund-status filter.</param>
    /// <param name="fromUtc">Optional inclusive window start.</param>
    /// <param name="toUtc">Optional inclusive window end.</param>
    /// <param name="page">Page number for pagination (1-based).</param>
    /// <param name="pageSize">Items per page (1-100).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("List refunds")]
    [EndpointDescription("Lists refunds with optional status and period filters, newest first. Non-admin actors are scoped to their own tenant.")]
    [ProducesResponseType<RefundListPage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] RefundStatus? status = null,
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        Guid? tenantId = null;
        var validationError = ResolveTenantScope(ref tenantId, "list refunds");
        if (validationError != null) return validationError;

        return Ok(await sender.Send(new ListRefundsQuery(
            tenantId,
            status,
            fromUtc?.UtcDateTime,
            toUtc?.UtcDateTime,
            page,
            pageSize), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Retrieve a single refund by id.
    /// </summary>
    /// <param name="refundId">Refund identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{refundId:guid}", Name = "GetRefundById")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("Retrieve a refund")]
    [EndpointDescription("Retrieves a single refund with its lifecycle state, decision trail and gateway references.")]
    [ProducesResponseType<RefundRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid refundId, CancellationToken ct)
    {
        var refund = await sender.Send(new GetRefundByIdQuery(refundId), ct).ConfigureAwait(false);
        if (refund is null) return NotFound();

        var validationError = ValidateTenantAccess(refund.TenantId, "read refund");
        if (validationError != null) return validationError;

        return Ok(refund);
    }

    /// <summary>
    ///     The pending refund-approval queue, oldest first. Requires the
    ///     <c>payments:refunds:approve</c> permission.
    /// </summary>
    /// <param name="page">Page number for pagination (1-based).</param>
    /// <param name="pageSize">Items per page (1-100).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("pending-approvals")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("List pending refund approvals")]
    [EndpointDescription("Lists refunds waiting for a manual approval decision, oldest first. Requires the payments:refunds:approve permission; non-admin actors are scoped to their own tenant.")]
    [ProducesResponseType<RefundApprovalQueuePage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingApprovals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        Guid? tenantId = null;
        var validationError = ResolveTenantScope(ref tenantId, "list pending refund approvals");
        if (validationError != null) return validationError;

        return Ok(await sender.Send(new GetPendingRefundApprovalsQuery(tenantId, page, pageSize), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Approve a pending refund and execute it. Requires the
    ///     <c>payments:refunds:approve</c> permission.
    /// </summary>
    /// <param name="refundId">Refund awaiting the decision.</param>
    /// <param name="body">Optional approval notes.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{refundId:guid}:approve")]
    [EndpointSummary("Approve a pending refund")]
    [EndpointDescription("Approves the pending refund and executes it through the gateway. Requires the payments:refunds:approve permission. Custom action per Google API guidelines.")]
    [ProducesResponseType<RefundDecisionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid refundId, [FromBody] RefundDecisionRequest? body, CancellationToken ct)
    {
        var validationError = await ValidateRefundTenantAccessAsync(refundId, "approve refund", ct).ConfigureAwait(false);
        if (validationError != null) return validationError;

        var result = await sender.Send(new ApproveRefundCommand(refundId, body?.Notes), ct).ConfigureAwait(false);

        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    ///     Reject a pending refund; no money moves. Requires the
    ///     <c>payments:refunds:approve</c> permission.
    /// </summary>
    /// <param name="refundId">Refund awaiting the decision.</param>
    /// <param name="body">Optional rejection notes.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{refundId:guid}:reject")]
    [EndpointSummary("Reject a pending refund")]
    [EndpointDescription("Rejects the pending refund; no money moves. Requires the payments:refunds:approve permission. Custom action per Google API guidelines.")]
    [ProducesResponseType<RefundDecisionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid refundId, [FromBody] RefundDecisionRequest? body, CancellationToken ct)
    {
        var validationError = await ValidateRefundTenantAccessAsync(refundId, "reject refund", ct).ConfigureAwait(false);
        if (validationError != null) return validationError;

        var result = await sender.Send(new RejectRefundCommand(refundId, body?.Notes), ct).ConfigureAwait(false);

        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    ///     Refund statistics for an inclusive window.
    /// </summary>
    /// <param name="fromUtc">Inclusive window start (UTC).</param>
    /// <param name="toUtc">Inclusive window end (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("statistics")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("Get refund statistics for a period")]
    [EndpointDescription("Returns refund counts by status, completed refund amounts by currency and the reason breakdown for an inclusive window.")]
    [ProducesResponseType<RefundStatistics>(StatusCodes.Status200OK)]
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

        Guid? tenantId = null;
        var validationError = ResolveTenantScope(ref tenantId, "read refund statistics");
        if (validationError != null) return validationError;

        return Ok(await sender.Send(new GetRefundStatisticsQuery(fromUtc.UtcDateTime, toUtc.UtcDateTime, tenantId), ct).ConfigureAwait(false));
    }

    /// <summary>Request body for requesting a refund.</summary>
    public sealed record RequestRefundHttpRequest(
        Guid PaymentId,
        decimal? Amount,
        string Reason,
        string? ReasonDetails = null,
        string? IdempotencyKey = null,
        RefundKind Kind = RefundKind.Full);

    /// <summary>Request body for approve/reject decisions.</summary>
    public sealed record RefundDecisionRequest(string? Notes = null);

    private IActionResult? ValidateTenantAccess(Guid requestedTenantId, string operation)
        => actorContextAccessor.ActorContext.IsSystemAdmin
            ? null
            : actorContextAccessor.ValidateTenantAccessAsActionResult(requestedTenantId, operation);

    /// <summary>
    ///     Resolves the tenant scope for platform-wide listings: system admins may query the
    ///     platform (null scope); everyone else is pinned to their authenticated tenant.
    /// </summary>
    private IActionResult? ResolveTenantScope(ref Guid? tenantId, string operation)
    {
        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsSystemAdmin)
        {
            return null;
        }

        if (actorContext.TenantId is null)
        {
            return new ObjectResult(new { error = $"A tenant context is required to {operation}." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        tenantId = actorContext.TenantId;
        return null;
    }

    private async Task<IActionResult?> ValidateRefundTenantAccessAsync(Guid refundId, string operation, CancellationToken ct)
    {
        var refund = await sender.Send(new GetRefundByIdQuery(refundId), ct).ConfigureAwait(false);
        if (refund is null)
        {
            return NotFound();
        }

        return ValidateTenantAccess(refund.TenantId, operation);
    }
}
