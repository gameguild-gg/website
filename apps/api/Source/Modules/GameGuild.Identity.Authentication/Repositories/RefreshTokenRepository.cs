using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository implementation for refresh token data access operations
/// </summary>
public class RefreshTokenRepository(IApplicationDbContext context) : IRefreshTokenRepository, IRefreshTokenLineageRepository, IRefreshTokenCleanupRepository
{
    private DbSet<RefreshToken> RefreshTokens { get => context.Set<RefreshToken>(); }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) { return await RefreshTokens.FirstOrDefaultAsync(r => r.Token == token, cancellationToken); }

    public async Task<List<RefreshToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        return await RefreshTokens.Where(r => r.UserId == userId && !r.IsRevoked && r.ExpiresAt > now)
            .OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<RefreshToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await RefreshTokens.Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<RefreshToken> CreateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        refreshToken.Id = Guid.NewGuid();
        refreshToken.UpdatedAt = SystemClock.UtcNow;

        RefreshTokens.Add(refreshToken);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return refreshToken;
    }

    public async Task<RefreshToken> UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        refreshToken.UpdatedAt = SystemClock.UtcNow;

        RefreshTokens.Update(refreshToken);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return refreshToken;
    }

    public async Task<bool> BindSessionAsync(Guid userId, string tokenHash, Guid sessionId, CancellationToken cancellationToken)
    {
        RequireIdentity(userId, sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        var token = await RefreshTokens.AsNoTracking().SingleOrDefaultAsync(value => value.Token == tokenHash, cancellationToken)
            .ConfigureAwait(false);
        if (token is null)
        {
            return false;
        }
        var session = await context.Set<UserSession>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (token.UserId != userId || token.IsRevoked || token.ExpiresAt <= SystemClock.UtcNow ||
            session is null || session.UserId != userId ||
            session.RefreshToken != tokenHash || !session.IsActive || session.ExpiresAt <= SystemClock.UtcNow ||
            token.SessionId.HasValue && token.SessionId.Value != sessionId)
        {
            throw new UnauthorizedAccessException("Invalid refresh-token session binding");
        }
        var tokenLink = TrackForMetadata(token);
        tokenLink.SessionId = sessionId;
        if (tokenLink.ExpiresAt > session.ExpiresAt)
        {
            tokenLink.ExpiresAt = session.ExpiresAt;
        }
        tokenLink.UpdatedAt = SystemClock.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task RecordRotationAsync(Guid userId, Guid parentTokenId, string replacementTokenHash, Guid sessionId,
        CancellationToken cancellationToken)
    {
        RequireIdentity(userId, sessionId);
        if (parentTokenId == Guid.Empty)
        {
            throw new ArgumentException("A persisted predecessor is required.", nameof(parentTokenId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementTokenHash);
        // ExecuteUpdate claims bypass the tracker; validate the committed-in-transaction row, not its stale tracked copy.
        var parent = await RefreshTokens.AsNoTracking().SingleOrDefaultAsync(value => value.Id == parentTokenId, cancellationToken)
            .ConfigureAwait(false);
        var child = await RefreshTokens.AsNoTracking().SingleOrDefaultAsync(value => value.Token == replacementTokenHash, cancellationToken)
            .ConfigureAwait(false);
        var session = await context.Set<UserSession>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (parent is null || child is null || session is null || parent.Id == child.Id ||
            parent.UserId != userId || child.UserId != userId || session.UserId != userId ||
            !parent.IsRevoked || parent.ReplacedByToken != replacementTokenHash ||
            child.IsRevoked || child.ExpiresAt <= SystemClock.UtcNow ||
            session.RefreshToken != replacementTokenHash || !session.IsActive || session.ExpiresAt <= SystemClock.UtcNow ||
            parent.SessionId.HasValue && parent.SessionId.Value != sessionId ||
            child.SessionId.HasValue && child.SessionId.Value != sessionId ||
            child.ParentTokenId.HasValue && child.ParentTokenId.Value != parentTokenId)
        {
            throw new UnauthorizedAccessException("Invalid refresh-token lineage");
        }
        // A legacy predecessor can be bound only by this observed successful rotation and owned session.
        var parentLink = TrackForMetadata(parent);
        var childLink = TrackForMetadata(child);
        parentLink.SessionId = sessionId;
        childLink.SessionId = sessionId;
        childLink.ParentTokenId = parent.Id;
        if (childLink.ExpiresAt > session.ExpiresAt)
        {
            childLink.ExpiresAt = session.ExpiresAt;
        }
        parentLink.UpdatedAt = childLink.UpdatedAt = SystemClock.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private RefreshToken TrackForMetadata(RefreshToken stored)
    {
        var tracked = RefreshTokens.Local.SingleOrDefault(value => value.Id == stored.Id);
        if (tracked is not null)
        {
            return tracked;
        }
        RefreshTokens.Attach(stored);
        return stored;
    }

    public async Task<Guid?> RevokeFamilyAsync(Guid userId, Guid tokenId, string? revokedByIp, CancellationToken cancellationToken)
    {
        var token = await RefreshTokens.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == tokenId && value.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (token is null) { throw new UnauthorizedAccessException("Invalid refresh-token family"); }
        if (!token.SessionId.HasValue) { return null; }
        var sessionId = token.SessionId.Value;
        RequireIdentity(userId, sessionId);
        var session = await context.Set<UserSession>().AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == sessionId && value.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (session is null) { return null; }

        var now = SystemClock.UtcNow;
        var reason = SessionTerminationReason.SecurityViolation.ToString();
        // Lock the session before querying descendants. A waiting stale refresh may
        // update metadata, but cannot reactivate this session or bind a usable child.
        var terminated = await context.Set<UserSession>().Where(value => value.Id == sessionId && value.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.IsActive, false)
                .SetProperty(value => value.TerminatedAt, now)
                .SetProperty(value => value.TerminationReason, reason)
                .SetProperty(value => value.UpdatedAt, now), cancellationToken).ConfigureAwait(false);
        if (terminated != 1) { throw new UnauthorizedAccessException("Invalid refresh-token family"); }
        var trackedSession = context.Set<UserSession>().Local.SingleOrDefault(value => value.Id == sessionId);
        if (trackedSession is not null)
        {
            trackedSession.IsActive = false;
            trackedSession.TerminatedAt = now;
            trackedSession.TerminationReason = reason;
            trackedSession.UpdatedAt = now;
        }
        var activeTokens = await RefreshTokens.Where(value => value.UserId == userId && value.SessionId == sessionId && !value.IsRevoked)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var activeToken in activeTokens)
        {
            activeToken.IsRevoked = true;
            activeToken.RevokedAt = now;
            activeToken.RevokedByIp = revokedByIp;
            activeToken.UpdatedAt = now;
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return sessionId;
    }

    private static void RequireIdentity(Guid userId, Guid sessionId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A persisted user ID is required.", nameof(userId));
        }
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("A persisted session ID is required.", nameof(sessionId));
        }
    }

    public async Task<bool> TryRevokeForRotationAsync(
        Guid tokenId,
        string expectedTokenHash,
        string replacementTokenHash,
        DateTime revokedAt,
        string? revokedByIp,
        CancellationToken cancellationToken)
    {
        if (tokenId == Guid.Empty)
        {
            throw new ArgumentException("Refresh token ID is required.", nameof(tokenId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementTokenHash);

        var affected = await RefreshTokens
            .Where(token =>
                token.Id == tokenId &&
                token.Token == expectedTokenHash &&
                !token.IsRevoked &&
                token.ReplacedByToken == null &&
                token.ExpiresAt > revokedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.IsRevoked, true)
                .SetProperty(token => token.RevokedAt, revokedAt)
                .SetProperty(token => token.RevokedByIp, revokedByIp)
                .SetProperty(token => token.ReplacedByToken, replacementTokenHash)
                .SetProperty(token => token.UpdatedAt, revokedAt), cancellationToken)
            .ConfigureAwait(false);

        return affected == 1;
    }

    public async Task RevokeAsync(string token, string? revokedByIp = null, string? replacedByToken = null, CancellationToken cancellationToken = default)
    {
        var refreshToken = await GetByTokenAsync(token, cancellationToken).ConfigureAwait(false);

        if (refreshToken == null || refreshToken.IsRevoked) return;

        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = SystemClock.UtcNow;
        refreshToken.RevokedByIp = revokedByIp;
        refreshToken.ReplacedByToken = replacedByToken;
        refreshToken.UpdatedAt = SystemClock.UtcNow;

        await UpdateAsync(refreshToken, cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeAllForUserAsync(Guid userId, string? revokedByIp = null, CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        var activeTokens = await RefreshTokens.Where(r => r.UserId == userId && !r.IsRevoked && r.ExpiresAt > now)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (activeTokens.Count == 0) return;

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
            token.RevokedByIp = revokedByIp;
            token.UpdatedAt = now;
        }

        RefreshTokens.UpdateRange(activeTokens);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteExpiredAndRevokedAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        // Keep replay markers through original expiry and retention, even when revoked earlier.
        // Delete leaves first. Retain predecessors while any child is retained, including an active descendant.
        int deletedRows;
        do
        {
            deletedRows = await DeleteExpiredAndRevokedBatchAsync(cutoffDate, 500, cancellationToken).ConfigureAwait(false);
        } while (deletedRows != 0);
    }

    public async Task<int> DeleteExpiredAndRevokedBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 1000) { throw new ArgumentOutOfRangeException(nameof(batchSize)); }
        var expiredTokens = await RefreshTokens.Where(r =>
            r.ExpiresAt < cutoffUtc && (!r.IsRevoked || r.RevokedAt.HasValue && r.RevokedAt.Value < cutoffUtc) &&
            !RefreshTokens.Any(child => child.ParentTokenId == r.Id))
            .OrderBy(r => r.ExpiresAt).ThenBy(r => r.Id).Take(batchSize).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (expiredTokens.Count == 0) { return 0; }
        RefreshTokens.RemoveRange(expiredTokens);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return expiredTokens.Count;
    }

    public async Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await RefreshTokens.FirstOrDefaultAsync(r => r.Id == id, cancellationToken); }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var refreshToken = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (refreshToken == null) return false;

        RefreshTokens.Remove(refreshToken);
        var changes = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return changes > 0;
    }
}
