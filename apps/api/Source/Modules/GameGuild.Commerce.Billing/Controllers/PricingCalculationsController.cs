using Asp.Versioning;
using GameGuild.Commerce;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Stored-calculation surface (issue #417): <c>GET /api/pricing/calculations/{id}</c>
///     replays a recorded pricing calculation from the durable ledger, and
///     <c>POST /api/pricing/validate/{id}</c> re-validates one stored calculation —
///     recomputing its final amount from the recorded breakdown and checking the issue's
///     invariants (totals vs parts, discounts vs subtotal, tax plausibility, proration
///     bound, rounding drift). SystemAdmin-gated, matching the sibling ledger surface.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/pricing/calculations")]
[Microsoft.AspNetCore.Http.Tags("commerce/pricing")]
[Authorize(Policy = Policies.SystemAdmin)]
public sealed class PricingCalculationsController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Get a stored price calculation by id
    /// </summary>
    /// <param name="calculationId">Ledger entry id of the calculation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored calculation with its full breakdown.</returns>
    [HttpGet("{calculationId:guid}")]
    [EndpointSummary("Get a stored price calculation")]
    [EndpointDescription("Retrieves one recorded pricing calculation from the pricing-decision ledger, including the full itemized breakdown (base, add-ons, usage, discounts, proration, tax, credits) that produced the final amount.")]
    [ProducesResponseType<PricingDecisionLedgerEntryDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalculation(Guid calculationId, CancellationToken ct = default)
    {
        var entry = await sender.Send(new GetPricingDecisionLedgerEntryQuery(calculationId), ct).ConfigureAwait(false);
        return entry is null ? NotFound() : Ok(entry);
    }

    /// <summary>
    ///     Validate the accuracy of a stored price calculation
    /// </summary>
    /// <param name="calculationId">Ledger entry id of the calculation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Validation outcome: recomputed vs stored final and invariant violations.</returns>
    [HttpPost("~/api/v{version:apiVersion}/pricing/validate/{calculationId:guid}")]
    [NoBusinessMutationEndpoint("Validation recomputes a stored calculation read-only; it mutates nothing.")]
    [EndpointSummary("Validate a stored price calculation")]
    [EndpointDescription("Re-validates one stored pricing calculation: recomputes the final amount from the recorded breakdown and checks the calculation invariants — totals match line items, discounts do not exceed the subtotal, tax rates are plausible, proration stays within the plan price, and no rounding drift accumulated.")]
    [ProducesResponseType<PricingCalculationValidationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateCalculation(Guid calculationId, CancellationToken ct = default)
    {
        var result = await sender.Send(new ValidatePricingCalculationQuery(calculationId), ct).ConfigureAwait(false);
        return result is null ? NotFound() : Ok(result);
    }
}

/// <summary>
///     Validation outcome for one stored calculation (issue #417).
/// </summary>
/// <param name="CalculationId">Ledger entry id.</param>
/// <param name="StoredFinalAmount">Final amount as persisted.</param>
/// <param name="RecomputedFinalAmount">Final amount recomputed from the recorded breakdown.</param>
/// <param name="Drifted">Whether the recomputation diverges from the stored final.</param>
/// <param name="Valid">True when the recomputation matches and no invariant is violated.</param>
/// <param name="Violations">Invariant violations in fail-closed detail.</param>
public sealed record PricingCalculationValidationResult(
    Guid CalculationId,
    decimal StoredFinalAmount,
    decimal RecomputedFinalAmount,
    bool Drifted,
    bool Valid,
    IReadOnlyList<string> Violations);
