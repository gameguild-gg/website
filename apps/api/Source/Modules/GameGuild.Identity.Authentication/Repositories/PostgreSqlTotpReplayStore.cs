using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GameGuild.Identity.Authentication;

/// <summary>Serializes acceptance across instances and keeps the watermark with the successful effects.</summary>
public sealed class PostgreSqlTotpReplayStore(IApplicationDbContext context) : ITotpReplayStore
{
    public Task<bool> TryAcceptAsync(Guid configurationId, string secretFingerprint, long matchedStep,
        DateTimeOffset acceptedAt, Func<CancellationToken, Task> persistSuccessfulVerification) =>
        TryAcceptAsync(configurationId, secretFingerprint, matchedStep, acceptedAt, persistSuccessfulVerification, CancellationToken.None);

    public async Task<bool> TryAcceptAsync(Guid configurationId, string secretFingerprint, long matchedStep,
        DateTimeOffset acceptedAt, Func<CancellationToken, Task> persistSuccessfulVerification,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (configurationId == Guid.Empty) { throw new ArgumentException("An enrollment is required.", nameof(configurationId)); }
        if (secretFingerprint is not { Length: 64 } || !secretFingerprint.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("Invalid enrollment fingerprint.", nameof(secretFingerprint));
        }
        if (matchedStep < 0) { throw new ArgumentOutOfRangeException(nameof(matchedStep)); }
        if (acceptedAt.Offset != TimeSpan.Zero) { throw new ArgumentException("A UTC instant is required.", nameof(acceptedAt)); }
        ArgumentNullException.ThrowIfNull(persistSuccessfulVerification);
        if (context is not DbContext databaseContext || databaseContext.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            throw new InvalidOperationException("TOTP acceptance requires the shared PostgreSQL context.");
        }

        // Direct callers receive the same atomic guarantee as callers inside a command transaction.
        var callerTransaction = databaseContext.Database.CurrentTransaction;
        await using var ownedTransaction = callerTransaction is null
            ? await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var savepoint = callerTransaction is not null ? "totp_" + Guid.NewGuid().ToString("N") : null;
        if (callerTransaction is not null)
        {
            if (!callerTransaction.SupportsSavepoints)
            {
                throw new InvalidOperationException("TOTP acceptance requires transaction savepoints.");
            }
            await callerTransaction.CreateSavepointAsync(savepoint!, cancellationToken).ConfigureAwait(false);
        }
        var trackedBeforeAcceptance = databaseContext.ChangeTracker.Entries()
            .Select(entry => new TrackedState(entry, entry.State, entry.CurrentValues.Clone(), entry.OriginalValues.Clone(),
                entry.Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name).ToArray()))
            .ToArray();
        try
        {
            var changed = await databaseContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "gameguild.authentication"."totp_replay_state"
                    (configuration_id, secret_fingerprint, last_accepted_step, last_accepted_at)
                VALUES ({configurationId}, {secretFingerprint.ToLowerInvariant()}, {matchedStep}, {acceptedAt})
                ON CONFLICT (configuration_id, secret_fingerprint)
                DO UPDATE SET last_accepted_step = EXCLUDED.last_accepted_step,
                              last_accepted_at = EXCLUDED.last_accepted_at
                WHERE "totp_replay_state".last_accepted_step < EXCLUDED.last_accepted_step
                """, cancellationToken).ConfigureAwait(false);
            if (changed == 1)
            {
                await persistSuccessfulVerification(cancellationToken).ConfigureAwait(false);
                if (ownedTransaction is not null) { await ownedTransaction.CommitAsync(cancellationToken).ConfigureAwait(false); }
            }
            if (callerTransaction is not null)
            {
                await callerTransaction.ReleaseSavepointAsync(savepoint!, cancellationToken).ConfigureAwait(false);
            }
            return changed == 1;
        }
        catch
        {
            if (callerTransaction is not null)
            {
                // Roll back accepted-proof effects even when the provider later converts failure into a denial.
                await callerTransaction.RollbackToSavepointAsync(savepoint!, CancellationToken.None).ConfigureAwait(false);
                await callerTransaction.ReleaseSavepointAsync(savepoint!, CancellationToken.None).ConfigureAwait(false);
            }
            else if (ownedTransaction is not null)
            {
                await ownedTransaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            RestoreTrackedState(databaseContext, trackedBeforeAcceptance);
            throw;
        }
    }

    private static void RestoreTrackedState(DbContext context, IReadOnlyList<TrackedState> snapshots)
    {
        var previousEntities = new HashSet<object>(snapshots.Select(snapshot => snapshot.Entry.Entity), ReferenceEqualityComparer.Instance);
        foreach (var entry in context.ChangeTracker.Entries().ToArray())
        {
            if (!previousEntities.Contains(entry.Entity)) { entry.State = EntityState.Detached; }
        }
        foreach (var snapshot in snapshots)
        {
            snapshot.Entry.State = EntityState.Unchanged;
            snapshot.Entry.CurrentValues.SetValues(snapshot.CurrentValues);
            snapshot.Entry.OriginalValues.SetValues(snapshot.OriginalValues);
            snapshot.Entry.State = snapshot.State;
            if (snapshot.State == EntityState.Modified)
            {
                foreach (var property in snapshot.Entry.Properties)
                {
                    property.IsModified = snapshot.ModifiedProperties.Contains(property.Metadata.Name, StringComparer.Ordinal);
                }
            }
        }
    }

    private sealed record TrackedState(EntityEntry Entry, EntityState State, PropertyValues CurrentValues,
        PropertyValues OriginalValues, string[] ModifiedProperties);
}
