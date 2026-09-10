using Asp.Versioning;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy/ledger")]
[Tags("economy-administration")]
[Authorize]
public sealed class EconomyLedgerQueryAdministrationController(
    IEconomyLedgerQueryReader ledger,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    [HttpGet("verification-runs")]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<EconomyJournalVerificationRunDetails>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListVerifications(
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await ledger.ListVerificationsAsync(
            tenantId, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("verification-runs/{verificationId:guid}")]
    [ProducesResponseType(typeof(EconomyJournalVerificationRunDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVerification(
        Guid verificationId,
        CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        var verification = await ledger.FindVerificationAsync(
            tenantId, verificationId, cancellationToken).ConfigureAwait(false);
        return verification is null ? NotFound() : Ok(verification);
    }

    [HttpGet("anchors")]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<EconomyAnchorOperationalDetails>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAnchors(
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await ledger.ListAnchorsAsync(
            tenantId, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("anchors/{anchorId:guid}")]
    [ProducesResponseType(typeof(EconomyAnchorOperationalDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnchor(Guid anchorId, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        var anchor = await ledger.FindAnchorAsync(
            tenantId, anchorId, cancellationToken).ConfigureAwait(false);
        return anchor is null ? NotFound() : Ok(anchor);
    }

    [HttpGet("anchors/{anchorId:guid}/verifications")]
    [ProducesResponseType(
        typeof(IReadOnlyList<EconomyAnchorVerificationOperationalStatus>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAnchorVerifications(
        Guid anchorId,
        CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await ledger.ReadAnchorVerificationsAsync(
            tenantId, anchorId, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("projection-generations")]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<EconomyProjectionGenerationOperationalDetails>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProjections(
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await ledger.ListProjectionsAsync(
            tenantId, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("projection-generations/{generation:long}")]
    [ProducesResponseType(typeof(EconomyProjectionGenerationOperationalDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjection(long generation, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        var projection = await ledger.FindProjectionAsync(
            tenantId, generation, cancellationToken).ConfigureAwait(false);
        return projection is null ? NotFound() : Ok(projection);
    }

    [HttpGet("projection-generations/{generation:long}/audit")]
    [ProducesResponseType(
        typeof(IReadOnlyList<EconomyProjectionApprovalAuditEntry>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectionAudit(
        long generation,
        CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await ledger.ReadProjectionAuditAsync(
            tenantId, generation, cancellationToken).ConfigureAwait(false));
    }

    private bool TryTenant(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue ||
            !actor.HasPermission(EconomyPermission.Keys.OperateLedger))
            return false;
        tenantId = actor.TenantId.Value;
        return true;
    }
}
