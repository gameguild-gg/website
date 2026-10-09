using Asp.Versioning;
using GameGuild.API.Dashboard;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.TestingLab;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.Access;

public sealed record AccessCapabilitiesResponse(IReadOnlyList<string> Capabilities);

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("v{version:apiVersion}/access/capabilities")]
public sealed class AccessCapabilitiesController(
    IActorContextAccessor actorContextAccessor,
    ITestingLabPermissionService testingLabPermissionService,
    ITenantMembershipChecker tenantMembershipChecker,
    IApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AccessCapabilitiesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AccessCapabilitiesResponse>> Get(CancellationToken cancellationToken = default)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } userId || actor.TenantId is not { } tenantId)
        {
            return Unauthorized();
        }

        if (!await tenantMembershipChecker.IsUserMemberOfTenantAsync(userId, tenantId, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var testingPermissions = await testingLabPermissionService
            .GetUserPermissionsAsync(userId, tenantId)
            .ConfigureAwait(false);
        var capabilities = new HashSet<string>(
            DashboardCapabilityResolver.Resolve(actor, testingPermissions),
            StringComparer.Ordinal);
        var isActiveCommitteeReviewer = await context.Set<TestingCommitteeMember>().AnyAsync(member =>
            member.UserId == userId &&
            member.TenantId == tenantId &&
            member.IsActive &&
            member.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (isActiveCommitteeReviewer)
        {
            capabilities.Add(DashboardCapabilities.TestingLabReviewApplications);
        }

        return Ok(new AccessCapabilitiesResponse(
            capabilities.Order(StringComparer.Ordinal).ToArray()));
    }
}
