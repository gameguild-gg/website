namespace GameGuild.Commerce.Payments;

/// <summary>
///     Repository interface for Payment entity data access
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    ///     Gets a payment by ID
    /// </summary>
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a payment by idempotency key (for duplicate detection)
    /// </summary>
    Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a payment by external payment ID
    /// </summary>
    Task<Payment?> GetByExternalPaymentIdAsync(string externalPaymentId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a payment by its complete immutable provider mapping.
    /// </summary>
    Task<Payment?> GetByProviderMappingAsync(
        string provider,
        string providerEnvironment,
        string providerAccountId,
        string providerObjectId,
        string providerObjectType,
        string providerMonetaryLeg,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all payments for a tenant
    /// </summary>
    Task<IEnumerable<Payment>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all payments for a subscription
    /// </summary>
    Task<IEnumerable<Payment>> GetBySubscriptionIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all payments for an order
    /// </summary>
    Task<IEnumerable<Payment>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all payments for an invoice
    /// </summary>
    Task<IEnumerable<Payment>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets payments by status
    /// </summary>
    Task<IEnumerable<Payment>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets payments due for retry: failed, retriable, with budget left and a scheduled
    ///     next-retry time that has passed (issue #415 retry-queue claim query).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="take">Optional cap on the number of payments returned per pass.</param>
    Task<IEnumerable<Payment>> GetDueForRetryAsync(CancellationToken cancellationToken = default, int? take = null);

    /// <summary>
    ///     Gets the persisted retry-attempt history for a payment, ordered by attempt number
    ///     (issue #415).
    /// </summary>
    Task<IReadOnlyList<PaymentRetryAttempt>> GetRetryAttemptsAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Persists one retry-attempt history entry (issue #415).
    /// </summary>
    Task AddRetryAttemptAsync(
        PaymentRetryAttempt attempt,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets payments within a date range
    /// </summary>
    Task<IEnumerable<Payment>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets payment count by status for a tenant
    /// </summary>
    Task<Dictionary<PaymentStatus, int>> GetCountByStatusAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets total revenue for a period
    /// </summary>
    Task<decimal> GetRevenueForPeriodAsync(
        DateTime startDate,
        DateTime endDate,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a new payment
    /// </summary>
    Task<Payment> AddAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing payment
    /// </summary>
    Task<Payment> UpdateAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a payment exists with the given idempotency key
    /// </summary>
    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}
