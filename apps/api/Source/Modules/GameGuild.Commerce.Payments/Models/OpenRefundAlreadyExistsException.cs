namespace GameGuild.Commerce.Payments;

/// <summary>
///     Thrown when a refund insert loses the storage-level single-open-refund race
///     (issue #419): another non-terminal refund row for the same payment committed first.
/// </summary>
public sealed class OpenRefundAlreadyExistsException(Guid paymentId, Exception innerException)
    : InvalidOperationException(
        $"Payment {paymentId} already has an open refund; retry or cancel it before requesting another refund",
        innerException)
{
    public Guid PaymentId { get; } = paymentId;
}
