using Asp.Versioning;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.Assets.Commands;
using GameGuild.CQRS;

namespace GameGuild.Assets.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/asset-libraries")]
[Authorize]
public sealed class AssetLibrariesController(
    IAssetLibraryService libraryService,
    IActorContextAccessor actorContextAccessor,
    ISender sender) : ControllerBase
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    [HttpGet("{resourceType}/{resourceId:guid}")]
    public async Task<IActionResult> Get(string resourceType, Guid resourceId, CancellationToken ct)
    {
        if (Actor.SubjectIdAsGuid is not { } userId) return Unauthorized();
        return Map(await libraryService.GetAsync(resourceType, resourceId, userId, Actor.TenantId, ct).ConfigureAwait(false));
    }

    [HttpPost("{resourceType}/{resourceId:guid}/folders")]
    public async Task<IActionResult> CreateFolder(
        string resourceType,
        Guid resourceId,
        [FromBody] CreateAssetFolderRequest request,
        CancellationToken ct)
    {
        if (Actor.SubjectIdAsGuid is not { } userId) return Unauthorized();
        return Map(await sender.Send(new CreateAssetFolderCommand(
            resourceType, resourceId, userId, Actor.TenantId, request.Name, request.ParentFolderId), ct).ConfigureAwait(false));
    }

    [HttpPut("folders/{folderId:guid}/restriction")]
    public async Task<IActionResult> RestrictFolder(
        Guid folderId,
        [FromBody] RestrictAssetFolderRequest request,
        CancellationToken ct)
    {
        if (Actor.SubjectIdAsGuid is not { } userId) return Unauthorized();
        return Map(await sender.Send(new RestrictAssetFolderCommand(
            folderId, userId, Actor.TenantId, request.Mode, request.TeamIds ?? [], request.Authorities ?? []), ct).ConfigureAwait(false));
    }

    [HttpPost("assets/{referenceId:guid}/copy")]
    public async Task<IActionResult> Copy(Guid referenceId, [FromBody] CopyAssetReferenceRequest request, CancellationToken ct)
    {
        if (Actor.SubjectIdAsGuid is not { } userId) return Unauthorized();
        return Map(await sender.Send(new CopyAssetReferenceCommand(
            referenceId, userId, Actor.TenantId, request.DisplayName, request.FolderId), ct).ConfigureAwait(false));
    }

    [HttpGet("assets/{referenceId:guid}/revisions")]
    public async Task<IActionResult> Revisions(Guid referenceId, CancellationToken ct)
    {
        if (Actor.SubjectIdAsGuid is not { } userId) return Unauthorized();
        return Map(await libraryService.GetRevisionsAsync(referenceId, userId, Actor.TenantId, ct).ConfigureAwait(false));
    }

    [HttpPost("assets/{referenceId:guid}/revisions/{revisionId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid referenceId, Guid revisionId, CancellationToken ct)
    {
        if (Actor.SubjectIdAsGuid is not { } userId) return Unauthorized();
        return Map(await sender.Send(new RestoreAssetRevisionCommand(
            referenceId, revisionId, userId, Actor.TenantId), ct).ConfigureAwait(false));
    }

    private IActionResult Map<T>(AssetLibraryResult<T> result) => result.IsSuccess
        ? Ok(result.Value)
        : result.Error switch
        {
            "NotFound" or "RevisionNotFound" => NotFound(),
            "Forbidden" => StatusCode(StatusCodes.Status403Forbidden),
            "Validation" or "InvalidFolder" or "InvalidParentFolder" => UnprocessableEntity(new { code = $"Assets.{result.Error}" }),
            _ => BadRequest()
        };
}

public sealed record CreateAssetFolderRequest(string Name, Guid? ParentFolderId);
public sealed record RestrictAssetFolderRequest(AssetFolderRestrictionMode Mode, IReadOnlyCollection<Guid>? TeamIds, IReadOnlyCollection<string>? Authorities);
public sealed record CopyAssetReferenceRequest(string? DisplayName, Guid? FolderId);
