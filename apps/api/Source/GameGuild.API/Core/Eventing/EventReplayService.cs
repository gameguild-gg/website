using Microsoft.EntityFrameworkCore;
using GameGuild.API.Database;

namespace GameGuild.API.Eventing;

public sealed class EventReplayService(ApplicationDbContext db, TimeProvider timeProvider) : IEventReplayService
{
    public async Task<EventTransportStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var outbox = db.Set<OutboxMessage>().AsNoTracking();
        return new EventTransportStatus(
            await outbox.LongCountAsync(message => message.CompletedAtUtc == null && message.DeadLetteredAtUtc == null, cancellationToken),
            await outbox.LongCountAsync(message => message.CompletedAtUtc != null, cancellationToken),
            await outbox.LongCountAsync(message => message.DeadLetteredAtUtc != null, cancellationToken),
            await db.Set<InboxReceipt>().LongCountAsync(
                receipt => receipt.CompletedAtUtc == null && receipt.DeadLetteredAtUtc == null,
                cancellationToken));
    }

    public async Task<IReadOnlyList<DeadLetterEvent>> GetDeadLettersAsync(
        CancellationToken cancellationToken = default) =>
        await (from receipt in db.Set<InboxReceipt>().AsNoTracking()
               join message in db.Set<OutboxMessage>().AsNoTracking() on receipt.EventId equals message.EventId
               where receipt.DeadLetteredAtUtc != null
               orderby receipt.DeadLetteredAtUtc descending
               select new DeadLetterEvent(
                   receipt.EventId,
                   message.EventName,
                   receipt.ConsumerName,
                   receipt.AttemptCount,
                   receipt.LastError,
                   receipt.DeadLetteredAtUtc!.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> ReplayAsync(
        Guid eventId,
        string? consumerName = null,
        CancellationToken cancellationToken = default)
    {
        var message = await db.Set<OutboxMessage>().FindAsync([eventId], cancellationToken).ConfigureAwait(false);
        if (message is null)
        {
            return false;
        }

        var receipts = await db.Set<InboxReceipt>()
            .Where(receipt => receipt.EventId == eventId
                              && (consumerName == null
                                  ? receipt.DeadLetteredAtUtc != null
                                  : receipt.ConsumerName == consumerName))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (receipts.Count == 0)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var receipt in receipts)
        {
            receipt.AttemptCount = 0;
            receipt.NextAttemptAtUtc = now;
            receipt.CompletedAtUtc = null;
            receipt.DeadLetteredAtUtc = null;
            receipt.LastError = null;
            receipt.UpdatedAtUtc = now;
        }

        message.CompletedAtUtc = null;
        message.DeadLetteredAtUtc = null;
        message.ClaimedUntilUtc = null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
