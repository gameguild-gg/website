using System.Security.Cryptography;
using System.Text;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Session management service handling user sessions and trusted devices.
/// </summary>
public sealed class SessionManagementService(
    ILogger<SessionManagementService> logger,
    IUserSessionRepository sessionRepository,
    ITrustedDeviceRepository trustedDeviceRepository,
    IRefreshTokenLineageRepository tokenLineageRepository,
    SessionOptions? sessionOptions = null,
    IAuthenticationAuditEventSink? auditEventSink = null) : ISessionManagementService
{
    private readonly SessionOptions _sessionOptions = sessionOptions ?? new SessionOptions();

    public async Task<UserSession> CreateSessionAsync(Guid userId, string ipAddress, string userAgent, string? deviceFingerprint = null, CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;

        return await CreateSessionAsync(
            Guid.NewGuid(),
            userId,
            ipAddress,
            userAgent,
            string.Empty,
            now.AddMinutes(_sessionOptions.AbsoluteTimeoutMinutes),
            deviceFingerprint,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserSession> CreateSessionAsync(
        Guid sessionId,
        Guid userId,
        string ipAddress,
        string userAgent,
        string refreshTokenHash,
        DateTime expiresAt,
        string? deviceFingerprint = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating session for user {UserId}", userId);

        var now = SystemClock.UtcNow;
        await EnforceMaxConcurrentSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

        deviceFingerprint = _sessionOptions.EnableDeviceFingerprinting
            ? deviceFingerprint ?? GenerateDeviceFingerprint(ipAddress, userAgent)
            : string.Empty;

        var session = new UserSession
        {
            Id = sessionId,
            UserId = userId,
            RefreshToken = refreshTokenHash,
            IpAddress = _sessionOptions.EnableLocationTracking ? ipAddress : "unknown",
            UserAgent = userAgent,
            DeviceFingerprint = deviceFingerprint,
            CreatedAt = now,
            UpdatedAt = now,
            LastUsedAt = now,
            ExpiresAt = CapAbsoluteExpiration(expiresAt, now),
            IsActive = true
        };

        var createdSession = await sessionRepository.CreateAsync(session, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(refreshTokenHash))
        {
            await RequireTokenBindingAsync(userId, refreshTokenHash, createdSession.Id, cancellationToken).ConfigureAwait(false);
        }
        await ForwardSessionAuditEventAsync(createdSession, "Authentication.SessionCreated", cancellationToken).ConfigureAwait(false);
        return createdSession;
    }

    public async Task<UserSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) { return await sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false); }

    public async Task<UserSession?> GetSessionByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return await sessionRepository.GetByRefreshTokenAsync(refreshToken, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<UserSession>> GetUserSessionsAsync(Guid userId, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        if (!activeOnly)
        {
            return await sessionRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        // Active listings must be expiry-aware: a row whose ExpiresAt has passed is no longer a
        // valid session even when the IsActive flag has not been lazily cleared yet.
        return await sessionRepository.GetActiveByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ValidateSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session is not { IsActive: true })
        {
            return false;
        }

        if (!IsExpired(session, SystemClock.UtcNow))
        {
            return true;
        }

        session.IsActive = false;
        session.TerminationReason = SessionTerminationReason.Expired.ToString();
        session.TerminatedAt = SystemClock.UtcNow;
        await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        return false;
    }

    public async Task<bool> RefreshSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session is not { IsActive: true })
        {
            return false;
        }

        var now = SystemClock.UtcNow;
        if (IsExpired(session, now))
        {
            await TerminateExpiredSessionAsync(session, cancellationToken).ConfigureAwait(false);
            return false;
        }

        session.LastUsedAt = now;
        session.ExpiresAt = CapAbsoluteExpiration(now.AddMinutes(_sessionOptions.AbsoluteTimeoutMinutes), session.CreatedAt);

        await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        await ForwardSessionAuditEventAsync(session, "Authentication.SessionRefreshed", cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> RefreshSessionAsync(Guid sessionId, string refreshTokenHash, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session is not { IsActive: true })
        {
            return false;
        }

        var now = SystemClock.UtcNow;
        if (IsExpired(session, now))
        {
            await TerminateExpiredSessionAsync(session, cancellationToken).ConfigureAwait(false);
            return false;
        }

        var effectiveExpiration = CapAbsoluteExpiration(expiresAt, session.CreatedAt);
        if (effectiveExpiration <= now)
        {
            await TerminateExpiredSessionAsync(session, cancellationToken).ConfigureAwait(false);
            return false;
        }

        session.RefreshToken = refreshTokenHash;
        session.LastUsedAt = now;
        session.ExpiresAt = effectiveExpiration;

        await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        await RequireTokenBindingAsync(session.UserId, refreshTokenHash, session.Id, cancellationToken).ConfigureAwait(false);

        await ForwardSessionAuditEventAsync(session, "Authentication.SessionRefreshed", cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> TerminateSessionAsync(Guid sessionId, SessionTerminationReason reason, CancellationToken cancellationToken = default)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            return false;
        }

        session.IsActive = false;
        session.TerminationReason = reason.ToString();
        session.TerminatedAt = SystemClock.UtcNow;

        await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        await ForwardSessionAuditEventAsync(session, "Authentication.SessionTerminated", cancellationToken, reason.ToString()).ConfigureAwait(false);

        logger.LogInformation("Session {SessionId} terminated. Reason: {Reason}", sessionId, reason);

        return true;
    }

    private async Task RequireTokenBindingAsync(Guid userId, string tokenHash, Guid sessionId, CancellationToken cancellationToken)
    {
        if (!await tokenLineageRepository.BindSessionAsync(userId, tokenHash, sessionId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The refresh credential could not be bound to its persisted session.");
        }
    }

    public async Task<int> TerminateAllUserSessionsAsync(Guid userId, SessionTerminationReason reason, Guid? exceptSessionId = null, CancellationToken cancellationToken = default)
    {
        var sessions = await sessionRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        var activeSessions = sessions.Where(s => s.IsActive && s.Id != exceptSessionId).ToList();

        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            session.TerminationReason = reason.ToString();
            session.TerminatedAt = SystemClock.UtcNow;
            await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
            await ForwardSessionAuditEventAsync(session, "Authentication.SessionTerminated", cancellationToken, session.TerminationReason).ConfigureAwait(false);
        }

        logger.LogInformation("Terminated {Count} sessions for user {UserId}. Reason: {Reason}", activeSessions.Count, userId, reason);

        return activeSessions.Count;
    }

    private async Task ForwardSessionAuditEventAsync(
        UserSession session,
        string actionType,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        if (auditEventSink is null)
        {
            return;
        }

        try
        {
            await auditEventSink.RecordAsync(new AuthenticationAuditEvent(
                actionType,
                session.UserId,
                true,
                "Session",
                session.IpAddress,
                session.UserAgent,
                session.Id,
                Metadata: new { session.CreatedAt, session.UpdatedAt, session.ExpiresAt, TerminationReason = reason ?? session.TerminationReason }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error forwarding session lifecycle event for user {UserId}", session.UserId);
        }
    }

    public async Task<bool> TrustDeviceAsync(Guid userId, string deviceFingerprint, string deviceName, CancellationToken cancellationToken = default)
    {
        if (!_sessionOptions.EnableDeviceFingerprinting || string.IsNullOrWhiteSpace(deviceFingerprint))
        {
            return false;
        }

        var now = SystemClock.UtcNow;
        var existingDevice = await trustedDeviceRepository.GetByUserAndFingerprintAsync(userId, deviceFingerprint, cancellationToken).ConfigureAwait(false);

        if (existingDevice != null)
        {
            var expired = existingDevice.ExpiresAt.HasValue && existingDevice.ExpiresAt.Value <= now;
            if (!existingDevice.IsActive || expired)
            {
                await EnforceMaxTrustedDevicesAsync(userId, cancellationToken).ConfigureAwait(false);
                existingDevice.IsActive = true;
            }

            existingDevice.DeviceName = deviceName;
            existingDevice.LastUsedAt = now;
            existingDevice.ExpiresAt = now.AddDays(_sessionOptions.TrustedDeviceDurationDays);
            existingDevice.UpdatedAt = now;
            await trustedDeviceRepository.UpdateAsync(existingDevice, cancellationToken).ConfigureAwait(false);

            return true;
        }

        await EnforceMaxTrustedDevicesAsync(userId, cancellationToken).ConfigureAwait(false);
        var trustedDevice = new TrustedDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceFingerprint = deviceFingerprint,
            DeviceName = deviceName,
            DeviceInfo = string.Empty,
            TrustedAt = now,
            LastUsedAt = now,
            ExpiresAt = now.AddDays(_sessionOptions.TrustedDeviceDurationDays),
            IsActive = true
        };

        await trustedDeviceRepository.CreateAsync(trustedDevice, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Device {DeviceFingerprint} trusted for user {UserId}", deviceFingerprint, userId);

        return true;
    }

    public async Task<bool> IsDeviceTrustedAsync(Guid userId, string deviceFingerprint, CancellationToken cancellationToken = default)
    {
        if (!_sessionOptions.EnableDeviceFingerprinting || string.IsNullOrWhiteSpace(deviceFingerprint))
        {
            return false;
        }

        var trustedDevice = await trustedDeviceRepository.GetByUserAndFingerprintAsync(userId, deviceFingerprint, cancellationToken).ConfigureAwait(false);

        if (trustedDevice is not { IsActive: true })
        {
            return false;
        }

        if (trustedDevice.ExpiresAt.HasValue && trustedDevice.ExpiresAt.Value < SystemClock.UtcNow)
        {
            return false;
        }

        return true;
    }

    public async Task<List<TrustedDevice>> GetTrustedDevicesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var devices = await trustedDeviceRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        return devices.Where(d => d.IsActive).ToList();
    }

    public async Task<bool> RevokeTrustedDeviceAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await trustedDeviceRepository.GetByIdAsync(deviceId, cancellationToken).ConfigureAwait(false);

        if (device == null || device.UserId != userId)
        {
            return false;
        }

        device.IsActive = false;
        device.UpdatedAt = SystemClock.UtcNow;

        await trustedDeviceRepository.UpdateAsync(device, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Trusted device {DeviceId} revoked for user {UserId}", deviceId, userId);

        return true;
    }

    private async Task EnforceMaxConcurrentSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeSessions = await sessionRepository.GetActiveByUserIdAsync(userId, cancellationToken).ConfigureAwait(false) ?? [];
        var sessionsToTerminate = activeSessions
            .OrderBy(session => session.LastUsedAt)
            .ThenBy(session => session.CreatedAt)
            .Take(Math.Max(0, activeSessions.Count - _sessionOptions.MaxConcurrentSessions + 1));

        foreach (var session in sessionsToTerminate)
        {
            session.IsActive = false;
            session.TerminationReason = SessionTerminationReason.MaxSessionsExceeded.ToString();
            session.TerminatedAt = SystemClock.UtcNow;
            await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnforceMaxTrustedDevicesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeDevices = ((await trustedDeviceRepository.GetActiveByUserIdAsync(userId, cancellationToken).ConfigureAwait(false)) ?? [])
            .OrderBy(device => device.LastUsedAt)
            .ThenBy(device => device.TrustedAt)
            .ToList();

        var devicesToRevoke = activeDevices
            .Take(Math.Max(0, activeDevices.Count - _sessionOptions.MaxTrustedDevices + 1));

        foreach (var device in devicesToRevoke)
        {
            device.IsActive = false;
            device.UpdatedAt = SystemClock.UtcNow;
            await trustedDeviceRepository.UpdateAsync(device, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool IsExpired(UserSession session, DateTime now)
    {
        return session.ExpiresAt <= now ||
               now - session.LastUsedAt >= TimeSpan.FromMinutes(_sessionOptions.IdleTimeoutMinutes) ||
               now >= session.CreatedAt.AddMinutes(_sessionOptions.AbsoluteTimeoutMinutes);
    }

    private DateTime CapAbsoluteExpiration(DateTime requestedExpiration, DateTime createdAt)
    {
        var absoluteExpiration = createdAt.AddMinutes(_sessionOptions.AbsoluteTimeoutMinutes);
        return requestedExpiration < absoluteExpiration ? requestedExpiration : absoluteExpiration;
    }

    private async Task TerminateExpiredSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        session.IsActive = false;
        session.TerminationReason = SessionTerminationReason.Expired.ToString();
        session.TerminatedAt = SystemClock.UtcNow;
        await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await ForwardSessionAuditEventAsync(session, "Authentication.SessionTerminated", cancellationToken, session.TerminationReason).ConfigureAwait(false);
    }

    public async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        await sessionRepository.DeleteExpiredAsync(SystemClock.UtcNow, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Cleaned up expired sessions");
    }

    public async Task<SessionSecurityAnalysis> AnalyzeSessionSecurityAsync(Guid userId, string ipAddress, string userAgent, CancellationToken cancellationToken = default)
    {
        var recentSessions = await sessionRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        var activeCount = recentSessions.Count(s => s.IsActive);

        var uniqueIps = recentSessions.Select(s => s.IpAddress).Distinct().Count();
        var uniqueDevices = recentSessions.Select(s => s.DeviceFingerprint).Distinct().Count();

        var riskLevel = RiskLevel.Low;

        if (uniqueIps > 10 || uniqueDevices > 5)
        {
            riskLevel = RiskLevel.Medium;
        }

        if (activeCount > 10)
        {
            riskLevel = RiskLevel.High;
        }

        return new SessionSecurityAnalysis
        {
            UserId = userId,
            ActiveSessionCount = activeCount,
            TotalDeviceCount = uniqueDevices,
            UnusualActivityDetected = riskLevel >= RiskLevel.Medium,
            RiskLevel = riskLevel,
            RiskFactors = riskLevel >= RiskLevel.Medium ? [$"Multiple IPs: {uniqueIps}", $"Multiple devices: {uniqueDevices}"] : []
        };
    }

    public async Task<List<ActivityTimelineEntry>> GetActivityTimelineAsync(Guid userId, int daysBack = 30, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting activity timeline for user {UserId} for the last {DaysBack} days", userId, daysBack);

        var timeline = new List<ActivityTimelineEntry>();
        var since = SystemClock.UtcNow.AddDays(-daysBack);

        // Get all sessions (active and inactive) for the time period
        var allSessions = await sessionRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        var relevantSessions = allSessions.Where(s => s.CreatedAt >= since).ToList();

        // Add session creation events
        foreach (var session in relevantSessions)
        {
            timeline.Add(new ActivityTimelineEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = session.CreatedAt,
                ActivityType = "SessionCreated",
                Description = $"New session started from {session.IpAddress}",
                IpAddress = session.IpAddress,
                UserAgent = session.UserAgent,
                DeviceFingerprint = session.DeviceFingerprint,
                SessionId = session.Id,
                IsSuspicious = false,
                RiskLevel = RiskLevel.Low
            });

            // Add session termination if not active
            if (!session.IsActive && session.LastUsedAt > session.CreatedAt)
            {
                timeline.Add(new ActivityTimelineEntry
                {
                    Id = Guid.NewGuid(),
                    Timestamp = session.LastUsedAt,
                    ActivityType = "SessionTerminated",
                    Description = $"Session ended",
                    IpAddress = session.IpAddress,
                    UserAgent = session.UserAgent,
                    DeviceFingerprint = session.DeviceFingerprint,
                    SessionId = session.Id,
                    IsSuspicious = false,
                    RiskLevel = RiskLevel.Low
                });
            }
        }

        // Get trusted devices added in the time period
        var trustedDevices = await trustedDeviceRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        var recentTrustedDevices = trustedDevices.Where(d => d.TrustedAt >= since).ToList();

        foreach (var device in recentTrustedDevices)
        {
            timeline.Add(new ActivityTimelineEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = device.TrustedAt,
                ActivityType = "DeviceTrusted",
                Description = $"Device '{device.DeviceName}' was marked as trusted",
                DeviceFingerprint = device.DeviceFingerprint,
                IsSuspicious = false,
                RiskLevel = RiskLevel.Low
            });
        }

        // Sort by timestamp descending (most recent first)
        return timeline.OrderByDescending(t => t.Timestamp).ToList();
    }

    private string GenerateDeviceFingerprint(string ipAddress, string userAgent)
    {
        var combined = $"{ipAddress}:{userAgent}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));

        return Convert.ToBase64String(hash);
    }
}
