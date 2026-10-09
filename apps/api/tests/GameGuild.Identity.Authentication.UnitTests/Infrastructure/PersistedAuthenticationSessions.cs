using Moq;

namespace GameGuild.Identity.Authentication.UnitTests.Infrastructure;

/// <summary>Models successful session persistence without discarding the supplied binding.</summary>
internal static class PersistedAuthenticationSessions
{
    public static void Configure(Mock<ISessionManagementService> sessions)
    {
        var stored = new Dictionary<Guid, UserSession>();
        sessions.Setup(value => value.CreateSessionAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid userId, string ip, string agent, string hash, DateTime expires,
                string? fingerprint, CancellationToken _) =>
            {
                var session = new UserSession
                {
                    Id = id, UserId = userId, RefreshToken = hash, ExpiresAt = expires,
                    IsActive = true, CreatedAt = SystemClock.UtcNow, LastUsedAt = SystemClock.UtcNow,
                    IpAddress = ip, UserAgent = agent, DeviceFingerprint = fingerprint ?? string.Empty
                };
                stored[id] = session;
                return session;
            });
        sessions.Setup(value => value.GetSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => stored.GetValueOrDefault(id));
    }
}
