using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository implementation for MFA attempt data access operations
/// </summary>
public class MfaAttemptRepository(IApplicationDbContext context) : IMfaAttemptRepository
{
    private DbSet<MfaAttempt> MfaAttempts { get => context.Set<MfaAttempt>(); }

    public async Task<MfaAttempt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await MfaAttempts.FirstOrDefaultAsync(m => m.Id == id, cancellationToken); }

    public async Task<List<MfaAttempt>> GetByUserIdAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await MfaAttempts.Where(m => m.UserId == userId).OrderByDescending(m => m.CreatedAt).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<List<MfaAttempt>> GetFailedAttemptsAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default)
    {
        return await MfaAttempts.Where(m => m.UserId == userId && !m.IsSuccessful && m.CreatedAt >= since).OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        var oldAttempts = await MfaAttempts.Where(m => m.CreatedAt < cutoffDate).ToListAsync(cancellationToken);

        if (oldAttempts.Count > 0)
        {
            MfaAttempts.RemoveRange(oldAttempts);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<MfaAttempt> CreateAsync(MfaAttempt attempt, CancellationToken cancellationToken = default)
    {
        attempt.Id = Guid.NewGuid();
        attempt.UpdatedAt = SystemClock.UtcNow;

        MfaAttempts.Add(attempt);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return attempt;
    }

    public async Task<int> CountFailedAttemptsAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default)
    {
        return await MfaAttempts.Where(m => m.UserId == userId && !m.IsSuccessful && m.CreatedAt >= since).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MfaAttempt>> GetByMethodAsync(MfaMethod method, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = MfaAttempts.Where(m => m.Method == method);

        if (fromDate.HasValue)
        {
            query = query.Where(m => m.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(m => m.CreatedAt <= toDate.Value);
        }

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<MfaAttempt> UpdateAsync(MfaAttempt attempt, CancellationToken cancellationToken = default)
    {
        attempt.UpdatedAt = SystemClock.UtcNow;

        MfaAttempts.Update(attempt);
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

        MfaAttempts.Remove(attempt);
        var changes = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return changes > 0;
    }
}
