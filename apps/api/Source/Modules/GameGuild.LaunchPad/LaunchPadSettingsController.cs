using GameGuild.Projects;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.LaunchPad;

[ApiController]
[Authorize]
[Route("v1/launch-pad/settings")]
public sealed class LaunchPadSettingsController(
    IApplicationDbContext context,
    IRequestContextAccessor requestContext,
    ILaunchPadAuthorizationService authorization,
    ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LaunchPadSettingsProjection>> GetSettings(CancellationToken cancellationToken)
    {
        var tenantId = requestContext.CurrentTenantId;
        if (!tenantId.HasValue) return Unauthorized();
        if (!await authorization.CanParticipateAsync(tenantId.Value, cancellationToken).ConfigureAwait(false)) return Forbid();

        var settings = await context.Set<LaunchPadSettings>()
            .SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        settings ??= await sender.Send(new CreateDefaultLaunchPadSettingsCommand(tenantId.Value), cancellationToken)
            .ConfigureAwait(false);
        return Ok(LaunchPadSettingsProjection.FromEntity(settings));
    }

    [HttpPut]
    public async Task<ActionResult<LaunchPadSettingsProjection>> UpdateSettings(
        [FromBody] UpdateLaunchPadSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = requestContext.CurrentTenantId;
        if (!tenantId.HasValue) return Unauthorized();
        if (!await authorization.CanManageSettingsAsync(tenantId.Value, cancellationToken).ConfigureAwait(false)) return Forbid();

        var settings = await sender.Send(new UpdateLaunchPadSettingsEndpointCommand(
            tenantId.Value, request.VersionSubmissionPolicy), cancellationToken).ConfigureAwait(false);
        return Ok(LaunchPadSettingsProjection.FromEntity(settings));
    }
}
public sealed record UpdateLaunchPadSettingsRequest(VersionSubmissionPolicy VersionSubmissionPolicy);

public sealed record LaunchPadSettingsProjection(
    Guid Id,
    Guid TenantId,
    VersionSubmissionPolicy VersionSubmissionPolicy,
    DateTime UpdatedAt)
{
    public static LaunchPadSettingsProjection FromEntity(LaunchPadSettings entity) => new(
        entity.Id,
        entity.TenantId!.Value,
        entity.VersionSubmissionPolicy,
        entity.UpdatedAt);
}
