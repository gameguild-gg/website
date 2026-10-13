using Asp.Versioning;

using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Read and verification surface over the durable pricing-decision ledger
///     (issue #400): every checkout/renewal calculation that produced a final price is
///     appended here with its full breakdown. Exposes the paginated ledger feed, the
///     per-entry breakdown, discount effectiveness/pricing analytics for the reporting
///     dashboard, and drift validation that recomputes sampled entries from their own
///     breakdown. Requires the SystemAdmin policy, matching the sibling billing
///     events/webhook inspection surfaces; tenants are a query filter, never a route
///     segment.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/pricing-discounts-ledger")]
[Microsoft.AspNetCore.Http.Tags("billing/pricing-discounts-ledger")]
[Authorize(Policy = Policies.SystemAdmin)]
public sealed class PricingDecisionLedgerController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     List pricing-decision ledger entries
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="orderId">Optional order correlation filter.</param>
    /// <param name="subscriptionId">Optional subscription correlation filter.</param>
    /// <param name="promoCode">Optional exact applied-code filter (case-insensitive).</param>
    /// <param name="decisionType">Optional decision type filter (CheckoutLineItem, RenewalQuote, OrderPromoApply).</param>
    /// <param name="driftStatus">Optional drift status filter (NotValidated, Verified, Drifted).</param>
    /// <param name="fromUtc">Optional inclusive lower bound on the decision timestamp (ISO-8601).</param>
    /// <param name="toUtc">Optional inclusive upper bound on the decision timestamp (ISO-8601).</param>
    /// <param name="skip">Items to skip.</param>
    /// <param name="take">Items to take (1-100, default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged ledger feed, newest first.</returns>
    [HttpGet]
    [EndpointSummary("List pricing-decision ledger entries")]
    [EndpointDescription(
        "Pages through the durable pricing-decision ledger: one row per checkout/renewal calculation that produced a final price, with headline amounts, applied promo codes, winning pricing rule and drift status. Filterable by tenant, order, subscription, promo code, decision type, drift status and decision date range.")]
    [ProducesResponseType<PagedResult<PricingDecisionLedgerEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListEntries(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? orderId = null,
        [FromQuery] Guid? subscriptionId = null,
        [FromQuery] string? promoCode = null,
        [FromQuery] string? decisionType = null,
        [FromQuery] string? driftStatus = null,
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new ListPricingDecisionLedgerEntriesQuery(
                tenantId,
                orderId,
                subscriptionId,
                promoCode,
                decisionType,
                driftStatus,
                fromUtc,
                toUtc,
                skip,
                take),
            ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get a pricing-decision ledger entry by id
    /// </summary>
    /// <param name="entryId">Ledger row identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ledger entry details including the full calculation breakdown document.</returns>
    [HttpGet("{entryId:guid}")]
    [EndpointSummary("Get a pricing-decision ledger entry by id")]
    [EndpointDescription(
        "Retrieves one pricing-decision ledger entry, including the full calculation breakdown JSON (base, per-rule adjustments, per-code promo applications, caps, final amount), correlation ids and the drift-validation outcome.")]
    [ProducesResponseType<PricingDecisionLedgerEntryDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntry(Guid entryId, CancellationToken ct = default)
    {
        var entry = await sender.Send(new GetPricingDecisionLedgerEntryQuery(entryId), ct).ConfigureAwait(false);
        return entry is null ? NotFound() : Ok(entry);
    }

    /// <summary>
    ///     Discount effectiveness and pricing analytics over the ledger
    /// </summary>
    /// <param name="fromUtc">Optional inclusive window start (ISO-8601).</param>
    /// <param name="toUtc">Optional inclusive window end (ISO-8601).</param>
    /// <param name="tenantId">Optional tenant scope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregated redemption, discount-vs-baseline revenue and drift-health metrics.</returns>
    [HttpGet("analytics/effectiveness")]
    [EndpointSummary("Discount effectiveness and pricing analytics")]
    [EndpointDescription(
        "Aggregates the pricing-decision ledger over a window: total decisions, baseline vs final revenue, discount given, promo redemptions, per-code effectiveness (redemptions, discount given, baseline vs influenced revenue), per-pricing-rule impact, stacking outcomes and drift status counts. Feeds the monetization reporting dashboard.")]
    [ProducesResponseType<PricingLedgerEffectivenessAggregate>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEffectiveness(
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        [FromQuery] Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetPricingLedgerEffectivenessQuery(fromUtc, toUtc, tenantId),
            ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Validate a sample of ledger entries for pricing drift
    /// </summary>
    /// <param name="request">Validation request (sample size, optional window).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Drift-validation summary; drifted entries are listed first.</returns>
    [HttpPost(":validate-drift")]
    [EndpointSummary("Validate ledger entries for pricing drift")]
    [EndpointDescription(
        "Drift detection over the pricing-decision ledger: samples entries (preferring never-validated ones), recomputes each final amount from its own breakdown document and stamps the entry Verified or Drifted. Drifted entries — where the stored final no longer follows from the recorded arithmetic — are surfaced first for review.")]
    [ProducesResponseType<PricingLedgerDriftValidationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateDrift(
        [FromBody] ValidatePricingLedgerDriftRequest? request,
        CancellationToken ct = default)
    {
        var command = new ValidatePricingLedgerDriftCommand(
            request?.SampleSize ?? 25,
            request?.FromUtc,
            request?.ToUtc);

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        return Ok(result);
    }
}

/// <summary>Request body for the drift-validation endpoint.</summary>
/// <param name="SampleSize">How many entries to validate (1-200, default 25).</param>
/// <param name="FromUtc">Inclusive window start (null = unbounded).</param>
/// <param name="ToUtc">Inclusive window end (null = unbounded).</param>
public sealed record ValidatePricingLedgerDriftRequest(
    int SampleSize = 25,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null);
