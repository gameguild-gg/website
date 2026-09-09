using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Assessments.Grading.Persistence;

public sealed record AcademicOutboxEvent(
    Guid Id,
    Guid TenantId,
    string EventType,
    string EventSchemaVersion,
    string PayloadCanonicalJson,
    string PayloadHash,
    DateTime OccurredAt);

/// <summary>
/// Consumers must deduplicate side effects by <see cref="AcademicOutboxEvent.Id"/>.
/// The delivery receipt is persisted only after the consumer returns successfully.
/// </summary>
public interface IAcademicOutboxConsumer
{
    string Key { get; }
    IReadOnlySet<string> EventTypes { get; }
    Task ConsumeAsync(AcademicOutboxEvent message, CancellationToken cancellationToken);
}

public interface IAcademicOutboxWriter
{
    AcademicOutboxMessage Enqueue(Guid tenantId, string eventType, string schemaVersion, string payloadCanonicalJson);
}

public sealed class AcademicOutboxWriter(
    IApplicationDbContext context,
    IEnumerable<IAcademicOutboxConsumer> consumers) : IAcademicOutboxWriter
{
    private readonly IReadOnlyDictionary<string, IAcademicOutboxConsumer> _consumers = consumers
        .ToDictionary(consumer => RequireKey(consumer.Key), StringComparer.Ordinal);

    public AcademicOutboxMessage Enqueue(
        Guid tenantId,
        string eventType,
        string schemaVersion,
        string payloadCanonicalJson)
    {
        var message = AcademicOutboxMessage.Create(tenantId, eventType, schemaVersion, payloadCanonicalJson);
        context.Set<AcademicOutboxMessage>().Add(message);

        var route = _consumers.Values
            .Where(consumer => consumer.EventTypes.Contains(eventType))
            .OrderBy(consumer => consumer.Key, StringComparer.Ordinal)
            .Select(consumer => consumer.Key)
            .ToArray();
        foreach (var consumerKey in route)
        {
            context.Set<AcademicOutboxDelivery>().Add(AcademicOutboxDelivery.Create(message.Id, consumerKey));
        }

        // The empty route is itself frozen. Consumers registered in a later deploy
        // must not receive historical events implicitly.
        if (route.Length == 0) message.MarkCompleted(SystemClock.UtcNow);
        return message;
    }

    private static string RequireKey(string key) =>
        string.IsNullOrWhiteSpace(key)
            ? throw new InvalidOperationException("Academic outbox consumer key is required.")
            : key;
}

public interface IAcademicOutboxDispatcher
{
    Task<int> DispatchBatchAsync(CancellationToken cancellationToken = default);
}

public sealed class AcademicOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<AcademicOutboxDispatcher> logger) : IAcademicOutboxDispatcher
{
    private const int BatchSize = 25;
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(5);

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken = default)
    {
        await using var discoveryScope = scopeFactory.CreateAsyncScope();
        var discoveryContext = discoveryScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var now = SystemClock.UtcNow;
        var deliveryIds = await discoveryContext.Set<AcademicOutboxDelivery>()
            .AsNoTracking()
            .Where(delivery =>
                (delivery.Status == AcademicOutboxDeliveryStatus.Pending ||
                 delivery.Status == AcademicOutboxDeliveryStatus.Failed ||
                 delivery.Status == AcademicOutboxDeliveryStatus.Processing && delivery.ClaimedAt < now - ClaimTimeout) &&
                (!delivery.NextAttemptAt.HasValue || delivery.NextAttemptAt <= now))
            .OrderBy(delivery => delivery.NextAttemptAt)
            .ThenBy(delivery => delivery.Id)
            .Select(delivery => delivery.Id)
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var processed = 0;
        foreach (var deliveryId in deliveryIds)
        {
            if (await DispatchOneAsync(deliveryId, cancellationToken).ConfigureAwait(false)) processed++;
        }
        return processed;
    }

    private async Task<bool> DispatchOneAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var delivery = await context.Set<AcademicOutboxDelivery>()
            .FirstOrDefaultAsync(value => value.Id == deliveryId, cancellationToken)
            .ConfigureAwait(false);
        if (delivery is null || delivery.Status == AcademicOutboxDeliveryStatus.Confirmed) return false;
        var message = await context.Set<AcademicOutboxMessage>()
            .FirstOrDefaultAsync(value => value.Id == delivery.OutboxMessageId, cancellationToken)
            .ConfigureAwait(false);
        if (message is null) return false;

        var workerId = $"{Environment.MachineName}:{Environment.ProcessId}";
        try
        {
            delivery.Claim(workerId, SystemClock.UtcNow);
            message.MarkProcessing();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        var consumer = scope.ServiceProvider.GetServices<IAcademicOutboxConsumer>()
            .SingleOrDefault(value => string.Equals(value.Key, delivery.ConsumerKey, StringComparison.Ordinal));
        if (consumer is null)
        {
            await RecordFailureAsync(context, delivery, message, "The frozen consumer is not registered in this deployment.", cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        try
        {
            await consumer.ConsumeAsync(ToEvent(message), cancellationToken).ConfigureAwait(false);
            delivery.Confirm(SystemClock.UtcNow);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var hasPendingDelivery = await context.Set<AcademicOutboxDelivery>()
                .AsNoTracking()
                .AnyAsync(value => value.OutboxMessageId == message.Id && value.Status != AcademicOutboxDeliveryStatus.Confirmed, cancellationToken)
                .ConfigureAwait(false);
            if (!hasPendingDelivery)
            {
                message.MarkCompleted(SystemClock.UtcNow);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Academic outbox delivery {DeliveryId} failed", delivery.Id);
            await RecordFailureAsync(context, delivery, message, exception.Message, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static async Task RecordFailureAsync(
        IApplicationDbContext context,
        AcademicOutboxDelivery delivery,
        AcademicOutboxMessage message,
        string error,
        CancellationToken cancellationToken)
    {
        var delaySeconds = Math.Min(300, 1 << Math.Min(delivery.AttemptCount, 8));
        delivery.Fail(error, SystemClock.UtcNow.AddSeconds(delaySeconds));
        message.MarkFailed();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static AcademicOutboxEvent ToEvent(AcademicOutboxMessage message) => new(
        message.Id,
        message.TenantId,
        message.EventType,
        message.EventSchemaVersion,
        message.PayloadCanonicalJson,
        message.PayloadHash,
        message.OccurredAt);
}

public sealed class AcademicOutboxBackgroundService(
    IAcademicOutboxDispatcher dispatcher,
    ILogger<AcademicOutboxBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await dispatcher.DispatchBatchAsync(stoppingToken).ConfigureAwait(false);
                if (processed == 0) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Academic outbox dispatch cycle failed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
