using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to update an existing product
/// </summary>
/// <param name="ProductId">ID of the product to update</param>
/// <param name="Name">Updated name</param>
/// <param name="Description">Updated description</param>
/// <param name="ShortDescription">Updated short description</param>
/// <param name="ImageUrl">Updated image URL</param>
/// <param name="Type">Updated product type</param>
/// <param name="IsBundle">Updated bundle flag</param>
/// <param name="BundleItems">Updated bundle items</param>
/// <param name="ReferralCommissionPercentage">Updated referral commission</param>
/// <param name="MaxAffiliateDiscount">Updated max affiliate discount</param>
/// <param name="AffiliateCommissionPercentage">Updated affiliate commission</param>
/// <param name="ExpectedVersion">Expected version for optimistic concurrency</param>
/// <param name="CategoryId">Catalog category assignment (single category, v1; null = leave unchanged)</param>
public sealed record UpdateProductCommand(
    Guid ProductId,
    string? Name = null,
    string? Description = null,
    string? ShortDescription = null,
    string? ImageUrl = null,
    ProductType? Type = null,
    bool? IsBundle = null,
    List<Guid>? BundleItems = null,
    decimal? ReferralCommissionPercentage = null,
    decimal? MaxAffiliateDiscount = null,
    decimal? AffiliateCommissionPercentage = null,
    long? ExpectedVersion = null,
    Guid? CategoryId = null
) : ICommand<ProductDto>;
