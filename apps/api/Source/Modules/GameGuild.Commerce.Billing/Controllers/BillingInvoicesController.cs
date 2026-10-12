using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Commerce.Billing;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/invoices")]
[Microsoft.AspNetCore.Http.Tags("billing/invoices")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
public sealed class BillingInvoicesController(ISender sender) : BaseApiController
{
    [HttpPost("{invoiceId:guid}/retry")]
    [EndpointSummary("Retry invoice payment")]
    [EndpointDescription("Accepts a local retry scheduling request for open or past-due invoices. External gateway capture requires configured payment-provider credentials.")]
    [ProducesResponseType<InvoicePaymentRetryResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<InvoicePaymentRetryResult>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<InvoicePaymentRetryResult>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<InvoicePaymentRetryResult>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RetryPayment(Guid invoiceId, CancellationToken ct)
    {
        var result = await sender.Send(new RetryInvoicePaymentCommand(invoiceId), ct).ConfigureAwait(false);

        return result.Code switch
        {
            "NotFound" => NotFound(result),
            "RetryAccepted" => Accepted(result),
            "RetryLimitExceeded" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    /// <summary>
    ///     Applies the calling user's account credits to one of their invoices (issue #410
    ///     multi-tender settlement). Requires an Idempotency-Key header; the optional body
    ///     caps the credit tender amount. User and tenant are derived from the actor.
    /// </summary>
    [HttpPost("{invoiceId:guid}/account-credits")]
    [EndpointSummary("Apply account credits to an invoice")]
    [EndpointDescription(
        "Settles part or all of an open/past-due invoice with the calling user's account credits, consumed FIFO by expiry in one atomic settlement (credit ledger rows + settlement record + invoice update). Requires the Idempotency-Key header; replays return the recorded settlement. The invoice must belong to the calling user; credits must match the invoice currency. The remaining balance stays owed to provider tenders.")]
    [ProducesResponseType<InvoiceAccountCreditSettlementResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<InvoiceAccountCreditSettlementResult>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<InvoiceAccountCreditSettlementResult>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<InvoiceAccountCreditSettlementResult>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApplyAccountCredits(
        Guid invoiceId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] ApplyInvoiceCreditsRequest? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return ProblemFrom(Error.Validation(
                "InvoiceCredits.IdempotencyKeyRequired",
                "The Idempotency-Key header is required."));
        }

        var result = await sender.Send(
            new ApplyInvoiceAccountCreditsCommand(invoiceId, idempotencyKey.Trim(), body?.MaxAmount),
            ct).ConfigureAwait(false);

        return result.IsSuccess ? Ok(result.Value) : ProblemFrom(result.Error);
    }

    /// <summary>Request body capping the credit tender of an invoice settlement.</summary>
    /// <param name="MaxAmount">Optional positive cap on the credit amount to apply.</param>
    public sealed record ApplyInvoiceCreditsRequest(decimal? MaxAmount = null);

    private IActionResult ProblemFrom(Error error)
    {
        var problem = ProblemDetailsMapper.ToProblemDetails(error);
        return new ObjectResult(problem) { StatusCode = problem.Status };
    }
}
