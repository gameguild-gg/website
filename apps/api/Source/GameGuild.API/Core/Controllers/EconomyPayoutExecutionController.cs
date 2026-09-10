using Asp.Versioning;
using GameGuild.API.Authorization;
using GameGuild.API.Setup;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Payouts;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

public sealed record ReserveApprovedPayoutExecutionRequest(string StepUpReceipt);

public sealed record DispatchPayoutExecutionRequest(
    long ExpectedVersion,
    string StepUpReceipt);

public sealed record PayoutProtectedOperationFailureResponse(
    EconomyProtectedOperationState State,
    Guid? ReviewId,
    IReadOnlyList<string> Diagnostics);

public sealed record EconomyPayoutExecutionOperationDto(
    Guid Id,
    Guid PayeeId,
    Guid WalletId,
    long HardCoinUnits,
    PayoutOperationState State,
    long Version,
    long FencingToken,
    long KillSwitchEpoch,
    long ReserveVersion,
    long ReserveAuthorizationEpoch,
    long PolicyVersion,
    Guid RiskDecisionId,
    string DestinationHash,
    string? ProviderPayoutId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static EconomyPayoutExecutionOperationDto From(PayoutOperation operation) => new(
        operation.Id,
        operation.PayeeId,
        operation.WalletId.Value,
        operation.Amount.Units,
        operation.State,
        operation.Version,
        operation.FencingToken,
        operation.KillSwitchEpoch,
        operation.ReserveVersion.Value,
        operation.ReserveAuthorizationEpoch,
        operation.PolicyVersion.Value,
        operation.RiskDecisionId,
        operation.DestinationHash,
        operation.ProviderPayoutId,
        operation.CreatedAt,
        operation.UpdatedAt);
}

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/economy/payouts")]
[Tags("economy")]
[Authorize]
public sealed class EconomyPayoutAccountController(
    ISender sender,
    IDurablePayoutApplicationService payouts,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    [HttpPost("onboarding")]
    [EndpointSummary("Create or refresh my payout provider onboarding")]
    [ProducesResponseType(typeof(ConnectOnboardingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateOrRefreshOnboarding(CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        try
        {
            return Ok(await sender.Send(
                new CreateOrRefreshPayoutOnboardingEndpointCommand(tenantId, actorId),
                cancellationToken).ConfigureAwait(false));
        }
        catch (PayoutExecutionDisabledException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, exception.Message);
        }
    }

    [HttpGet("account")]
    [EndpointSummary("Get my payout provider account readiness")]
    [ProducesResponseType(typeof(ConnectAccountSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAccount(CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        try
        {
            return Ok(await payouts.GetAccountAsync(tenantId, actorId, cancellationToken).ConfigureAwait(false));
        }
        catch (PayoutEligibilityException)
        {
            return NotFound();
        }
        catch (PayoutExecutionDisabledException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, exception.Message);
        }
    }

    private bool TryActor(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue || !actor.SubjectIdAsGuid.HasValue)
            return false;
        tenantId = actor.TenantId.Value;
        actorId = actor.SubjectIdAsGuid.Value;
        return true;
    }
}

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy/payout-requests")]
[Tags("economy-administration")]
[Authorize]
public sealed class EconomyPayoutExecutionAdministrationController(
    ISender sender,
    IDurablePayoutApplicationService payouts,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpPost("{requestId:guid}/reserve")]
    [EndpointSummary("Reserve FIFO funds for a fully approved payout request")]
    [EndpointDescription("Tenant and actor authority come exclusively from the authenticated actor context. Fresh MFA and the full capability control plane are required.")]
    [ProducesResponseType(typeof(EconomyPayoutExecutionOperationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(PayoutProtectedOperationFailureResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Reserve(
        Guid requestId,
        [FromBody] ReserveApprovedPayoutExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryOperator(out var tenantId, out var actorId, out _)) return Forbid();
        var transactionBinding = PayoutProtectedOperationBinding.Reservation(requestId);
        var operation = EconomyStepUpOperation.Create(
            "economy.payout.reserve",
            $"payout-request:{requestId:N}",
            transactionBinding);
        return await ExecuteAsync(sender.Send(new ReservePayoutExecutionEndpointCommand(
            operation,
            request.StepUpReceipt,
            transactionBinding,
            tenantId,
            actorId,
            requestId,
            timeProvider.GetUtcNow()), cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("operations/{operationId:guid}/dispatch")]
    [EndpointSummary("Atomically authorize and enqueue an approved payout dispatch")]
    [ProducesResponseType(typeof(EconomyPayoutExecutionOperationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(PayoutProtectedOperationFailureResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Dispatch(
        Guid operationId,
        [FromBody] DispatchPayoutExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryOperator(out var tenantId, out var actorId, out _)) return Forbid();
        var transactionBinding = PayoutProtectedOperationBinding.Dispatch(
            operationId, request.ExpectedVersion);
        var operation = EconomyStepUpOperation.Create(
            "economy.payout.dispatch",
            $"payout-operation:{operationId:N}",
            transactionBinding);
        return await ExecuteAsync(sender.Send(new DispatchPayoutExecutionEndpointCommand(
            operation,
            request.StepUpReceipt,
            transactionBinding,
            tenantId,
            actorId,
            operationId,
            request.ExpectedVersion,
            timeProvider.GetUtcNow()), cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("operations/{operationId:guid}/reconcile")]
    [EndpointSummary("Reconcile an in-flight payout directly with its provider")]
    [ProducesResponseType(typeof(EconomyPayoutExecutionOperationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reconcile(Guid operationId, CancellationToken cancellationToken)
    {
        if (!TryOperator(out var tenantId, out var actorId, out _)) return Forbid();
        return await ExecuteAsync(sender.Send(new ReconcilePayoutExecutionEndpointCommand(
            new ReconcilePayoutOperationCommand(tenantId, actorId, operationId)), cancellationToken))
            .ConfigureAwait(false);
    }

    [HttpGet("operations")]
    [EndpointSummary("List tenant-scoped payout execution operations")]
    [ProducesResponseType(typeof(IReadOnlyList<EconomyPayoutExecutionOperationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult List([FromQuery] int take = 100)
    {
        if (!TryOperator(out var tenantId, out _, out _)) return Forbid();
        if (take is < 1 or > 100) return BadRequest("Take must be between 1 and 100.");
        return Ok(payouts.List(tenantId, take).Select(EconomyPayoutExecutionOperationDto.From).ToArray());
    }

    [HttpGet("operations/{operationId:guid}")]
    [EndpointSummary("Get a tenant-scoped payout execution operation")]
    [ProducesResponseType(typeof(EconomyPayoutExecutionOperationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get(Guid operationId)
    {
        if (!TryOperator(out var tenantId, out _, out _)) return Forbid();
        try
        {
            return Ok(EconomyPayoutExecutionOperationDto.From(payouts.Get(tenantId, operationId)));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private static async Task<IActionResult> ExecuteAsync(Task<PayoutOperation> operation)
    {
        try
        {
            return new OkObjectResult(EconomyPayoutExecutionOperationDto.From(await operation.ConfigureAwait(false)));
        }
        catch (KeyNotFoundException)
        {
            return new NotFoundResult();
        }
        catch (EconomyProtectedOperationException exception)
        {
            var status = exception.State switch
            {
                EconomyProtectedOperationState.Denied => StatusCodes.Status403Forbidden,
                EconomyProtectedOperationState.ReviewRequired or EconomyProtectedOperationState.Hold or
                    EconomyProtectedOperationState.Challenge => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status503ServiceUnavailable
            };
            return new ObjectResult(new PayoutProtectedOperationFailureResponse(
                exception.State, exception.ReviewId, exception.Diagnostics)) { StatusCode = status };
        }
        catch (Exception exception) when (exception is PayoutEligibilityException or
                                          PayoutExecutionDisabledException or
                                          PayoutStaleCommandException or
                                          PayoutProviderBindingException or
                                          ReauthenticationEvidenceException or
                                          EconomyCapabilityAuthorizationException or
                                          GameGuild.Identity.Authentication.StepUpReceiptInvalidException)
        {
            return new ConflictObjectResult(exception.Message);
        }
    }

    private bool TryOperator(out Guid tenantId, out Guid actorId, out ActorContext actor)
    {
        actor = actorContextAccessor.ActorContext;
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue || !actor.SubjectIdAsGuid.HasValue ||
            !actor.HasPermission(EconomyPermission.Keys.OperatePayouts))
            return false;
        tenantId = actor.TenantId.Value;
        actorId = actor.SubjectIdAsGuid.Value;
        return true;
    }

}

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/integrations/economy/stripe-connect")]
[Tags("economy-integrations")]
[ApiController]
public sealed class EconomyStripeConnectWebhookController(
    ISender sender,
    IStripeConnectWebhookNormalizer normalizer,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EconomyPayoutExecutionOperationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("Stripe-Signature", out var signature) ||
            string.IsNullOrWhiteSpace(signature.ToString()))
            return BadRequest("Stripe-Signature is required.");
        await using var payload = new MemoryStream();
        await Request.Body.CopyToAsync(payload, cancellationToken).ConfigureAwait(false);
        var providerEvent = await normalizer.NormalizeAsync(
            payload.ToArray(), signature.ToString(), timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        var operation = await sender.Send(
            new ApplyStripePayoutProviderEventEndpointCommand(providerEvent),
            cancellationToken).ConfigureAwait(false);
        return Ok(EconomyPayoutExecutionOperationDto.From(operation));
    }
}
