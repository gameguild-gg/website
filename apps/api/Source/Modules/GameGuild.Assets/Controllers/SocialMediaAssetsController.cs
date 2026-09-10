using Asp.Versioning;
using GameGuild.Assets.Commands;
using GameGuild.Assets.SocialMedia;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Assets.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("v{version:apiVersion}/assets/social-media")]
public sealed class SocialMediaAssetsController(
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    ISocialMediaAssetService assets) : ControllerBase
{
    [HttpGet("{assetReferenceId:guid}")]
    [ProducesResponseType(typeof(SocialMediaAssetDescriptor), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid assetReferenceId, CancellationToken cancellationToken)
    {
        var actorId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue)
            return Unauthorized();

        var descriptor = await assets.InspectOwnedAsync(
            assetReferenceId,
            actorId.Value,
            cancellationToken).ConfigureAwait(false);

        return descriptor is null ? NotFound() : Ok(descriptor);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(101 * 1024 * 1024)]
    [ProducesResponseType(typeof(SocialMediaAssetDescriptor), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.SubjectIdAsGuid.HasValue || !actor.TenantId.HasValue)
            return Unauthorized();

        if (file is null || file.Length <= 0)
            return BadRequest(new ProblemDetails { Title = "A media file is required." });

        await using var stream = file.OpenReadStream();
        var validation = await SocialMediaAssetPolicy.ValidateAsync(
            stream,
            file.ContentType,
            file.Length,
            cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
            return BadRequest(new ProblemDetails { Title = "Invalid social media upload", Detail = validation.Error });

        var result = await sender.Send(new UploadAssetCommand(
            stream,
            file.FileName,
            SocialMediaAssetPolicy.NormalizeMimeType(file.ContentType),
            actor.SubjectIdAsGuid.Value,
            actor.TenantId.Value,
            file.FileName,
            AssetAccessPolicy.Authenticated), cancellationToken).ConfigureAwait(false);

        if (result.Error is not null)
            return result.Error == "Forbidden"
                ? Forbid()
                : BadRequest(new ProblemDetails { Title = "Media upload failed", Detail = result.Error });

        var descriptor = await assets.InspectOwnedAsync(
            result.AssetReferenceId,
            actor.SubjectIdAsGuid.Value,
            cancellationToken).ConfigureAwait(false);
        if (descriptor is null)
            return Problem("The uploaded asset could not be inspected.");

        return Created($"/v1/assets/{descriptor.AssetReferenceId}", descriptor);
    }
}
