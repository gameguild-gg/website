namespace GameGuild.Commerce.Payments;

/// <summary>
///     Persistence for the tenant-scoped payment-method registry (issue #414).
/// </summary>
public interface IPaymentMethodRepository
{
    /// <summary>
    ///     Loads a live (non-deleted) payment method by ID.
    /// </summary>
    Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads a live (non-deleted) payment method by gateway reference within a tenant.
    /// </summary>
    Task<PaymentMethod?> GetByGatewayIdAsync(Guid tenantId, string gatewayProvider, string gatewayPaymentMethodId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists every live (non-deleted) payment method across tenants that references the
    ///     given gateway token. Gateway payment-method ids are globally unique per provider,
    ///     so provider webhooks (for example Stripe <c>payment_method.updated</c>, issue #414)
    ///     resolve their target through this cross-tenant lookup instead of a tenant hint.
    /// </summary>
    Task<IReadOnlyList<PaymentMethod>> ListByGatewayIdAsync(string gatewayProvider, string gatewayPaymentMethodId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads a soft-deleted payment method by gateway reference within a tenant
    ///     (used to restore a previously removed token).
    /// </summary>
    Task<PaymentMethod?> GetDeletedByGatewayIdAsync(Guid tenantId, string gatewayProvider, string gatewayPaymentMethodId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the live payment methods for a tenant, optionally scoped to one owner.
    /// </summary>
    Task<IReadOnlyList<PaymentMethod>> ListAsync(Guid tenantId, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads the owner's current default (live) payment method in a tenant, if any.
    /// </summary>
    Task<PaymentMethod?> GetDefaultAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads all live card payment methods that have an expiry date (expiry monitor).
    /// </summary>
    Task<IReadOnlyList<PaymentMethod>> ListExpiringCandidatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a new payment method.
    /// </summary>
    Task AddAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Persists changes to an existing payment method.
    /// </summary>
    Task<PaymentMethod> UpdateAsync(PaymentMethod paymentMethod, CancellationToken cancellationToken = default);
}
