using System.Buffers.Binary;
using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using GameGuild.API.Database;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GameGuild.API.Core.Filters;

/// <summary>Shared account and source-IP admission for all password sign-in entry points.</summary>
public sealed class PasswordSignInAdmissionService(
    ApplicationDbContext database,
    AuthenticationSecurityOptions options,
    IUserEnumerationProtectionService protection,
    ILogger<PasswordSignInAdmissionService> logger) : IPasswordSignInAdmissionService
{
    private static readonly TimeSpan FailedAttemptWindow = TimeSpan.FromHours(1);
    private static readonly SemaphoreSlim[] InProcessLockStripes = Enumerable.Range(0, 128)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public async Task<IAsyncDisposable> AdmitAsync(
        string identifier, HttpContext? context, AuthenticationTimingOrigin timingOrigin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedEmail = identifier.ToLowerInvariant();
        var remoteIpAddress = context?.Connection.RemoteIpAddress;
        var normalizedIpAddress = remoteIpAddress is null ? null
            : remoteIpAddress.IsIPv4MappedToIPv6
                ? remoteIpAddress.MapToIPv4().ToString()
                : remoteIpAddress.ToString();
        if (context is not null)
        {
            context.Response.Headers.CacheControl = "no-store";
        }

        var lease = await TryAcquireLockAsync(database, normalizedEmail,
            options.EnableIpThrottling ? normalizedIpAddress : null, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            throw await CreateDenialAsync(timingOrigin, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var now = DateTime.UtcNow;
            if (options.EnableIpThrottling && IPAddress.TryParse(normalizedIpAddress, out _))
            {
                var recentIpFailures = await database.Set<AuthenticationAttempt>()
                    .Where(attempt => attempt.IpAddress == normalizedIpAddress &&
                        !attempt.IsSuccessful && attempt.AttemptedAt >= now.Subtract(FailedAttemptWindow))
                    .OrderByDescending(attempt => attempt.AttemptedAt)
                    .Select(attempt => attempt.AttemptedAt)
                    .Take(options.MaxAttemptsPerIpPerHour)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                if (recentIpFailures.Count >= options.MaxAttemptsPerIpPerHour)
                {
                    logger.LogWarning("Password sign-in throttled after reaching the source-IP hourly failure limit");
                    throw await CreateDenialAsync(timingOrigin, cancellationToken).ConfigureAwait(false);
                }
            }

            var recentFailures = await database.Set<AuthenticationAttempt>()
                .Where(attempt => attempt.Email == normalizedEmail &&
                    !attempt.IsSuccessful && attempt.AttemptedAt >= now.Subtract(FailedAttemptWindow))
                .OrderByDescending(attempt => attempt.AttemptedAt)
                .Select(attempt => attempt.AttemptedAt)
                .Take(options.MaxFailedAttemptsPerHour)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var lockoutEndsAt = recentFailures.Count >= options.MaxFailedAttemptsPerHour
                ? recentFailures[0].AddMinutes(options.AccountLockoutDurationMinutes)
                : DateTime.MinValue;
            if (lockoutEndsAt > now)
            {
                var identifierHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)));
                logger.LogWarning("Password sign-in lockout enforced for identifier hash {IdentifierHash} until {LockoutEndsAtUtc}",
                    identifierHash, lockoutEndsAt);
                throw await CreateDenialAsync(timingOrigin, cancellationToken).ConfigureAwait(false);
            }

            // The caller owns the lease through credential work and durable attempt/session recording.
            return lease;
        }
        catch
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<UnauthorizedAccessException> CreateDenialAsync(
        AuthenticationTimingOrigin origin, CancellationToken cancellationToken)
    {
        if (protection is IAuthenticationTimingProtection timingProtection)
        {
            await timingProtection.CompleteAuthenticationTimingAsync(origin, false, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await protection.AddTimingProtectionDelayAsync(false, origin.StartedAtUtc).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new UnauthorizedAccessException(protection.GetGenericErrorMessage("login"));
    }

    private static async Task<IAsyncDisposable?> TryAcquireLockAsync(
        ApplicationDbContext database,
        string normalizedEmail,
        string? normalizedIpAddress,
        CancellationToken cancellationToken)
    {
        var lockKeys = new List<long> { CreateEmailLockKey(normalizedEmail) };
        if (normalizedIpAddress is not null)
        {
            lockKeys.Add(CreateIpLockKey(normalizedIpAddress));
        }

        lockKeys.Sort();

        if (!database.Database.IsRelational())
        {
            var stripes = lockKeys
                .Select(GetLockStripeIndex)
                .Distinct()
                .OrderBy(index => index)
                .Select(index => InProcessLockStripes[index])
                .ToArray();
            var acquiredStripes = new List<SemaphoreSlim>(stripes.Length);

            try
            {
                foreach (var stripe in stripes)
                {
                    await stripe.WaitAsync(cancellationToken).ConfigureAwait(false);
                    acquiredStripes.Add(stripe);
                }

                return new InProcessLockLease(acquiredStripes.ToArray());
            }
            catch
            {
                for (var index = acquiredStripes.Count - 1; index >= 0; index--)
                {
                    acquiredStripes[index].Release();
                }

                throw;
            }
        }

        if (!database.Database.IsNpgsql())
        {
            throw new InvalidOperationException(
                "Account lockout requires PostgreSQL advisory locks for relational database providers.");
        }

        await database.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var connection = database.Database.GetDbConnection();
        var acquiredKeys = new List<long>(lockKeys.Count);

        try
        {
            foreach (var lockKey in lockKeys)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_try_advisory_lock(@lock_key)";
                command.Transaction = database.Database.CurrentTransaction?.GetDbTransaction();

                var parameter = command.CreateParameter();
                parameter.ParameterName = "lock_key";
                parameter.DbType = DbType.Int64;
                parameter.Value = lockKey;
                command.Parameters.Add(parameter);

                var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (result is not true)
                {
                    await ReleasePostgreSqlLocksAsync(database, acquiredKeys).ConfigureAwait(false);
                    await database.Database.CloseConnectionAsync().ConfigureAwait(false);
                    return null;
                }

                acquiredKeys.Add(lockKey);
            }

            return new PostgreSqlLockLease(database, acquiredKeys.ToArray());
        }
        catch
        {
            try
            {
                await ReleasePostgreSqlLocksAsync(database, acquiredKeys).ConfigureAwait(false);
            }
            catch
            {
                // Clearing the connection pool below releases any session advisory locks.
            }

            if (connection is NpgsqlConnection npgsqlConnection)
            {
                NpgsqlConnection.ClearPool(npgsqlConnection);
            }

            await database.Database.CloseConnectionAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static int GetLockStripeIndex(long lockKey) =>
        (int)((ulong)lockKey % (uint)InProcessLockStripes.Length);

    private static long CreateEmailLockKey(string normalizedEmail)
    {
        var scopedIdentifier = $"gameguild:auth:local-sign-in-lockout:v1:{normalizedEmail}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(scopedIdentifier));
        return BinaryPrimitives.ReadInt64BigEndian(digest);
    }

    private static long CreateIpLockKey(string normalizedIpAddress)
    {
        var scopedIdentifier = $"gameguild:auth:local-sign-in-lockout:ip:v1:{normalizedIpAddress}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(scopedIdentifier));
        return BinaryPrimitives.ReadInt64BigEndian(digest);
    }

    private static async Task ReleasePostgreSqlLocksAsync(ApplicationDbContext database, IReadOnlyList<long> lockKeys)
    {
        var connection = database.Database.GetDbConnection();

        for (var index = lockKeys.Count - 1; index >= 0; index--)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(@lock_key)";
            command.Transaction = database.Database.CurrentTransaction?.GetDbTransaction();

            var parameter = command.CreateParameter();
            parameter.ParameterName = "lock_key";
            parameter.DbType = DbType.Int64;
            parameter.Value = lockKeys[index];
            command.Parameters.Add(parameter);

            var unlocked = await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
            if (unlocked is not true)
            {
                throw new InvalidOperationException("The PostgreSQL sign-in lock was not held at release time.");
            }
        }
    }

    private sealed class InProcessLockLease(SemaphoreSlim[] semaphores) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            for (var index = semaphores.Length - 1; index >= 0; index--)
            {
                semaphores[index].Release();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class PostgreSqlLockLease(ApplicationDbContext database, long[] lockKeys) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            var connection = database.Database.GetDbConnection();

            try
            {
                await ReleasePostgreSqlLocksAsync(database, lockKeys).ConfigureAwait(false);
            }
            catch
            {
                if (connection is NpgsqlConnection npgsqlConnection)
                {
                    NpgsqlConnection.ClearPool(npgsqlConnection);
                }

                throw;
            }
            finally
            {
                await database.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }
    }

}
