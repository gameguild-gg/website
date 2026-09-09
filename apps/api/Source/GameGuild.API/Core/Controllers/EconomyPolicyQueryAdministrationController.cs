using Asp.Versioning;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy/policies")]
[Tags("economy-administration")]
[Authorize]
public sealed class EconomyPolicyQueryAdministrationController(
    IEconomyPolicyQueryReader policies,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<EconomyCapabilityPolicyOperationalStatus>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] EconomyValueMovementCapability? capability = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await policies.ListAsync(
            tenantId,
            capability,
            limit,
            cursor,
            timeProvider.GetUtcNow(),
            cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("{policyId:guid}")]
    [ProducesResponseType(typeof(EconomyPolicyOperationalDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid policyId, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        var policy = await policies.FindAsync(
            tenantId, policyId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return policy is null ? NotFound() : Ok(policy);
    }

    [HttpGet("{policyId:guid}/audit")]
    [ProducesResponseType(typeof(IReadOnlyList<EconomyPolicyAuditEntry>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Audit(Guid policyId, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await policies.ReadAuditAsync(
            tenantId, policyId, cancellationToken).ConfigureAwait(false));
    }

    private bool TryTenant(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue ||
            !actor.HasPermission(EconomyPermission.Keys.ManagePolicies))
            return false;
        tenantId = actor.TenantId.Value;
        return true;
    }
}
