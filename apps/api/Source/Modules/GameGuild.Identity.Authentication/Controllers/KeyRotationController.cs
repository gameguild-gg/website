using Asp.Versioning;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Controller for JWT signing key rotation management
/// </summary>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("auth/signing-keys")]
[Authorize(Policy = Policies.SystemAdmin)]
public class KeyRotationController : BaseApiController
{
    private readonly IKeyRotationService _keyRotationService;
    private readonly ILogger<KeyRotationController> _logger;
    private readonly ISender _sender;

    public KeyRotationController(
        IKeyRotationService keyRotationService,
        ILogger<KeyRotationController> logger,
        ISender sender)
    {
        _keyRotationService = keyRotationService;
        _logger = logger;
        _sender = sender;
    }

    /// <summary>
    ///     Get signing keys with optional status filter
    /// </summary>
    /// <param name="status">Filter by status: 'active', 'valid', or null for all</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("v{version:apiVersion}/auth/signing-keys")]
    [EndpointSummary("Get signing keys")]
    [EndpointDescription("Retrieves signing keys with optional status filtering. Use status=active for current signing key, status=valid for all keys usable for validation.")]
    [ProducesResponseType(typeof(List<JwtKeyInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JwtKeyInfoDto>>> GetSigningKeys([FromQuery] string? status, CancellationToken cancellationToken)
    {
        if (status?.ToLowerInvariant() == "active")
        {
            var activeKey = await _keyRotationService.GetActiveSigningKeyAsync(cancellationToken).ConfigureAwait(false);
            if (activeKey == null)
                return Ok(new List<JwtKeyInfoDto>());
            return Ok(new List<JwtKeyInfoDto> { JwtKeyInfoDto.FromEntity(activeKey) });
        }

        if (status?.ToLowerInvariant() == "valid")
        {
            var validKeys = await _keyRotationService.GetValidationKeysAsync(cancellationToken).ConfigureAwait(false);
            return Ok(validKeys.Select(JwtKeyInfoDto.FromEntity).ToList());
        }

        // Return all keys (active + valid)
        var keys = await _keyRotationService.GetValidationKeysAsync(cancellationToken).ConfigureAwait(false);
        return Ok(keys.Select(JwtKeyInfoDto.FromEntity).ToList());
    }

    /// <summary>
    ///     Manually rotate to a new signing key
    /// </summary>
    [HttpPost("v{version:apiVersion}/auth/signing-keys:rotate")]
    [EndpointSummary("Rotate signing key")]
    [EndpointDescription("Manually rotates to a new signing key. Previous keys remain valid for token validation during grace period.")]
    [ProducesResponseType(typeof(JwtKeyInfoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JwtKeyInfoDto>> RotateKey(
        [FromBody] RotateKeyRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Manual key rotation requested by {User}. Reason: {Reason}",
            User.Identity?.Name, request.Reason);

        var newKey = await _sender.Send(new RotateSigningKeyCommand(
            request.Reason ?? "manual-rotation",
            request.ValidityDays ?? 90), cancellationToken).ConfigureAwait(false);

        return Ok(JwtKeyInfoDto.FromEntity(newKey));
    }

    /// <summary>
    ///     Clean up expired keys
    /// </summary>
    [HttpPost("v{version:apiVersion}/auth/signing-keys:cleanup")]
    [EndpointSummary("Cleanup expired keys")]
    [EndpointDescription("Removes signing keys that have been expired beyond the retention period.")]
    [ProducesResponseType(typeof(CleanupResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<CleanupResult>> CleanupExpiredKeys(
        [FromBody] CleanupKeysRequest? request,
        CancellationToken cancellationToken)
    {
        var count = await _sender.Send(
            new CleanupExpiredSigningKeysCommand(request?.RetentionDays ?? 30),
            cancellationToken).ConfigureAwait(false);

        return Ok(new CleanupResult { DeletedCount = count });
    }
}

/// <summary>
///     DTO for JWT signing key information (without exposing key material)
/// </summary>
public sealed record JwtKeyInfoDto
{
    public string KeyId { get; init; } = string.Empty;
    public string Algorithm { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime ValidFrom { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? RotatedAt { get; init; }
    public string? RotationReason { get; init; }
    public int KeyVersion { get; init; }

    public static JwtKeyInfoDto FromEntity(JwtSigningKey key)
    {
        return new JwtKeyInfoDto
        {
            KeyId = key.KeyId,
            Algorithm = key.Algorithm,
            IsActive = key.IsActive,
            ValidFrom = key.ValidFrom,
            ExpiresAt = key.ExpiresAt,
            RotatedAt = key.RotatedAt,
            RotationReason = key.RotationReason,
            KeyVersion = key.KeyVersion
        };
    }
}

/// <summary>
///     Request to manually rotate signing key
/// </summary>
public sealed record RotateKeyRequest
{
    public string? Reason { get; init; }
    public int? ValidityDays { get; init; }
}

/// <summary>
///     Request to cleanup expired keys
/// </summary>
public sealed record CleanupKeysRequest
{
    public int? RetentionDays { get; init; }
}

/// <summary>
///     Result of cleanup operation
/// </summary>
public sealed record CleanupResult
{
    public int DeletedCount { get; init; }
}
