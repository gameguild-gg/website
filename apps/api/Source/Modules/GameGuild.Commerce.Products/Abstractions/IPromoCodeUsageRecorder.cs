namespace GameGuild.Commerce.Products;

/// <summary>Outcome of an attempt to record promo code usage</summary>
public enum PromoUsageRecordingStatus
{
    /// <summary>A new usage row was recorded atomically</summary>
    Recorded = 0,

    /// <summary>The idempotency key was already recorded; the existing usage is returned without counting again</summary>
    Replay = 1,

    /// <summary>The code is inactive, missing, soft-deleted, or past its total usage limit (fail closed)</summary>
    RejectedTotalLimit = 2,

    /// <summary>The user has exhausted their per-user limit for this code (fail closed)</summary>
    RejectedPerUserLimit = 3
}

/// <summary>
/// Records promo code usage atomically: guarded counter increments plus the usage row
/// are committed in a single database transaction, with idempotency-key replay protection.
/// </summary>
public interface IPromoCodeUsageRecorder
{
    /// <summary>
    /// Record one usage of a promo code. When <paramref name="idempotencyKey"/> was already
    /// recorded the call is a replay: the existing usage row is returned and nothing is counted
    /// again. Limit breaches fail closed and never persist a row.
    /// </summary>
    /// <param name="promoCode">The promo code being redeemed (limits and tenant are read from it)</param>
    /// <param name="userId">Redeeming user</param>
    /// <param name="discountApplied">The actual discount amount that was applied</param>
    /// <param name="currency">Currency of the discount (ISO 4217)</param>
    /// <param name="idempotencyKey">Caller-supplied replay-protection key</param>
    /// <param name="orderId">Optional order linkage</param>
    /// <param name="orderLineItemId">Optional order line item linkage</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recording status plus the persisted (or replayed) usage row when available</returns>
    Task<(PromoUsageRecordingStatus Status, PromoCodeUse? Usage)> RecordAsync(
        PromoCode promoCode,
        Guid userId,
        decimal discountApplied,
        string currency,
        string? idempotencyKey,
        Guid? orderId = null,
        Guid? orderLineItemId = null,
        CancellationToken cancellationToken = default);
}
