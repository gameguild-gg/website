using Asp.Versioning;
using GameGuild.Commerce.Billing;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     External billing provider management API (issue #397): lists the configured
///     providers with read-only configuration health (config validity, webhook endpoint
///     readiness) and runtime enabled state, allows administrators to explicitly enable
///     or disable a provider at runtime, and produces report-only provider-migration
///     dry-run assessments. All actions require the SystemAdmin policy and additionally
///     enforce the system-administrator guard at the controller layer (fail closed).
///     Gateway routing and provider failover are tracked separately in issue #413.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/external-providers")]
[Microsoft.AspNetCore.Http.Tags("billing/external-providers")]
[EnableRateLimiting(RateLimitPolicies.Api)]
public sealed class BillingExternalProvidersController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     List the configured external billing providers with health and enabled state
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Provider management and health snapshots for every supported provider</returns>
    /// <remarks>
    ///     Returns one entry per supported external billing provider (stripe, paypal, applepay,
    ///     apple_app_store, googlepay, google_play_store) with read-only configuration health
    ///     (credentials configured, configuration valid, webhook endpoint verification material
    ///     present) and the runtime enabled state (defaults to enabled; an explicit administrator
    ///     disable persists a management override).
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Policies.SystemAdmin)]
    [EndpointSummary("List external billing providers with health and enabled state")]
    [EndpointDescription(
        "Returns one entry per supported external billing provider with read-only configuration health (credentials configured, configuration valid, webhook endpoint verification material present) and the runtime enabled state. Requires the system administrator policy.")]
    [ProducesResponseType<IReadOnlyList<ExternalBillingProviderStatusDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListProviders(CancellationToken ct)
    {
        var adminGuard = RequireSystemAdmin("list external billing providers");
        if (adminGuard is not null)
        {
            return adminGuard;
        }

        var providers = await sender.Send(new GetExternalBillingProvidersQuery(), ct).ConfigureAwait(false);

        return Ok(providers);
    }

    /// <summary>
    ///     Get the health and enabled state of a single external billing provider
    /// </summary>
    /// <param name="providerKey">Provider key (stripe, paypal, ...)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Provider management and health snapshot</returns>
    /// <remarks>
    ///     Fails closed with 404 for unknown provider keys.
    /// </remarks>
    [HttpGet("{providerKey}")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [EndpointSummary("Get one external billing provider by key")]
    [EndpointDescription(
        "Returns the management and health snapshot of a single external billing provider. Fails closed with 404 for unknown provider keys.")]
    [ProducesResponseType<ExternalBillingProviderStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProvider(string providerKey, CancellationToken ct)
    {
        var adminGuard = RequireSystemAdmin("inspect an external billing provider");
        if (adminGuard is not null)
        {
            return adminGuard;
        }

        var provider = await sender.Send(new GetExternalBillingProviderQuery(providerKey), ct).ConfigureAwait(false);

        return provider is null ? NotFound() : Ok(provider);
    }

    /// <summary>
    ///     Enable an external billing provider at runtime
    /// </summary>
    /// <param name="providerKey">Provider key (stripe, paypal, ...)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated provider management and health snapshot</returns>
    /// <remarks>
    ///     Persists an explicit enable decision (idempotent) and writes an audit event.
    ///     Fails closed with 404 for unknown provider keys. Availability still requires
    ///     valid provider configuration.
    /// </remarks>
    [HttpPost("{providerKey}:enable")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
    [EndpointSummary("Enable an external billing provider")]
    [EndpointDescription(
        "Persists an explicit enable decision for an external billing provider (idempotent) and writes an audit event. Fails closed with 404 for unknown provider keys. Availability still requires valid provider configuration.")]
    [ProducesResponseType<ExternalBillingProviderStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableProvider(string providerKey, CancellationToken ct)
    {
        var adminGuard = RequireSystemAdmin("enable an external billing provider");
        if (adminGuard is not null)
        {
            return adminGuard;
        }

        try
        {
            var status = await sender.Send(new SetExternalBillingProviderEnabledCommand(providerKey, Enable: true), ct).ConfigureAwait(false);

            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    ///     Disable an external billing provider at runtime
    /// </summary>
    /// <param name="providerKey">Provider key (stripe, paypal, ...)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated provider management and health snapshot</returns>
    /// <remarks>
    ///     Persists an explicit disable decision (idempotent) and writes an audit event.
    ///     Fails closed with 404 for unknown provider keys.
    /// </remarks>
    [HttpPost("{providerKey}:disable")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
    [EndpointSummary("Disable an external billing provider")]
    [EndpointDescription(
        "Persists an explicit disable decision for an external billing provider (idempotent) and writes an audit event. Fails closed with 404 for unknown provider keys.")]
    [ProducesResponseType<ExternalBillingProviderStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableProvider(string providerKey, CancellationToken ct)
    {
        var adminGuard = RequireSystemAdmin("disable an external billing provider");
        if (adminGuard is not null)
        {
            return adminGuard;
        }

        try
        {
            var status = await sender.Send(new SetExternalBillingProviderEnabledCommand(providerKey, Enable: false), ct).ConfigureAwait(false);

            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    ///     Produce a report-only migration dry-run between two external billing providers
    /// </summary>
    /// <param name="request">Source and target provider keys</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Migration dry-run report; no state is mutated</returns>
    /// <remarks>
    ///     Scans subscriptions and classifies each by its external-provider binding: bound to the
    ///     source provider while lacking a target-provider external identifier (the migration work
    ///     list), already on the target provider, unattributable external identifiers (manual
    ///     review), and subscriptions without external identifiers. The report never mutates
    ///     state; executing provider switching is out of scope and gateway routing/failover is
    ///     tracked in issue #413. Fails closed with 400 for unknown or identical provider keys.
    /// </remarks>
    [HttpPost("migration:dry-run")]
    [Authorize(Policy = Policies.SystemAdmin)]
    [EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
    [EndpointSummary("Report-only migration dry-run between two billing providers")]
    [EndpointDescription(
        "Scans subscriptions and classifies each by its external-provider binding to list the ones bound to the source provider that lack a target-provider external identifier. Report-only: no state is mutated. Executing provider switching is out of scope; gateway routing and failover are tracked in issue #413. Fails closed with 400 for unknown or identical provider keys.")]
    [ProducesResponseType<BillingProviderMigrationReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MigrationDryRun([FromBody] MigrationDryRunRequest request, CancellationToken ct)
    {
        var adminGuard = RequireSystemAdmin("run a billing provider migration dry-run");
        if (adminGuard is not null)
        {
            return adminGuard;
        }

        var report = await sender.Send(
            new MigrateBillingProviderCommand(request.SourceProvider, request.TargetProvider),
            ct).ConfigureAwait(false);

        return Ok(report);
    }

    /// <summary>
    ///     Request body for the migration dry-run endpoint.
    /// </summary>
    public sealed record MigrationDryRunRequest(string SourceProvider, string TargetProvider);

    private IActionResult? RequireSystemAdmin(string operation)
        => actorContextAccessor.ActorContext.IsSystemAdmin
            ? null
            : new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"Only system administrators may {operation}."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
}
