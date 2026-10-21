namespace GameGuild.Commerce.Billing;

/// <summary>
///     Provider-specific execution arm of the billing webhook retry pipeline (issue #408).
///     A replay re-routes a previously accepted inbox row through the provider's event
///     routing. Implementations must fail closed for rows without retained proof of
///     signature verification.
/// </summary>
public interface IBillingWebhookReplayHandler
{
    /// <summary>Provider this handler replays (matches <see cref="PaymentProviders"/> keys).</summary>
    string Provider { get; }

    /// <summary>
    ///     Replays the stored inbox row. The handler owns the row's terminal state
    ///     transition (processed / failed) and its integration-event publication.
    /// </summary>
    /// <param name="webhookEvent">Stored inbox row to replay.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processing result of the replay attempt.</returns>
    Task<WebhookProcessingResult> ReplayAsync(
        BillingWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Resolves the replay handler for a stored inbox row's provider (issue #408). Rows
///     whose provider has no replay handler surface an explicit non-retryable failure
///     instead of being silently "requeued" without execution.
/// </summary>
public sealed class BillingWebhookReplayDispatcher(IEnumerable<IBillingWebhookReplayHandler> handlers)
{
    private readonly IReadOnlyList<IBillingWebhookReplayHandler> _handlers =
        handlers.ToArray();

    /// <summary>Gets every registered replay handler, keyed by provider.</summary>
    public IReadOnlyList<string> SupportedProviders =>
        _handlers.Select(handler => handler.Provider).ToArray();

    /// <summary>Resolves the replay handler for the given provider, or null when unsupported.</summary>
    public IBillingWebhookReplayHandler? Resolve(string provider) =>
        _handlers.FirstOrDefault(handler =>
            string.Equals(handler.Provider, provider, StringComparison.OrdinalIgnoreCase));
}
