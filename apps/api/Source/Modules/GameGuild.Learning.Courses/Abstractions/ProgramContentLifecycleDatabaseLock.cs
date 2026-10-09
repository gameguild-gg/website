using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Acquires transaction-scoped PostgreSQL advisory locks for content lifecycle decisions.
/// Lock IDs are sorted so multi-content tree deletes share one ordering with cue links.
/// </summary>
public static class ProgramContentLifecycleDatabaseLock
{
    public static async Task<IDbContextTransaction?> AcquireAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> contentIds,
        CancellationToken cancellationToken = default)
    {
        if (context is not DbContext dbContext ||
            dbContext.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return null;
        }

        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            foreach (var contentId in contentIds.Distinct().OrderBy(id => id))
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                        "SELECT pg_advisory_xact_lock({0})",
                        [CreateLockKey(contentId)],
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return transaction;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }

    public static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken = default)
        => transaction == null ? Task.CompletedTask : transaction.CommitAsync(cancellationToken);

    private static long CreateLockKey(Guid contentId)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(contentId.ToByteArray(), hash);
        return BitConverter.ToInt64(hash);
    }
}
