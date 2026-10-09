using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Projects;

public interface IProjectLifecycleLock
{
    Task<IProjectLifecycleLockHandle> AcquireAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}

public interface IProjectLifecycleLockHandle : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public sealed class ProjectLifecycleLock(IApplicationDbContext context) : IProjectLifecycleLock
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> FallbackLocks = new();

    public async Task<IProjectLifecycleLockHandle> AcquireAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (context is DbContext dbContext &&
            dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            var lockKey = CreateLockKey(projectId);
            var openedConnection = dbContext.Database.GetDbConnection().State != ConnectionState.Open;
            try
            {
                if (openedConnection)
                {
                    await dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                }

                await dbContext.Database.ExecuteSqlRawAsync(
                        "SELECT pg_advisory_lock({0})",
                        [lockKey],
                        cancellationToken)
                    .ConfigureAwait(false);
                return new Handle(dbContext, lockKey, openedConnection, null);
            }
            catch
            {
                if (openedConnection)
                {
                    await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
                }

                throw;
            }
        }

        var fallback = FallbackLocks.GetOrAdd(projectId, static _ => new SemaphoreSlim(1, 1));
        await fallback.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Handle(null, null, false, fallback);
    }

    private static long CreateLockKey(Guid projectId)
    {
        Span<byte> source = stackalloc byte[33];
        "project-lifecycle"u8.CopyTo(source);
        projectId.TryWriteBytes(source[17..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(source, hash);
        return BitConverter.ToInt64(hash);
    }

    private sealed class Handle(
        DbContext? dbContext,
        long? lockKey,
        bool closeConnection,
        SemaphoreSlim? fallback) : IProjectLifecycleLockHandle
    {
        private bool _released;

        public Task CommitAsync(CancellationToken cancellationToken = default) => ReleaseAsync();

        public async ValueTask DisposeAsync() => await ReleaseAsync().ConfigureAwait(false);

        private async Task ReleaseAsync()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            try
            {
                if (dbContext != null && lockKey.HasValue)
                {
                    await dbContext.Database.ExecuteSqlRawAsync(
                            "SELECT pg_advisory_unlock({0})",
                            [lockKey.Value],
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                if (dbContext != null && closeConnection)
                {
                    await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
                }

                fallback?.Release();
            }
        }
    }
}
