using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository implementation for authentication attempt data access operations
/// </summary>
public class AuthenticationAttemptRepository(IApplicationDbContext context) : IAuthenticationAttemptRepository
{
    private DbSet<AuthenticationAttempt> AuthenticationAttempts { get => context.Set<AuthenticationAttempt>(); }

    public async Task<AuthenticationAttempt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await AuthenticationAttempts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken); }

    public async Task<List<AuthenticationAttempt>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await AuthenticationAttempts.Where(a => a.UserId == userId).OrderByDescending(a => a.AttemptedAt).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<List<AuthenticationAttempt>> GetSuspiciousAttemptsAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        return await AuthenticationAttempts.Where(a => (a.IsSuspicious || a.RiskScore > 70) && a.AttemptedAt >= since).OrderByDescending(a => a.AttemptedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<AuthenticationAttempt>> GetFailedAttemptsAsync(string identifier, DateTime since, CancellationToken cancellationToken = default)
    {
        return await AuthenticationAttempts.Where(a => (a.Email == identifier.ToLowerInvariant() || a.IpAddress == identifier) && !a.IsSuccessful && a.AttemptedAt >= since)
            .OrderByDescending(a => a.AttemptedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AuthenticationAttempt?> GetLastSuccessfulAttemptAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await AuthenticationAttempts.Where(a => a.UserId == userId && a.IsSuccessful).OrderByDescending(a => a.AttemptedAt).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        var oldAttempts = await AuthenticationAttempts.Where(a => a.AttemptedAt < cutoffDate).ToListAsync(cancellationToken);

        if (oldAttempts.Count > 0)
        {
            AuthenticationAttempts.RemoveRange(oldAttempts);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<AuthenticationAttempt> CreateAsync(AuthenticationAttempt attempt, CancellationToken cancellationToken = default)
    {
        attempt.Id = Guid.NewGuid();
        attempt.UpdatedAt = SystemClock.UtcNow;
        attempt.Email = attempt.Email.ToLowerInvariant();

        AuthenticationAttempts.Add(attempt);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return attempt;
    }

    public async Task<IReadOnlyList<AuthenticationAttempt>> GetByEmailAsync(string email, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await AuthenticationAttempts.Where(a => a.Email == email.ToLowerInvariant()).OrderByDescending(a => a.AttemptedAt).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuthenticationAttempt>> GetByIpAddressAsync(string ipAddress, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = AuthenticationAttempts.Where(a => a.IpAddress == ipAddress);

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.AttemptedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.AttemptedAt <= toDate.Value);
        }

        return await query.OrderByDescending(a => a.AttemptedAt).ToListAsync(cancellationToken);
    }

    public async Task<AuthenticationAttempt> UpdateAsync(AuthenticationAttempt attempt, CancellationToken cancellationToken = default)
    {
        attempt.UpdatedAt = SystemClock.UtcNow;

        AuthenticationAttempts.Update(attempt);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return attempt;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var attempt = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (attempt == null)
        {
            return false;
        }

        AuthenticationAttempts.Remove(attempt);
        var changes = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return changes > 0;
    }

    public async Task<int> CountFailedAttemptsAsync(string email, DateTime fromDate, CancellationToken cancellationToken = default)
    {
        return await AuthenticationAttempts.Where(a => a.Email == email.ToLowerInvariant() && !a.IsSuccessful && a.AttemptedAt >= fromDate).CountAsync(cancellationToken);
    }

    public async Task<int> CountFailedAttemptsByIpAsync(string ipAddress, DateTime fromDate, CancellationToken cancellationToken = default)
    {
        return await AuthenticationAttempts.Where(a => a.IpAddress == ipAddress && !a.IsSuccessful && a.AttemptedAt >= fromDate).CountAsync(cancellationToken);
    }

    public async Task<int> CleanupOldAttemptsAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        var oldAttempts = await AuthenticationAttempts.Where(a => a.AttemptedAt < olderThan).ToListAsync(cancellationToken);

        if (oldAttempts.Count == 0)
        {
            return 0;
        }

        AuthenticationAttempts.RemoveRange(oldAttempts);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return oldAttempts.Count;
    }

    public async Task<List<AuthenticationAttempt>> GetRecentAttemptsAsync(Guid userId, DateTime since, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await AuthenticationAttempts
            .Where(a => a.UserId == userId && a.AttemptedAt >= since)
            .OrderByDescending(a => a.AttemptedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AuthenticationAttempt>> GetRecentAttemptsByIpAsync(string ipAddress, DateTime since, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await AuthenticationAttempts
            .Where(a => a.IpAddress == ipAddress && a.AttemptedAt >= since)
            .OrderByDescending(a => a.AttemptedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(int TotalAttempts, int SuccessfulAttempts, int FailedAttempts)> GetUserStatisticsAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default)
    {
        var attempts = await AuthenticationAttempts
            .Where(a => a.UserId == userId && a.AttemptedAt >= since)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var total = attempts.Count;
        var successful = attempts.Count(a => a.IsSuccessful);
        var failed = attempts.Count(a => !a.IsSuccessful);

        return (total, successful, failed);
    }
}
