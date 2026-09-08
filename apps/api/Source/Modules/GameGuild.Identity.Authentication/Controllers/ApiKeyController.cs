using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Controller for API key management
/// </summary>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("auth/api-keys")]
[Authorize]
public class ApiKeyController : BaseApiController
{
    private readonly ISender _dispatcher;

    public ApiKeyController(ISender dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    ///     Create a new API key
    /// </summary>
    [HttpPost("v{version:apiVersion}/auth/api-keys")]
    [EndpointSummary("Create a new API key")]
    [ProducesResponseType(typeof(CreateApiKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateApiKey(
        [FromBody] CreateApiKeyCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(command, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    ///     List all API keys for the current user
    /// </summary>
    [HttpGet("v{version:apiVersion}/auth/api-keys")]
    [EndpointSummary("List all API keys")]
    [ProducesResponseType(typeof(List<ApiKeyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListApiKeys(CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ListApiKeysQuery(), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    ///     Revoke an API key
    /// </summary>
    [HttpPost("v{version:apiVersion}/auth/api-keys/{keyId}:revoke")]
    [EndpointSummary("Revoke an API key")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeApiKey(
        Guid keyId,
        [FromBody] RevokeApiKeyRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new RevokeApiKeyCommand
        {
            KeyId = keyId,
            Reason = request?.Reason
        };

        var result = await _dispatcher.Send(command, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(new { message = "API key revoked successfully" })
            : BadRequest(new { error = result.Error });
    }
}

public sealed record RevokeApiKeyRequest
{
    public string? Reason { get; init; }
}
