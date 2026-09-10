using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GameGuild.API.Database;

namespace GameGuild.API.Eventing;

public sealed class OutboxDispatcher(
    ApplicationDbContext db,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher> logger) : IOutboxDispatcher
{
    private const int BatchSize = 50;
    private static readonly TimeSpan ClaimDuration = TimeSpan.FromMinutes(2);

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var now = timeProvider.GetUtcNow();
        var messages = await ClaimAsync(now, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                await DispatchAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await RecordTransportFailureAsync(message, exception, cancellationToken).ConfigureAwait(false);
            }
        }

        EventTransportMetrics.RecordDispatchCycle(messages.Count, stopwatch.Elapsed);
        return messages.Count;
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var executionStrategy = db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            var isPostgres = db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;
            await using var transaction = isPostgres
                ? await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
                : null;

            List<OutboxMessage> messages;
            if (isPostgres)
            {
                messages = await db.Set<OutboxMessage>().FromSqlInterpolated($$"""
                SELECT message.*
                FROM "gameguild.integration"."outbox_messages" AS message
                WHERE message."CompletedAtUtc" IS NULL
                  AND message."DeadLetteredAtUtc" IS NULL
                  AND (message."ClaimedUntilUtc" IS NULL OR message."ClaimedUntilUtc" <= {{now}})
                  AND (
                    NOT EXISTS (
                      SELECT 1 FROM "gameguild.integration"."inbox_receipts" receipt
                      WHERE receipt."EventId" = message."EventId")
                    OR EXISTS (
                      SELECT 1 FROM "gameguild.integration"."inbox_receipts" receipt
                      WHERE receipt."EventId" = message."EventId"
                        AND receipt."CompletedAtUtc" IS NULL
                        AND receipt."DeadLetteredAtUtc" IS NULL
                        AND receipt."NextAttemptAtUtc" <= {{now}})
                    OR NOT EXISTS (
                      SELECT 1 FROM "gameguild.integration"."inbox_receipts" receipt
                      WHERE receipt."EventId" = message."EventId"
                        AND receipt."CompletedAtUtc" IS NULL
                        AND receipt."DeadLetteredAtUtc" IS NULL))
                  AND NOT EXISTS (
                    SELECT 1
                    FROM "gameguild.integration"."outbox_messages" earlier
                    WHERE earlier."TenantId" = message."TenantId"
                      AND earlier."AggregateType" = message."AggregateType"
                      AND earlier."AggregateId" = message."AggregateId"
                      AND earlier."CompletedAtUtc" IS NULL
                      AND (earlier."OccurredAtUtc", earlier."EventId") < (message."OccurredAtUtc", message."EventId"))
                ORDER BY message."OccurredAtUtc", message."EventId"
                LIMIT {{BatchSize}}
                FOR UPDATE SKIP LOCKED
                """).ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var receipts = await db.Set<InboxReceipt>().AsNoTracking().ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                var candidates = await db.Set<OutboxMessage>()
                    .Where(message => message.CompletedAtUtc == null
                                      && message.DeadLetteredAtUtc == null
                                      && (message.ClaimedUntilUtc == null || message.ClaimedUntilUtc <= now))
                    .OrderBy(message => message.OccurredAtUtc)
                    .ThenBy(message => message.EventId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                var blockers = await db.Set<OutboxMessage>().AsNoTracking()
                    .Where(message => message.CompletedAtUtc == null)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                messages = candidates
                    .Where(message =>
                    {
                        var eventReceipts = receipts.Where(receipt => receipt.EventId == message.EventId).ToList();
                        var isDue = eventReceipts.Count == 0 || eventReceipts.All(receipt =>
                                receipt.CompletedAtUtc is not null || receipt.DeadLetteredAtUtc is not null)
                            || eventReceipts.Any(receipt =>
                                receipt.CompletedAtUtc is null
                                && receipt.DeadLetteredAtUtc is null
                                && receipt.NextAttemptAtUtc <= now);
                        var hasEarlierBlocker = blockers.Any(earlier =>
                            earlier.EventId != message.EventId
                            && earlier.TenantId == message.TenantId
                            && earlier.AggregateType == message.AggregateType
                            && earlier.AggregateId == message.AggregateId
                            && (earlier.OccurredAtUtc < message.OccurredAtUtc
                                || earlier.OccurredAtUtc == message.OccurredAtUtc
                                && string.CompareOrdinal(earlier.EventId.ToString(), message.EventId.ToString()) < 0));
                        return isDue && !hasEarlierBlocker;
                    })
                    .OrderBy(message => message.OccurredAtUtc)
                    .ThenBy(message => message.EventId)
                    .Take(BatchSize)
                    .ToList();
            }

            foreach (var message in messages)
            {
                message.ClaimedUntilUtc = now + ClaimDuration;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return messages;
        }).ConfigureAwait(false);
    }

    private async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var integrationEvent = DurableEventSerializer.Deserialize(message);
        var handlerContract = typeof(IIntegrationEventHandler<>).MakeGenericType(integrationEvent.GetType());
        await using var discoveryScope = scopeFactory.CreateAsyncScope();
        var handlerTypes = ((IEnumerable<object>?)discoveryScope.ServiceProvider.GetService(
                typeof(IEnumerable<>).MakeGenericType(handlerContract)))?.Select(handler => handler.GetType()).ToList() ?? [];
        handlerTypes = handlerTypes
            .GroupBy(handlerType => handlerType.FullName ?? handlerType.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        foreach (var handlerType in handlerTypes)
        {
            await DispatchConsumerAsync(
                    message.EventId,
                    integrationEvent,
                    handlerContract,
                    handlerType,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var receipts = await db.Set<InboxReceipt>().AsNoTracking()
            .Where(receipt => receipt.EventId == message.EventId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var finishedAt = timeProvider.GetUtcNow();
        var currentConsumerNames = handlerTypes
            .Select(handlerType => handlerType.FullName ?? handlerType.Name)
            .ToHashSet(StringComparer.Ordinal);
        var currentReceipts = receipts
            .Where(receipt => currentConsumerNames.Contains(receipt.ConsumerName))
            .ToList();
        var allConsumersTerminal = currentReceipts.Count == currentConsumerNames.Count
            && currentReceipts.All(receipt =>
                receipt.CompletedAtUtc is not null || receipt.DeadLetteredAtUtc is not null);
        if (currentConsumerNames.Count == 0
            || allConsumersTerminal && currentReceipts.All(receipt => receipt.CompletedAtUtc is not null))
        {
            message.CompletedAtUtc = finishedAt;
        }
        else if (allConsumersTerminal && currentReceipts.Any(receipt => receipt.DeadLetteredAtUtc is not null))
        {
            message.DeadLetteredAtUtc = finishedAt;
        }

        message.ClaimedUntilUtc = null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchConsumerAsync(
        Guid eventId,
        IDurableIntegrationEvent integrationEvent,
        Type handlerContract,
        Type handlerType,
        CancellationToken cancellationToken)
    {
        var consumerName = handlerType.FullName ?? handlerType.Name;
        Exception? failure = null;
        await using (var consumerScope = scopeFactory.CreateAsyncScope())
        {
            var consumerDb = consumerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var executionStrategy = consumerDb.Database.CreateExecutionStrategy();
            var completed = await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = consumerDb.Database.IsRelational()
                    ? await consumerDb.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
                    : null;
                try
                {
                    var consumerInbox = consumerScope.ServiceProvider.GetRequiredService<IInboxStore>();
                    var receipt = await consumerInbox.TryLockAsync(
                            eventId,
                            consumerName,
                            timeProvider.GetUtcNow(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (receipt is null)
                    {
                        if (transaction is not null)
                        {
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        }

                        return true;
                    }

                    var handler = ((IEnumerable<object>?)consumerScope.ServiceProvider.GetService(
                            typeof(IEnumerable<>).MakeGenericType(handlerContract)))
                        ?.First(candidate => candidate.GetType() == handlerType)
                        ?? throw new InvalidOperationException($"Consumer '{consumerName}' could not be resolved.");
                    var task = (Task?)handlerContract.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!
                        .Invoke(handler, [integrationEvent, cancellationToken]);
                    await (task ?? throw new InvalidOperationException($"Consumer '{consumerName}' returned no task."))
                        .ConfigureAwait(false);
                    await consumerInbox.CompleteAsync(receipt, timeProvider.GetUtcNow(), cancellationToken)
                        .ConfigureAwait(false);
                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    EventTransportMetrics.RecordConsumerCompletion(integrationEvent.EventName, consumerName);
                    return true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    if (transaction is not null)
                    {
                        await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    }

                    throw;
                }
                catch (Exception exception)
                {
                    failure = exception is TargetInvocationException { InnerException: not null }
                        ? exception.InnerException
                        : exception;
                    if (transaction is not null)
                    {
                        await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    }

                    return false;
                }
            }).ConfigureAwait(false);
            if (completed) return;
        }

        await using var failureScope = scopeFactory.CreateAsyncScope();
        var failureInbox = failureScope.ServiceProvider.GetRequiredService<IInboxStore>();
        var failureReceipt = await failureInbox.RecordFailureAsync(
                eventId,
                consumerName,
                failure!,
                timeProvider.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);
        EventTransportMetrics.RecordConsumerFailure(
            integrationEvent.EventName,
            consumerName,
            failureReceipt.DeadLetteredAtUtc is not null);
        logger.LogWarning(
            failure,
            "Integration event consumer {ConsumerName} failed for event {EventId}",
            consumerName,
            eventId);
    }

    private async Task RecordTransportFailureAsync(
        OutboxMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        const string consumerName = "GameGuild.EventTransport";
        await using (var failureScope = scopeFactory.CreateAsyncScope())
        {
            var failureInbox = failureScope.ServiceProvider.GetRequiredService<IInboxStore>();
            var receipt = await failureInbox.RecordFailureAsync(
                    message.EventId,
                    consumerName,
                    exception,
                    timeProvider.GetUtcNow(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (receipt.DeadLetteredAtUtc is not null)
            {
                message.DeadLetteredAtUtc = receipt.DeadLetteredAtUtc;
            }

            EventTransportMetrics.RecordConsumerFailure(
                message.EventName,
                consumerName,
                receipt.DeadLetteredAtUtc is not null);
        }

        message.ClaimedUntilUtc = null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogError(exception, "Integration event transport failed for event {EventId}", message.EventId);
    }
}
