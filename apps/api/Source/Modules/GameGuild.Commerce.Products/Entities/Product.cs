using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Represents a product in the system.
///     Inherits from EntityBase to provide GUID IDs, version control, timestamps, and soft delete functionality.
/// </summary>
/// <remarks>
///     <para>
///         <b>Creator Dependency:</b> Products have an optional creator relationship to User.
///         For reduced coupling, use <see cref="GetCreatorInfo"/> to get an <see cref="ICreator"/>
///         abstraction rather than accessing the User directly when full User functionality isn't needed.
///     </para>
/// </remarks>
[Table("Products")]
[Index(nameof(Name))]
[Index(nameof(Type))]
[Index(nameof(IsPublished))]
[Index(nameof(CreatorId))]
[Index(nameof(CategoryId))]
public class Product : EntityBase
{
    /// <summary>
    ///     Default constructor
    /// </summary>
    public Product() { }

    /// <summary>
    ///     Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial product data</param>
    public Product(object partial) : base(partial) { }

    /// <summary>
    ///     Product name
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Full product description
    /// </summary>
    [MaxLength(4000)]
    public string? Description { get; set; }

    /// <summary>
    ///     Short description for listings
    /// </summary>
    [MaxLength(500)]
    public string? ShortDescription { get; set; }

    /// <summary>
    ///     Product image URL
    /// </summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>
    ///     Type of product
    /// </summary>
    public ProductType Type { get; set; } = ProductType.Program;

    /// <summary>
    ///     Whether this product is a bundle of other products
    /// </summary>
    public bool IsBundle { get; set; }

    /// <summary>
    ///     Whether this product is visible to anonymous catalog reads.
    /// </summary>
    public bool IsPublished { get; set; } = true;

    /// <summary>
    ///     Catalog category this product is filed under (single category, v1 of the
    ///     hierarchical catalog). <c>null</c> means uncategorized.
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    ///     Navigation property to the catalog category.
    /// </summary>
    public virtual ProductCategory? Category { get; set; }

    /// <summary>
    ///     Optional per-product inventory state. <c>null</c> means stock is not
    ///     tracked for this product (unlimited availability).
    /// </summary>
    public virtual ProductInventory? Inventory { get; set; }

    /// <summary>
    ///     Creator user ID
    /// </summary>
    public Guid? CreatorId { get; set; }

    /// <summary>
    ///     Navigation property to the creator.
    ///     For reduced coupling, prefer using <see cref="GetCreatorInfo"/> when you only need basic creator data.
    /// </summary>
    public virtual User? Creator { get; set; }

    /// <summary>
    ///     Gets creator information as an <see cref="ICreator"/> abstraction.
    ///     Reduces coupling by not requiring access to the full User entity.
    /// </summary>
    /// <returns>CreatorInfo if creator is loaded, null otherwise</returns>
    public CreatorInfo? GetCreatorInfo() => Creator?.ToCreatorInfo();

    // Navigation properties

    /// <summary>
    ///     Pricing options for this product
    /// </summary>
    public virtual ICollection<ProductPricing> Pricing { get; set; } = new List<ProductPricing>();

    /// <summary>
    ///     Subscription plans for this product
    /// </summary>
    public virtual ICollection<ProductSubscriptionPlan> SubscriptionPlans { get; set; } = new List<ProductSubscriptionPlan>();

    /// <summary>
    ///     User-product relationships (purchases/access)
    /// </summary>
    public virtual ICollection<UserProduct> UserProducts { get; set; } = new List<UserProduct>();

    /// <summary>
    ///     Promo codes for this product
    /// </summary>
    public virtual ICollection<PromoCode> PromoCodes { get; set; } = new List<PromoCode>();

    /// <summary>
    ///     Commission configuration for affiliate/referral programs.
    ///     Replaces the deprecated inline commission fields.
    /// </summary>
    public virtual ProductCommissionConfig? CommissionConfig { get; set; }

    /// <summary>
    ///     Type-safe collection of bundle items.
    ///     Replaces the deprecated BundleItemsJson field.
    /// </summary>
    public virtual ICollection<ProductBundleItem> BundleItems { get; set; } = new List<ProductBundleItem>();

    /// <summary>
    ///     Products that include this product in their bundles.
    /// </summary>
    public virtual ICollection<ProductBundleItem> IncludedInBundles { get; set; } = new List<ProductBundleItem>();

    /// <summary>
    ///     Factory method to create a new product
    /// </summary>
    public static Product Create(
        string name,
        ProductType type = ProductType.Program,
        string? description = null,
        string? shortDescription = null,
        string? imageUrl = null,
        Guid? creatorId = null,
        bool isBundle = false,
        Guid? tenantId = null)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            Description = description,
            ShortDescription = shortDescription,
            ImageUrl = imageUrl,
            CreatorId = creatorId,
            IsBundle = isBundle,
            TenantId = tenantId
        };
    }

    /// <summary>
    ///     Creates a product with commission configuration
    /// </summary>
    public static (Product Product, ProductCommissionConfig CommissionConfig) CreateWithCommission(
        string name,
        ProductType type = ProductType.Program,
        string? description = null,
        string? shortDescription = null,
        string? imageUrl = null,
        Guid? creatorId = null,
        bool isBundle = false,
        decimal referralCommissionPercentage = 30m,
        decimal affiliateCommissionPercentage = 30m,
        decimal maxAffiliateDiscount = 0m,
        Guid? tenantId = null)
    {
        var product = Create(name, type, description, shortDescription, imageUrl, creatorId, isBundle, tenantId);
        var commissionConfig = ProductCommissionConfig.Create(
            product.Id,
            referralCommissionPercentage,
            affiliateCommissionPercentage,
            maxAffiliateDiscount,
            tenantId);

        return (product, commissionConfig);
    }

    /// <summary>
    ///     Adds a product to this bundle
    /// </summary>
    /// <param name="includedProductId">Product ID to include</param>
    /// <param name="quantity">Quantity to include</param>
    /// <param name="displayOrder">Display order in bundle</param>
    /// <returns>The created bundle item</returns>
    public ProductBundleItem AddToBundleTypeSafe(Guid includedProductId, int quantity = 1, int displayOrder = 0)
    {
        if (!IsBundle)
            throw new InvalidOperationException("Cannot add items to a non-bundle product");

        if (BundleItems.Any(bi => bi.IncludedProductId == includedProductId))
            throw new InvalidOperationException("Product is already in this bundle");

        var bundleItem = ProductBundleItem.Create(Id, includedProductId, quantity, displayOrder, true, TenantId);
        BundleItems.Add(bundleItem);
        return bundleItem;
    }

    /// <summary>
    ///     Removes a product from this bundle
    /// </summary>
    public bool RemoveFromBundle(Guid includedProductId)
    {
        var item = BundleItems.FirstOrDefault(bi => bi.IncludedProductId == includedProductId);
        if (item == null)
            return false;

        BundleItems.Remove(item);
        return true;
    }

    /// <summary>
    ///     Gets bundle item product IDs (type-safe version)
    /// </summary>
    public IEnumerable<Guid> GetBundleProductIds()
    {
        return BundleItems.OrderBy(bi => bi.DisplayOrder).Select(bi => bi.IncludedProductId);
    }

    #region Deprecated API (backwards compatibility)

    /// <summary>
    ///     Referral commission percentage. Delegated to CommissionConfig.
    /// </summary>
    [Obsolete("Use CommissionConfig.ReferralCommissionPercentage instead")]
    [NotMapped]
    public decimal ReferralCommissionPercentage
    {
        get => CommissionConfig?.ReferralCommissionPercentage ?? 30m;
        set
        {
            if (CommissionConfig != null)
            {
                // Cannot set via this deprecated property - use CommissionConfig methods
                throw new InvalidOperationException("Use CommissionConfig to update commission settings");
            }
        }
    }

    /// <summary>
    ///     Affiliate commission percentage. Delegated to CommissionConfig.
    /// </summary>
    [Obsolete("Use CommissionConfig.AffiliateCommissionPercentage instead")]
    [NotMapped]
    public decimal AffiliateCommissionPercentage
    {
        get => CommissionConfig?.AffiliateCommissionPercentage ?? 30m;
        set
        {
            if (CommissionConfig != null)
            {
                throw new InvalidOperationException("Use CommissionConfig to update commission settings");
            }
        }
    }

    /// <summary>
    ///     Maximum affiliate discount. Delegated to CommissionConfig.
    /// </summary>
    [Obsolete("Use CommissionConfig.MaxAffiliateDiscount instead")]
    [NotMapped]
    public decimal MaxAffiliateDiscount
    {
        get => CommissionConfig?.MaxAffiliateDiscount ?? 0m;
        set
        {
            if (CommissionConfig != null)
            {
                throw new InvalidOperationException("Use CommissionConfig to update commission settings");
            }
        }
    }

    /// <summary>
    ///     Gets bundle item IDs. Delegated to GetBundleProductIds().
    /// </summary>
    [Obsolete("Use GetBundleProductIds() or BundleItems collection instead")]
    public List<Guid> GetBundleItemIds() => GetBundleProductIds().ToList();

    /// <summary>
    ///     Sets bundle item IDs. Use AddToBundleTypeSafe() for new code.
    /// </summary>
    [Obsolete("Use AddToBundleTypeSafe() and RemoveFromBundle() methods instead")]
    public void SetBundleItemIds(IEnumerable<Guid>? bundleItemIds)
    {
        if (!IsBundle)
            throw new InvalidOperationException("Cannot set bundle items on a non-bundle product");

        if (bundleItemIds == null)
        {
            BundleItems.Clear();
            return;
        }

        // Clear existing and add new items
        BundleItems.Clear();
        var order = 0;
        foreach (var productId in bundleItemIds)
        {
            var bundleItem = ProductBundleItem.Create(Id, productId, 1, order++, true, TenantId);
            BundleItems.Add(bundleItem);
        }
    }

    #endregion
}
