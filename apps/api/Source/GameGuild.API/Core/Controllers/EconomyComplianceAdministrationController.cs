using Asp.Versioning;
using GameGuild.Compliance.FinancialCrime;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.TrustSafety;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

public sealed record AssignFinancialCrimeCaseRequest(long ExpectedVersion);
public sealed record DecideFinancialCrimeCaseRequest(
    Guid Id,
    long Version,
    long ExpectedCaseVersion,
    FinancialCrimeOutcome Outcome,
    long PolicyVersion,
    string ReasonCode,
    string EvidenceHash,
    string RawObjectReference,
    DateTimeOffset ExpiresAt);
public sealed record RecordRegulatoryReferenceRequest(
    string Kind,
    string JurisdictionCode,
    string ReferenceHash);
public sealed record AssignTrustSafetyAppealRequest(long ExpectedVersion);
public sealed record DecideTrustSafetyAppealRequest(
    long ExpectedVersion,
    bool Overturn,
    string ReasonCode,
    string EvidenceHash);

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy/compliance")]
[Tags("economy-compliance-administration")]
[Authorize]
public sealed class EconomyComplianceAdministrationController(
    ISender sender,
    IFinancialCrimeControlPlane financialCrime,
    ITrustSafetyControlPlane trustSafety,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpGet("financial-crime/cases")]
    [ProducesResponseType(typeof(IReadOnlyList<FinancialCrimeCase>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFinancialCrimeCases(
        [FromQuery] FinancialCrimeCaseState? state = null,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out var tenantId, out _)) return Forbid();
        return Ok(await financialCrime.ReadCasesAsync(tenantId, state, take, cancellationToken)
            .ConfigureAwait(false));
    }

    [HttpGet("financial-crime/cases/{caseId:guid}")]
    [ProducesResponseType(typeof(FinancialCrimeCaseDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFinancialCrimeCase(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out _)) return Forbid();
        try
        {
            return Ok(await financialCrime.ReadCaseDetailsAsync(tenantId, caseId, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("financial-crime/cases/{caseId:guid}/assignment")]
    [ProducesResponseType(typeof(FinancialCrimeCase), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignFinancialCrimeCase(
        Guid caseId,
        [FromBody] AssignFinancialCrimeCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        try
        {
            return Ok(await sender.Send(new AssignFinancialCrimeCaseEndpointCommand(
                tenantId, caseId, actorId, request.ExpectedVersion,
                timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (FinancialCrimeConflictException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpPost("financial-crime/cases/{caseId:guid}/decisions")]
    [ProducesResponseType(typeof(FinancialCrimeCaseDecision), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DecideFinancialCrimeCase(
        Guid caseId,
        [FromBody] DecideFinancialCrimeCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        try
        {
            var details = await financialCrime.ReadCaseDetailsAsync(tenantId, caseId, cancellationToken)
                .ConfigureAwait(false);
            var decision = new FinancialCrimeCaseDecision(
                request.Id,
                caseId,
                tenantId,
                details.Case.SubjectHash,
                request.Version,
                request.Outcome,
                request.PolicyVersion,
                request.ReasonCode,
                request.EvidenceHash,
                request.RawObjectReference,
                actorId,
                timeProvider.GetUtcNow(),
                request.ExpiresAt);
            var result = await sender.Send(
                new DecideFinancialCrimeCaseEndpointCommand(decision, request.ExpectedCaseVersion),
                cancellationToken).ConfigureAwait(false);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (FinancialCrimeConflictException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpPost("financial-crime/cases/{caseId:guid}/regulatory-references")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordRegulatoryReference(
        Guid caseId,
        [FromBody] RecordRegulatoryReferenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        try
        {
            await sender.Send(new RecordRegulatoryReferenceEndpointCommand(
                tenantId, caseId, request.Kind, request.JurisdictionCode,
                request.ReferenceHash, actorId, timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("trust-safety/appeals")]
    [ProducesResponseType(typeof(IReadOnlyList<TrustSafetyAppeal>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTrustSafetyAppeals(
        [FromQuery] TrustSafetyAppealState? state = null,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out var tenantId, out _)) return Forbid();
        return Ok(await trustSafety.ReadAppealsAsync(tenantId, state, take, cancellationToken)
            .ConfigureAwait(false));
    }

    [HttpPost("trust-safety/appeals/{appealId:guid}/assignment")]
    [ProducesResponseType(typeof(TrustSafetyAppeal), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignTrustSafetyAppeal(
        Guid appealId,
        [FromBody] AssignTrustSafetyAppealRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        try
        {
            return Ok(await sender.Send(new AssignTrustSafetyAppealEndpointCommand(
                tenantId, appealId, actorId, request.ExpectedVersion,
                timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (TrustSafetyConflictException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpPost("trust-safety/appeals/{appealId:guid}/decisions")]
    [ProducesResponseType(typeof(TrustSafetyAppeal), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DecideTrustSafetyAppeal(
        Guid appealId,
        [FromBody] DecideTrustSafetyAppealRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        try
        {
            return Ok(await sender.Send(new DecideTrustSafetyAppealEndpointCommand(
                tenantId, appealId, actorId, request.ExpectedVersion, request.Overturn,
                request.ReasonCode, request.EvidenceHash, timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (TrustSafetyConflictException exception)
        {
            return Conflict(exception.Message);
        }
    }

    private bool TryActor(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue || !actor.SubjectIdAsGuid.HasValue ||
            !actor.HasPermission(EconomyPermission.Keys.OperateCompliance))
            return false;
        tenantId = actor.TenantId.Value;
        actorId = actor.SubjectIdAsGuid.Value;
        return true;
    }
}
