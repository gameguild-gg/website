using System.Globalization;
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
    IAuthenticationAuditEventSink? auditEventSink = null,
    IUserAgentParser? userAgentParser = null,
    IGeolocationProvider? geolocationProvider = null) : ISessionManagementService
{
    private readonly SessionOptions _sessionOptions = sessionOptions ?? new SessionOptions();
    private readonly IUserAgentParser _userAgentParser = userAgentParser ?? UserAgentParser.Shared;
    private readonly IGeolocationProvider? _geolocationProvider = geolocationProvider;

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
            DeviceInfo = BuildDeviceInfo(deviceFingerprint, ipAddress, userAgent),
            Location = await ResolveSessionLocationAsync(ipAddress, cancellationToken).ConfigureAwait(false),
            CreatedAt = now,
            UpdatedAt = now,
            LastUsedAt = now,
            ExpiresAt = CapAbsoluteExpiration(expiresAt, now),
            IsActive = true
        };

        var createdSession = await sessionRepository.CreateAsync(session, cancellationToken).ConfigureAwait(false);
        SessionAnalyticsMetrics.RecordCreated();
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
        SessionAnalyticsMetrics.RecordTerminated(SessionTerminationReason.Expired);

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

    public async Task<bool> RefreshSessionAsync(Guid sessionId, string refreshTokenHash, DateTime expiresAt, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default)
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
        await ApplyRefreshEnrichmentAsync(session, ipAddress, userAgent, cancellationToken).ConfigureAwait(false);

        await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        await RequireTokenBindingAsync(session.UserId, refreshTokenHash, session.Id, cancellationToken).ConfigureAwait(false);

        await ForwardSessionAuditEventAsync(session, "Authentication.SessionRefreshed", cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    ///     Re-enriches device metadata (and, when the caller's IP changed, the location) during
    ///     token rotation. The device name is preserved: it is either the original parsed label
    ///     or a user-assigned custom name, and rotation must not reset either.
    /// </summary>
    private async Task ApplyRefreshEnrichmentAsync(UserSession session, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var agentChanged = !string.IsNullOrWhiteSpace(userAgent) &&
                           !string.Equals(session.UserAgent, userAgent, StringComparison.Ordinal);
        var effectiveIpAddress = string.IsNullOrWhiteSpace(ipAddress) ? session.IpAddress : ipAddress;
        var locationChanged = _sessionOptions.EnableLocationTracking &&
                              !string.IsNullOrWhiteSpace(ipAddress) &&
                              !string.Equals(session.IpAddress, ipAddress, StringComparison.Ordinal);

        if (agentChanged)
        {
            session.UserAgent = userAgent!;
            var parsed = _userAgentParser.Parse(userAgent);
            var existing = SessionEnrichmentJson.DeserializeDeviceInfo(session.DeviceInfo) ?? new DeviceInfo();
            session.DeviceInfo = SessionEnrichmentJson.SerializeDeviceInfo(new DeviceInfo
            {
                // Preserved members: the fingerprint is stable per device and the name is user-managed.
                Fingerprint = existing.Fingerprint,
                DeviceName = existing.DeviceName,
                IpAddress = effectiveIpAddress,
                DeviceType = parsed.DeviceType,
                OperatingSystem = parsed.OperatingSystem,
                OsVersion = parsed.OsVersion,
                Browser = parsed.Browser,
                BrowserVersion = parsed.BrowserVersion,
                UserAgent = parsed.UserAgent,
                IsMobile = parsed.IsMobile,
                IsBot = parsed.IsBot
            });
        }

        if (locationChanged || (agentChanged && string.IsNullOrWhiteSpace(session.Location)))
        {
            session.IpAddress = effectiveIpAddress;
            if (_sessionOptions.EnableLocationTracking)
            {
                var resolved = await ResolveSessionLocationAsync(effectiveIpAddress, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    session.Location = resolved;
                }
            }
        }
    }

    /// <summary>Builds the persisted device metadata JSON from the server-side user-agent parse.</summary>
    private string? BuildDeviceInfo(string? deviceFingerprint, string ipAddress, string userAgent)
    {
        var parsed = _userAgentParser.Parse(userAgent);
        if (!string.IsNullOrWhiteSpace(deviceFingerprint))
        {
            parsed.Fingerprint = deviceFingerprint;
        }
        parsed.IpAddress = ipAddress;
        return SessionEnrichmentJson.SerializeDeviceInfo(parsed);
    }

    /// <summary>
    ///     Resolves the coarse location for a session IP through the registered geolocation
    ///     provider. Fail-open: enrichment must never block session persistence.
    /// </summary>
    private async Task<string?> ResolveSessionLocationAsync(string ipAddress, CancellationToken cancellationToken)
    {
        if (!_sessionOptions.EnableLocationTracking || _geolocationProvider is null || string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        try
        {
            var resolved = await _geolocationProvider.ResolveAsync(ipAddress, cancellationToken).ConfigureAwait(false);
            return SessionEnrichmentJson.SerializeLocation(SessionEnrichmentJson.ToLocationInfo(resolved, ipAddress));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Geolocation enrichment failed for a session IP; continuing without location");
            return null;
        }
    }

    public async Task<bool> TerminateSessionAsync(Guid sessionId, SessionTerminationReason reason, CancellationToken cancellationToken = default)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            return false;
        }

        var wasActive = session.IsActive;
        session.IsActive = false;
        session.TerminationReason = reason.ToString();
        session.TerminatedAt = SystemClock.UtcNow;

        await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        if (wasActive)
        {
            SessionAnalyticsMetrics.RecordTerminated(reason);
        }

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

        if (activeSessions.Count > 0)
        {
            SessionAnalyticsMetrics.RecordTerminated(reason, activeSessions.Count);
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

        var now = SystemClock.UtcNow;
        return devices.Where(d => d.IsActive && (!d.ExpiresAt.HasValue || d.ExpiresAt.Value > now)).ToList();
    }

    /// <summary>
    ///     Names or renames one of the caller's own active sessions. The custom name is stored
    ///     on the session's device metadata (surviving token rotation) and must remain non-empty;
    ///     naming another user's session or an inactive session is rejected.
    /// </summary>
    public async Task<bool> RenameSessionAsync(Guid userId, Guid sessionId, string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > MaxSessionNameLength)
        {
            return false;
        }

        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is not { IsActive: true } || session.UserId != userId)
        {
            return false;
        }

        var deviceInfo = SessionEnrichmentJson.DeserializeDeviceInfo(session.DeviceInfo) ?? _userAgentParser.Parse(session.UserAgent);
        if (!string.IsNullOrWhiteSpace(session.DeviceFingerprint))
        {
            deviceInfo.Fingerprint = session.DeviceFingerprint;
        }
        deviceInfo.IpAddress = session.IpAddress;
        deviceInfo.DeviceName = trimmed;
        session.DeviceInfo = SessionEnrichmentJson.SerializeDeviceInfo(deviceInfo);

        await sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await ForwardSessionAuditEventAsync(session, "Authentication.SessionRenamed", cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Session {SessionId} renamed for user {UserId}", sessionId, userId);

        return true;
    }

    /// <summary>The maximum accepted length of a user-defined session name.</summary>
    public const int MaxSessionNameLength = 64;

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
            await ForwardSessionAuditEventAsync(session, "Authentication.SessionTerminated", cancellationToken, session.TerminationReason).ConfigureAwait(false);
        }

        var evictedCount = sessionsToTerminate.Count();
        if (evictedCount > 0)
        {
            SessionAnalyticsMetrics.RecordTerminated(SessionTerminationReason.MaxSessionsExceeded, evictedCount);
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
        SessionAnalyticsMetrics.RecordTerminated(SessionTerminationReason.Expired);
        await ForwardSessionAuditEventAsync(session, "Authentication.SessionTerminated", cancellationToken, session.TerminationReason).ConfigureAwait(false);
    }

    public async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        await sessionRepository.DeleteExpiredAsync(SystemClock.UtcNow, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Cleaned up expired sessions");
    }

    public async Task<SessionSecurityAnalysis> AnalyzeSessionSecurityAsync(Guid userId, string ipAddress, string userAgent, CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        var recentSessions = await sessionRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        var activeSessions = recentSessions.Where(s => s.IsActive && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastUsedAt)
            .ToList();
        var activeCount = activeSessions.Count;

        var uniqueIps = recentSessions.Select(s => s.IpAddress).Where(ip => !string.IsNullOrEmpty(ip)).Distinct().Count();
        var uniqueDevices = recentSessions.Select(s => s.DeviceFingerprint).Where(fp => !string.IsNullOrEmpty(fp)).Distinct().Count();

        // ── Additive risk score ─────────────────────────────────────────────
        // Count-based factors remain the baseline (they work with no location data);
        // geo factors only fire when real location metadata is present.
        var riskScore = 0;
        var riskFactors = new List<string>();

        if (uniqueIps > 10)
        {
            riskScore += 20;
            riskFactors.Add($"Multiple IPs: {uniqueIps}");
        }

        if (uniqueDevices > 5)
        {
            riskScore += 20;
            riskFactors.Add($"Multiple devices: {uniqueDevices}");
        }

        if (activeCount > 10)
        {
            riskScore += 30;
            riskFactors.Add($"High active session count: {activeCount}");
        }

        var sessionLocations = activeSessions
            .Select(s => SessionEnrichmentJson.DeserializeLocation(s.Location))
            .OfType<LocationInfo>()
            .ToList();

        // Concurrent location analysis: distinct populated locations among ACTIVE sessions.
        var concurrentLocations = sessionLocations
            .Where(l => !string.IsNullOrEmpty(l.Country))
            .Select(l => (l.Country!, l.City ?? string.Empty))
            .Distinct()
            .ToList();
        if (concurrentLocations.Count >= 2)
        {
            riskScore += 25;
            riskFactors.Add($"Concurrent locations: {concurrentLocations.Count}");
        }

        // Geo-velocity analysis: implied travel speed between consecutive session uses
        // carrying coordinates (session history first, then the current request).
        var observations = activeSessions
            .OrderBy(s => s.LastUsedAt)
            .Select(s => (Moment: s.LastUsedAt, Location: SessionEnrichmentJson.DeserializeLocation(s.Location)))
            .Where(o => o.Location is not null)
            .Select(o => (o.Moment, Location: o.Location!))
            .ToList();

        LocationInfo? requestLocation = null;
        if (_sessionOptions.EnableLocationTracking && _geolocationProvider is not null && !string.IsNullOrWhiteSpace(ipAddress))
        {
            try
            {
                requestLocation = SessionEnrichmentJson.ToLocationInfo(
                    await _geolocationProvider.ResolveAsync(ipAddress, cancellationToken).ConfigureAwait(false), ipAddress);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Geolocation resolution failed during session security analysis; continuing without it");
            }
        }

        if (requestLocation is not null)
        {
            observations.Add((now, requestLocation));
        }

        double? maxVelocityKmh = null;
        for (var i = 1; i < observations.Count; i++)
        {
            var speed = GeoVelocity.SpeedKmh(observations[i - 1].Location, observations[i].Location, observations[i].Moment - observations[i - 1].Moment);
            if (speed is not { } current)
            {
                continue;
            }

            maxVelocityKmh = Math.Max(maxVelocityKmh ?? 0, current);
            if (current > GeoVelocity.DefaultImpossibleTravelSpeedKmh)
            {
                riskScore += 35;
                riskFactors.Add($"Impossible travel: {current:F0} km/h between consecutive sessions");
                break;
            }
        }

        // Per-session anomaly detection: an active session located outside the account's
        // majority country (when at least one location is known) is flagged individually.
        var anomalousSessionIds = new List<string>();
        var majorityCountry = sessionLocations
            .Where(l => !string.IsNullOrEmpty(l.Country))
            .GroupBy(l => l.Country!, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
        if (majorityCountry is not null)
        {
            anomalousSessionIds = activeSessions
                .Where(s => SessionEnrichmentJson.DeserializeLocation(s.Location)?.Country is { } country &&
                            !string.Equals(country, majorityCountry, StringComparison.Ordinal))
                .Select(s => s.Id.ToString())
                .ToList();
            if (anomalousSessionIds.Count > 0)
            {
                riskScore += 15 * Math.Min(anomalousSessionIds.Count, 2);
                riskFactors.Add($"Sessions outside usual country ({majorityCountry}): {anomalousSessionIds.Count}");
            }
        }

        var riskLevel = riskScore switch
        {
            >= 80 => RiskLevel.Critical,
            >= 60 => RiskLevel.High,
            >= 30 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        return new SessionSecurityAnalysis
        {
            UserId = userId,
            ActiveSessionCount = activeCount,
            TotalDeviceCount = uniqueDevices,
            UnusualActivityDetected = riskLevel >= RiskLevel.Medium,
            RiskScore = riskScore,
            RiskLevel = riskLevel,
            RiskFactors = riskFactors,
            Metadata = new Dictionary<string, string>
            {
                ["geo_data_available"] = sessionLocations.Count > 0 || requestLocation is not null ? "true" : "false",
                ["concurrent_locations"] = concurrentLocations.Count.ToString(CultureInfo.InvariantCulture),
                ["max_observed_velocity_kmh"] = maxVelocityKmh is null
                    ? string.Empty
                    : maxVelocityKmh.Value.ToString("F0", CultureInfo.InvariantCulture),
                ["anomalous_session_ids"] = string.Join(",", anomalousSessionIds)
            }
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
