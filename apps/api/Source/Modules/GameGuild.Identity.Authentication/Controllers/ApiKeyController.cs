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
    [Authorize(Policy = ApiKeyScopePolicies.Prefix + ApiKeyScopes.ManageApiKeys)]
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
    [Authorize(Policy = ApiKeyScopePolicies.Prefix + ApiKeyScopes.ManageApiKeys)]
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
    ///     Rotate an API key: issues a replacement key and starts the old key's
    ///     overlap (grace) window, after which the old key is revoked.
    /// </summary>
    [HttpPost("v{version:apiVersion}/auth/api-keys/{keyId}:rotate")]
    [Authorize(Policy = ApiKeyScopePolicies.Prefix + ApiKeyScopes.ManageApiKeys)]
    [EndpointSummary("Rotate an API key")]
    [ProducesResponseType(typeof(RotateApiKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RotateApiKey(
        Guid keyId,
        [FromBody] RotateApiKeyRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new RotateApiKeyCommand
        {
            KeyId = keyId,
            Name = request?.Name,
            Scopes = request?.Scopes,
            ExpiresAt = request?.ExpiresAt,
            GracePeriod = request?.GracePeriodMinutes is { } graceMinutes ? TimeSpan.FromMinutes(graceMinutes) : null
        };

        var result = await _dispatcher.Send(command, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    ///     Revoke an API key
    /// </summary>
    [HttpPost("v{version:apiVersion}/auth/api-keys/{keyId}:revoke")]
    [Authorize(Policy = ApiKeyScopePolicies.Prefix + ApiKeyScopes.ManageApiKeys)]
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

public sealed record RotateApiKeyRequest
{
    /// <summary>
    ///     Optional display name for the replacement key; defaults to the old key's name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     Optional scopes for the replacement key; defaults to the old key's scopes.
    /// </summary>
    public string[]? Scopes { get; init; }

    /// <summary>
    ///     Optional absolute expiry for the replacement key; defaults to preserving the old key's expiry.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    ///     Optional overlap window, in minutes, during which the old key keeps working;
    ///     defaults to the configured rotation grace period. Zero revokes the old key immediately.
    /// </summary>
    public int? GracePeriodMinutes { get; init; }
}
