using GameGuild.Modules.Product.GraphQL;
using GameGuild.Common.Enums;
using GameGuild.Common.Entities;
using GameGuild.Modules.Product.Services;
using ProductEntity = GameGuild.Modules.Product.Models.Product;
using ProductTypeEnum = GameGuild.Common.Enums.ProductType;
using PromoCodeTypeEnum = GameGuild.Common.Enums.PromoCodeType;

namespace GameGuild.Modules.Product.GraphQL;

/// <summary>
/// GraphQL mutations for Product module
/// </summary>
[ExtendObjectType<GameGuild.Modules.User.GraphQL.Mutation>]
public class ProductMutations
{
    /// <summary>
    /// Creates a new product
    /// </summary>
    public async Task<ProductEntity> CreateProduct(
        CreateProductInput input,
        [Service] IProductService productService)
    {
        var product = new ProductEntity
        {
            Title = input.Name,
            Name = input.Name,
            ShortDescription = input.ShortDescription,
            Description = input.ShortDescription, // Using short description for both fields
            Type = input.Type,
            IsBundle = input.IsBundle
        };

        if (input.TenantId.HasValue)
        {
            // Note: TenantId might be set through other means - check the actual Product model
            // product.TenantId = input.TenantId.Value;
        }

        return await productService.CreateProductAsync(product);
    }

    /// <summary>
    /// Updates an existing product
    /// </summary>
    public async Task<ProductEntity?> UpdateProduct(
        UpdateProductInput input,
        [Service] IProductService productService)
    {
        var product = await productService.GetProductByIdAsync(input.Id);
        if (product == null) return null;

        // Update only provided properties
        if (!string.IsNullOrEmpty(input.Name))
        {
            product.Name = input.Name;
            product.Title = input.Name;
        }

        if (!string.IsNullOrEmpty(input.ShortDescription))
        {
            product.ShortDescription = input.ShortDescription;
            product.Description = input.ShortDescription;
        }

        if (input.Type.HasValue)
            product.Type = input.Type.Value;

        if (input.IsBundle.HasValue)
            product.IsBundle = input.IsBundle.Value;

        if (input.Status.HasValue)
            product.Status = input.Status.Value;

        if (input.Visibility.HasValue)
            product.Visibility = input.Visibility.Value;

        return await productService.UpdateProductAsync(product);
    }

    /// <summary>
    /// Deletes a product
    /// </summary>
    public async Task<bool> DeleteProduct(
        Guid id,
        [Service] IProductService productService)
    {
        await productService.DeleteProductAsync(id);
        return true;
    }

    /// <summary>
    /// Publishes a product
    /// </summary>
    public async Task<ProductEntity?> PublishProduct(
        Guid id,
        [Service] IProductService productService)
    {
        return await productService.PublishProductAsync(id);
    }

    /// <summary>
    /// Unpublishes a product
    /// </summary>
    public async Task<ProductEntity?> UnpublishProduct(
        Guid id,
        [Service] IProductService productService)
    {
        return await productService.UnpublishProductAsync(id);
    }

    /// <summary>
    /// Archives a product
    /// </summary>
    public async Task<ProductEntity?> ArchiveProduct(
        Guid id,
        [Service] IProductService productService)
    {
        return await productService.ArchiveProductAsync(id);
    }

    /// <summary>
    /// Sets product visibility
    /// </summary>
    public async Task<ProductEntity?> SetProductVisibility(
        Guid id,
        Common.Entities.Visibility visibility,
        [Service] IProductService productService)
    {
        return await productService.SetVisibilityAsync(id, visibility);
    }

    /// <summary>
    /// Adds a product to a bundle
    /// </summary>
    public async Task<ProductEntity?> AddToBundle(
        BundleManagementInput input,
        [Service] IProductService productService)
    {
        return await productService.AddToBundleAsync(input.BundleId, input.ProductId);
    }

    /// <summary>
    /// Removes a product from a bundle
    /// </summary>
    public async Task<ProductEntity?> RemoveFromBundle(
        BundleManagementInput input,
        [Service] IProductService productService)
    {
        return await productService.RemoveFromBundleAsync(input.BundleId, input.ProductId);
    }

    /// <summary>
    /// Sets product pricing
    /// </summary>
    public async Task<Models.ProductPricing> SetProductPricing(
        SetProductPricingInput input,
        [Service] IProductService productService)
    {
        return await productService.SetPricingAsync(input.ProductId, input.BasePrice, input.Currency);
    }

    /// <summary>
    /// Updates product pricing
    /// </summary>
    public async Task<Models.ProductPricing?> UpdateProductPricing(
        UpdateProductPricingInput input,
        [Service] IProductService productService)
    {
        return await productService.UpdatePricingAsync(input.PricingId, input.BasePrice ?? 0);
    }

    /// <summary>
    /// Grants user access to a product
    /// </summary>
    public async Task<Models.UserProduct> GrantUserAccess(
        GrantProductAccessInput input,
        [Service] IProductService productService)
    {
        return await productService.GrantUserAccessAsync(
            input.UserId,
            input.ProductId,
            input.AcquisitionType,
            input.PurchasePrice,
            input.Currency,
            input.ExpiresAt);
    }

    /// <summary>
    /// Revokes user access to a product
    /// </summary>
    public async Task<bool> RevokeUserAccess(
        Guid userId,
        Guid productId,
        [Service] IProductService productService)
    {
        await productService.RevokeUserAccessAsync(userId, productId);
        return true;
    }

    /// <summary>
    /// Creates a promotional code
    /// </summary>
    public async Task<Models.PromoCode> CreatePromoCode(
        CreatePromoCodeInput input,
        [Service] IProductService productService)
    {
        var promoCode = new Models.PromoCode
        {
            Code = input.Code,
            Name = input.Code,
            Description = $"Promotional code {input.Code}",
            Type = input.DiscountType,
            ProductId = input.ProductId,
            ValidFrom = input.ValidFrom,
            ValidUntil = input.ValidUntil,
            MaxUses = input.MaxUses,
            IsActive = true
        };

        // Set the discount value based on type
        if (input.DiscountType == PromoCodeTypeEnum.PercentageOff)
        {
            promoCode.DiscountPercentage = input.DiscountValue;
        }
        else if (input.DiscountType == PromoCodeTypeEnum.FixedAmountOff)
        {
            promoCode.DiscountAmount = input.DiscountValue;
        }

        return await productService.CreatePromoCodeAsync(promoCode);
    }

    /// <summary>
    /// Updates a promotional code
    /// </summary>
    public async Task<Models.PromoCode?> UpdatePromoCode(
        UpdatePromoCodeInput input,
        [Service] IProductService productService)
    {
        var promoCode = await productService.GetPromoCodeAsync(input.Code ?? "");
        if (promoCode == null) return null;

        // Update only provided properties
        if (!string.IsNullOrEmpty(input.Code))
        {
            promoCode.Code = input.Code;
            promoCode.Name = input.Code;
        }

        if (input.DiscountType.HasValue)
            promoCode.Type = input.DiscountType.Value;

        if (input.DiscountValue.HasValue)
        {
            if (promoCode.Type == PromoCodeTypeEnum.PercentageOff)
            {
                promoCode.DiscountPercentage = input.DiscountValue.Value;
                promoCode.DiscountAmount = null;
            }
            else if (promoCode.Type == PromoCodeTypeEnum.FixedAmountOff)
            {
                promoCode.DiscountAmount = input.DiscountValue.Value;
                promoCode.DiscountPercentage = null;
            }
        }

        if (input.ValidFrom.HasValue)
            promoCode.ValidFrom = input.ValidFrom.Value;

        if (input.ValidUntil.HasValue)
            promoCode.ValidUntil = input.ValidUntil.Value;

        if (input.MaxUses.HasValue)
            promoCode.MaxUses = input.MaxUses.Value;

        return await productService.UpdatePromoCodeAsync(promoCode);
    }

    /// <summary>
    /// Deletes a promotional code
    /// </summary>
    public async Task<bool> DeletePromoCode(
        Guid id,
        [Service] IProductService productService)
    {
        await productService.DeletePromoCodeAsync(id);
        return true;
    }

    /// <summary>
    /// Uses a promotional code
    /// </summary>
    public async Task<Models.PromoCodeUse> UsePromoCode(
        Guid userId,
        string code,
        decimal discountAmount,
        [Service] IProductService productService)
    {
        return await productService.UsePromoCodeAsync(userId, code, discountAmount);
    }
}
