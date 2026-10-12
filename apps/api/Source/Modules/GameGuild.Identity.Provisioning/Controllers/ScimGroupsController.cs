using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Provisioning.Scim;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     RFC 7644 §3.3 SCIM Groups endpoint backed by tenant roles; members map to role
///     assignments. Authorized exclusively with tenant-scoped provisioning tokens.
/// </summary>
[ApiVersionNeutral]
[Microsoft.AspNetCore.Http.Tags("scim")]
[Authorize(AuthenticationSchemes = ScimProvisioningAuthenticationOptions.SchemeName)]
[TypeFilter(typeof(ScimExceptionFilter))]
public sealed class ScimGroupsController(ISender dispatcher) : BaseApiController
{
    /// <summary>Lists provisioned groups with optional filter and 1-based pagination.</summary>
    [HttpGet("scim/v2/Groups")]
    [ProducesResponseType(typeof(ScimListResponse<ScimGroupResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery(Name = "filter")] string? filter,
        [FromQuery(Name = "startIndex")] string? startIndex,
        [FromQuery(Name = "count")] string? count,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimListGroupsQuery(filter, startIndex, count), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : ScimBadRequest(result.Error);
    }

    /// <summary>Creates a group. Idempotent on externalId (repeat POST returns 200).</summary>
    [HttpPost("scim/v2/Groups")]
    [ProducesResponseType(typeof(ScimGroupResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ScimGroupResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ScimGroupRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimCreateGroupCommand(request), cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return ScimBadRequest(result.Error);
        }

        return result.Value.Created
            ? StatusCode(StatusCodes.Status201Created, result.Value.Resource)
            : Ok(result.Value.Resource);
    }

    /// <summary>Fetches one provisioned group with its members.</summary>
    [HttpGet("scim/v2/Groups/{roleId:guid}")]
    [ProducesResponseType(typeof(ScimGroupResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimGetGroupQuery(roleId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : ScimNotFound(result.Error);
    }

    /// <summary>Replaces a group, including its full member list (RFC 7644 §3.5.1).</summary>
    [HttpPut("scim/v2/Groups/{roleId:guid}")]
    [ProducesResponseType(typeof(ScimGroupResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Replace(Guid roleId, [FromBody] ScimGroupRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimReplaceGroupCommand(roleId, request), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : ScimNotFound(result.Error);
    }

    /// <summary>
    ///     Patches a group: displayName, externalId and the members paths
    ///     (<c>members</c>, <c>members[value eq "…"]</c>). Membership changes are
    ///     audited and bump the tenant security version.
    /// </summary>
    [HttpPatch("scim/v2/Groups/{roleId:guid}")]
    [ProducesResponseType(typeof(ScimGroupResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(Guid roleId, [FromBody] ScimPatchRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimPatchGroupCommand(roleId, request), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : ScimNotFound(result.Error);
    }

    /// <summary>Deletes a group: memberships are removed and the backing role deactivated.</summary>
    [HttpDelete("scim/v2/Groups/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid roleId, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimDeleteGroupCommand(roleId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : ScimNotFound(result.Error);
    }

    private IActionResult ScimBadRequest(GameGuild.Error error)
        => new ObjectResult(new ScimErrorBody([ScimConstants.ErrorSchema], 400, null, error.Description)) { StatusCode = 400 };

    private IActionResult ScimNotFound(GameGuild.Error error)
        => new ObjectResult(new ScimErrorBody([ScimConstants.ErrorSchema], 404, null, error.Description)) { StatusCode = 404 };
}
