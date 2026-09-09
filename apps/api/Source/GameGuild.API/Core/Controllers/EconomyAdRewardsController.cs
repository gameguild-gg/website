using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.AdRewards;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

public sealed record StartMyAdRewardSessionRequest(
    string Network,
    string CreativeId,
    double RequiredDurationSeconds,
    string IdempotencyKey);
public sealed record CompleteMyAdRewardSessionRequest(
    string Token,
    AdPlaybackEvidence Playback,
    ProviderCompletionProof? ProviderProof,
    string IdempotencyKey);
public sealed record AdRewardProtectedOperationFailureResponse(
    EconomyProtectedOperationState State,
    Guid? ReviewId,
    IReadOnlyList<string> Diagnostics);
public sealed record AdRewardRequestRiskContext(
    string DeviceRiskHash,
    string IpRiskHash,
    string AsnRiskHash);
public interface IAdRewardRequestRiskContextResolver
{
    ValueTask<AdRewardRequestRiskContext> ResolveAsync(
        Guid tenantId,
        Guid actorId,
        CancellationToken cancellationToken = default);
}

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/economy/ad-rewards")]
[Tags("economy-ad-rewards")]
[Authorize]
public sealed class EconomyAdRewardsController(
    ISender sender,
    IDurableAdRewardSessionReader reader,
    IEconomyWalletDirectory wallets,
    IAdRewardRequestRiskContextResolver requestRiskContext,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(DurableAdRewardSessionResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Start(
        [FromBody] StartMyAdRewardSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        var wallet = await wallets.GetOwnerWalletAsync(tenantId, actorId, cancellationToken)
            .ConfigureAwait(false);
        AdRewardRequestRiskContext risk;
        try
        {
            risk = await requestRiskContext.ResolveAsync(tenantId, actorId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AdRewardRiskContextUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                State = "RiskContextUnavailable",
                Message = "Ad reward risk evidence is unavailable."
            });
        }
        var result = await sender.Send(new StartAdRewardSessionEndpointCommand(new StartDurableAdRewardSessionRequest(
            tenantId,
            actorId,
            wallet.WalletId,
            request.Network,
            request.CreativeId,
            risk.DeviceRiskHash,
            risk.IpRiskHash,
            risk.AsnRiskHash,
            TimeSpan.FromSeconds(request.RequiredDurationSeconds),
            new IdempotencyKey(request.IdempotencyKey),
            timeProvider.GetUtcNow())), cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("sessions/{sessionId:guid}/complete")]
    [ProducesResponseType(typeof(DurableAdRewardCompletionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AdRewardProtectedOperationFailureResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(AdRewardProtectedOperationFailureResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(AdRewardProtectedOperationFailureResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Complete(
        Guid sessionId,
        [FromBody] CompleteMyAdRewardSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out _, out _)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProviderProof is not null && request.ProviderProof.SessionId != sessionId)
            return BadRequest("Provider proof is bound to another ad reward session.");
        try
        {
            var result = await sender.Send(new CompleteAdRewardSessionEndpointCommand(new CompleteDurableAdRewardSessionRequest(
                new SignedAdRewardSession(request.Token),
                request.Playback,
                request.ProviderProof,
                new IdempotencyKey(request.IdempotencyKey),
                timeProvider.GetUtcNow())), cancellationToken).ConfigureAwait(false);
            return result.SessionId == sessionId
                ? Ok(result)
                : Conflict("The signed token is bound to another ad reward session.");
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
            return new ObjectResult(new AdRewardProtectedOperationFailureResponse(
                exception.State, exception.ReviewId, exception.Diagnostics)) { StatusCode = status };
        }
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType(typeof(DurableAdRewardSessionStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Status(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        var status = await reader.FindAsync(tenantId, actorId, sessionId, cancellationToken)
            .ConfigureAwait(false);
        return status is null ? NotFound() : Ok(status);
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
[Route("api/v{version:apiVersion}/admin/economy/ad-rewards")]
[Tags("economy-administration")]
[Authorize]
public sealed class EconomyAdRewardsAdministrationController(
    ISender sender,
    IDurableAdRewardReportReader reportReader,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpPost("reports")]
    [ProducesResponseType(typeof(DurableAdProviderReportImportResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportReport(
        [FromBody] AdProviderReport report,
        CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue ||
            !actor.HasPermission(EconomyPermission.Keys.OperateAdRewards))
            return Forbid();
        return Ok(await sender.Send(
            new ImportAdRewardReportEndpointCommand(new ImportDurableAdProviderReportRequest(
                actor.TenantId.Value,
                report,
                timeProvider.GetUtcNow())),
            cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("reports")]
    [ProducesResponseType(typeof(IReadOnlyList<DurableAdProviderReportStatus>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListReports(
        [FromQuery] string? network = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue ||
            !actor.HasPermission(EconomyPermission.Keys.OperateAdRewards))
            return Forbid();
        return Ok(await reportReader.ListAsync(
            actor.TenantId.Value, network, limit, cancellationToken).ConfigureAwait(false));
    }
}
