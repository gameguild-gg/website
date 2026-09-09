using Asp.Versioning;
using GameGuild.API.Authorization;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.Controllers;

public sealed record CaptureLegacyEconomyMigrationRequest(Guid BatchId, string JurisdictionCode);

public sealed record BackfillLegacyEconomyWalletRequest(
    Guid LegacyWalletId,
    Guid RiskDecisionId,
    string OperationFingerprint);

public sealed record ProposeLegacyEconomyCutoverRequest(string Reason, string StepUpReceipt);

public sealed record ApproveLegacyEconomyCutoverRequest(string StepUpReceipt);

public sealed record RollbackLegacyEconomyCutoverRequest(string Reason, string StepUpReceipt);

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy/legacy-migration/batches")]
[Tags("economy-legacy-migration-administration")]
[Authorize]
public sealed class EconomyLegacyMigrationAdministrationController(
    ISender sender,
    ILegacyEconomyShadowMigration migration,
    ILegacyEconomyQueryReader queries,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<LegacyEconomyShadowBatchSummary>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] LegacyEconomyShadowState? state = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out var tenantId, out _)) return Forbid();
        return Ok(await queries.ListAsync(
            tenantId, state, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("{batchId:guid}")]
    [ProducesResponseType(typeof(LegacyEconomyShadowBatchView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid batchId, CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out _)) return Forbid();
        var batch = await migration.GetAsync(tenantId, batchId, cancellationToken).ConfigureAwait(false);
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpPost]
    [ProducesResponseType(typeof(LegacyEconomyShadowBatchView), StatusCodes.Status201Created)]
    public async Task<IActionResult> Capture(
        [FromBody] CaptureLegacyEconomyMigrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        return await ExecuteAsync(() => sender.Send(new CaptureLegacyEconomyEndpointCommand(
            new CaptureLegacyEconomyShadowCommand(
                request.BatchId,
                tenantId,
                actorId,
                request.JurisdictionCode,
                timeProvider.GetUtcNow())),
            cancellationToken), created: true).ConfigureAwait(false);
    }

    [HttpPost("{batchId:guid}/wallets:backfill")]
    [ProducesResponseType(typeof(LegacyEconomyShadowBatchView), StatusCodes.Status200OK)]
    public async Task<IActionResult> Backfill(
        Guid batchId,
        [FromBody] BackfillLegacyEconomyWalletRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        return await ExecuteAsync(() => sender.Send(new BackfillLegacyEconomyEndpointCommand(
            new BackfillLegacyEconomyWalletCommand(
                batchId,
                tenantId,
                actorId,
                request.LegacyWalletId,
                request.RiskDecisionId,
                request.OperationFingerprint,
                timeProvider.GetUtcNow())),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("{batchId:guid}:reconcile")]
    [ProducesResponseType(typeof(LegacyEconomyShadowBatchView), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reconcile(Guid batchId, CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        return await ExecuteAsync(() => sender.Send(new ReconcileLegacyEconomyEndpointCommand(
            new ReconcileLegacyEconomyShadowCommand(
                batchId, tenantId, actorId, timeProvider.GetUtcNow())),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("{batchId:guid}/cutover:propose")]
    [ProducesResponseType(typeof(LegacyEconomyShadowBatchView), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProposeCutover(
        Guid batchId,
        [FromBody] ProposeLegacyEconomyCutoverRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        var operation = EconomyStepUpOperation.Create(
            "economy.legacy-cutover.propose",
            $"legacy-cutover:{batchId:N}",
            batchId.ToString("N"),
            request.Reason);
        return await ExecuteAsync(() => sender.Send(new ProposeLegacyEconomyCutoverEndpointCommand(
            operation, request.StepUpReceipt, batchId, tenantId, actorId, request.Reason),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("{batchId:guid}/cutover:approve")]
    [ProducesResponseType(typeof(LegacyEconomyShadowBatchView), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveCutover(
        Guid batchId,
        [FromBody] ApproveLegacyEconomyCutoverRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        var operation = EconomyStepUpOperation.Create(
            "economy.legacy-cutover.approve",
            $"legacy-cutover:{batchId:N}",
            batchId.ToString("N"));
        return await ExecuteAsync(() => sender.Send(new ApproveLegacyEconomyCutoverEndpointCommand(
            operation, request.StepUpReceipt, batchId, tenantId, actorId),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("{batchId:guid}/cutover:rollback")]
    [ProducesResponseType(typeof(LegacyEconomyShadowBatchView), StatusCodes.Status200OK)]
    public async Task<IActionResult> RollbackCutover(
        Guid batchId,
        [FromBody] RollbackLegacyEconomyCutoverRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        var operation = EconomyStepUpOperation.Create(
            "economy.legacy-cutover.rollback",
            $"legacy-cutover:{batchId:N}",
            batchId.ToString("N"),
            request.Reason);
        return await ExecuteAsync(() => sender.Send(new RollbackLegacyEconomyCutoverEndpointCommand(
            operation, request.StepUpReceipt, batchId, tenantId, actorId, request.Reason),
            cancellationToken)).ConfigureAwait(false);
    }

    private async Task<IActionResult> ExecuteAsync(
        Func<Task<LegacyEconomyShadowBatchView>> action,
        bool created = false)
    {
        try
        {
            var result = await action().ConfigureAwait(false);
            return created ? StatusCode(StatusCodes.Status201Created, result) : Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
        catch (LegacyEconomyShadowMigrationException exception) { return Conflict(exception.Message); }
        catch (DbUpdateConcurrencyException exception) { return Conflict(exception.Message); }
        catch (EconomyCapabilityAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                state = exception.State,
                diagnostics = exception.Diagnostics
            });
        }
    }

    private bool TryActor(out Guid tenantId, out Guid actorId)
    {
        var actor = actorContextAccessor.ActorContext;
        tenantId = actor.TenantId ?? Guid.Empty;
        actorId = actor.SubjectIdAsGuid ?? Guid.Empty;
        return actor.IsAuthenticated && tenantId != Guid.Empty && actorId != Guid.Empty &&
               actor.HasPermission(EconomyPermission.Keys.ManageLegacyMigration);
    }
}
