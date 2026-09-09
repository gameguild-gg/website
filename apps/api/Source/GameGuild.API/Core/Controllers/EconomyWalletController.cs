using Asp.Versioning;
using GameGuild.API.Setup;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Commands;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Integrations;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Payouts;
using GameGuild.Finance.Economy.Payouts.Commands;
using GameGuild.Finance.Economy.Payouts.Queries;
using GameGuild.Finance.Economy.Queries;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.Transfers;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

public sealed record EconomySelfServiceCapabilityDto(
    EconomyValueMovementCapability Capability,
    EconomyCapabilityReadinessState State,
    IReadOnlyList<string> Diagnostics);

public sealed record EconomyTransferProtectedOperationFailureResponse(
    EconomyProtectedOperationState State,
    Guid? ReviewId,
    IReadOnlyList<string> Diagnostics);

public sealed record EconomyTopUpFailureResponse(string State, string Message);

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/economy")]
[Tags("economy")]
[Authorize]
public sealed class EconomyWalletController(
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    IEconomyProviderCapabilityReadiness capabilityReadiness) : BaseApiController
{
    [HttpGet("wallet")]
    [EndpointSummary("Get my Economy wallet")]
    [ProducesResponseType(typeof(EconomyWalletSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyWallet(CancellationToken cancellationToken)
    {
        if (!HasSelfServiceContext())
            return Forbid();

        var wallet = await sender.Send(new GetMyEconomyWalletQuery(), cancellationToken).ConfigureAwait(false);
        return wallet is null ? NotFound() : Ok(wallet);
    }

    [HttpGet("wallet/transactions")]
    [EndpointSummary("List my Economy wallet transactions")]
    [ProducesResponseType(typeof(IReadOnlyList<EconomyWalletTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMyTransactions([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        if (!HasSelfServiceContext())
            return Forbid();

        return Ok(await sender.Send(new ListMyEconomyWalletTransactionsQuery(take), cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("conversions/hard-to-soft")]
    [EndpointSummary("Convert my confirmed HardCoin balance into SoftCoin")]
    [ProducesResponseType(typeof(SelfServiceHardToSoftConversionReceipt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConvertMyHardToSoft(
        [FromBody] ConvertMyHardToSoftRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasSelfServiceContext())
            return Forbid();

        ArgumentNullException.ThrowIfNull(request);
        var receipt = await sender.Send(new ConvertMyHardToSoftCommand(request), cancellationToken).ConfigureAwait(false);
        return Ok(receipt);
    }

    [HttpPost("transfers")]
    [EndpointSummary("Send a typed Economy transfer to another user in my tenant")]
    [EndpointDescription("The server resolves wallets, jurisdiction, policy, reserve, risk, and posting authority. The request contains business intent only.")]
    [ProducesResponseType(typeof(SelfServiceEconomyTransferReceipt), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(EconomyTransferProtectedOperationFailureResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(EconomyTransferProtectedOperationFailureResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(EconomyTransferProtectedOperationFailureResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateMyTransfer(
        [FromBody] SelfServiceEconomyTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasSelfServiceContext())
            return Forbid();

        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var receipt = await sender.Send(
                new CreateMyEconomyTransferCommand(request), cancellationToken).ConfigureAwait(false);
            return Ok(receipt);
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
            return StatusCode(status, new EconomyTransferProtectedOperationFailureResponse(
                exception.State, exception.ReviewId, exception.Diagnostics));
        }
        catch (SelfServiceEconomyTransferException exception)
        {
            return Conflict(exception.Message);
        }
        catch (EconomyWalletUnavailableException)
        {
            return Conflict("An active sender and recipient Economy wallet are required.");
        }
        catch (RegisteredPostingRejectedException)
        {
            return Conflict("The Economy transfer could not be committed.");
        }
    }

    [HttpPost("top-ups")]
    [EndpointSummary("Create my HardCoin top-up payment intent")]
    [EndpointDescription("The server derives tenant, wallet, jurisdiction, signed quote, amount, provider binding, and idempotency authority.")]
    [ProducesResponseType(typeof(SelfServiceHardCoinTopUpReceipt), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(EconomyTopUpFailureResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(EconomyTopUpFailureResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateMyTopUp(
        [FromBody] CreateMyHardCoinTopUpRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasSelfServiceContext())
            return Forbid();

        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var receipt = await sender.Send(
                new CreateMyHardCoinTopUpCommand(request), cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetMyTopUp), new { topUpId = receipt.TopUpId }, receipt);
        }
        catch (EconomyTopUpProviderUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new EconomyTopUpFailureResponse(
                "ProviderUnavailable", "The top-up provider is not available."));
        }
        catch (EconomyTopUpProviderAmbiguousException)
        {
            return Conflict(new EconomyTopUpFailureResponse(
                "Ambiguous", "The top-up provider outcome requires reconciliation."));
        }
        catch (EconomyTopUpReplayConflictException)
        {
            return Conflict(new EconomyTopUpFailureResponse(
                "Conflict", "The idempotency key is already bound to another top-up."));
        }
        catch (EconomySelfServiceCommandRejectedException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new EconomyTopUpFailureResponse(
                "Disabled", "HardCoin top-up is disabled by the active Economy controls."));
        }
        catch (EconomyWalletUnavailableException)
        {
            return Conflict(new EconomyTopUpFailureResponse(
                "WalletUnavailable", "An active Economy wallet is required."));
        }
    }

    [HttpGet("top-ups")]
    [EndpointSummary("List my HardCoin top-ups")]
    [ProducesResponseType(typeof(IReadOnlyList<EconomyTopUpStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMyTopUps(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!HasSelfServiceContext())
            return Forbid();
        if (take is < 1 or > 100)
            return BadRequest("Take must be between 1 and 100.");

        var topUps = await sender.Send(new ListMyHardCoinTopUpsQuery(take), cancellationToken)
            .ConfigureAwait(false);
        return Ok(topUps);
    }

    [HttpGet("top-ups/{topUpId:guid}")]
    [EndpointSummary("Get one of my HardCoin top-ups")]
    [ProducesResponseType(typeof(EconomyTopUpStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyTopUp(
        Guid topUpId,
        CancellationToken cancellationToken)
    {
        if (!HasSelfServiceContext())
            return Forbid();
        if (topUpId == Guid.Empty)
            return BadRequest("Top-up ID is required.");

        var topUp = await sender.Send(new GetMyHardCoinTopUpQuery(topUpId), cancellationToken)
            .ConfigureAwait(false);
        return topUp is null ? NotFound() : Ok(topUp);
    }

    [HttpGet("capabilities")]
    [EndpointSummary("Get my Economy capability readiness")]
    [ProducesResponseType(typeof(IReadOnlyList<EconomySelfServiceCapabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetMyCapabilityReadiness()
    {
        if (!HasSelfServiceContext())
            return Forbid();

        EconomyValueMovementCapability[] capabilities =
        [
            EconomyValueMovementCapability.ConfirmHardCoinFunding,
            EconomyValueMovementCapability.ConvertHardToSoft,
            EconomyValueMovementCapability.Transfer,
            EconomyValueMovementCapability.IssueAdReward,
            EconomyValueMovementCapability.BountyEscrow,
            EconomyValueMovementCapability.BountyClaim,
            EconomyValueMovementCapability.BountyReclaim,
            EconomyValueMovementCapability.MarketplaceSettlement,
            EconomyValueMovementCapability.MarketplaceRefund,
            EconomyValueMovementCapability.PayoutExecution
        ];
        var result = capabilities
            .Select(capability =>
            {
                var readiness = capabilityReadiness.Assess(capability);
                return new EconomySelfServiceCapabilityDto(
                    capability,
                    readiness.State,
                    [.. readiness.Diagnostics]);
            })
            .ToArray();
        return Ok(result);
    }

    [HttpGet("payouts")]
    [EndpointSummary("List my payout operations")]
    [ProducesResponseType(typeof(IReadOnlyList<EconomyPayoutOperationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListMyPayouts(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetSelfServiceActor(out var tenantId, out var actorId))
            return Forbid();
        if (take is < 1 or > 100)
            return BadRequest("Take must be between 1 and 100.");

        var payouts = await sender.Send(new ListMyPayoutOperationsQuery(tenantId, actorId, take), cancellationToken)
            .ConfigureAwait(false);
        return Ok(payouts);
    }

    [HttpPost("payout-requests")]
    [EndpointSummary("Submit my payout request")]
    [EndpointDescription("Records a withdrawal request only. It does not reserve or transfer value until KYC, risk, provider, and FIFO eligibility checks pass.")]
    [ProducesResponseType(typeof(EconomyPayoutRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateMyPayoutRequest(
        [FromBody] CreateMyPayoutRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasSelfServiceContext())
            return Forbid();

        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var created = await sender.Send(new CreateMyPayoutRequestCommand(request), cancellationToken)
                .ConfigureAwait(false);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (PayoutRequestWalletUnavailableException exception)
        {
            return Conflict(exception.Message);
        }
        catch (PayoutRequestInsufficientWithdrawableFundsException exception)
        {
            return Conflict(exception.Message);
        }
        catch (PayoutRequestReplayConflictException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpGet("payout-requests")]
    [EndpointSummary("List my payout requests")]
    [ProducesResponseType(typeof(IReadOnlyList<EconomyPayoutRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListMyPayoutRequests(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetSelfServiceActor(out var tenantId, out var actorId))
            return Forbid();
        if (take is < 1 or > 100)
            return BadRequest("Take must be between 1 and 100.");

        var requests = await sender.Send(new ListMyPayoutRequestsQuery(tenantId, actorId, take), cancellationToken)
            .ConfigureAwait(false);
        return Ok(requests);
    }

    [HttpPost("payout-requests/{requestId:guid}/cancel")]
    [EndpointSummary("Cancel my pending payout request")]
    [ProducesResponseType(typeof(EconomyPayoutRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelMyPayoutRequest(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (!HasSelfServiceContext())
            return Forbid();

        try
        {
            var cancelled = await sender.Send(new CancelMyPayoutRequestCommand(requestId), cancellationToken)
                .ConfigureAwait(false);
            return Ok(cancelled);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (PayoutRequestStaleCommandException exception)
        {
            return Conflict(exception.Message);
        }
        catch (PayoutRequestTransitionException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpGet("payouts/{operationId:guid}")]
    [EndpointSummary("Get my payout operation")]
    [ProducesResponseType(typeof(EconomyPayoutOperationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyPayout(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetSelfServiceActor(out var tenantId, out var actorId))
            return Forbid();

        var payout = await sender.Send(new GetMyPayoutOperationQuery(tenantId, actorId, operationId), cancellationToken)
            .ConfigureAwait(false);
        return payout is null ? NotFound() : Ok(payout);
    }

    private bool HasSelfServiceContext()
    {
        var actor = actorContextAccessor.ActorContext;
        return actor.IsAuthenticated && actor.SubjectIdAsGuid.HasValue && actor.TenantId.HasValue;
    }

    private bool TryGetSelfServiceActor(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        if (!HasSelfServiceContext())
            return false;

        tenantId = actorContextAccessor.ActorContext.TenantId!.Value;
        actorId = actorContextAccessor.ActorContext.SubjectIdAsGuid!.Value;
        return true;
    }
}
