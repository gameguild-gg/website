namespace GameGuild.Commerce.Payments;

/// <summary>
///     Persistence for <see cref="Refund"/> rows (issue #419). The payment's
///     <see cref="Payment.RefundedAmount"/> stays the cumulative aggregate of completed refunds.
/// </summary>
public interface IRefundRepository
{
    /// <summary>Gets a refund by id.</summary>
    Task<Refund?> GetByIdAsync(Guid refundId, CancellationToken cancellationToken = default);

    /// <summary>Gets the refund for a payment with the given retry-safe idempotency key, if any.</summary>
    Task<Refund?> GetByPaymentAndIdempotencyKeyAsync(Guid paymentId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Lists refunds, newest first, with optional tenant/status/period filters and paging.</summary>
    Task<(IReadOnlyList<Refund> Items, int Total)> ListAsync(
        Guid? tenantId = null,
        RefundStatus? status = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new refund row. Returns the existing row when the idempotency key already resolved.</summary>
    Task<Refund> AddAsync(Refund refund, CancellationToken cancellationToken = default);

    /// <summary>Persists refund state changes.</summary>
    Task<Refund> UpdateAsync(Refund refund, CancellationToken cancellationToken = default);
}
