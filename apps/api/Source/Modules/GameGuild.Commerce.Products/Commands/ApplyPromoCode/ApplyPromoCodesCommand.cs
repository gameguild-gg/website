using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to apply one or more promo codes to an order. Applying records usage
/// atomically (idempotent on <paramref name="IdempotencyKey"/>, fail-closed on limits).
/// </summary>
/// <param name="OrderAmount">The order amount to apply codes to</param>
/// <param name="PromoCodes">List of promo codes to apply</param>
/// <param name="ProductId">Optional product ID for product-specific codes</param>
/// <param name="UserId">Optional user ID for user-specific validation and usage recording</param>
/// <param name="IdempotencyKey">Optional replay-protection key; repeated submissions of the same key never count twice</param>
/// <param name="OrderId">Optional order linkage recorded with the usage</param>
public sealed record ApplyPromoCodesCommand(
    decimal OrderAmount,
    List<string> PromoCodes,
    Guid? ProductId = null,
    Guid? UserId = null,
    string? IdempotencyKey = null,
    Guid? OrderId = null
) : ICommand<PromoCodeApplicationResult>;
