using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Unified pricing engine surface (issue #417): the audited calculate endpoint and the
///     read-only plan-change preview. Every pricing decision on the platform funnels through
///     the single <see cref="IPricingEngine"/> path behind these endpoints.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/pricing")]
[Microsoft.AspNetCore.Http.Tags("commerce/pricing")]
[Authorize]
public sealed class PricingController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Run a full pricing calculation through the unified engine: base plan or custom
    ///     amount, add-ons, usage tiers, promo discounts, proration, tax, credits, currency —
    ///     itemized, explained, self-validated, and recorded in the pricing-decision ledger.
    /// </summary>
    /// <param name="request">Calculation inputs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Itemized pricing calculation</returns>
    [HttpPost(":calculate")]
    [EndpointSummary("Calculate price through the unified pricing engine")]
    [EndpointDescription("Calculates a full price (base, add-ons, usage tiers, discounts, proration, tax, credits) through the single centralized pricing engine and records the decision for audit.")]
    [ProducesResponseType(typeof(PricingCalculationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CalculatePrice(
        [FromBody] PricingCalculationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.Send(new CalculatePriceCommand(request), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Preview a plan change before finalizing: the immediate prorated charge and the
    ///     next-renewal amount side by side. Read-only — no ledger decision, no credit spend.
    /// </summary>
    /// <param name="request">Preview inputs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Immediate charge plus next-renewal preview</returns>
    [HttpPost(":preview")]
    [NoBusinessMutationEndpoint("Preview is a read-only quote; the plan change itself is a separate command.")]
    [EndpointSummary("Preview a plan-change price")]
    [EndpointDescription("Previews what a plan change costs before it is applied: the immediate prorated charge and the next renewal amount, with discounts, tax, and an itemized breakdown.")]
    [ProducesResponseType(typeof(PricingPreviewResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewPriceChange(
        [FromBody] PricingPreviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.Send(new PreviewPriceChangeCommand(request), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
