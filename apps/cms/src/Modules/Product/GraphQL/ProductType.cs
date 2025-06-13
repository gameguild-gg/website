using HotChocolate.Types;
using GameGuild.Common.Services;
using GameGuild.Common.Enums;
using GameGuild.Common.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GameGuild.Modules.Product.Models;
using ProductEntity = GameGuild.Modules.Product.Models.Product;

namespace GameGuild.Modules.Product.GraphQL;

/// <summary>
/// GraphQL type definition for Product entity with DAC permission integration
/// </summary>
public class ProductType : ObjectType<ProductEntity>
{
    protected override void Configure(IObjectTypeDescriptor<ProductEntity> descriptor)
    {
        descriptor.Name("Product");
        descriptor.Description("Represents a product in the CMS system with full EntityBase support and DAC permissions.");

        // Base Entity Properties
        descriptor
            .Field(p => p.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier for the product (UUID).");

        descriptor
            .Field(p => p.Version)
            .Description("Version control for optimistic concurrency.");

        descriptor
            .Field(p => p.CreatedAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("The date and time when the product was created.");

        descriptor
            .Field(p => p.UpdatedAt)
            .Type<DateTimeType>()
            .Description("The date and time when the product was last updated.");

        descriptor
            .Field(p => p.DeletedAt)
            .Type<DateTimeType>()
            .Description("The date and time when the product was soft deleted (null if not deleted).");

        descriptor
            .Field(p => p.IsDeleted)
            .Type<NonNullType<BooleanType>>()
            .Description("Indicates whether the product has been soft deleted.");

        // Product-specific Properties
        descriptor
            .Field(p => p.Title)
            .Type<NonNullType<StringType>>()
            .Description("The title of the product.");

        descriptor
            .Field(p => p.ShortDescription)
            .Type<StringType>()
            .Description("Short description of the product.");

        descriptor
            .Field(p => p.Description)
            .Type<StringType>()
            .Description("Detailed description of the product.");

        descriptor
            .Field(p => p.Type)
            .Type<NonNullType<EnumType<ProductType>>>()
            .Description("The type of the product.");

        descriptor
            .Field(p => p.Status)
            .Type<NonNullType<EnumType<ContentStatus>>>()
            .Description("The publication status of the product.");

        descriptor
            .Field(p => p.Visibility)
            .Type<NonNullType<EnumType<Common.Entities.Visibility>>>()
            .Description("The visibility status of the product.");

        descriptor
            .Field(p => p.IsBundle)
            .Type<NonNullType<BooleanType>>()
            .Description("Indicates whether this product is a bundle containing other products.");

        descriptor
            .Field(p => p.Name)
            .Type<NonNullType<StringType>>()
            .Description("The name of the product (product-specific field).");

        descriptor
            .Field(p => p.Creator)
            .Type<ObjectType<GameGuild.Modules.User.Models.User>>()
            .Description("The user who created this product.");

        descriptor
            .Field(p => p.Tenant)
            .Type<ObjectType<GameGuild.Modules.Tenant.Models.Tenant>>()
            .Description("The tenant this product belongs to.");

        // Related entities
        descriptor
            .Field(p => p.ProductPricings)
            .Type<ListType<ProductPricingType>>()
            .Description("Pricing information for this product.");

        descriptor
            .Field(p => p.UserProducts)
            .Type<ListType<UserProductType>>()
            .Description("User access records for this product.");

        descriptor
            .Field(p => p.PromoCodes)
            .Type<ListType<PromoCodeType>>()
            .Description("Promotional codes associated with this product.");

        // Computed fields based on DAC permissions
        descriptor.Field("canEdit")
            .Type<BooleanType>()
            .Description("Indicates if the current user can edit this product")
            .Resolve(async context =>
            {
                var product = context.Parent<ProductEntity>();
                var user = context.GetUser();
                var userId = GetUserId(user);
                
                if (userId == null) return false;
                
                var permissionService = context.Service<IPermissionService>();
                
                // Hierarchical permission check: Resource → Content-Type → Tenant
                try
                {
                    // 1. Check resource-level permission
                    var hasResourcePermission = await permissionService.HasResourcePermissionAsync<ProductPermission, ProductEntity>(
                        userId.Value, product.Tenant?.Id, product.Id, PermissionType.Edit);
                    if (hasResourcePermission) return true;
                }
                catch
                {
                    // Continue to fallbacks if resource checking fails
                }
                
                // 2. Check content-type permission
                var hasContentTypePermission = await permissionService.HasContentTypePermissionAsync(
                    userId.Value, product.Tenant?.Id, "Product", PermissionType.Edit);
                if (hasContentTypePermission) return true;
                
                // 3. Check tenant permission
                var hasTenantPermission = await permissionService.HasTenantPermissionAsync(
                    userId.Value, product.Tenant?.Id, PermissionType.Edit);
                return hasTenantPermission;
            });
            
        descriptor.Field("canDelete")
            .Type<BooleanType>()
            .Description("Indicates if the current user can delete this product")
            .Resolve(async context =>
            {
                var product = context.Parent<ProductEntity>();
                var user = context.GetUser();
                var userId = GetUserId(user);
                
                if (userId == null) return false;
                
                var permissionService = context.Service<IPermissionService>();
                
                try
                {
                    var hasResourcePermission = await permissionService.HasResourcePermissionAsync<ProductPermission, ProductEntity>(
                        userId.Value, product.Tenant?.Id, product.Id, PermissionType.Delete);
                    if (hasResourcePermission) return true;
                }
                catch { }
                
                var hasContentTypePermission = await permissionService.HasContentTypePermissionAsync(
                    userId.Value, product.Tenant?.Id, "Product", PermissionType.Delete);
                if (hasContentTypePermission) return true;
                
                var hasTenantPermission = await permissionService.HasTenantPermissionAsync(
                    userId.Value, product.Tenant?.Id, PermissionType.Delete);
                return hasTenantPermission;
            });

        descriptor.Field("canPublish")
            .Type<BooleanType>()
            .Description("Indicates if the current user can publish this product")
            .Resolve(async context =>
            {
                var product = context.Parent<ProductEntity>();
                var user = context.GetUser();
                var userId = GetUserId(user);
                
                if (userId == null) return false;
                
                var permissionService = context.Service<IPermissionService>();
                
                try
                {
                    var hasResourcePermission = await permissionService.HasResourcePermissionAsync<ProductPermission, ProductEntity>(
                        userId.Value, product.Tenant?.Id, product.Id, PermissionType.Publish);
                    if (hasResourcePermission) return true;
                }
                catch { }
                
                var hasContentTypePermission = await permissionService.HasContentTypePermissionAsync(
                    userId.Value, product.Tenant?.Id, "Product", PermissionType.Publish);
                if (hasContentTypePermission) return true;
                
                var hasTenantPermission = await permissionService.HasTenantPermissionAsync(
                    userId.Value, product.Tenant?.Id, PermissionType.Publish);
                return hasTenantPermission;
            });

        descriptor.Field("hasAccess")
            .Type<BooleanType>()
            .Description("Indicates if the current user has access to this product")
            .Resolve(async context =>
            {
                var product = context.Parent<ProductEntity>();
                var user = context.GetUser();
                var userId = GetUserId(user);
                
                if (userId == null) return false;
                
                var productService = context.Service<Services.IProductService>();
                return await productService.HasUserAccessAsync(userId.Value, product.Id);
            });

        descriptor.Field("currentPricing")
            .Type<ProductPricingType>()
            .Description("The current active pricing for this product")
            .Resolve(async context =>
            {
                var product = context.Parent<ProductEntity>();
                var productService = context.Service<Services.IProductService>();
                return await productService.GetCurrentPricingAsync(product.Id);
            });

        descriptor.Field("bundleItems")
            .Type<ListType<ProductType>>()
            .Description("Items included in this bundle (only applicable if IsBundle is true)")
            .Resolve(async context =>
            {
                var product = context.Parent<ProductEntity>();
                if (!product.IsBundle) return new List<ProductEntity>();
                
                var productService = context.Service<Services.IProductService>();
                return await productService.GetBundleItemsAsync(product.Id);
            });
    }
    
    private static Guid? GetUserId(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;
            
        var userIdClaim = user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            return userId;
            
        return null;
    }
}

/// <summary>
/// GraphQL type for ProductPricing entity
/// </summary>
public class ProductPricingType : ObjectType<ProductPricing>
{
    protected override void Configure(IObjectTypeDescriptor<ProductPricing> descriptor)
    {
        descriptor.Name("ProductPricing");
        descriptor.Description("Represents pricing information for a product");

        descriptor.Field(pp => pp.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier for the pricing");

        descriptor.Field(pp => pp.Name)
            .Type<NonNullType<StringType>>()
            .Description("The name of this pricing tier");

        descriptor.Field(pp => pp.BasePrice)
            .Type<NonNullType<DecimalType>>()
            .Description("The base price for this product");

        descriptor.Field(pp => pp.Currency)
            .Type<NonNullType<StringType>>()
            .Description("The currency code (e.g., USD, EUR)");

        descriptor.Field(pp => pp.IsDefault)
            .Type<NonNullType<BooleanType>>()
            .Description("Indicates if this is the default pricing");

        descriptor.Field(pp => pp.CreatedAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("When this pricing was created");
    }
}

/// <summary>
/// GraphQL type for UserProduct entity
/// </summary>
public class UserProductType : ObjectType<UserProduct>
{
    protected override void Configure(IObjectTypeDescriptor<UserProduct> descriptor)
    {
        descriptor.Name("UserProduct");
        descriptor.Description("Represents a user's access to a product");

        descriptor.Field(up => up.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier for the user product record");

        descriptor.Field(up => up.AcquisitionType)
            .Type<NonNullType<EnumType<ProductAcquisitionType>>>()
            .Description("How the user acquired access to this product");

        descriptor.Field(up => up.AccessStatus)
            .Type<NonNullType<EnumType<ProductAccessStatus>>>()
            .Description("The current access status");

        descriptor.Field(up => up.PricePaid)
            .Type<NonNullType<DecimalType>>()
            .Description("The price paid for this product");

        descriptor.Field(up => up.Currency)
            .Type<NonNullType<StringType>>()
            .Description("The currency used for payment");

        descriptor.Field(up => up.AccessEndDate)
            .Type<DateTimeType>()
            .Description("When the access expires (null for permanent access)");

        descriptor.Field(up => up.User)
            .Type<ObjectType<GameGuild.Modules.User.Models.User>>()
            .Description("The user who has access");

        descriptor.Field(up => up.Product)
            .Type<ProductType>()
            .Description("The product being accessed");
    }
}

/// <summary>
/// GraphQL type for PromoCode entity
/// </summary>
public class PromoCodeType : ObjectType<PromoCode>
{
    protected override void Configure(IObjectTypeDescriptor<PromoCode> descriptor)
    {
        descriptor.Name("PromoCode");
        descriptor.Description("Represents a promotional code for products");

        descriptor.Field(pc => pc.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier for the promo code");

        descriptor.Field(pc => pc.Code)
            .Type<NonNullType<StringType>>()
            .Description("The promotional code");

        descriptor.Field(pc => pc.Type)
            .Type<NonNullType<EnumType<PromoCodeType>>>()
            .Description("The type of discount (percentage or fixed amount)");

        descriptor.Field(pc => pc.DiscountPercentage)
            .Type<DecimalType>()
            .Description("The discount percentage (for percentage-based discounts)");

        descriptor.Field(pc => pc.DiscountAmount)
            .Type<DecimalType>()
            .Description("The discount amount (for fixed amount discounts)");

        descriptor.Field(pc => pc.ValidFrom)
            .Type<DateTimeType>()
            .Description("When the promo code becomes valid");

        descriptor.Field(pc => pc.ValidUntil)
            .Type<DateTimeType>()
            .Description("When the promo code expires");

        descriptor.Field(pc => pc.MaxUses)
            .Type<IntType>()
            .Description("Maximum number of times this code can be used");

        descriptor.Field("currentUsageCount")
            .Type<NonNullType<IntType>>()
            .Description("Current number of times this code has been used")
            .Resolve(async context =>
            {
                var promoCode = context.Parent<PromoCode>();
                var dbContext = context.Service<Data.ApplicationDbContext>();
                return await dbContext.PromoCodeUses
                    .Where(pcu => !pcu.IsDeleted && pcu.PromoCodeId == promoCode.Id)
                    .CountAsync();
            });

        descriptor.Field("isValid")
            .Type<NonNullType<BooleanType>>()
            .Description("Indicates if the promo code is currently valid")
            .Resolve(async context =>
            {
                var promoCode = context.Parent<PromoCode>();
                var productService = context.Service<Services.IProductService>();
                return await productService.IsPromoCodeValidAsync(promoCode.Code);
            });
    }
}
