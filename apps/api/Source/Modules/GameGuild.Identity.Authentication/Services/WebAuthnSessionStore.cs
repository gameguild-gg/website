using System.Collections.Concurrent;
using Fido2NetLib;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     In-memory store for pending WebAuthn challenge sessions.
///     Shared by registration and authentication sub-services.
/// </summary>
internal static class WebAuthnSessionStore
{
    private static readonly ConcurrentDictionary<string, WebAuthnSession> PendingSessions = new();

    public static string Store(WebAuthnSession session)
    {
        var sessionId = Guid.NewGuid().ToString();
        PendingSessions[sessionId] = session;
        CleanupExpiredSessions();
        return sessionId;
    }

    public static WebAuthnSession? FindByUser(Guid userId, Func<WebAuthnSession, bool> predicate) =>
        PendingSessions.Values.FirstOrDefault(s => s.UserId == userId && predicate(s));

    public static WebAuthnSession? FindFirst(Func<WebAuthnSession, bool> predicate) =>
        PendingSessions.Values.FirstOrDefault(predicate);

    public static void RemoveByUser(Guid userId)
    {
        var key = PendingSessions.FirstOrDefault(p => p.Value.UserId == userId).Key;
        if (key != null)
        {
            PendingSessions.TryRemove(key, out _);
        }
    }

    public static void RemoveFirst(Func<WebAuthnSession, bool> predicate)
    {
        var key = PendingSessions.FirstOrDefault(p => predicate(p.Value)).Key;
        if (key != null)
        {
            PendingSessions.TryRemove(key, out _);
        }
    }

    private static void CleanupExpiredSessions()
    {
        var expiredKeys = PendingSessions
            .Where(p => p.Value.CreatedAt.AddMinutes(5) < SystemClock.UtcNow)
            .Select(p => p.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            PendingSessions.TryRemove(key, out _);
        }
    }

    internal sealed class WebAuthnSession
    {
        public Guid? UserId { get; init; }
        public byte[] Challenge { get; init; } = [];
        public DateTime CreatedAt { get; init; } = SystemClock.UtcNow;
        public CredentialCreateOptions? RegistrationOptions { get; init; }
        public AssertionOptions? AssertionOptions { get; init; }
    }
}
