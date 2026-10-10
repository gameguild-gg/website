using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Provisioning.Scim;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     RFC 7644 §3.3 SCIM Users endpoint. Authorized exclusively with tenant-scoped
///     provisioning tokens; the tenant always comes from the token, never the request.
/// </summary>
[ApiVersionNeutral]
[Microsoft.AspNetCore.Http.Tags("scim")]
[Authorize(AuthenticationSchemes = ScimProvisioningAuthenticationOptions.SchemeName)]
[TypeFilter(typeof(ScimExceptionFilter))]
public sealed class ScimUsersController(ISender dispatcher) : BaseApiController
{
    /// <summary>Lists provisioned users with optional filter and 1-based pagination.</summary>
    [HttpGet("scim/v2/Users")]
    [ProducesResponseType(typeof(ScimListResponse<ScimUserResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery(Name = "filter")] string? filter,
        [FromQuery(Name = "startIndex")] string? startIndex,
        [FromQuery(Name = "count")] string? count,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimListUsersQuery(filter, startIndex, count), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : ScimBadRequest(result.Error);
    }

    /// <summary>
    ///     Creates a user. Idempotent on externalId: a repeated POST with the same
    ///     externalId returns the existing resource with 200 instead of creating a copy.
    /// </summary>
    [HttpPost("scim/v2/Users")]
    [ProducesResponseType(typeof(ScimUserResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ScimUserResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ScimUserRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimCreateUserCommand(request), cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return ScimBadRequest(result.Error);
        }

        return result.Value.Created
            ? StatusCode(StatusCodes.Status201Created, result.Value.Resource)
            : Ok(result.Value.Resource);
    }

    /// <summary>Fetches one provisioned user by id.</summary>
    [HttpGet("scim/v2/Users/{userId:guid}")]
    [ProducesResponseType(typeof(ScimUserResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimGetUserQuery(userId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : NotFoundScim(result.Error);
    }

    /// <summary>Replaces a provisioned user (RFC 7644 §3.5.1).</summary>
    [HttpPut("scim/v2/Users/{userId:guid}")]
    [ProducesResponseType(typeof(ScimUserResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Replace(Guid userId, [FromBody] ScimUserRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimReplaceUserCommand(userId, request), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : NotFoundScim(result.Error);
    }

    /// <summary>Patches a provisioned user (RFC 7644 §3.5.2 add/remove/replace).</summary>
    [HttpPatch("scim/v2/Users/{userId:guid}")]
    [ProducesResponseType(typeof(ScimUserResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(Guid userId, [FromBody] ScimPatchRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimPatchUserCommand(userId, request), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : NotFoundScim(result.Error);
    }

    /// <summary>
    ///     Deprovisions a user: soft delete plus immediate session and token revocation.
    /// </summary>
    [HttpDelete("scim/v2/Users/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimDeleteUserCommand(userId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : NotFoundScim(result.Error);
    }

    private IActionResult ScimBadRequest(GameGuild.Error error)
        => new ObjectResult(new ScimErrorBody([ScimConstants.ErrorSchema], 400, null, error.Description)) { StatusCode = 400 };

    private IActionResult NotFoundScim(GameGuild.Error error)
        => new ObjectResult(new ScimErrorBody([ScimConstants.ErrorSchema], 404, null, error.Description)) { StatusCode = 404 };
}
