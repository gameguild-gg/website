using Microsoft.EntityFrameworkCore;
using GameGuild.API.Database;

namespace GameGuild.API.Eventing;

public sealed class InboxStore(ApplicationDbContext db) : IInboxStore
{
    private const int MaxAttempts = 5;

    public async Task<InboxReceipt?> TryLockAsync(
        Guid eventId,
        string consumerName,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        InboxReceipt? receipt;
        if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO "gameguild.integration"."inbox_receipts"
                    ("EventId", "ConsumerName", "AttemptCount", "NextAttemptAtUtc", "UpdatedAtUtc")
                VALUES ({{eventId}}, {{consumerName}}, 0, {{now}}, {{now}})
                ON CONFLICT ("EventId", "ConsumerName") DO NOTHING
                """, cancellationToken).ConfigureAwait(false);
            receipt = await db.Set<InboxReceipt>().FromSqlInterpolated($$"""
                SELECT receipt.*
                FROM "gameguild.integration"."inbox_receipts" receipt
                WHERE receipt."EventId" = {{eventId}}
                  AND receipt."ConsumerName" = {{consumerName}}
                  AND receipt."CompletedAtUtc" IS NULL
                  AND receipt."DeadLetteredAtUtc" IS NULL
                  AND receipt."NextAttemptAtUtc" <= {{now}}
                FOR UPDATE SKIP LOCKED
                """).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            receipt = await db.Set<InboxReceipt>().FindAsync([eventId, consumerName], cancellationToken)
                .ConfigureAwait(false);
            if (receipt is null)
            {
                receipt = new InboxReceipt
                {
                    EventId = eventId,
                    ConsumerName = consumerName,
                    AttemptCount = 0,
                    NextAttemptAtUtc = now,
                    UpdatedAtUtc = now
                };
                db.Add(receipt);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (receipt is not null
            && (receipt.CompletedAtUtc is not null
                || receipt.DeadLetteredAtUtc is not null
                || receipt.NextAttemptAtUtc > now))
        {
            return null;
        }

        return receipt;
    }

    public async Task CompleteAsync(
        InboxReceipt receipt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        receipt.AttemptCount++;
        receipt.CompletedAtUtc = now;
        receipt.LastError = null;
        receipt.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<InboxReceipt> RecordFailureAsync(
        Guid eventId,
        string consumerName,
        Exception exception,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var receipt = await db.Set<InboxReceipt>().FindAsync([eventId, consumerName], cancellationToken)
            .ConfigureAwait(false);
        if (receipt is null)
        {
            receipt = new InboxReceipt
            {
                EventId = eventId,
                ConsumerName = consumerName,
                AttemptCount = 0,
                NextAttemptAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Add(receipt);
        }

        if (receipt.CompletedAtUtc is not null || receipt.DeadLetteredAtUtc is not null)
        {
            return receipt;
        }

        receipt.AttemptCount++;
        receipt.LastError = exception.ToString()[..Math.Min(exception.ToString().Length, 4000)];
        receipt.UpdatedAtUtc = now;
        if (receipt.AttemptCount >= MaxAttempts)
        {
            receipt.DeadLetteredAtUtc = now;
        }
        else
        {
            receipt.NextAttemptAtUtc = now + EventRetryPolicy.GetDelay(receipt.AttemptCount);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return receipt;
    }
}

internal static class EventRetryPolicy
{
    public static TimeSpan GetDelay(int attemptCount) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Math.Max(0, attemptCount)), 900));
}
