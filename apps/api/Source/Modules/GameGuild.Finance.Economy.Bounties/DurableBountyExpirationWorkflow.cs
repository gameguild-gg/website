using System.Data;
using GameGuild.Finance.Economy.Bounties.Persistence;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Bounties;

public sealed record BountyExpirationBatchResult(
    DateTimeOffset EvaluatedAt,
    IReadOnlyList<BountyId> ExpiredBounties);

public interface IDurableBountyExpirationWorkflow
{
    Task<BountyExpirationBatchResult> ExpireDueAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken = default);
}

public interface IBountyExpirationTransition
{
    Task<bool> PrepareForReclaimAsync(
        BountyId bountyId,
        DateTimeOffset reclaimedAt,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlBountyExpirationWorkflow :
    IDurableBountyExpirationWorkflow,
    IBountyExpirationTransition
{
    private readonly DbContext _db;

    public PostgreSqlBountyExpirationWorkflow(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext ?? throw new InvalidOperationException(
            "PostgreSQL bounty expiration requires the application's relational DbContext.");
    }

    public async Task<BountyExpirationBatchResult> ExpireDueAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        if (batchSize > 1_000)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "A bounty expiration batch cannot exceed 1,000 rows.");

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.ReadCommitted, async _ =>
        {
            var due = await _db.Set<BountyRow>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM public.economy_bounties
                    WHERE "Status" = {(int)BountyStatus.Open}
                      AND "ExpiresAt" <= {now}
                    ORDER BY "ExpiresAt", "Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT {batchSize}
                    """)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var bounty in due)
            {
                bounty.Status = BountyStatus.Expired;
                bounty.Version = checked(bounty.Version + 1);
                _db.Set<BountyExpirationEventRow>().Add(new BountyExpirationEventRow
                {
                    Id = Guid.NewGuid(),
                    BountyId = bounty.Id,
                    ExpiresAt = bounty.ExpiresAt,
                    RecordedAt = now,
                    BountyVersion = bounty.Version
                });
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new BountyExpirationBatchResult(now, due.Select(row => new BountyId(row.Id)).ToArray());
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> PrepareForReclaimAsync(
        BountyId bountyId,
        DateTimeOffset reclaimedAt,
        CancellationToken cancellationToken = default)
    {
        var affected = await _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.economy_bounties
            SET "Status" = {(int)BountyStatus.Open}
            WHERE "Id" = {bountyId.Value}
              AND "Status" = {(int)BountyStatus.Expired}
              AND "ExpiresAt" <= {reclaimedAt};
            """, cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }
}

internal sealed class LegacyOpenBountyExpirationTransition : IBountyExpirationTransition
{
    internal static LegacyOpenBountyExpirationTransition Instance { get; } = new();

    public Task<bool> PrepareForReclaimAsync(
        BountyId bountyId,
        DateTimeOffset reclaimedAt,
        CancellationToken cancellationToken = default) => Task.FromResult(false);
}
