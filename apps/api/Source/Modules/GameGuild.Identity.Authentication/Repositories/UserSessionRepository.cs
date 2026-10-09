using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository implementation for user session data access operations
/// </summary>
public class UserSessionRepository(IApplicationDbContext context) : IUserSessionRepository, IUserSessionCleanupRepository
{
    private DbSet<UserSession> UserSessions { get => context.Set<UserSession>(); }

    public async Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await UserSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken); }

    public async Task<UserSession?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return await UserSessions.FirstOrDefaultAsync(s => s.RefreshToken == refreshToken, cancellationToken);
    }

    public async Task<List<UserSession>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await UserSessions.Where(s => s.UserId == userId && s.IsActive && s.ExpiresAt > SystemClock.UtcNow).OrderByDescending(s => s.LastUsedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<UserSession>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await UserSessions.Where(s => s.UserId == userId).OrderByDescending(s => s.LastUsedAt).ToListAsync(cancellationToken);
    }

    public async Task<UserSession> CreateAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        if (session.Id == Guid.Empty)
        {
            session.Id = Guid.NewGuid();
        }

        session.UpdatedAt = SystemClock.UtcNow;
        session.LastUsedAt = SystemClock.UtcNow;

        UserSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return session;
    }

    public async Task<UserSession> UpdateAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        session.UpdatedAt = SystemClock.UtcNow;

        var entry = UserSessions.Update(session);
        if (session.IsActive)
        {
            // Active metadata writes cannot undo a concurrent committed termination.
            entry.Property(value => value.IsActive).IsModified = false;
            entry.Property(value => value.TerminatedAt).IsModified = false;
            entry.Property(value => value.TerminationReason).IsModified = false;
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return session;
    }

    public async Task TerminateAsync(Guid sessionId, string reason, CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            return;
        }

        session.IsActive = false;
        session.TerminationReason = reason;
        session.TerminatedAt = SystemClock.UtcNow;
        session.UpdatedAt = SystemClock.UtcNow;

        await UpdateAsync(session, cancellationToken).ConfigureAwait(false);
    }

    public async Task TerminateAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var activeSessions = await UserSessions.Where(s => s.UserId == userId && s.IsActive).ToListAsync(cancellationToken);

        if (activeSessions.Count == 0)
        {
            return;
        }

        var now = SystemClock.UtcNow;

        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            session.TerminationReason = reason;
            session.TerminatedAt = now;
            session.UpdatedAt = now;
        }

        UserSessions.UpdateRange(activeSessions);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task TerminateAllExceptAsync(Guid userId, Guid keepSessionId, string reason, CancellationToken cancellationToken = default)
    {
        var activeSessions = await UserSessions.Where(s => s.UserId == userId && s.IsActive && s.Id != keepSessionId).ToListAsync(cancellationToken);

        if (activeSessions.Count == 0)
        {
            return;
        }

        var now = SystemClock.UtcNow;

        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            session.TerminationReason = reason;
            session.TerminatedAt = now;
            session.UpdatedAt = now;
        }

        UserSessions.UpdateRange(activeSessions);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var expiredSessions = await UserSessions.Where(s =>
            (s.ExpiresAt < now || !s.IsActive && s.TerminatedAt.HasValue && s.TerminatedAt.Value.AddDays(30) < now) &&
            !context.Set<RefreshToken>().Any(token => token.SessionId == s.Id)).ToListAsync(cancellationToken);

        if (expiredSessions.Count == 0)
        {
            return;
        }

        UserSessions.RemoveRange(expiredSessions);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DeleteRetainedSessionBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 1000) { throw new ArgumentOutOfRangeException(nameof(batchSize)); }
        var expiredSessions = await UserSessions.Where(session =>
                (session.ExpiresAt < cutoffUtc || !session.IsActive && session.TerminatedAt.HasValue && session.TerminatedAt.Value < cutoffUtc) &&
                !context.Set<RefreshToken>().Any(token => token.SessionId == session.Id))
            .OrderBy(session => session.ExpiresAt).ThenBy(session => session.Id).Take(batchSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (expiredSessions.Count == 0) { return 0; }
        UserSessions.RemoveRange(expiredSessions);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return expiredSessions.Count;
    }

    public async Task<int> CountActiveSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;

        return await UserSessions.CountAsync(s => s.UserId == userId && s.IsActive && s.ExpiresAt > now, cancellationToken);
    }

    // Helper methods for backward compatibility
    public async Task<IReadOnlyList<UserSession>> GetByDeviceFingerprintAsync(string deviceFingerprint, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = UserSessions.Where(s => s.DeviceFingerprint == deviceFingerprint);

        if (activeOnly)
        {
            query = query.Where(s => s.IsActive && s.ExpiresAt > SystemClock.UtcNow);
        }

        return await query.OrderByDescending(s => s.LastUsedAt).ToListAsync(cancellationToken);
    }

    public async Task<bool> UpdateLastUsedAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session is not { IsActive: true })
        {
            return false;
        }

        session.LastUsedAt = SystemClock.UtcNow;
        session.UpdatedAt = SystemClock.UtcNow;

        await UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> MarkDeviceAsTrustedAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            return false;
        }

        session.IsTrustedDevice = true;
        session.TrustedAt = SystemClock.UtcNow;
        session.UpdatedAt = SystemClock.UtcNow;

        await UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
