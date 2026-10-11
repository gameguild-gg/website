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
///     Self-service payment-method management (issue #414). Tokenized gateway references
///     only — raw card data is never accepted (PCI DSS).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/payment-methods")]
[Microsoft.AspNetCore.Http.Tags("billing/payment-methods")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
public sealed class PaymentMethodsController(
    ISender sender,
    IPaymentMethodService paymentMethodService,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     List registered payment methods (own methods; system admins see the whole tenant).
    /// </summary>
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("List payment methods")]
    [EndpointDescription("Lists the live payment methods registered for the tenant. Non-admin actors see their own methods; system admins see all methods in the requested tenant.")]
    [ProducesResponseType<IEnumerable<PaymentMethodDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        CancellationToken ct = default)
    {
        var actor = PaymentMethodActors.Resolve(actorContextAccessor);
        if (actor is null) { return Forbid(); }

        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated && !actorContext.IsSystemAdmin)
        {
            var requestedTenantId = tenantId ?? actorContext.TenantId ?? Guid.Empty;
            var validationError = ValidateTenantAccess(requestedTenantId, "list payment methods");
            if (validationError != null) { return validationError; }

            tenantId = actorContext.TenantId;
        }

        if (tenantId is null || tenantId == Guid.Empty)
        {
            return BadRequest(new { error = "tenantId is required when listing payment methods as a system administrator." });
        }

        return FromResult(await paymentMethodService.ListAsync(actor, tenantId.Value, ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Get a single payment method by ID.
    /// </summary>
    [HttpGet("{paymentMethodId:guid}", Name = "GetPaymentMethodById")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("Get a payment method")]
    [EndpointDescription("Returns a single registered payment method. Only the owner or a system administrator can read it.")]
    [ProducesResponseType<PaymentMethodDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid paymentMethodId, CancellationToken ct = default)
    {
        var actor = PaymentMethodActors.Resolve(actorContextAccessor);
        if (actor is null) { return Forbid(); }

        return FromResult(await paymentMethodService.GetByIdAsync(actor, paymentMethodId, ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Register a payment method from a tokenized gateway result.
    /// </summary>
    [HttpPost]
    [EndpointSummary("Register a payment method from a gateway token")]
    [EndpointDescription("Registers a payment method from a Stripe payment method token (pm_...) produced by Stripe.js/Elements or a confirmed SetupIntent. Raw card data is never accepted; only display metadata returned by the gateway is stored.")]
    [ProducesResponseType<PaymentMethodDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreatePaymentMethodRequestBody body, CancellationToken ct = default)
    {
        if (body.TenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TenantId cannot be empty" });
        }

        if (string.IsNullOrWhiteSpace(body.GatewayPaymentMethodId))
        {
            return BadRequest(new { error = "GatewayPaymentMethodId is required" });
        }

        if (!StripePaymentMethodIdentifier.IsValid(body.GatewayPaymentMethodId))
        {
            return BadRequest(new { error = StripePaymentMethodIdentifier.ValidationMessage });
        }

        var validationError = ValidateTenantAccess(body.TenantId, "register a payment method");
        if (validationError != null) { return validationError; }

        var result = await sender.Send(
            new RegisterPaymentMethodCommand(
                body.TenantId,
                body.GatewayPaymentMethodId.Trim(),
                body.SetAsDefault,
                body.Nickname,
                body.BillingAddress,
                body.OwnerUserId),
            ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return FromResult(result);
        }

        return CreatedAtRoute("GetPaymentMethodById", new { paymentMethodId = result.Value.Id }, result.Value);
    }

    /// <summary>
    ///     Update display-only fields of a payment method.
    /// </summary>
    [HttpPut("{paymentMethodId:guid}")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("Update payment method display fields")]
    [EndpointDescription("Updates display-only fields (nickname, billing address). Card details come from the gateway and cannot be edited here.")]
    [ProducesResponseType<PaymentMethodDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid paymentMethodId, [FromBody] UpdatePaymentMethodRequestBody body, CancellationToken ct = default)
    {
        var result = await sender.Send(
            new UpdatePaymentMethodCommand(paymentMethodId, body.Nickname, body.BillingAddress),
            ct).ConfigureAwait(false);

        return FromResult(result);
    }

    /// <summary>
    ///     Make this payment method the default (custom action per Google API guidelines).
    /// </summary>
    [HttpPost("{paymentMethodId:guid}:set-default")]
    [EndpointSummary("Set the default payment method")]
    [EndpointDescription("Makes this payment method its owner's default inside the tenant. Enforces the single-default invariant and re-points subscriptions billed with the previous default.")]
    [ProducesResponseType<PaymentMethodDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetDefault(Guid paymentMethodId, CancellationToken ct = default)
    {
        var result = await sender.Send(new SetDefaultPaymentMethodCommand(paymentMethodId), ct).ConfigureAwait(false);

        return FromResult(result);
    }

    /// <summary>
    ///     Remove a payment method (soft delete).
    /// </summary>
    [HttpDelete("{paymentMethodId:guid}")]
    [EndpointSummary("Remove a payment method")]
    [EndpointDescription("Soft-deletes a payment method. Fails closed with 409 Conflict while any active subscription still bills with it; set a new default first to re-point subscriptions.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid paymentMethodId, CancellationToken ct = default)
    {
        var result = await sender.Send(new RemovePaymentMethodCommand(paymentMethodId), ct).ConfigureAwait(false);

        return FromResult(result);
    }

    /// <summary>
    ///     Request body for registering a payment method from a gateway token.
    /// </summary>
    public sealed record CreatePaymentMethodRequestBody(
        Guid TenantId,
        string GatewayPaymentMethodId,
        bool SetAsDefault = false,
        string? Nickname = null,
        string? BillingAddress = null,
        Guid? OwnerUserId = null);

    /// <summary>
    ///     Request body for updating payment-method display fields.
    /// </summary>
    public sealed record UpdatePaymentMethodRequestBody(string? Nickname = null, string? BillingAddress = null);

    #region Private Methods

    /// <summary>
    ///     Maps a service <see cref="Result{T}"/> onto an <see cref="IActionResult"/>
    ///     (200 with payload on success, RFC 7807 ProblemDetails otherwise).
    /// </summary>
    private IActionResult FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var problemDetails = ProblemDetailsMapper.ToProblemDetails(result.Error);
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }

    private IActionResult FromResult(Result result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }

        var problemDetails = ProblemDetailsMapper.ToProblemDetails(result.Error);
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }

    /// <summary>
    ///     Validates that the authenticated user has access to the specified tenant.
    /// </summary>
    private IActionResult? ValidateTenantAccess(Guid requestedTenantId, string operation)
        => actorContextAccessor.ActorContext.IsSystemAdmin
            ? null
            : actorContextAccessor.ValidateTenantAccessAsActionResult(requestedTenantId, operation);

    #endregion
}
