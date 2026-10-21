using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     FX rate admin controller (issue #412 spec B): persisted rate lookup with
///     historical/effective-date filters, sync status/staleness observability, manual sync
///     trigger and the audited manual rate-override command.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/fx-rates")]
[Microsoft.AspNetCore.Http.Tags("fx-rates")]
[Authorize]
public sealed class FxRatesController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Lists persisted FX quotations (market snapshots and overrides) with optional pair and
    ///     effective-date-window filters for historical lookup.
    /// </summary>
    /// <param name="baseCurrency">Optional base code filter (exact direction).</param>
    /// <param name="quoteCurrency">Optional quote code filter.</param>
    /// <param name="fromEffectiveDateUtc">Optional inclusive effective-date window start.</param>
    /// <param name="toEffectiveDateUtc">Optional inclusive effective-date window end.</param>
    /// <param name="limit">Maximum rows (1..2000, default 500).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [EndpointSummary("List persisted FX rates")]
    [EndpointDescription("Persisted FX quotations with pair and effective-date filters, newest effective date first.")]
    [ProducesResponseType(typeof(IReadOnlyList<FxRateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRates(
        [FromQuery] string? baseCurrency = null,
        [FromQuery] string? quoteCurrency = null,
        [FromQuery] DateTime? fromEffectiveDateUtc = null,
        [FromQuery] DateTime? toEffectiveDateUtc = null,
        [FromQuery] int limit = 500,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetFxRatesQuery(baseCurrency, quoteCurrency, fromEffectiveDateUtc, toEffectiveDateUtc, limit),
            ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     FX sync status: recent runs, consecutive failures, staleness assessment and active
    ///     grid/override counts.
    /// </summary>
    /// <param name="runLimit">Recent runs to include (1..50, default 10).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("sync-status")]
    [EndpointSummary("FX sync status")]
    [EndpointDescription("Recent FX sync runs with the consecutive-failure counter and the staleness assessment of the newest persisted rates.")]
    [ProducesResponseType(typeof(FxSyncStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSyncStatus([FromQuery] int runLimit = 10, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetFxSyncStatusQuery(runLimit), ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Triggers one FX rate sync pass immediately (same code path as the scheduled worker).
    ///     Rejected while no provider is configured.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("sync")]
    [EndpointSummary("Trigger an FX rate sync")]
    [EndpointDescription("Runs one FX sync pass now through the primary (or fallback) provider and persists the rate snapshot.")]
    [ProducesResponseType(typeof(FxRateSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RunSync(CancellationToken ct)
    {
        var result = await sender.Send(new RunFxRateSyncCommand(), ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Sets or replaces an operator manual FX rate override for an exact direction
    ///     (audit-logged with reason; effective immediately).
    /// </summary>
    /// <param name="body">Override payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("overrides")]
    [EndpointSummary("Set a manual FX rate override")]
    [EndpointDescription("Persists an audited manual override for one exact currency pair direction; overrides win over synced market rates until replaced or expired.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetOverride([FromBody] SetManualFxRateOverrideRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var id = await sender.Send(
            new SetManualFxRateOverrideCommand(
                body.BaseCurrency,
                body.QuoteCurrency,
                body.Rate,
                body.Reason,
                body.ExpiresAtUtc),
            ct).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, new { id });
    }
}

/// <summary>Request payload for the manual FX rate override command.</summary>
public sealed record SetManualFxRateOverrideRequest(
    string BaseCurrency,
    string QuoteCurrency,
    decimal Rate,
    string Reason,
    DateTime? ExpiresAtUtc = null);
