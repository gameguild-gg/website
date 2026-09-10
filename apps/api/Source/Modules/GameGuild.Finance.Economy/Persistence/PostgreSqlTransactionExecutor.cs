using System.Data;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Persistence;

/// <summary>
/// Executes an explicit relational transaction as a single retriable unit.
/// </summary>
public static class PostgreSqlTransactionExecutor
{
    public static Task<TResult> ExecuteAsync<TResult>(
        IApplicationDbContext db,
        IsolationLevel isolationLevel,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(operation);

        return db is DbContext relational
            ? ExecuteAsync(relational, isolationLevel, operation, cancellationToken)
            : ExecuteContractTransactionAsync(db, operation, cancellationToken);
    }

    public static Task<TResult> ExecuteAsync<TResult>(
        DbContext db,
        IsolationLevel isolationLevel,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(operation);

        if (db.Database.CurrentTransaction is not null)
            return operation(cancellationToken);

        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                isolationLevel, cancellationToken).ConfigureAwait(false);
            var result = await operation(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        });
    }

    private static async Task<TResult> ExecuteContractTransactionAsync<TResult>(
        IApplicationDbContext db,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public static Task ExecuteAsync(
        DbContext db,
        IsolationLevel isolationLevel,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken) =>
        ExecuteAsync<object?>(db, isolationLevel, async token =>
        {
            await operation(token).ConfigureAwait(false);
            return null;
        }, cancellationToken);
}
