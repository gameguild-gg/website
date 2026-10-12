namespace GameGuild.Commerce.Products;

/// <summary> Interface for promotional code services </summary>
public interface IPromoCodeService {
  Task<PromoCode> CreatePromoCodeAsync(PromoCode promoCode);

  Task<PromoCode?> GetPromoCodeByCodeAsync(string code);

  Task<bool> ValidatePromoCodeAsync(string code, Guid userId, Guid? productId = null);

  Task<decimal> CalculateDiscountAsync(string code, decimal originalAmount);

  /// <summary>
  /// Apply a promo code for a user and record the usage atomically. The actual computed
  /// discount is persisted; limit breaches fail closed and replayed idempotency keys
  /// return the previously recorded usage.
  /// </summary>
  /// <param name="code">The promo code string (normalized case-insensitively)</param>
  /// <param name="userId">Redeeming user</param>
  /// <param name="transactionId">Owning transaction/order identifier</param>
  /// <param name="orderAmount">Order amount the discount is computed from</param>
  /// <param name="idempotencyKey">Optional replay-protection key</param>
  Task<PromoCodeUse> ApplyPromoCodeAsync(string code, Guid userId, Guid transactionId, decimal orderAmount = 0, string? idempotencyKey = null);

  Task<bool> DeactivatePromoCodeAsync(Guid id);

  Task<IEnumerable<PromoCode>> GetActivePromoCodesAsync();
}
