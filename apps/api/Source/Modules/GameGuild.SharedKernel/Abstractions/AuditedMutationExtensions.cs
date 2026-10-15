using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild;

/// <summary>
///     Fail-closed persistence helpers for commands that must not commit without their audit event.
///     The mutation and the audit emission share one transaction: when the audit emission fails the
///     transaction is never committed, so the mutation rolls back with it.
/// </summary>
public static class AuditedMutationExtensions
{
    /// <summary>
    ///     Begins a database transaction when the underlying context is relational and no transaction
    ///     already owns the command. Callers participate in an existing transaction without taking
    ///     ownership of its commit or disposal. Non-relational providers (the in-memory test provider)
    ///     do not support transactions; callers then run the mutation and the audit emission in one
    ///     <see cref="IApplicationDbContext.SaveChangesAsync"/> batch, which is equally atomic for those
    ///     providers.
    /// </summary>
    public static async Task<IDbContextTransaction?> BeginAuditedMutationTransactionAsync(
        this IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context is not DbContext relational)
        {
            return null;
        }

        if (!relational.Database.IsRelational() || relational.Database.CurrentTransaction is not null)
        {
            return null;
        }

        return await relational.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Commits a transaction returned by <see cref="BeginAuditedMutationTransactionAsync"/>.
    /// </summary>
    public static Task CommitAuditedMutationAsync(
        this IDbContextTransaction? transaction,
        CancellationToken cancellationToken = default) =>
        transaction is null
            ? Task.CompletedTask
            : transaction.CommitAsync(cancellationToken);

    /// <summary>
    ///     Persists tracked mutations and then records the required audit event inside the same
    ///     transaction. The audit callback is expected to throw when the audit event cannot be
    ///     persisted; the exception propagates, the transaction is disposed without committing, and
    ///     the mutation rolls back (fail closed). With a null transaction (non-relational provider)
    ///     both writes share the single save batch, which is atomic for that provider.
    /// </summary>
    /// <param name="context">The shared module context carrying the mutation.</param>
    /// <param name="recordAuditAsync">
    ///     Records the required audit event. Must throw on failure; it must not swallow errors,
    ///     because a committed mutation without its audit event violates the audit contract.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SaveWithRequiredAuditAsync(
        this IApplicationDbContext context,
        Func<CancellationToken, Task> recordAuditAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(recordAuditAsync);

        var transaction = await context.BeginAuditedMutationTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await recordAuditAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAuditedMutationAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
