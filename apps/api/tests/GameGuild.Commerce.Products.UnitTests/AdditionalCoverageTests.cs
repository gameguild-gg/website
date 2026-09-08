using FluentAssertions;
using GameGuild.Commerce.Products;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests;

#region ProductMappingExtensions Tests

public class ProductMappingExtensionsTests
{
    [Fact]
    public void Product_ToDto_MapsAllProperties()
    {
        var product = new Product
        {
            Name = "Test Product",
            Description = "Description",
            ShortDescription = "Short",
            ImageUrl = "https://example.com/img.png",
            Type = ProductType.Program,
            IsBundle = false,
            CreatorId = Guid.NewGuid()
        };

        var result = product.ToDto();

        result.Name.Should().Be("Test Product");
        result.Description.Should().Be("Description");
        result.ShortDescription.Should().Be("Short");
        result.ImageUrl.Should().Be("https://example.com/img.png");
        result.Type.Should().Be(ProductType.Program);
        result.IsBundle.Should().BeFalse();
    }

    [Fact]
    public void Product_ToDto_WithPricing_IncludesPricing()
    {
        var product = new Product { Name = "Test" };
        var pricing = new List<ProductPricingDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Standard", 9.99m, null, "USD", null, null, true, 9.99m, false)
        };

        var result = product.ToDto(pricing);
        result.Pricing.Should().HaveCount(1);
    }

    [Fact]
    public void Product_ToDto_WithNullCommissionConfig_UsesDefaults()
    {
        var product = new Product { Name = "Test" };

        var result = product.ToDto();

        result.ReferralCommissionPercentage.Should().Be(30m);
        result.MaxAffiliateDiscount.Should().Be(0m);
        result.AffiliateCommissionPercentage.Should().Be(30m);
    }

    [Fact]
    public void PromoCode_ToDto_MapsAllProperties()
    {
        var promo = new PromoCode
        {
            Code = "SAVE20",
            Name = "Save 20%",
            Description = "20% off",
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = 20m,
            Currency = "USD",
            IsActive = true,
            IsExclusive = false,
            StackingPriority = 1
        };

        var result = promo.ToDto();

        result.Code.Should().Be("SAVE20");
        result.Name.Should().Be("Save 20%");
        result.Type.Should().Be(PromoCodeType.PercentageOff);
        result.DiscountPercentage.Should().Be(20m);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public void PromoCode_ToDto_WithExplicitUsageCount()
    {
        var promo = new PromoCode { Code = "TEST", Name = "Test" };
        var result = promo.ToDto(42);
        result.UsageCount.Should().Be(42);
    }

    [Fact]
    public void ProductPricing_ToDto_NoSale()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(
            Guid.NewGuid(), "Standard", 19.99m, currency: "USD", isDefault: true);

        var result = pricing.ToDto();

        result.Name.Should().Be("Standard");
        result.BasePrice.Should().Be(19.99m);
        result.CurrentPrice.Should().Be(19.99m);
        result.IsSaleActive.Should().BeFalse();
    }

    [Fact]
    public void ProductPricing_ToDto_ActiveSale()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(
            Guid.NewGuid(), "Sale", 29.99m, "USD",
            19.99m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7), false);

        var result = pricing.ToDto();

        result.IsSaleActive.Should().BeTrue();
        result.CurrentPrice.Should().Be(19.99m);
    }

    [Fact]
    public void ProductPricing_ToDto_ExpiredSale()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(
            Guid.NewGuid(), "Old Sale", 29.99m, "USD",
            14.99m, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-1), false);

        var result = pricing.ToDto();

        result.IsSaleActive.Should().BeFalse();
        result.CurrentPrice.Should().Be(29.99m);
    }

    [Fact]
    public void ProductPricing_ToDto_FutureSale()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(
            Guid.NewGuid(), "Future Sale", 49.99m, "USD",
            29.99m, DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(14), false);

        var result = pricing.ToDto();

        result.IsSaleActive.Should().BeFalse();
        result.CurrentPrice.Should().Be(49.99m);
    }
}

#endregion

#region Record/DTO Instantiation Tests

public class PromoCodeRecordTests
{
    [Fact]
    public void CreatePromoCodeRequest_AllDefaults()
    {
        var request = new CreatePromoCodeRequest("SAVE10", "Save 10%");
        request.Code.Should().Be("SAVE10");
        request.Name.Should().Be("Save 10%");
        request.Type.Should().Be(PromoCodeType.PercentageOff);
        request.Currency.Should().Be("USD");
        request.IsActive.Should().BeTrue();
        request.IsExclusive.Should().BeFalse();
        request.StackingPriority.Should().Be(0);
    }

    [Fact]
    public void CreatePromoCodeRequest_AllProperties()
    {
        var request = new CreatePromoCodeRequest(
            "FLAT5", "Flat $5", "Get $5 off", PromoCodeType.FixedAmountOff,
            null, 5m, "EUR", 10m, 100, 1,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30),
            true, true, 5, Guid.NewGuid());
        request.DiscountAmount.Should().Be(5m);
        request.Currency.Should().Be("EUR");
        request.IsExclusive.Should().BeTrue();
    }

    [Fact]
    public void UpdatePromoCodeRequest_AllDefaults()
    {
        var request = new UpdatePromoCodeRequest();
        request.Name.Should().BeNull();
        request.Description.Should().BeNull();
        request.Type.Should().BeNull();
        request.DiscountPercentage.Should().BeNull();
        request.DiscountAmount.Should().BeNull();
        request.Currency.Should().BeNull();
        request.MinimumOrderAmount.Should().BeNull();
        request.MaxUses.Should().BeNull();
        request.MaxUsesPerUser.Should().BeNull();
        request.ValidFrom.Should().BeNull();
        request.ValidUntil.Should().BeNull();
        request.IsActive.Should().BeNull();
        request.IsExclusive.Should().BeNull();
        request.StackingPriority.Should().BeNull();
        request.ProductId.Should().BeNull();
    }

    [Fact]
    public void PatchPromoCodeRequest_AllDefaults()
    {
        var request = new PatchPromoCodeRequest();
        request.Name.Should().BeNull();
        request.Description.Should().BeNull();
        request.Type.Should().BeNull();
        request.DiscountPercentage.Should().BeNull();
        request.DiscountAmount.Should().BeNull();
        request.Currency.Should().BeNull();
        request.MinimumOrderAmount.Should().BeNull();
        request.MaxUses.Should().BeNull();
        request.MaxUsesPerUser.Should().BeNull();
        request.ValidFrom.Should().BeNull();
        request.ValidUntil.Should().BeNull();
        request.IsActive.Should().BeNull();
        request.IsExclusive.Should().BeNull();
        request.StackingPriority.Should().BeNull();
        request.ProductId.Should().BeNull();
    }

    [Fact]
    public void ValidatePromoCodeRequest_CanBeCreated()
    {
        var request = new ValidatePromoCodeRequest("SAVE10", 50m, Guid.NewGuid());
        request.Code.Should().Be("SAVE10");
        request.OrderAmount.Should().Be(50m);
    }

    [Fact]
    public void ApplyPromoCodesRequest_CanBeCreated()
    {
        var codes = new List<string> { "CODE1", "CODE2" };
        var request = new ApplyPromoCodesRequest(100m, codes, Guid.NewGuid());
        request.OrderAmount.Should().Be(100m);
        request.PromoCodes.Should().HaveCount(2);
    }

    [Fact]
    public void PatchPromoCodeCommand_AllDefaults()
    {
        var cmd = new PatchPromoCodeCommand(Guid.NewGuid());
        cmd.Name.Should().BeNull();
        cmd.Description.Should().BeNull();
        cmd.Type.Should().BeNull();
        cmd.DiscountPercentage.Should().BeNull();
        cmd.DiscountAmount.Should().BeNull();
        cmd.Currency.Should().BeNull();
        cmd.MinimumOrderAmount.Should().BeNull();
        cmd.MaxUses.Should().BeNull();
        cmd.MaxUsesPerUser.Should().BeNull();
        cmd.ValidFrom.Should().BeNull();
        cmd.ValidUntil.Should().BeNull();
        cmd.IsActive.Should().BeNull();
        cmd.IsExclusive.Should().BeNull();
        cmd.StackingPriority.Should().BeNull();
        cmd.ProductId.Should().BeNull();
    }

    [Fact]
    public void PatchProductCommand_AllDefaults()
    {
        var cmd = new PatchProductCommand(Guid.NewGuid());
        cmd.Name.Should().BeNull();
        cmd.Description.Should().BeNull();
        cmd.ShortDescription.Should().BeNull();
        cmd.ImageUrl.Should().BeNull();
        cmd.Type.Should().BeNull();
        cmd.IsBundle.Should().BeNull();
        cmd.BundleItems.Should().BeNull();
        cmd.ReferralCommissionPercentage.Should().BeNull();
        cmd.MaxAffiliateDiscount.Should().BeNull();
        cmd.AffiliateCommissionPercentage.Should().BeNull();
        cmd.ExpectedVersion.Should().BeNull();
    }

    [Fact]
    public void BatchProductCreateItem_AllDefaults()
    {
        var item = new BatchProductCreateItem("Test Product");
        item.Name.Should().Be("Test Product");
        item.Description.Should().BeNull();
        item.ShortDescription.Should().BeNull();
        item.ImageUrl.Should().BeNull();
        item.Type.Should().Be(ProductType.Program);
        item.IsBundle.Should().BeFalse();
        item.CreatorId.Should().BeNull();
        item.BundleItems.Should().BeNull();
        item.ReferralCommissionPercentage.Should().Be(30m);
        item.MaxAffiliateDiscount.Should().Be(0m);
        item.AffiliateCommissionPercentage.Should().Be(30m);
    }

    [Fact]
    public void BatchCreateProductsCommand_CanBeCreated()
    {
        var items = new List<BatchProductCreateItem> { new("P1"), new("P2") };
        var cmd = new BatchCreateProductsCommand(items, Guid.NewGuid());
        cmd.Products.Should().HaveCount(2);
        cmd.TenantId.Should().NotBeNull();
    }

    [Fact]
    public void GetPromoCodesQuery_AllDefaults()
    {
        var query = new GetPromoCodesQuery();
        query.IsActive.Should().BeNull();
        query.Type.Should().BeNull();
        query.ProductId.Should().BeNull();
        query.SearchTerm.Should().BeNull();
        query.Skip.Should().Be(0);
        query.Take.Should().Be(50);
    }

    [Fact]
    public void CalculateProductPriceQuery_AllDefaults()
    {
        var query = new CalculateProductPriceQuery(Guid.NewGuid());
        query.PricingId.Should().BeNull();
        query.PromoCodes.Should().BeNull();
        query.UserId.Should().BeNull();
    }
}

public class EntitlementRecordTests
{
    [Fact]
    public void GrantEntitlementRequest_AllDefaults()
    {
        var request = new GrantEntitlementRequest(Guid.NewGuid(), Guid.NewGuid(), ProductAcquisitionType.Purchase);
        request.PricePaid.Should().Be(0m);
        request.Currency.Should().Be("USD");
        request.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void GrantEntitlementRequest_AllProperties()
    {
        var request = new GrantEntitlementRequest(
            Guid.NewGuid(), Guid.NewGuid(), ProductAcquisitionType.Subscription,
            19.99m, "EUR", DateTime.UtcNow.AddYears(1));
        request.PricePaid.Should().Be(19.99m);
        request.Currency.Should().Be("EUR");
        request.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public void RevokeEntitlementRequest_CanBeCreated()
    {
        var request = new RevokeEntitlementRequest(Guid.NewGuid(), Guid.NewGuid(), "Refunded");
        request.Reason.Should().Be("Refunded");
    }

    [Fact]
    public void RevokeEntitlementRequest_NullReason()
    {
        var request = new RevokeEntitlementRequest(Guid.NewGuid(), Guid.NewGuid());
        request.Reason.Should().BeNull();
    }
}

#endregion

#region Entity Constructor Tests

public class EntityPartialConstructorTests
{
    [Fact]
    public void PromoCodeUse_PartialConstructor()
    {
        var entity = new PromoCodeUse(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void PromoCodeUse_DefaultConstructor()
    {
        var entity = new PromoCodeUse();
        entity.Should().NotBeNull();
    }

    [Fact]
    public void ProductSubscriptionPlan_PartialConstructor()
    {
        var entity = new ProductSubscriptionPlan(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void ProductSubscriptionPlan_DefaultConstructor()
    {
        var entity = new ProductSubscriptionPlan();
        entity.IsActive.Should().BeTrue();
        entity.Currency.Should().Be("USD");
        entity.IntervalCount.Should().Be(1);
    }

    [Fact]
    public void PricingTier_PartialConstructor()
    {
        var entity = new PricingTier(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void UserProduct_PartialConstructor()
    {
        var entity = new UserProduct(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void PromoCode_PartialConstructor()
    {
        var entity = new PromoCode(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void Product_PartialConstructor()
    {
        var entity = new Product(new { });
        entity.Should().NotBeNull();
    }
}

#endregion

#region PromoStackingRule Tests

public class PromoStackingRuleAdditionalTests
{
    [Fact]
    public void PromoStackingRule_PartialConstructor()
    {
        var rule = new PromoStackingRule(new { });
        rule.Should().NotBeNull();
    }

    [Fact]
    public void PromoStackingRule_DefaultValues()
    {
        var rule = new PromoStackingRule { Name = "Default" };
        rule.IsActive.Should().BeTrue();
        rule.Priority.Should().Be(0);
        rule.MaxStackableCount.Should().Be(3);
        rule.AllowExclusiveStacking.Should().BeFalse();
        rule.AllowSameTypeStacking.Should().BeFalse();
        rule.ConflictStrategy.Should().Be(ConflictResolutionStrategy.HighestDiscount);
    }

    [Fact]
    public void CanStack_InactiveRule_ReturnsFalse()
    {
        var rule = new PromoStackingRule { Name = "Inactive", IsActive = false };
        var code1 = new PromoCode { Code = "A", Name = "A" };
        var code2 = new PromoCode { Code = "B", Name = "B" };

        rule.CanStack(code1, code2).Should().BeFalse();
    }

    [Fact]
    public void CanStack_ExclusiveCode_NoAllow_ReturnsFalse()
    {
        var rule = new PromoStackingRule { Name = "NoExclusive", IsActive = true, AllowExclusiveStacking = false };
        var code1 = new PromoCode { Code = "A", Name = "A", IsExclusive = true };
        var code2 = new PromoCode { Code = "B", Name = "B" };

        rule.CanStack(code1, code2).Should().BeFalse();
    }

    [Fact]
    public void CanStack_SameType_NoAllow_ReturnsFalse()
    {
        var rule = new PromoStackingRule { Name = "NoSameType", IsActive = true, AllowSameTypeStacking = false };
        var code1 = new PromoCode { Code = "A", Name = "A", Type = PromoCodeType.PercentageOff };
        var code2 = new PromoCode { Code = "B", Name = "B", Type = PromoCodeType.PercentageOff };

        rule.CanStack(code1, code2).Should().BeFalse();
    }

    [Fact]
    public void CanStack_DifferentTypes_Active_ReturnsTrue()
    {
        var rule = new PromoStackingRule { Name = "Allow", IsActive = true, AllowSameTypeStacking = false };
        var code1 = new PromoCode { Code = "A", Name = "A", Type = PromoCodeType.PercentageOff };
        var code2 = new PromoCode { Code = "B", Name = "B", Type = PromoCodeType.FixedAmountOff };

        rule.CanStack(code1, code2).Should().BeTrue();
    }

    [Fact]
    public void CanStack_SameType_AllowSame_ReturnsTrue()
    {
        var rule = new PromoStackingRule { Name = "AllowSame", IsActive = true, AllowSameTypeStacking = true };
        var code1 = new PromoCode { Code = "A", Name = "A", Type = PromoCodeType.PercentageOff };
        var code2 = new PromoCode { Code = "B", Name = "B", Type = PromoCodeType.PercentageOff };

        rule.CanStack(code1, code2).Should().BeTrue();
    }
}

#endregion

#region ProductPricingVersion Tests

public class ProductPricingVersionAdditionalTests
{
    [Fact]
    public void Create_ValidInput_ReturnsVersion()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Standard", 19.99m);

        var version = ProductPricingVersion.Create(pricing, 2, DateTime.UtcNow, "Update", Guid.NewGuid());

        version.Should().NotBeNull();
        version.PriceVersion.Should().Be(2);
        version.ChangeReason.Should().Be("Update");
        version.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_NullPricing_Throws()
    {
        var act = () => ProductPricingVersion.Create(null!, 1, DateTime.UtcNow);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_InvalidVersion_Throws()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 10m);
        var act = () => ProductPricingVersion.Create(pricing, 0, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateInitial_ReturnsVersionOne()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 10m);
        var version = ProductPricingVersion.CreateInitial(pricing, Guid.NewGuid());
        version.PriceVersion.Should().Be(1);
    }

    [Fact]
    public void GetEffectivePrice_NoSale_ReturnsBasePrice()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 29.99m);
        var version = ProductPricingVersion.Create(pricing, 1, DateTime.UtcNow);
        var price = version.GetEffectivePrice(DateTime.UtcNow);
        price.Should().Be(29.99m);
    }

    [Fact]
    public void GetEffectivePrice_WithSale_ReturnsSalePrice()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 29.99m, salePrice: 19.99m);
        var version = ProductPricingVersion.Create(pricing, 1, DateTime.UtcNow);
        var price = version.GetEffectivePrice(DateTime.UtcNow);
        price.Should().Be(19.99m);
    }
}

#endregion

#region ProductPricing Additional Tests

public class ProductPricingAdditionalTests
{
    [Fact]
    public void CreateWithVersion_Full_ReturnsCorrectPricing()
    {
        var productId = Guid.NewGuid();
        var (pricing, version) = ProductPricing.CreateWithVersion(
            productId, "Premium", 49.99m, "EUR", 39.99m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), true,
            Guid.NewGuid(), Guid.NewGuid());

        pricing.Name.Should().Be("Premium");
        pricing.BasePrice.Should().Be(49.99m);
        pricing.Currency.Should().Be("EUR");
        pricing.IsDefault.Should().BeTrue();
        version.PriceVersion.Should().Be(1);
    }

    [Fact]
    public void CreateWithVersion_Minimal_ReturnsCorrectPricing()
    {
        var (pricing, version) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Default", 9.99m);
        pricing.BasePrice.Should().Be(9.99m);
        pricing.Currency.Should().Be("USD");
        version.Should().NotBeNull();
    }

    [Fact]
    public void CreateWithVersion_EmptyProductId_Throws()
    {
        var act = () => ProductPricing.CreateWithVersion(Guid.Empty, "Test", 10m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateWithVersion_NegativePrice_Throws()
    {
        var act = () => ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", -1m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateBasePrice_ValidPrice_ReturnsVersion()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 10m);
        var version = pricing.UpdateBasePrice(20m, "Price increase");
        pricing.BasePrice.Should().Be(20m);
        version.ChangeReason.Should().Be("Price increase");
    }

    [Fact]
    public void UpdateBasePrice_NegativePrice_Throws()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 10m);
        var act = () => pricing.UpdateBasePrice(-5m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateSalePrice_ValidPrice_ReturnsVersion()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 20m);
        var version = pricing.UpdateSalePrice(15m, "Sale");
        pricing.SalePrice.Should().Be(15m);
        version.ChangeReason.Should().Be("Sale");
    }

    [Fact]
    public void UpdateSalePrice_NegativePrice_Throws()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 20m);
        var act = () => pricing.UpdateSalePrice(-1m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateSalePrice_Null_ClearsSale()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 20m, salePrice: 15m);
        pricing.UpdateSalePrice(null, "End sale");
        pricing.SalePrice.Should().BeNull();
    }

    [Fact]
    public void UpdatePrices_ValidPrices_ReturnsVersion()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 30m);
        var version = pricing.UpdatePrices(25m, 20m, "Flash sale");
        pricing.BasePrice.Should().Be(25m);
        pricing.SalePrice.Should().Be(20m);
        version.ChangeReason.Should().Be("Flash sale");
    }

    [Fact]
    public void UpdatePrices_NegativeBase_Throws()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 30m);
        var act = () => pricing.UpdatePrices(-1m, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdatePrices_NegativeSale_Throws()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 30m);
        var act = () => pricing.UpdatePrices(20m, -1m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdatePrices_SaleGreaterThanBase_Throws()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 30m);
        var act = () => pricing.UpdatePrices(20m, 25m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateInitialVersion_Works()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Standard", 9.99m);
        var version = pricing.CreateInitialVersion(Guid.NewGuid());
        version.PriceVersion.Should().Be(1);
    }

    [Fact]
    public void GetVersionAt_NoVersions_ReturnsNull()
    {
        var pricing = new ProductPricing { ProductId = Guid.NewGuid(), Name = "Test" };
        var version = pricing.GetVersionAt(DateTime.UtcNow);
        version.Should().BeNull();
    }

    [Fact]
    public void GetCurrentActiveVersion_NoVersions_ReturnsNull()
    {
        var pricing = new ProductPricing { ProductId = Guid.NewGuid(), Name = "Test" };
        var version = pricing.GetCurrentActiveVersion();
        version.Should().BeNull();
    }

    [Fact]
    public void GetCurrentPrice_NoSale_ReturnsBasePrice()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 19.99m);
        pricing.GetCurrentPrice().Should().Be(19.99m);
    }

    [Fact]
    public void GetCurrentPrice_ActiveSale_ReturnsSalePrice()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(
            Guid.NewGuid(), "Test", 29.99m, "USD",
            19.99m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7), false);
        pricing.GetCurrentPrice().Should().Be(19.99m);
    }

    [Fact]
    public void IsSaleActive_NoSalePrice_ReturnsFalse()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(Guid.NewGuid(), "Test", 19.99m);
        pricing.IsSaleActive().Should().BeFalse();
    }

    [Fact]
    public void IsSaleActive_ActiveSale_ReturnsTrue()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(
            Guid.NewGuid(), "Test", 29.99m, "USD",
            19.99m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7), false);
        pricing.IsSaleActive().Should().BeTrue();
    }

    [Fact]
    public void IsSaleActive_NullDates_WithSalePrice_ReturnsTrue()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(
            Guid.NewGuid(), "Test", 29.99m, "USD", 19.99m, null, null, false);
        pricing.IsSaleActive().Should().BeTrue();
    }
}

#endregion

#region Controller Constructor Tests

public class ProductControllerConstructorTests
{
    [Fact]
    public void ProductsController_CanBeConstructed()
    {
        var controller = new ProductsController(
            new Mock<IMediator>().Object,
            new Mock<IActorContextAccessor>().Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void PromoCodesController_CanBeConstructed()
    {
        var controller = new PromoCodesController(new Mock<IMediator>().Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void EntitlementsController_CanBeConstructed()
    {
        var controller = new EntitlementsController(
            new Mock<IEntitlementService>().Object,
            new Mock<IActorContextAccessor>().Object,
            new Mock<ISender>().Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void UserEntitlementsController_CanBeConstructed()
    {
        var controller = new UserEntitlementsController(
            new Mock<IEntitlementService>().Object,
            new Mock<IActorContextAccessor>().Object);
        controller.Should().NotBeNull();
    }
}

#endregion

#region CreatorExtensions Tests

public class CreatorExtensionsAdditionalTests
{
    [Fact]
    public void ToCreatorInfo_FromUser_ReturnsInfo()
    {
        var user = new GameGuild.Identity.Users.User
        {
            Name = "testuser",
            Email = "test@example.com"
        };

        var info = user.ToCreatorInfo();
        info.Should().NotBeNull();
        info.Name.Should().Be("testuser");
        info.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void ToCreatorInfo_NullUser_ThrowsArgumentNullException()
    {
        GameGuild.Identity.Users.User? user = null;
        var act = () => user!.ToCreatorInfo();
        act.Should().Throw<ArgumentNullException>();
    }
}

#endregion

#region PromoCode Entity Methods Tests

public class PromoCodeMethodTests
{
    [Fact]
    public void IsCurrentlyValid_ActiveAndInRange_ReturnsTrue()
    {
        var promo = new PromoCode
        {
            Code = "TEST", Name = "Test",
            IsActive = true,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidUntil = DateTime.UtcNow.AddDays(1)
        };
        promo.IsCurrentlyValid().Should().BeTrue();
    }

    [Fact]
    public void IsCurrentlyValid_Inactive_ReturnsFalse()
    {
        var promo = new PromoCode
        {
            Code = "TEST", Name = "Test",
            IsActive = false
        };
        promo.IsCurrentlyValid().Should().BeFalse();
    }

    [Fact]
    public void GetIsExclusive_ReturnsValue()
    {
        var promo = new PromoCode { Code = "TEST", Name = "Test", IsExclusive = true };
        promo.GetIsExclusive().Should().BeTrue();
    }
}

#endregion
