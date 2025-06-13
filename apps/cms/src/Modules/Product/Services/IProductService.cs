using GameGuild.Common.Entities;
using GameGuild.Common.Enums;
using GameGuild.Modules.Product.Models;
using ProductEntity = GameGuild.Modules.Product.Models.Product;

namespace GameGuild.Modules.Product.Services;

/// <summary>
/// Service interface for Product business logic
/// Provides operations for managing products, pricing, subscriptions, and access control
/// </summary>
public interface IProductService
{
    // Basic CRUD operations
    Task<ProductEntity?> GetProductByIdAsync(Guid id);
    Task<ProductEntity?> GetProductByIdWithDetailsAsync(Guid id);
    Task<IEnumerable<ProductEntity>> GetProductsAsync(int skip = 0, int take = 50);
    Task<IEnumerable<ProductEntity>> GetProductsByTypeAsync(ProductType type, int skip = 0, int take = 50);
    Task<IEnumerable<ProductEntity>> GetPublishedProductsAsync(int skip = 0, int take = 50);
    Task<ProductEntity> CreateProductAsync(ProductEntity product);
    Task<ProductEntity> UpdateProductAsync(ProductEntity product);
    Task DeleteProductAsync(Guid id);
    Task<bool> ProductExistsAsync(Guid id);

    // Visibility and publishing
    Task<ProductEntity> PublishProductAsync(Guid id);
    Task<ProductEntity> UnpublishProductAsync(Guid id);
    Task<ProductEntity> ArchiveProductAsync(Guid id);
    Task<ProductEntity> SetVisibilityAsync(Guid id, Common.Entities.Visibility visibility);

    // Bundle management
    Task<IEnumerable<ProductEntity>> GetBundleItemsAsync(Guid bundleId);
    Task<ProductEntity> AddToBundleAsync(Guid bundleId, Guid productId);
    Task<ProductEntity> RemoveFromBundleAsync(Guid bundleId, Guid productId);
    Task<bool> IsProductInBundleAsync(Guid bundleId, Guid productId);

    // Pricing management
    Task<ProductPricing?> GetCurrentPricingAsync(Guid productId);
    Task<IEnumerable<ProductPricing>> GetPricingHistoryAsync(Guid productId);
    Task<ProductPricing> SetPricingAsync(Guid productId, decimal basePrice, string currency = "USD");
    Task<ProductPricing> UpdatePricingAsync(Guid pricingId, decimal basePrice);

    // Subscription management
    Task<IEnumerable<ProductSubscriptionPlan>> GetSubscriptionPlansAsync(Guid productId);
    Task<ProductSubscriptionPlan?> GetSubscriptionPlanAsync(Guid planId);
    Task<ProductSubscriptionPlan> CreateSubscriptionPlanAsync(ProductSubscriptionPlan plan);
    Task<ProductSubscriptionPlan> UpdateSubscriptionPlanAsync(ProductSubscriptionPlan plan);
    Task DeleteSubscriptionPlanAsync(Guid planId);

    // User access and ownership
    Task<bool> HasUserAccessAsync(Guid userId, Guid productId);
    Task<UserProduct?> GetUserProductAsync(Guid userId, Guid productId);
    Task<IEnumerable<UserProduct>> GetUserProductsAsync(Guid userId);
    Task<UserProduct> GrantUserAccessAsync(Guid userId, Guid productId, ProductAcquisitionType acquisitionType, 
        decimal purchasePrice = 0, string currency = "USD", DateTime? expiresAt = null);
    Task RevokeUserAccessAsync(Guid userId, Guid productId);

    // Promo code management
    Task<PromoCode?> GetPromoCodeAsync(string code);
    Task<PromoCode> CreatePromoCodeAsync(PromoCode promoCode);
    Task<PromoCode> UpdatePromoCodeAsync(PromoCode promoCode);
    Task DeletePromoCodeAsync(Guid id);
    Task<PromoCodeUse> UsePromoCodeAsync(Guid userId, string code, decimal discountAmount);
    Task<bool> IsPromoCodeValidAsync(string code, Guid? productId = null);

    // Analytics and statistics
    Task<int> GetProductCountAsync(ProductType? type = null, Common.Entities.Visibility? visibility = null);
    Task<int> GetUserCountForProductAsync(Guid productId);
    Task<decimal> GetTotalRevenueForProductAsync(Guid productId);
    Task<IEnumerable<ProductEntity>> GetPopularProductsAsync(int count = 10);
    Task<IEnumerable<ProductEntity>> GetRecentProductsAsync(int count = 10);

    // Search and filtering
    Task<IEnumerable<ProductEntity>> SearchProductsAsync(string searchTerm, int skip = 0, int take = 50);
    Task<IEnumerable<ProductEntity>> GetProductsByCreatorAsync(Guid creatorId, int skip = 0, int take = 50);
    Task<IEnumerable<ProductEntity>> GetProductsInPriceRangeAsync(decimal minPrice, decimal maxPrice, 
        string currency = "USD", int skip = 0, int take = 50);
}
