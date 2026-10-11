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
///     Manual fraud-review administration (issue #409). Lists payments held by the fraud
///     gate for review and resolves them (approve / decline) with notes. Authorization is
///     enforced per request through the <c>payments:admin</c> permission
///     (<see cref="GetFraudReviewsQuery"/> / <see cref="ResolveFraudReviewCommand"/>) in
///     addition to the controller-level authentication requirement.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/fraud/reviews")]
[Microsoft.AspNetCore.Http.Tags("fraud/reviews")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
public sealed class FraudReviewsController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     List the pending manual fraud reviews, oldest first.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter (system administrators only; other callers are scoped to their own tenant).</param>
    /// <param name="page">Page number for pagination (1-based).</param>
    /// <param name="pageSize">Items per page (1-100).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("List pending fraud reviews")]
    [EndpointDescription("Lists payments held by the fraud gate with a pending manual review, oldest first. Requires the payments:admin permission.")]
    [ProducesResponseType<IEnumerable<FraudReviewListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReviews(
        [FromQuery] Guid? tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) { page = 1; }
        if (pageSize < 1 || pageSize > 100) { pageSize = 20; }

        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated && !actorContext.IsSystemAdmin)
        {
            var requestedTenantId = tenantId ?? actorContext.TenantId ?? Guid.Empty;
            var validationError = ValidateTenantAccess(requestedTenantId, "list fraud reviews");
            if (validationError != null) { return validationError; }

            tenantId = actorContext.TenantId;
        }

        return Ok(await sender.Send(new GetFraudReviewsQuery(tenantId, page, pageSize), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Approve a pending fraud review, releasing the fraud hold so the payment may be retried.
    /// </summary>
    /// <param name="paymentId">Payment whose review is approved.</param>
    /// <param name="body">Optional reviewer notes.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{paymentId:guid}:approve")]
    [EndpointSummary("Approve a pending fraud review")]
    [EndpointDescription("Approves the fraud review for a held payment, releasing the fraud hold so the payment can be retried through the manual retry flow. Requires the payments:admin permission.")]
    [ProducesResponseType<FraudReviewResolutionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveReview(Guid paymentId, [FromBody] ResolveFraudReviewRequest? body, CancellationToken ct)
    {
        var result = await sender.Send(new ResolveFraudReviewCommand(paymentId, Approve: true, body?.Notes), ct).ConfigureAwait(false);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    ///     Decline a pending fraud review, keeping the payment failed and non-retryable.
    /// </summary>
    /// <param name="paymentId">Payment whose review is declined.</param>
    /// <param name="body">Optional reviewer notes.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{paymentId:guid}:deny")]
    [EndpointSummary("Decline a pending fraud review")]
    [EndpointDescription("Declines the fraud review for a held payment; it stays failed and is never retried. Requires the payments:admin permission.")]
    [ProducesResponseType<FraudReviewResolutionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DenyReview(Guid paymentId, [FromBody] ResolveFraudReviewRequest? body, CancellationToken ct)
    {
        var result = await sender.Send(new ResolveFraudReviewCommand(paymentId, Approve: false, body?.Notes), ct).ConfigureAwait(false);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Request body for resolving a fraud review.</summary>
    public sealed record ResolveFraudReviewRequest(string? Notes);

    private IActionResult? ValidateTenantAccess(Guid requestedTenantId, string operation)
        => actorContextAccessor.ActorContext.IsSystemAdmin
            ? null
            : actorContextAccessor.ValidateTenantAccessAsActionResult(requestedTenantId, operation);
}
