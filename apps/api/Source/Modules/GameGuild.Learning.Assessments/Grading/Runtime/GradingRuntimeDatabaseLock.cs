using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

internal static class GradingRuntimeDatabaseLock
{
    public static async Task<IDbContextTransaction?> AcquireAsync(
        IApplicationDbContext context,
        string scope,
        Guid resourceId,
        Guid? discriminator = null,
        CancellationToken cancellationToken = default)
    {
        if (context is not DbContext dbContext || !dbContext.Database.IsRelational())
        {
            return null;
        }

        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            if (dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                        "SELECT pg_advisory_xact_lock({0})",
                        [CreateLockKey(scope, resourceId, discriminator)],
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

    public static Task CommitAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken = default) =>
        transaction is null ? Task.CompletedTask : transaction.CommitAsync(cancellationToken);

    private static long CreateLockKey(string scope, Guid resourceId, Guid? discriminator)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Lock scope is required.", nameof(scope));
        }

        var source = Encoding.UTF8.GetBytes($"grading:{scope.Trim()}:{resourceId:N}:{discriminator?.ToString("N") ?? "none"}");
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(source, hash);
        return BitConverter.ToInt64(hash);
    }
}
