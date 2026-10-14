using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.CQRS.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Controller for managing content-type scoped permission grants (layer 2 of the
///     3-layer DAC model, issues #360/#365). Provides REST endpoints for granting,
///     revoking, denying and listing content-type permissions. Mutations dispatch the
///     guarded Authorization-module command types; their handlers enforce
///     tenant-admin/system-admin authorization, bump the tenant security version and
///     audit every change.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("access-control/content-type-permissions")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/authorization/content-types")]
[Authorize]
[Produces("application/json")]
public sealed class ContentTypePermissionsController(
    ISender sender,
    ILogger<ContentTypePermissionsController> logger) : BaseApiController
{
    /// <summary>
    ///     Grants content-type scoped permissions to a user (null user = the tenant
    ///     default row for the content type).
    /// </summary>
    /// <param name="command">The grant command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The id of the grant row.</returns>
    /// <response code="200">Permissions granted successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an administrator of the target tenant.</response>
    [HttpPost("grants")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GrantPermission(
        [FromBody] GrantContentTypePermissionGrantCommand command,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Granting content-type permissions {Permissions} on {ContentType} in tenant {TenantId}",
            LogRedaction.Sanitize(string.Join(", ", command.Permissions)),
            LogRedaction.Sanitize(command.ContentTypeName),
            LogRedaction.RedactId(command.TenantId.Value, "tid"));

        var grantId = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return Ok(new { GrantId = grantId });
    }

    /// <summary>
    ///     Adds explicit DENY-WINS entries to a content-type grant row.
    /// </summary>
    /// <param name="command">The deny command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The id of the grant row.</returns>
    /// <response code="200">Permissions denied successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an administrator of the target tenant.</response>
    [HttpPost("denies")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DenyPermission(
        [FromBody] DenyContentTypePermissionGrantCommand command,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Denying content-type permissions {Permissions} on {ContentType} in tenant {TenantId}",
            LogRedaction.Sanitize(string.Join(", ", command.Permissions)),
            LogRedaction.Sanitize(command.ContentTypeName),
            LogRedaction.RedactId(command.TenantId.Value, "tid"));

        var grantId = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return Ok(new { GrantId = grantId });
    }

    /// <summary>
    ///     Revokes content-type scoped permissions from a user (the row is soft-deleted
    ///     once its payload is empty).
    /// </summary>
    /// <param name="command">The revoke command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Boolean indicating success.</returns>
    /// <response code="200">Permissions revoked successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an administrator of the target tenant.</response>
    [HttpPost("grants:revoke")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokePermission(
        [FromBody] RevokeContentTypePermissionGrantCommand command,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Revoking content-type permissions {Permissions} on {ContentType} in tenant {TenantId}",
            LogRedaction.Sanitize(string.Join(", ", command.Permissions)),
            LogRedaction.Sanitize(command.ContentTypeName),
            LogRedaction.RedactId(command.TenantId.Value, "tid"));

        var success = await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return Ok(new { Success = success });
    }

    /// <summary>
    ///     Soft-deletes one content-type grant row by id (tenant-isolated).
    /// </summary>
    /// <param name="tenantId">The tenant the row must belong to.</param>
    /// <param name="grantId">The grant row id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Grant deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an administrator of the target tenant.</response>
    /// <response code="404">Grant not found.</response>
    [HttpDelete("{tenantId:guid}/grants/{grantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGrant(
        [FromRoute] Guid tenantId,
        [FromRoute] Guid grantId,
        CancellationToken cancellationToken)
    {
        var command = new RevokeContentTypePermissionGrantByIdCommand
        {
            GrantId = grantId,
            TenantId = new TenantId(tenantId)
        };
        await sender.Send(command, cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Lists the content-type grant rows of one tenant, optionally narrowed to one
    ///     content type.
    /// </summary>
    /// <param name="tenantId">The tenant scope.</param>
    /// <param name="contentType">Optional content-type name filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching grant rows.</returns>
    /// <response code="200">Grants retrieved successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is reading another tenant's grants.</response>
    [HttpGet("{tenantId:guid}/grants")]
    [ProducesResponseType(typeof(GetContentTypePermissionGrantsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetGrants(
        [FromRoute] Guid tenantId,
        [FromQuery] string? contentType,
        CancellationToken cancellationToken)
    {
        var query = new GetContentTypePermissionGrantsQuery
        {
            TenantId = new TenantId(tenantId),
            ContentTypeName = contentType
        };

        var result = await sender.Send(query, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Checks whether a user holds a content-type scoped permission, resolved through
    ///     the canonical effective-permission engine (tenant → content-type → resource,
    ///     DENY-WINS, deny-by-default).
    /// </summary>
    /// <param name="tenantId">The tenant scope.</param>
    /// <param name="contentType">The content type name.</param>
    /// <param name="permission">The unqualified permission name to check (e.g. <c>Read</c>).</param>
    /// <param name="userId">Optional user id (defaults to the current user).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Boolean indicating whether the permission is granted.</returns>
    /// <response code="200">Check completed.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("{tenantId:guid}/has-permission")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> HasPermission(
        [FromRoute] Guid tenantId,
        [FromQuery] string contentType,
        [FromQuery] string permission,
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        var resolver = HttpContext.RequestServices.GetRequiredService<IDacPermissionResolver>();
        var actorAccessor = HttpContext.RequestServices.GetRequiredService<GameGuild.Identity.Context.Actors.IActorContextAccessor>();
        var actorUserId = userId ?? actorAccessor.ActorContext.SubjectIdAsGuid;
        if (actorUserId is not { } subject || subject == Guid.Empty)
        {
            return Unauthorized();
        }

        var granted = await resolver.HasPermissionAsync(
                new DacPermissionQuery
                {
                    UserId = subject,
                    TenantId = tenantId,
                    Layer = PermissionLayer.Auto,
                    ContentType = contentType
                },
                permission,
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(new { HasPermission = granted, ContentType = contentType, Permission = permission });
    }
}
