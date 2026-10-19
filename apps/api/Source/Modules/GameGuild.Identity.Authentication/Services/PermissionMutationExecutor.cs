using System.Data.Common;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Identity.Authentication;

/// <summary>Atomic permission writes that preserve caller-owned state and pending audit evidence.</summary>
internal sealed class PermissionMutationExecutor(IApplicationDbContext context)
{
    public async Task<Result<T>> ExecuteAsync<T>(Guid actorId, Guid? tenantId,
        Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken,
        Func<T, bool>? shouldCommit = null, bool useEntityTenantScope = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context is not DbContext db || !db.Database.IsRelational())
        {
            return Result.Failure<T>(Error.Failure(
                "PermissionWrite.RelationalContextRequired", "Permission updates require a relational context."));
        }
        db.ChangeTracker.DetectChanges();
        if (db.ChangeTracker.HasChanges() || db.ChangeTracker.Entries().Any(entry =>
                entry.Entity is IHasDomainEvents { DomainEvents.Count: > 0 }
                || entry.Entity is IHasIntegrationEvents { IntegrationEvents.Count: > 0 }))
        {
            return Result.Failure<T>(Error.Conflict(
                "PermissionWrite.PendingWork", "Persist the caller's pending work inside its transaction before updating a permission."));
        }
        // Security mutations have no replay key. Execute once; ambiguous commit failures
        // propagate rather than creating a second version/audit entry on an automatic retry.
        var automaticDetection = db.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            // The grant, version repository and audit capture must all observe their edits,
            // even when the caller disabled EF's automatic detection for earlier batch work.
            db.ChangeTracker.AutoDetectChangesEnabled = true;
            return await new PermissionMutationExecutionStrategy(db).ExecuteAsync(
                () => ExecuteOnceAsync(db, actorId, tenantId, operation, cancellationToken, shouldCommit, useEntityTenantScope)).ConfigureAwait(false);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = automaticDetection;
        }
    }

    private async Task<Result<T>> ExecuteOnceAsync<T>(
        DbContext db, Guid actorId, Guid? tenantId,
        Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken, Func<T, bool>? shouldCommit, bool useEntityTenantScope)
    {
        var callerTransaction = db.Database.CurrentTransaction;
        if (callerTransaction is { SupportsSavepoints: false })
        {
            return Result.Failure<T>(Error.Failure(
                "PermissionWrite.SavepointRequired", "The caller transaction must support permission rollback savepoints."));
        }
        var before = db.ChangeTracker.Entries().ToDictionary(entry => entry.Entity,
            entry => new TrackedState(entry.State, entry.CurrentValues.Clone(), entry.OriginalValues.Clone()),
            ReferenceEqualityComparer.Instance);
        var buffer = context as IPermissionMutationAuditBuffer;
        using var auditScope = buffer?.BeginPermissionMutationAuditScope(actorId, tenantId, useEntityTenantScope);
        var ownsTransaction = callerTransaction is null;
        var transaction = callerTransaction ?? await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var savepoint = "permission_" + Guid.NewGuid().ToString("N");
        if (!ownsTransaction)
        {
            await transaction.CreateSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);
        }

        async Task UndoAsync(Exception? originalFailure)
        {
            try
            {
                if (ownsTransaction)
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    await transaction.RollbackToSavepointAsync(savepoint, CancellationToken.None).ConfigureAwait(false);
                    await transaction.ReleaseSavepointAsync(savepoint, CancellationToken.None).ConfigureAwait(false);
                }
                auditScope?.RestoreAfterRollback();
                foreach (var entry in db.ChangeTracker.Entries().ToArray())
                {
                    if (!before.TryGetValue(entry.Entity, out var original))
                    {
                        entry.State = EntityState.Detached;
                        continue;
                    }
                    entry.CurrentValues.SetValues(original.Current);
                    entry.OriginalValues.SetValues(original.Original);
                    entry.State = original.State;
                }
            }
            catch (Exception cleanupFailure)
            {
                if (originalFailure is null)
                {
                    throw;
                }
                throw new AggregateException("Permission mutation failed and rollback could not be confirmed.",
                    originalFailure, cleanupFailure);
            }
        }

        Result<T> result;
        var accepted = false;
        try
        {
            try
            {
                result = await operation(cancellationToken).ConfigureAwait(false);
                accepted = result.IsSuccess && (shouldCommit?.Invoke(result.Value) ?? true);
                if (accepted)
                {
                    if (ownsTransaction)
                    {
                        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await transaction.ReleaseSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (DbUpdateConcurrencyException failure)
            {
                await UndoAsync(failure).ConfigureAwait(false);
                return Result.Failure<T>(Error.Conflict("PermissionWrite.Concurrency", "The permission changed; reload its current version."));
            }
            catch (DbUpdateException failure) when (failure.InnerException is DbException { SqlState: "23505" }
                && failure.Entries.Any(entry => entry.State == EntityState.Added && entry.Entity is TenantSecurityVersion))
            {
                await UndoAsync(failure).ConfigureAwait(false);
                return Result.Failure<T>(Error.Conflict("PermissionWrite.Concurrency", "The security version was initialized concurrently; retry the request."));
            }
            catch (Exception failure)
            {
                await UndoAsync(failure).ConfigureAwait(false);
                throw;
            }
            if (!accepted)
            {
                await UndoAsync(null).ConfigureAwait(false);
            }
        }
        finally
        {
            if (ownsTransaction)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
        if (ownsTransaction && accepted && buffer is not null)
        {
            // Committed audit delivery must survive cancellation of the originating request.
            await buffer.FlushPendingPermissionAuditChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
        return result;
    }

    private sealed record TrackedState(EntityState State, PropertyValues Current, PropertyValues Original);

    private sealed class PermissionMutationExecutionStrategy(DbContext db) : ExecutionStrategy(db, 0, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception _) => false;
    }
}
