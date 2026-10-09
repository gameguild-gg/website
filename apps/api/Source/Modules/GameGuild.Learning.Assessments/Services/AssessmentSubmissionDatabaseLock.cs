using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Learning.Assessments;

internal static class AssessmentSubmissionDatabaseLock
{
    public static async Task<IDbContextTransaction?> AcquireAsync(
        IApplicationDbContext context,
        Guid assessmentId,
        Guid enrollmentId,
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
            await dbContext.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock({0})",
                    [CreateLockKey(assessmentId, enrollmentId)],
                    cancellationToken)
                .ConfigureAwait(false);
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

    public static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken = default) =>
        transaction == null ? Task.CompletedTask : transaction.CommitAsync(cancellationToken);

    private static long CreateLockKey(Guid assessmentId, Guid enrollmentId)
    {
        Span<byte> source = stackalloc byte[32];
        assessmentId.TryWriteBytes(source[..16]);
        enrollmentId.TryWriteBytes(source[16..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(source, hash);
        return BitConverter.ToInt64(hash);
    }
}
