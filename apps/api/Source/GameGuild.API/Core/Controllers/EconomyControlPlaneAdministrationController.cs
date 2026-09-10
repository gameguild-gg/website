using System.Text.Json;
using Asp.Versioning;
using GameGuild.API.Authorization;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Projections;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

public sealed record ProposeEconomyPolicyRequest(
    Guid Id,
    EconomyValueMovementCapability Capability,
    string JurisdictionCode,
    long Version,
    JsonElement Payload,
    DateTimeOffset EffectiveAt,
    DateTimeOffset ExpiresAt,
    bool ProviderReady);

public sealed record ApproveEconomyPolicyRequest(string StepUpReceipt);
public sealed record ActivateEconomyKillSwitchRequest(
    Guid Id,
    EconomyValueMovementCapability? Capability,
    string Reason);
public sealed record EconomyStepUpRequest(string StepUpReceipt);
public sealed record InspectEconomyCapabilityReadinessRequest(
    string SubjectReference,
    string JurisdictionCode,
    EconomyValueMovementCapability Capability,
    Guid RiskDecisionId,
    string OperationFingerprint,
    string ProviderHash,
    string DestinationHash,
    IReadOnlyList<string> SourceRootHashes);
public sealed record PublishEconomyAnchorRequest(string? DispatchSnapshotHash);
public sealed record ProposeEconomyReserveRequest(
    Guid Id,
    long Version,
    long? ExpectedActiveVersion,
    long PolicyVersion,
    long AuthorizationEpoch,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    ReserveBufferPosition Buffers,
    IReadOnlyCollection<ReserveServiceObservation> Services,
    IReadOnlyCollection<Guid> CustodyObservationIds,
    long IrreversibleInFlightProviderCostUsdNanos);

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy")]
[Tags("economy-administration")]
[Authorize]
public sealed class EconomyControlPlaneAdministrationController(
    ISender sender,
    IEconomyCapabilityReadinessInspector capabilityReadiness,
    IEconomyOperationsReader operations,
    IEconomyReserveCustodyControlPlane reserves,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpPost("policies")]
    [ProducesResponseType(typeof(EconomyCapabilityPolicy), StatusCodes.Status201Created)]
    public async Task<IActionResult> ProposePolicy(
        [FromBody] ProposeEconomyPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ManagePolicies, out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        var policy = await sender.Send(new ProposeEconomyPolicyEndpointCommand(new EconomyCapabilityPolicyProposal(
            request.Id,
            tenantId,
            request.Capability,
            request.JurisdictionCode,
            request.Version,
            request.Payload,
            actorId,
            now,
            request.EffectiveAt,
            request.ExpiresAt,
            request.ProviderReady)), cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, policy);
    }

    [HttpPost("policies/{policyId:guid}/approve")]
    [ProducesResponseType(typeof(EconomyCapabilityPolicy), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApprovePolicy(
        Guid policyId,
        [FromBody] ApproveEconomyPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ManagePolicies, out _, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        var operation = EconomyStepUpOperation.Create(
            "economy.policy.approve",
            $"policy:{policyId:N}",
            policyId.ToString("N"));
        return Ok(await sender.Send(new ApproveEconomyPolicyEndpointCommand(
            operation,
            request.StepUpReceipt,
            policyId,
            actorId),
            cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("capabilities/readiness")]
    [NoBusinessMutationEndpoint("Capability readiness inspection is read-only and uses POST for its structured evaluation context.")]
    [ProducesResponseType(typeof(EconomyCapabilityEvaluationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> InspectCapabilityReadiness(
        [FromBody] InspectEconomyCapabilityReadinessRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ReadOperations, out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        var result = await capabilityReadiness.InspectAsync(new EconomyCapabilityEvaluationContext(
            tenantId,
            actorId,
            request.SubjectReference,
            request.JurisdictionCode,
            request.Capability,
            request.RiskDecisionId,
            request.OperationFingerprint,
            request.ProviderHash,
            request.DestinationHash,
            request.SourceRootHashes,
            timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("capabilities/configuration")]
    [ProducesResponseType(typeof(EconomyCapabilityConfigurationSnapshot), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReadCapabilityConfiguration(
        [FromQuery] bool includeInactiveKillSwitches = false,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(EconomyPermission.Keys.ReadOperations, out var tenantId, out _)) return Forbid();
        return Ok(await operations.ReadCapabilityConfigurationAsync(
            tenantId,
            includeInactiveKillSwitches,
            limit,
            timeProvider.GetUtcNow(),
            cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("kill-switches")]
    [ProducesResponseType(typeof(EconomyKillSwitchState), StatusCodes.Status201Created)]
    public async Task<IActionResult> ActivateKillSwitch(
        [FromBody] ActivateEconomyKillSwitchRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ManageKillSwitches, out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        var scope = request.Capability.HasValue
            ? EconomyKillSwitchScope.ForCapability(tenantId, request.Capability.Value)
            : EconomyKillSwitchScope.ForTenant(tenantId);
        var state = await sender.Send(new ActivateEconomyKillSwitchEndpointCommand(
            request.Id,
            scope,
            request.Reason,
            actorId,
            timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, state);
    }

    [HttpPost("kill-switches/{killSwitchId:guid}/release-proposals")]
    [ProducesResponseType(typeof(EconomyKillSwitchState), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProposeKillSwitchRelease(
        Guid killSwitchId,
        [FromBody] EconomyStepUpRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ManageKillSwitches, out _, out var actorId)) return Forbid();
        var operation = EconomyStepUpOperation.Create(
            "economy.kill-switch.release.propose",
            $"kill-switch:{killSwitchId:N}",
            killSwitchId.ToString("N"));
        return Ok(await sender.Send(new ProposeEconomyKillSwitchReleaseEndpointCommand(
            operation,
            request.StepUpReceipt,
            killSwitchId,
            actorId),
            cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("kill-switches/{killSwitchId:guid}/release-approvals")]
    [ProducesResponseType(typeof(EconomyKillSwitchState), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveKillSwitchRelease(
        Guid killSwitchId,
        [FromBody] EconomyStepUpRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ManageKillSwitches, out _, out var actorId)) return Forbid();
        var operation = EconomyStepUpOperation.Create(
            "economy.kill-switch.release.approve",
            $"kill-switch:{killSwitchId:N}",
            killSwitchId.ToString("N"));
        return Ok(await sender.Send(new ApproveEconomyKillSwitchReleaseEndpointCommand(
            operation,
            request.StepUpReceipt,
            killSwitchId,
            actorId),
            cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("kill-switches/{killSwitchId:guid}/release")]
    [ProducesResponseType(typeof(EconomyKillSwitchState), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReleaseKillSwitch(Guid killSwitchId, CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ManageKillSwitches, out _, out _)) return Forbid();
        return Ok(await sender.Send(new ReleaseEconomyKillSwitchEndpointCommand(
            killSwitchId,
            timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("ledger/verification-runs")]
    [ProducesResponseType(typeof(JournalIntegrityRunResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyJournal(CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.OperateLedger, out _, out var actorId)) return Forbid();
        var result = await sender.Send(new VerifyEconomyJournalEndpointCommand(
            $"admin:{actorId:N}",
            timeProvider.GetUtcNow(),
            10_000),
            cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("ledger/health")]
    [ProducesResponseType(typeof(EconomyLedgerHealthSnapshot), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReadLedgerHealth(CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ReadOperations, out _, out _)) return Forbid();
        return Ok(await operations.ReadLedgerHealthAsync(
            timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("ledger/anchors")]
    [ProducesResponseType(typeof(EconomyAnchorPublicationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PublishAnchor(
        [FromBody] PublishEconomyAnchorRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.OperateLedger, out _, out _)) return Forbid();
        var result = await sender.Send(new PublishEconomyAnchorEndpointCommand(
            timeProvider.GetUtcNow(),
            true,
            request.DispatchSnapshotHash),
            cancellationToken).ConfigureAwait(false);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpPost("ledger/anchors/verification-runs")]
    [ProducesResponseType(typeof(AnchorVerificationRunResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyAnchors(CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.OperateLedger, out _, out _)) return Forbid();
        return Ok(await sender.Send(new VerifyEconomyAnchorsEndpointCommand(
            timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("ledger/projection-generations")]
    [ProducesResponseType(typeof(ProjectionGenerationState), StatusCodes.Status201Created)]
    public async Task<IActionResult> RebuildProjections(CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.OperateLedger, out _, out var actorId)) return Forbid();
        var generation = await sender.Send(new RebuildEconomyProjectionsEndpointCommand(
            actorId, timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, generation);
    }

    [HttpPost("ledger/projection-generations/{generation:long}/approvals")]
    [ProducesResponseType(typeof(ProjectionGenerationState), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveProjection(
        long generation,
        [FromBody] EconomyStepUpRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.OperateLedger, out _, out var actorId)) return Forbid();
        var operation = EconomyStepUpOperation.Create(
            "economy.projection.approve",
            $"projection:{generation}",
            generation.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Ok(await sender.Send(new ApproveEconomyProjectionEndpointCommand(
            operation,
            request.StepUpReceipt,
            generation,
            actorId),
            cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("custody/observations")]
    [ProducesResponseType(typeof(DurableCustodyObservation), StatusCodes.Status201Created)]
    public async Task<IActionResult> IngestCustodyObservation(
        [FromBody] CustodyObservationCommand request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ManageReserves, out _, out _)) return Forbid();
        return StatusCode(
            StatusCodes.Status201Created,
            await sender.Send(new IngestEconomyCustodyObservationEndpointCommand(request), cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("reserves/liabilities")]
    [ProducesResponseType(typeof(EconomyLiabilitySnapshot), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculateLiabilities(CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ManageReserves, out _, out _)) return Forbid();
        return Ok(await reserves.CalculateLiabilitiesAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("reserves/proposals")]
    [ProducesResponseType(typeof(DurableReserveProposalState), StatusCodes.Status201Created)]
    public async Task<IActionResult> ProposeReserve(
        [FromBody] ProposeEconomyReserveRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ManageReserves, out _, out var actorId)) return Forbid();
        var proposed = await sender.Send(new ProposeEconomyReserveEndpointCommand(new DurableReserveProposalCommand(
            request.Id,
            request.Version,
            request.ExpectedActiveVersion,
            request.PolicyVersion,
            request.AuthorizationEpoch,
            request.ObservedAt,
            request.ExpiresAt,
            request.Buffers,
            request.Services,
            request.CustodyObservationIds,
            request.IrreversibleInFlightProviderCostUsdNanos,
            actorId,
            timeProvider.GetUtcNow())), cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, proposed);
    }

    [HttpPost("reserves/proposals/{proposalId:guid}/approve")]
    [ProducesResponseType(typeof(ReserveHead), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveReserve(
        Guid proposalId,
        [FromBody] EconomyStepUpRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(EconomyPermission.Keys.ManageReserves, out _, out var actorId)) return Forbid();
        var operation = EconomyStepUpOperation.Create(
            "economy.reserve.approve",
            $"reserve:{proposalId:N}",
            proposalId.ToString("N"));
        return Ok(await sender.Send(new ApproveEconomyReserveEndpointCommand(
            operation,
            request.StepUpReceipt,
            proposalId,
            actorId),
            cancellationToken).ConfigureAwait(false));
    }

    private bool TryActor(string permission, out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue || !actor.SubjectIdAsGuid.HasValue ||
            !actor.HasPermission(permission))
            return false;
        tenantId = actor.TenantId.Value;
        actorId = actor.SubjectIdAsGuid.Value;
        return true;
    }
}
