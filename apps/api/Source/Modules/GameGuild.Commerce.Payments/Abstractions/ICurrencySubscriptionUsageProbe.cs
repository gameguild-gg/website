namespace GameGuild.Commerce.Payments;

/// <summary>
///     Cross-module seam (issue #412 spec A): counts active subscriptions still holding a
///     currency so <c>DisableCurrency</c> can warn-and-require-confirmation. Payments owns the
///     abstraction because it owns the currency configuration; the Subscriptions module
///     (which references Payments) provides the data-backed implementation. This mirrors the
///     IPlanPricingResolver / ISubscriptionPaymentContextService seam pattern.
/// </summary>
public interface ICurrencySubscriptionUsageProbe
{
    /// <summary>
    ///     Counts active subscriptions for the tenant whose price is denominated in
    ///     <paramref name="currencyCode"/>. Returns null when usage cannot be determined —
    ///     callers must treat null fail-closed (as "possibly in use").
    /// </summary>
    /// <param name="tenantId">Tenant scope.</param>
    /// <param name="currencyCode">ISO 4217 code, upper-case.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int?> CountActiveSubscriptionsUsingCurrencyAsync(Guid tenantId, string currencyCode, CancellationToken cancellationToken = default);
}

/// <summary>
///     Default probe used when the Subscriptions module is not part of the host: usage is
///     unknown, and <c>DisableCurrency</c> requires explicit confirmation (fail-closed — an
///     in-use currency must never be silently disabled because the usage check was missing).
/// </summary>
public sealed class UnknownCurrencySubscriptionUsageProbe : ICurrencySubscriptionUsageProbe
{
    /// <inheritdoc />
    public Task<int?> CountActiveSubscriptionsUsingCurrencyAsync(Guid tenantId, string currencyCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<int?>(null);
    }
}
