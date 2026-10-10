namespace GameGuild.Commerce.Billing;

/// <summary>
///     Filter criteria for searching the durable billing webhook inbox (issue #396).
///     Null members are not filtered.
/// </summary>
/// <param name="Status">Normalized status filter: processed, failed or pending.</param>
/// <param name="Provider">Lowercase provider filter.</param>
/// <param name="EventType">Exact provider event type filter.</param>
/// <param name="FromUtc">Inclusive lower bound on CreatedAt.</param>
/// <param name="ToUtc">Inclusive upper bound on CreatedAt.</param>
public sealed record BillingWebhookEventSearchCriteria(
    string? Status = null,
    string? Provider = null,
    string? EventType = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

/// <summary>
///     Repository for managing billing webhook events
/// </summary>
public interface IBillingWebhookRepository
{
    /// <summary>
    ///     Get a webhook event by ID
    /// </summary>
    Task<BillingWebhookEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a webhook event by external event ID
    /// </summary>
    Task<BillingWebhookEvent?> GetByExternalEventIdAsync(string externalEventId, string provider, CancellationToken cancellationToken = default);

    /// <summary>Get an event by its provider-scoped idempotency identity.</summary>
    Task<BillingWebhookEvent?> GetByProviderScopeAsync(
        string provider,
        string providerEnvironment,
        string providerAccountId,
        string webhookEndpointId,
        string externalEventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all webhook events for a provider
    /// </summary>
    Task<IEnumerable<BillingWebhookEvent>> GetByProviderAsync(string provider, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get failed webhook events that need retry
    /// </summary>
    Task<IEnumerable<BillingWebhookEvent>> GetFailedEventsAsync(int maxAttempts = 3, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get failed webhook events that are candidates for the asynchronous retry worker:
    ///     failed, not yet processed, and below the retry attempt ceiling.
    /// </summary>
    Task<IEnumerable<BillingWebhookEvent>> GetRetryCandidatesAsync(
        int maxAttempts,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Search the webhook inbox with the given criteria, newest first, and count the
    ///     total matching rows for pagination.
    /// </summary>
    Task<(IReadOnlyList<BillingWebhookEvent> Items, int TotalCount)> SearchEventsAsync(
        BillingWebhookEventSearchCriteria criteria,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Create a new webhook event
    /// </summary>
    Task<BillingWebhookEvent> CreateAsync(BillingWebhookEvent webhookEvent, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Atomically claims an accepted webhook for processing.
    /// </summary>
    Task<bool> TryClaimProcessingAsync(
        BillingWebhookEvent webhookEvent,
        DateTime staleBefore,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Update an existing webhook event
    /// </summary>
    Task<BillingWebhookEvent> UpdateAsync(BillingWebhookEvent webhookEvent, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete a webhook event
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if a webhook event with the given external ID already exists
    /// </summary>
    Task<bool> ExistsAsync(string externalEventId, string provider, CancellationToken cancellationToken = default);
}
