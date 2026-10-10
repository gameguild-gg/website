using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Repository implementation for billing webhook events.
///     Uses ExternalEventId as the idempotency key to prevent duplicate processing.
/// </summary>
public class BillingWebhookRepository(IApplicationDbContext context, ILogger<BillingWebhookRepository> logger) 
    : CommerceRepositoryBase<BillingWebhookEvent>(context), IBillingWebhookRepository
{
    private static readonly HashSet<string> WebhookIdempotencyIndexes = new(StringComparer.Ordinal)
    {
        "ix_billing_webhook_events_external_id_provider",
        "ix_billing_webhook_events_provider_scope_event"
    };
    /// <inheritdoc />
    public new async Task<BillingWebhookEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting webhook event by ID: {Id}", id);
        return await Entities.FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<BillingWebhookEvent?> GetByExternalEventIdAsync(string externalEventId, string provider, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting webhook event by external ID: {ExternalEventId} for provider: {Provider}", externalEventId, provider);
        return await Entities
            .FirstOrDefaultAsync(e => e.ExternalEventId == externalEventId && e.Provider == provider, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<BillingWebhookEvent?> GetByProviderScopeAsync(
        string provider,
        string providerEnvironment,
        string providerAccountId,
        string webhookEndpointId,
        string externalEventId,
        CancellationToken cancellationToken = default)
    {
        return await Entities.FirstOrDefaultAsync(
                webhookEvent => webhookEvent.Provider == provider &&
                                webhookEvent.ProviderEnvironment == providerEnvironment &&
                                webhookEvent.ProviderAccountId == providerAccountId &&
                                webhookEvent.WebhookEndpointId == webhookEndpointId &&
                                webhookEvent.ExternalEventId == externalEventId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BillingWebhookEvent>> GetByProviderAsync(string provider, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting webhook events for provider: {Provider}", provider);
        return await Entities
            .Where(e => e.Provider == provider)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BillingWebhookEvent>> GetFailedEventsAsync(int maxAttempts = 3, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting failed webhook events with max attempts: {MaxAttempts}", maxAttempts);
        return await Entities
            .Where(e => e.IsFailed && e.ProcessingAttempts < maxAttempts)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BillingWebhookEvent>> GetRetryCandidatesAsync(
        int maxAttempts,
        int take,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Getting retry candidates with max attempts {MaxAttempts}, take {Take}",
            maxAttempts,
            take);
        return await Entities
            .Where(e => e.IsFailed && !e.IsProcessed && e.ProcessingAttempts < maxAttempts)
            .OrderBy(e => e.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<BillingWebhookEvent> Items, int TotalCount)> SearchEventsAsync(
        BillingWebhookEventSearchCriteria criteria,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var query = Entities.AsQueryable();

        query = criteria.Status switch
        {
            BillingEventStatusFilter.Processed => query.Where(e => e.IsProcessed && !e.IsFailed),
            BillingEventStatusFilter.Failed => query.Where(e => e.IsFailed),
            BillingEventStatusFilter.Pending => query.Where(e => !e.IsProcessed && !e.IsFailed),
            _ => query
        };

        if (criteria.Provider is not null)
        {
            query = query.Where(e => e.Provider == criteria.Provider);
        }

        if (criteria.EventType is not null)
        {
            query = query.Where(e => e.EventType == criteria.EventType);
        }

        if (criteria.FromUtc.HasValue)
        {
            var fromUtc = criteria.FromUtc.Value;
            query = query.Where(e => e.CreatedAt >= fromUtc);
        }

        if (criteria.ToUtc.HasValue)
        {
            var toUtc = criteria.ToUtc.Value;
            query = query.Where(e => e.CreatedAt <= toUtc);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public new async Task<BillingWebhookEvent> CreateAsync(BillingWebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);
        
        var existingEvent = webhookEvent.ProviderEnvironment is not null &&
                            webhookEvent.ProviderAccountId is not null &&
                            webhookEvent.WebhookEndpointId is not null
            ? await GetByProviderScopeAsync(
                    webhookEvent.Provider,
                    webhookEvent.ProviderEnvironment,
                    webhookEvent.ProviderAccountId,
                    webhookEvent.WebhookEndpointId,
                    webhookEvent.ExternalEventId,
                    cancellationToken)
                .ConfigureAwait(false)
            : await GetByExternalEventIdAsync(webhookEvent.ExternalEventId, webhookEvent.Provider, cancellationToken)
                .ConfigureAwait(false);
        if (existingEvent is not null)
        {
            logger.LogWarning("Duplicate webhook event detected: {ExternalEventId} for provider: {Provider}. Returning existing event.", 
                webhookEvent.ExternalEventId, webhookEvent.Provider);
            return existingEvent;
        }
        
        logger.LogInformation("Creating webhook event: {ExternalEventId} for provider: {Provider}", webhookEvent.ExternalEventId, webhookEvent.Provider);
        await Entities.AddAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
        try
        {
            await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return webhookEvent;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            Entities.Remove(webhookEvent);
            var concurrentWinner = webhookEvent.ProviderEnvironment is not null &&
                                   webhookEvent.ProviderAccountId is not null &&
                                   webhookEvent.WebhookEndpointId is not null
                ? await GetByProviderScopeAsync(
                        webhookEvent.Provider,
                        webhookEvent.ProviderEnvironment,
                        webhookEvent.ProviderAccountId,
                        webhookEvent.WebhookEndpointId,
                        webhookEvent.ExternalEventId,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await GetByExternalEventIdAsync(
                        webhookEvent.ExternalEventId,
                        webhookEvent.Provider,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (concurrentWinner is not null)
            {
                logger.LogWarning(
                    "Concurrent duplicate webhook event detected: {ExternalEventId} for provider: {Provider}. Returning winner.",
                    webhookEvent.ExternalEventId,
                    webhookEvent.Provider);
                return concurrentWinner;
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryClaimProcessingAsync(
        BillingWebhookEvent webhookEvent,
        DateTime staleBefore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);

        if (!webhookEvent.TryBeginProcessing(staleBefore))
        {
            return false;
        }

        Entities.Update(webhookEvent);
        try
        {
            await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation(
                "Webhook processing claim lost for {ExternalEventId} from {Provider}.",
                webhookEvent.ExternalEventId,
                webhookEvent.Provider);
            return false;
        }
    }

    /// <inheritdoc />
    public new async Task<BillingWebhookEvent> UpdateAsync(BillingWebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);
        logger.LogInformation("Updating webhook event: {Id}", webhookEvent.Id);
        
        Entities.Update(webhookEvent);
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return webhookEvent;
    }

    /// <inheritdoc />
    public new async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting webhook event: {Id}", id);
        
        var webhookEvent = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (webhookEvent is not null)
        {
            Entities.Remove(webhookEvent);
            await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string externalEventId, string provider, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Checking if webhook event exists: {ExternalEventId} for provider: {Provider}", externalEventId, provider);
        return await Entities
            .AnyAsync(e => e.ExternalEventId == externalEventId && e.Provider == provider, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is not DbException { SqlState: "23505" } databaseException)
            {
                continue;
            }

            var constraintName = databaseException.GetType()
                .GetProperty("ConstraintName")
                ?.GetValue(databaseException) as string;
            return constraintName is not null &&
                   WebhookIdempotencyIndexes.Contains(constraintName);
        }

        return false;
    }
}
