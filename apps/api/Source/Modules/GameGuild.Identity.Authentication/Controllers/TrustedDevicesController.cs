using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Trusted Devices API Controller - RESTful API for managing trusted devices
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/auth/trusted-devices")]
[Microsoft.AspNetCore.Http.Tags("auth/trusted-devices")]
[Authorize]
public sealed class TrustedDevicesController(ISessionManagementService sessionService, ISender sender) : AuthControllerBase
{
    /// <summary>
    ///     Get trusted devices for the current user
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of trusted devices</returns>
    [HttpGet]
    [EndpointSummary("Get trusted devices")]
    [EndpointDescription("Retrieves a list of devices that have been marked as trusted for the current user.")]
    [ProducesResponseType<List<TrustedDeviceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTrustedDevices(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var devices = await sessionService.GetTrustedDevicesAsync(userId).ConfigureAwait(false);

        var response = devices.Select(d => new TrustedDeviceResponse
        {
            Id = d.Id,
            DeviceName = d.DeviceName,
            DeviceInfo = ParseDeviceInfo(d.DeviceInfo),
            TrustedAt = d.TrustedAt,
            LastUsedAt = d.LastUsedAt,
            ExpiresAt = d.ExpiresAt
        })
            .ToList();

        return Ok(response);
    }

    /// <summary>
    ///     Trust the current device
    /// </summary>
    /// <param name="body">Device trust request with optional device name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpPost]
    [EndpointSummary("Trust current device")]
    [EndpointDescription("Marks the current device as trusted, allowing faster authentication in the future.")]
    [ProducesResponseType<SessionSuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TrustCurrentDevice([FromBody] TrustDeviceRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var userId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        var deviceFingerprint = GenerateDeviceFingerprint(ipAddress, userAgent);
        var success = await sender.Send(
            new TrustDeviceCommand(userId, deviceFingerprint, body.DeviceName), ct).ConfigureAwait(false);

        if (!success)
        {
            return BadRequest(new SessionErrorResponse { Error = "Failed to trust device" });
        }

        return Ok(new SessionSuccessResponse { Message = "Device trusted successfully" });
    }

    /// <summary>
    ///     Revoke trust for a specific device
    /// </summary>
    /// <param name="deviceId">Device identifier to revoke trust for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpDelete("{deviceId:guid}")]
    [EndpointSummary("Revoke device trust")]
    [EndpointDescription("Removes a device from the trusted devices list.")]
    [ProducesResponseType<SessionSuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeTrustedDevice(Guid deviceId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var success = await sender.Send(new RevokeTrustedDeviceCommand(userId, deviceId), ct).ConfigureAwait(false);

        if (!success)
        {
            return NotFound(new SessionErrorResponse { Error = "Trusted device not found" });
        }

        return Ok(new SessionSuccessResponse { Message = "Device trust revoked successfully" });
    }

    #region Private Helpers

    private static DeviceInfo? ParseDeviceInfo(string? deviceInfoJson)
    {
        if (string.IsNullOrEmpty(deviceInfoJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DeviceInfo>(deviceInfoJson);
        }
        catch
        {
            return null;
        }
    }

    private static string GenerateDeviceFingerprint(string ipAddress, string userAgent)
    {
        using var sha256 = SHA256.Create();
        var input = $"{ipAddress}:{userAgent}";
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }

    #endregion
}
