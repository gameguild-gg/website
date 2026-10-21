using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Currency configuration controller (issue #412 spec A): per-tenant currency enablement,
///     display preferences and base-currency management. Fail-closed defaults: currencies are
///     disabled until enabled and no base currency exists until one is set.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/currencies")]
[Microsoft.AspNetCore.Http.Tags("currencies")]
[Authorize]
public sealed class CurrenciesController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Lists every catalog currency annotated with the tenant's enablement, base and display
    ///     preferences plus catalog metadata (symbol, English name, minor-unit exponent) for
    ///     selector UIs. Enabled currencies come first.
    /// </summary>
    [HttpGet]
    [EndpointSummary("List tenant currency configurations")]
    [EndpointDescription("Returns all ISO 4217 catalog currencies with the tenant's enablement, base-currency marker and display preferences.")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantCurrencyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrencies(CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantCurrenciesQuery(), ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Enables a currency for the tenant (creates the configuration row on first use).
    /// </summary>
    /// <param name="currencyCode">ISO 4217 code.</param>
    /// <param name="body">Optional display preferences.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{currencyCode}/enable")]
    [EndpointSummary("Enable a currency")]
    [EndpointDescription("Enables an ISO 4217 currency for the tenant. Idempotent. Audit-logged.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable(string currencyCode, [FromBody] CurrencyDisplayPreferencesRequest? body, CancellationToken ct)
    {
        await sender.Send(new EnableCurrencyCommand(currencyCode, body?.DisplaySymbol, body?.DisplayOrder), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Disables a currency. Rejects the base currency outright; a currency still held by
    ///     active subscriptions requires <c>confirmActiveSubscriptionImpact=true</c>.
    /// </summary>
    /// <param name="currencyCode">ISO 4217 code.</param>
    /// <param name="body">Confirmation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{currencyCode}/disable")]
    [EndpointSummary("Disable a currency")]
    [EndpointDescription("Disables an ISO 4217 currency for the tenant. The base currency is always rejected; currencies held by active subscriptions need explicit confirmation.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Disable(string currencyCode, [FromBody] DisableCurrencyRequest? body, CancellationToken ct)
    {
        await sender.Send(new DisableCurrencyCommand(currencyCode, body?.ConfirmActiveSubscriptionImpact ?? false), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Sets the tenant's base (calculation/reporting) currency; enables it if needed and
    ///     clears the previous base marker.
    /// </summary>
    /// <param name="currencyCode">ISO 4217 code.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{currencyCode}/set-base")]
    [EndpointSummary("Set the tenant base currency")]
    [EndpointDescription("Makes the currency the tenant's base currency for calculation and reporting, clearing the previous marker. Audit-logged.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetBase(string currencyCode, CancellationToken ct)
    {
        await sender.Send(new SetBaseCurrencyCommand(currencyCode), ct).ConfigureAwait(false);
        return NoContent();
    }
}

/// <summary>Optional display preferences supplied when enabling a currency.</summary>
public sealed record CurrencyDisplayPreferencesRequest(string? DisplaySymbol = null, int? DisplayOrder = null);

/// <summary>Confirmation payload for disabling a currency in active use.</summary>
public sealed record DisableCurrencyRequest(bool ConfirmActiveSubscriptionImpact = false);
