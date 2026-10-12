using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     JWT-administered management of SCIM provisioning tokens (issue / list / rotate /
///     revoke). Guarded by the <c>Provisioning.ManageTokens</c> policy; every mutation
///     is audited and bumps the tenant security version.
/// </summary>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("auth/scim-provisioning")]
[Authorize]
public sealed class ScimProvisioningTokensAdminController(ISender dispatcher) : BaseApiController
{
    /// <summary>Issues a new tenant-scoped provisioning token. The plaintext is returned once.</summary>
    [HttpPost("v{version:apiVersion}/auth/scim-provisioning-tokens")]
    [Authorize(Policy = Policies.ProvisioningManageTokens)]
    [EndpointSummary("Issue a SCIM provisioning token")]
    [ProducesResponseType(typeof(CreateScimProvisioningTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateScimProvisioningTokenRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateScimProvisioningTokenCommand(request.Name, request.Scopes, request.ExpiresAt);
        var result = await dispatcher.Send(command, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Lists the tenant's provisioning tokens (no plaintext).</summary>
    [HttpGet("v{version:apiVersion}/auth/scim-provisioning-tokens")]
    [Authorize(Policy = Policies.ProvisioningManageTokens)]
    [EndpointSummary("List SCIM provisioning tokens")]
    [ProducesResponseType(typeof(List<ScimProvisioningTokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ListScimProvisioningTokensQuery(), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Rotates a provisioning token; the old token stays valid for the grace window.</summary>
    [HttpPost("v{version:apiVersion}/auth/scim-provisioning-tokens/{tokenId:guid}:rotate")]
    [Authorize(Policy = Policies.ProvisioningManageTokens)]
    [EndpointSummary("Rotate a SCIM provisioning token")]
    [ProducesResponseType(typeof(RotateScimProvisioningTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rotate(Guid tokenId, [FromBody] RotateScimProvisioningTokenRequest? request, CancellationToken cancellationToken)
    {
        var command = new RotateScimProvisioningTokenCommand(
            tokenId,
            request?.Name,
            request?.Scopes,
            request?.ExpiresAt,
            request?.GracePeriodMinutes);
        var result = await dispatcher.Send(command, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Revokes a provisioning token immediately.</summary>
    [HttpPost("v{version:apiVersion}/auth/scim-provisioning-tokens/{tokenId:guid}:revoke")]
    [Authorize(Policy = Policies.ProvisioningManageTokens)]
    [EndpointSummary("Revoke a SCIM provisioning token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid tokenId, [FromBody] RevokeScimProvisioningTokenRequest? request, CancellationToken cancellationToken)
    {
        var command = new RevokeScimProvisioningTokenCommand(tokenId, request?.Reason);
        var result = await dispatcher.Send(command, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(new { message = "SCIM provisioning token revoked" })
            : BadRequest(new { error = result.Error });
    }
}

public sealed record CreateScimProvisioningTokenRequest
{
    public required string Name { get; init; }

    /// <summary>Token scopes; defaults to scim:read + scim:write.</summary>
    public string[]? Scopes { get; init; }

    public DateTime? ExpiresAt { get; init; }
}

public sealed record RotateScimProvisioningTokenRequest
{
    public string? Name { get; init; }

    public string[]? Scopes { get; init; }

    public DateTime? ExpiresAt { get; init; }

    /// <summary>Overlap window in minutes; defaults to the configured rotation grace period. Zero revokes immediately.</summary>
    public int? GracePeriodMinutes { get; init; }
}

public sealed record RevokeScimProvisioningTokenRequest
{
    public string? Reason { get; init; }
}
