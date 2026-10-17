using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Sessions API Controller - RESTful API for session and device management
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("auth/sessions")]
[Authorize]
public sealed class SessionController(ISessionManagementService sessionService, ISender sender) : AuthControllerBase
{
    #region Collection Operations - /v1/auth/sessions

    /// <summary>
    ///     Get current user's active sessions
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of active sessions with device and location information</returns>
    [HttpGet("v{version:apiVersion}/auth/sessions")]
    [EndpointSummary("Get active sessions")]
    [EndpointDescription("Retrieves a list of all active sessions for the current user, including device and location information.")]
    [ProducesResponseType<List<SessionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var sessions = await sessionService.GetUserSessionsAsync(userId).ConfigureAwait(false);

        var response = sessions.Select(s => new SessionResponse
        {
            Id = s.Id,
            DeviceInfo = ParseDeviceInfo(s.DeviceInfo),
            Location = ParseLocation(s.Location),
            IpAddress = s.IpAddress,
            CreatedAt = s.CreatedAt,
            LastUsedAt = s.LastUsedAt,
            ExpiresAt = s.ExpiresAt,
            IsTrustedDevice = s.IsTrustedDevice,
            IsCurrent = IsCurrentSession(s.Id)
        })
            .ToList();

        return Ok(response);
    }

    #endregion

    #region Security Analysis - /v1/auth/sessions:analyze-security

    /// <summary>
    ///     Analyze session security
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Security analysis with risk assessment and recommendations</returns>
    [HttpGet("v{version:apiVersion}/auth/sessions:analyze-security")]
    [EndpointSummary("Analyze session security")]
    [EndpointDescription("Analyzes the current session for security risks and provides recommendations.")]
    [ProducesResponseType<SessionSecurityAnalysis>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AnalyzeSessionSecurity(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        SessionSecurityAnalysis analysis = await sessionService.AnalyzeSessionSecurityAsync(userId, ipAddress, userAgent).ConfigureAwait(false);

        return Ok(analysis);
    }

    #endregion

    #region Single Session Operations - /v1/auth/sessions/{sessionId}

    /// <summary>
    ///     Terminate a specific session
    /// </summary>
    /// <param name="sessionId">Session identifier to terminate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpDelete("v{version:apiVersion}/auth/sessions/{sessionId:guid}")]
    [EndpointSummary("Terminate a session")]
    [EndpointDescription("Terminates a specific session by its identifier. The session must belong to the current user.")]
    [ProducesResponseType<SessionSuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TerminateSession(Guid sessionId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var session = await sessionService.GetSessionAsync(sessionId).ConfigureAwait(false);

        if (session == null || session.UserId != userId)
        {
            return NotFound(new SessionErrorResponse { Error = "Session not found" });
        }

        var success = await sender.Send(
            new TerminateSessionCommand(sessionId, SessionTerminationReason.UserLogout), ct).ConfigureAwait(false);

        if (!success)
        {
            return BadRequest(new SessionErrorResponse { Error = "Failed to terminate session" });
        }

        return Ok(new SessionSuccessResponse { Message = "Session terminated successfully" });
    }

    /// <summary>
    ///     Name or rename one of the current user's own sessions
    /// </summary>
    /// <param name="sessionId">Session identifier to name</param>
    /// <param name="body">Rename request carrying the new session name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpPatch("v{version:apiVersion}/auth/sessions/{sessionId:guid}/name")]
    [EndpointSummary("Name or rename a session")]
    [EndpointDescription("Assigns a user-defined name to one of the current user's own active sessions. The acting user is taken from the authenticated principal, never from the body.")]
    [ProducesResponseType<SessionSuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SessionRenameErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RenameSession(Guid sessionId, [FromBody] RenameSessionRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var userId = GetCurrentUserId();

        var name = body.Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > SessionManagementService.MaxSessionNameLength)
        {
            return BadRequest(new SessionRenameErrorResponse
            {
                Error = $"Session name must be between 1 and {SessionManagementService.MaxSessionNameLength} characters"
            });
        }

        var success = await sender.Send(new RenameSessionCommand(userId, sessionId, name), ct).ConfigureAwait(false);

        if (!success)
        {
            return NotFound(new SessionErrorResponse { Error = "Session not found" });
        }

        return Ok(new SessionSuccessResponse { Message = "Session renamed successfully" });
    }

    #endregion

    #region Bulk Operations - /v1/auth/sessions:action

    /// <summary>
    ///     Terminate all sessions except the current one
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of terminated sessions</returns>
    [HttpPost("v{version:apiVersion}/auth/sessions:terminate-others")]
    [EndpointSummary("Terminate other sessions")]
    [EndpointDescription("Terminates all active sessions except the current one.")]
    [ProducesResponseType<SessionTerminationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TerminateOtherSessions(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var currentSessionId = GetCurrentSessionId();

        var terminatedCount = await sender.Send(
            new TerminateUserSessionsCommand(userId, SessionTerminationReason.UserLogout, currentSessionId), ct).ConfigureAwait(false);

        return Ok(new SessionTerminationResponse
        {
            Message = "Other sessions terminated successfully",
            TerminatedCount = terminatedCount
        });
    }

    /// <summary>
    ///     Terminate all sessions (including current)
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of terminated sessions</returns>
    [HttpPost("v{version:apiVersion}/auth/sessions:terminate-all")]
    [EndpointSummary("Terminate all sessions")]
    [EndpointDescription("Terminates all active sessions including the current one. User will need to sign in again.")]
    [ProducesResponseType<SessionTerminationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TerminateAllSessions(CancellationToken ct)
    {
        var terminatedCount = await sender.Send(
            new RevokeAllUserTokensCommand(HttpContext.Connection.RemoteIpAddress?.ToString()), ct).ConfigureAwait(false);

        return Ok(new SessionTerminationResponse
        {
            Message = "All sessions terminated successfully",
            TerminatedCount = terminatedCount
        });
    }

    /// <summary>
    ///     Refresh the current session
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpPost("v{version:apiVersion}/auth/sessions:refresh")]
    [EndpointSummary("Refresh current session")]
    [EndpointDescription("Extends the current session's expiration time.")]
    [ProducesResponseType<SessionSuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshSession(CancellationToken ct)
    {
        var currentSessionId = GetCurrentSessionId();

        if (currentSessionId == null)
        {
            return BadRequest(new SessionErrorResponse { Error = "No active session found" });
        }

        var success = await sender.Send(new RefreshUserSessionCommand(currentSessionId.Value), ct).ConfigureAwait(false);

        if (!success)
        {
            return BadRequest(new SessionErrorResponse { Error = "Failed to refresh session" });
        }

        return Ok(new SessionSuccessResponse { Message = "Session refreshed successfully" });
    }

    #endregion

    #region Private Helpers

    private Guid? GetCurrentSessionId()
    {
        var sessionIdClaim = User.FindFirst("session_id")?.Value;

        if (string.IsNullOrEmpty(sessionIdClaim) || !Guid.TryParse(sessionIdClaim, out var sessionId))
        {
            return null;
        }

        return sessionId;
    }

    private bool IsCurrentSession(Guid sessionId)
    {
        var currentSessionId = GetCurrentSessionId();
        return currentSessionId == sessionId;
    }

    private static DeviceInfo? ParseDeviceInfo(string? deviceInfoJson)
    {
        return SessionEnrichmentJson.DeserializeDeviceInfo(deviceInfoJson);
    }

    private static LocationInfo? ParseLocation(string? locationJson)
    {
        return SessionEnrichmentJson.DeserializeLocation(locationJson);
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

/// <summary>
///     Success response for session operations
/// </summary>
public sealed record SessionSuccessResponse
{
    /// <summary>
    ///     Success message
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
///     Error response for session operations
/// </summary>
public sealed record SessionErrorResponse
{
    /// <summary>
    ///     Error message
    /// </summary>
    public required string Error { get; init; }
}

/// <summary>
///     Response for session termination operations
/// </summary>
public sealed record SessionTerminationResponse
{
    /// <summary>
    ///     Success message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///     Number of sessions terminated
    /// </summary>
    public required int TerminatedCount { get; init; }
}

/// <summary>
///     Request to name or rename one of the current user's own sessions
/// </summary>
public sealed record RenameSessionRequest
{
    /// <summary>
    ///     User-defined session name (1–64 characters, trimmed)
    /// </summary>
    public required string Name { get; init; }
}

/// <summary>
///     Error response for session rename validation failures
/// </summary>
public sealed record SessionRenameErrorResponse
{
    /// <summary>
    ///     Error message
    /// </summary>
    public required string Error { get; init; }
}
