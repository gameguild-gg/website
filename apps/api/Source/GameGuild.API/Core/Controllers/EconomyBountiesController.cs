using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Economy.Bounties;
using GameGuild.Economy.Contracts;
using GameGuild.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

public sealed record CreateMyBountyRequest(
    CurrencyCode Currency,
    long AmountUnits,
    bool RequiresPrerequisite,
    int MinimumReputation,
    bool RequiresInstructorVerification,
    DateTimeOffset ExpiresAt,
    string IdempotencyKey);

public sealed record CompleteMyBountyRequest(
    string IdempotencyKey);

public sealed record BountyProtectedOperationFailureResponse(
    EconomyProtectedOperationState State,
    Guid? ReviewId,
    IReadOnlyList<string> Diagnostics);

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/economy/bounties")]
[Tags("economy-bounties")]
[Authorize]
public sealed class EconomyBountiesController(
    ISender sender,
    IDurableBountyApplicationService bounties,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpPost]
    [ProducesResponseType(typeof(DurableBountyView), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMyBountyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out _, out _)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        return await ExecuteProtectedAsync(
            () => sender.Send(new CreateBountyEndpointCommand(new CreateDurableBountyRequest(
                new CoinAmount(request.Currency, request.AmountUnits),
                new BountyEligibilityRequirements(
                    request.RequiresPrerequisite,
                    request.MinimumReputation,
                    request.RequiresInstructorVerification),
                request.ExpiresAt,
                new IdempotencyKey(request.IdempotencyKey),
                timeProvider.GetUtcNow())), cancellationToken),
            result => CreatedAtAction(
                nameof(Get), new { version = "1", bountyId = result.Id.Value }, result));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DurableBountyView>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] BountyStatus? status,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out _)) return Forbid();
        return Ok(await bounties.ListAsync(tenantId, status, cancellationToken));
    }

    [HttpGet("{bountyId:guid}")]
    [ProducesResponseType(typeof(DurableBountyView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid bountyId, CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out _)) return Forbid();
        var result = await bounties.FindAsync(tenantId, new BountyId(bountyId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{bountyId:guid}:claim")]
    [ProducesResponseType(typeof(DurableBountyView), StatusCodes.Status200OK)]
    public async Task<IActionResult> Claim(
        Guid bountyId,
        [FromBody] CompleteMyBountyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out _, out _)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        return await ExecuteProtectedAsync(
            () => sender.Send(new ClaimBountyEndpointCommand(new ClaimDurableBountyRequest(
                new BountyId(bountyId),
                new IdempotencyKey(request.IdempotencyKey),
                timeProvider.GetUtcNow())), cancellationToken),
            Ok);
    }

    [HttpPost("{bountyId:guid}:reclaim")]
    [ProducesResponseType(typeof(DurableBountyView), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reclaim(
        Guid bountyId,
        [FromBody] CompleteMyBountyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out _, out _)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        return await ExecuteProtectedAsync(
            () => sender.Send(new ReclaimBountyEndpointCommand(new ReclaimDurableBountyRequest(
                new BountyId(bountyId),
                new IdempotencyKey(request.IdempotencyKey),
                timeProvider.GetUtcNow())), cancellationToken),
            Ok);
    }

    private static async Task<IActionResult> ExecuteProtectedAsync(
        Func<Task<DurableBountyView>> action,
        Func<DurableBountyView, IActionResult> success)
    {
        try
        {
            return success(await action().ConfigureAwait(false));
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
            return new ObjectResult(new BountyProtectedOperationFailureResponse(
                exception.State, exception.ReviewId, exception.Diagnostics)) { StatusCode = status };
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
[Route("api/v{version:apiVersion}/admin/economy/bounties")]
[Tags("economy-administration")]
[Authorize]
public sealed class EconomyBountiesAdministrationController(
    IDurableBountyApplicationService bounties,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    [HttpGet("expired")]
    [ProducesResponseType(typeof(IReadOnlyList<DurableBountyView>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListExpired(CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue ||
            !actor.HasPermission(EconomyPermission.Keys.OperateBounties))
            return Forbid();
        return Ok(await bounties.ListAsync(
            actor.TenantId.Value, BountyStatus.Expired, cancellationToken));
    }
}
