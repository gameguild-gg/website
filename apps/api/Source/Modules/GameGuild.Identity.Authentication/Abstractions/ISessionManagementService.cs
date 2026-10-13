
namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service for managing user sessions and device tracking
/// </summary>
public interface ISessionManagementService
{
    Task<UserSession> CreateSessionAsync(Guid userId, string ipAddress, string userAgent, string? deviceFingerprint = null, CancellationToken cancellationToken = default);

    Task<UserSession> CreateSessionAsync(
        Guid sessionId,
        Guid userId,
        string ipAddress,
        string userAgent,
        string refreshTokenHash,
        DateTime expiresAt,
        string? deviceFingerprint = null,
        CancellationToken cancellationToken = default);

    Task<UserSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<UserSession?> GetSessionByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<List<UserSession>> GetUserSessionsAsync(Guid userId, bool activeOnly = true, CancellationToken cancellationToken = default);

    Task<bool> ValidateSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<bool> RefreshSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<bool> RefreshSessionAsync(Guid sessionId, string refreshTokenHash, DateTime expiresAt, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Names or renames one of the user's own active sessions. Returns false when the name
    ///     is empty/oversized, the session belongs to another user, or it is no longer active.
    /// </summary>
    Task<bool> RenameSessionAsync(Guid userId, Guid sessionId, string name, CancellationToken cancellationToken = default);

    Task<bool> TerminateSessionAsync(Guid sessionId, SessionTerminationReason reason, CancellationToken cancellationToken = default);

    Task<int> TerminateAllUserSessionsAsync(Guid userId, SessionTerminationReason reason, Guid? exceptSessionId = null, CancellationToken cancellationToken = default);

    Task<bool> TrustDeviceAsync(Guid userId, string deviceFingerprint, string deviceName, CancellationToken cancellationToken = default);

    Task<bool> IsDeviceTrustedAsync(Guid userId, string deviceFingerprint, CancellationToken cancellationToken = default);

    Task<List<TrustedDevice>> GetTrustedDevicesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> RevokeTrustedDeviceAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);

    Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default);

    Task<SessionSecurityAnalysis> AnalyzeSessionSecurityAsync(Guid userId, string ipAddress, string userAgent, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the activity timeline for a user showing session events over time.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="daysBack">Number of days to look back (default 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of activity timeline entries</returns>
    Task<List<ActivityTimelineEntry>> GetActivityTimelineAsync(Guid userId, int daysBack = 30, CancellationToken cancellationToken = default);
}
