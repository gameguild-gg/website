using FluentAssertions;
using GameGuild.Commerce.Products;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests;

public class ProductPricingTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IPromoCodeRepository> _promoCodeRepository = new();

    [Fact]
    public void GetCurrentPrice_WithNoActiveSale_ReturnsBasePrice()
    {
        var (pricing, _) = ProductPricing.CreateWithVersion(
            Guid.NewGuid(),
            "Standard",
            149m,
            salePrice: null,
            isDefault: true);

        pricing.GetCurrentPrice().Should().Be(149m);
        pricing.IsSaleActive().Should().BeFalse();
    }

    [Fact]
    public void GetCurrentPrice_WithActiveSale_ReturnsSalePrice()
    {
        var now = SystemClock.UtcNow;
        var (pricing, _) = ProductPricing.CreateWithVersion(
            Guid.NewGuid(),
            "Launch",
            200m,
            "USD",
            125m,
            now.AddDays(-1),
            now.AddDays(1),
            isDefault: true);

        pricing.IsSaleActive().Should().BeTrue();
        pricing.GetCurrentPrice().Should().Be(125m);
    }

    [Fact]
    public void UpdatePrices_ShouldCreateNewActiveVersionAndSupersedePreviousVersion()
    {
        var (pricing, initialVersion) = ProductPricing.CreateWithVersion(
            Guid.NewGuid(),
            "Default",
            250m,
            isDefault: true);
        pricing.Versions.Add(initialVersion);

        var changedBy = Guid.NewGuid();
        var newVersion = pricing.UpdatePrices(300m, 199m, "Seasonal update", changedBy);

        pricing.BasePrice.Should().Be(300m);
        pricing.SalePrice.Should().Be(199m);
        pricing.GetCurrentPrice().Should().Be(199m);
        initialVersion.IsActive.Should().BeFalse();
        initialVersion.EffectiveTo.Should().NotBeNull();
        newVersion.IsActive.Should().BeTrue();
        newVersion.PriceVersion.Should().Be(2);
        newVersion.ChangeReason.Should().Be("Seasonal update");
        newVersion.CreatedByUserId.Should().Be(changedBy);
    }

    [Fact]
    public async Task CalculatePriceAsync_WithPercentageDiscount_AppliesDiscountToEffectivePrice()
    {
        var product = CreateProductWithPricing(100m);
        var promo = CreatePromo("SAVE20", PromoCodeType.PercentageOff, percentage: 20m);
        _promoCodeRepository.Setup(r => r.GetByCodeAsync("SAVE20", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);

        var result = await CreateService().CalculatePriceAsync(product, promoCodes: new[] { "SAVE20" });

        result.BasePrice.Should().Be(100m);
        result.PromoDiscount.Should().Be(20m);
        result.FinalPrice.Should().Be(80m);
        result.AppliedPromoCodes.Should().Equal("SAVE20");
    }

    [Fact]
    public async Task CalculatePriceAsync_WithFixedDiscount_AppliesDiscountWithoutGoingBelowZero()
    {
        var product = CreateProductWithPricing(30m);
        var promo = CreatePromo("TAKE50", PromoCodeType.FixedAmountOff, amount: 50m);
        _promoCodeRepository.Setup(r => r.GetByCodeAsync("TAKE50", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);

        var result = await CreateService().CalculatePriceAsync(product, promoCodes: new[] { "TAKE50" });

        result.PromoDiscount.Should().Be(30m);
        result.FinalPrice.Should().Be(0m);
    }

    [Fact]
    public async Task ApplyPromoCodesAsync_WithMultipleDiscounts_AppliesInOrderToRemainingAmount()
    {
        var percentage = CreatePromo("SAVE10", PromoCodeType.PercentageOff, percentage: 10m);
        var fixedAmount = CreatePromo("TAKE15", PromoCodeType.FixedAmountOff, amount: 15m);
        _promoCodeRepository.Setup(r => r.GetByCodeAsync("SAVE10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(percentage);
        _promoCodeRepository.Setup(r => r.GetByCodeAsync("TAKE15", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixedAmount);

        var result = await CreateService().ApplyPromoCodesAsync(100m, new[] { "SAVE10", "TAKE15" });

        result.TotalDiscount.Should().Be(25m);
        result.FinalAmount.Should().Be(75m);
        result.AppliedCodes.Select(code => code.Code).Should().Equal("SAVE10", "TAKE15");
        result.RejectedCodes.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyPromoCodesAsync_WithExclusiveCode_ShouldRejectLaterCodes()
    {
        var exclusive = CreatePromo("ONLYONE", PromoCodeType.PercentageOff, percentage: 25m, isExclusive: true);
        var later = CreatePromo("TAKE10", PromoCodeType.FixedAmountOff, amount: 10m);
        _promoCodeRepository.Setup(r => r.GetByCodeAsync("ONLYONE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(exclusive);
        _promoCodeRepository.Setup(r => r.GetByCodeAsync("TAKE10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(later);

        var result = await CreateService().ApplyPromoCodesAsync(100m, new[] { "ONLYONE", "TAKE10" });

        result.TotalDiscount.Should().Be(25m);
        result.FinalAmount.Should().Be(75m);
        result.AppliedCodes.Select(code => code.Code).Should().Equal("ONLYONE");
        result.RejectedCodes.Should().ContainSingle(code =>
            code.Code == "TAKE10" && code.Reason.Contains("exclusive", StringComparison.OrdinalIgnoreCase));
    }

    private PricingEngineService CreateService()
        => new(
            _productRepository.Object,
            _promoCodeRepository.Object,
            Mock.Of<IPricingRuleRepository>(),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

    private static Product CreateProductWithPricing(decimal basePrice, decimal? salePrice = null)
    {
        var product = Product.Create("Listing Syndication", ProductType.Service);
        var (pricing, initialVersion) = ProductPricing.CreateWithVersion(
            product.Id,
            "Default",
            basePrice,
            salePrice,
            isDefault: true);
        pricing.Versions.Add(initialVersion);
        product.Pricing.Add(pricing);
        return product;
    }

    private static PromoCode CreatePromo(
        string code,
        PromoCodeType type,
        decimal? percentage = null,
        decimal? amount = null,
        bool isExclusive = false)
    {
        return new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code,
            Type = type,
            DiscountPercentage = percentage,
            DiscountAmount = amount,
            IsActive = true,
            IsExclusive = isExclusive,
            CreatedBy = Guid.NewGuid(),
            ValidFrom = SystemClock.UtcNow.AddDays(-1),
            ValidUntil = SystemClock.UtcNow.AddDays(1)
        };
    }
}
