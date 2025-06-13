using GameGuild.Common.Enums;

namespace GameGuild.Modules.Product.Interfaces;

/// <summary>
/// Interface for product-related services
/// </summary>
public interface IProductService
{
    Task<Models.Product> CreateProductAsync(Models.Product product);
    Task<Models.Product?> GetProductByIdAsync(int id);
    Task<IEnumerable<Models.Product>> GetProductsByTypeAsync(ProductType type);
    Task<Models.Product> UpdateProductAsync(Models.Product product);
    Task<bool> DeleteProductAsync(int id);
    Task<IEnumerable<Models.Product>> GetActiveProductsAsync();
    Task<IEnumerable<Models.Product>> GetProductsBundleAsync();
    Task<bool> AddProductToBundleAsync(int bundleId, int productId);
    Task<bool> RemoveProductFromBundleAsync(int bundleId, int productId);
}

/// <summary>
/// Interface for product pricing services
/// </summary>
public interface IProductPricingService
{
    Task<Models.ProductPricing> CreatePricingAsync(Models.ProductPricing pricing);
    Task<Models.ProductPricing?> GetPricingByIdAsync(int id);
    Task<IEnumerable<Models.ProductPricing>> GetPricingsByProductIdAsync(int productId);
    Task<Models.ProductPricing?> GetDefaultPricingForProductAsync(int productId);
    Task<Models.ProductPricing> UpdatePricingAsync(Models.ProductPricing pricing);
    Task<bool> DeletePricingAsync(int id);
    Task<decimal> GetEffectivePriceAsync(int productId, int? promoCodeId = null);
}

/// <summary>
/// Interface for user product access services
/// </summary>
public interface IUserProductService
{
    Task<Models.UserProduct> GrantProductAccessAsync(int userId, int productId, ProductAcquisitionType acquisitionType, decimal pricePaid = 0);
    Task<Models.UserProduct?> GetUserProductAsync(int userId, int productId);
    Task<IEnumerable<Models.UserProduct>> GetUserProductsAsync(int userId);
    Task<bool> HasProductAccessAsync(int userId, int productId);
    Task<bool> RevokeProductAccessAsync(int userId, int productId, string reason);
    Task<bool> ExtendProductAccessAsync(int userId, int productId, DateTime newEndDate);
    Task<IEnumerable<Models.UserProduct>> GetExpiringAccessAsync(DateTime thresholdDate);
}

/// <summary>
/// Interface for promotional code services
/// </summary>
public interface IPromoCodeService
{
    Task<Models.PromoCode> CreatePromoCodeAsync(Models.PromoCode promoCode);
    Task<Models.PromoCode?> GetPromoCodeByCodeAsync(string code);
    Task<bool> ValidatePromoCodeAsync(string code, int userId, int? productId = null);
    Task<decimal> CalculateDiscountAsync(string code, decimal originalAmount);
    Task<Models.PromoCodeUse> ApplyPromoCodeAsync(string code, int userId, int transactionId);
    Task<bool> DeactivatePromoCodeAsync(int id);
    Task<IEnumerable<Models.PromoCode>> GetActivePromoCodesAsync();
}
