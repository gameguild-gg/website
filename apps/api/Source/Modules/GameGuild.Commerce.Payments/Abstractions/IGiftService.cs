namespace GameGuild.Commerce.Payments;

/// <summary>Public (non-secret) view of a gift for the redemption preview endpoint.</summary>
/// <param name="Id">Gift id.</param>
/// <param name="Status">Current status machine state.</param>
/// <param name="Amount">Monetary value.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="DurationMonths">Gift duration in months.</param>
/// <param name="PlanId">Optional plan restriction.</param>
/// <param name="GiverDisplayName">Masked giver identity (local part of the giver email is never exposed; v1 shows a display label).</param>
/// <param name="RecipientName">Optional recipient display name.</param>
/// <param name="PersonalMessage">Optional personal message.</param>
/// <param name="DeliveryDate">Optional scheduled delivery date.</param>
/// <param name="ExpiresAt">Redemption deadline (purchase + 1 year).</param>
/// <param name="RedemptionCodePrefix">Non-secret support prefix.</param>
public sealed record GiftPublicView(
    Guid Id,
    GiftStatus Status,
    decimal Amount,
    string Currency,
    int DurationMonths,
    Guid? PlanId,
    string GiverDisplayName,
    string? RecipientName,
    string? PersonalMessage,
    DateTime? DeliveryDate,
    DateTime ExpiresAt,
    string RedemptionCodePrefix);

/// <summary>Request to purchase a gift subscription.</summary>
/// <param name="TenantId">Tenant of the giver (actor-derived at the boundary; kept explicit for the service contract).</param>
/// <param name="GiverUserId">Giver (actor-derived at the boundary).</param>
/// <param name="RecipientEmail">Recipient email address.</param>
/// <param name="Amount">Positive monetary value.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="DurationMonths">Duration 1-36 months.</param>
/// <param name="PlanId">Optional plan restriction.</param>
/// <param name="RecipientName">Optional recipient display name.</param>
/// <param name="PersonalMessage">Optional personal message (max 500 chars).</param>
/// <param name="DeliveryDate">Optional scheduled delivery date (redemption blocked before).</param>
/// <param name="PaymentId">Optional payment reference.</param>
public sealed record PurchaseGiftRequest(
    Guid TenantId,
    Guid GiverUserId,
    string RecipientEmail,
    decimal Amount,
    string Currency,
    int DurationMonths,
    Guid? PlanId = null,
    string? RecipientName = null,
    string? PersonalMessage = null,
    DateTime? DeliveryDate = null,
    Guid? PaymentId = null);

/// <summary>Result of a gift purchase: the gift plus the full redemption code shown exactly once.</summary>
/// <param name="Gift">Public view of the created gift.</param>
/// <param name="RedemptionCode">Full redemption code — only returned at purchase time.</param>
public sealed record GiftPurchaseResult(GiftPublicView Gift, string RedemptionCode);

/// <summary>
///     Gift subscription service (issue #410 v1): purchase with hashed secure codes,
///     status machine Purchased → Delivered → Redeemed/Expired, and redemption granting
///     account credit to the redeeming account.
/// </summary>
public interface IGiftService
{
    /// <summary>Purchases a gift and returns the full redemption code exactly once.</summary>
    Task<Result<GiftPurchaseResult>> PurchaseAsync(PurchaseGiftRequest request, CancellationToken cancellationToken = default);

    /// <summary>Non-secret preview of a gift by redemption code (no giver identity leak).</summary>
    Task<Result<GiftPublicView>> GetByCodeAsync(Guid tenantId, string redemptionCode, CancellationToken cancellationToken = default);

    /// <summary>Redeems a gift for the authenticated user: grants account credit of the gift value.</summary>
    Task<Result<GiftPublicView>> RedeemAsync(Guid tenantId, string redemptionCode, Guid recipientUserId, CancellationToken cancellationToken = default);

    /// <summary>Gifts the user has sent, newest first (paged).</summary>
    Task<PagedResult<GiftPublicView>> GetSentAsync(Guid tenantId, Guid giverUserId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>Gifts the user has redeemed (received), newest first (paged).</summary>
    Task<PagedResult<GiftPublicView>> GetReceivedAsync(Guid tenantId, Guid recipientUserId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>Marks due gifts Delivered (delivery date reached) and expired gifts Expired (bounded pass).</summary>
    Task<int> ProgressDueAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken = default);
}
