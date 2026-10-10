using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using GameGuild.Commerce.Products;

namespace GameGuild.Commerce.Products.UnitTests;

public class ExtendedCoverageTests
{
    // ── Additional DTO instantiation for coverage ───────────────────────
    [Fact]
    public void PromoCodeApplicationResult_WithAppliedAndRejected()
    {
        var applied = new List<AppliedPromoCode>
        {
            new("CODE1", 10m, 10m),
            new("CODE2", 5m, null)
        };
        var rejected = new List<RejectedPromoCode>
        {
            new("BAD1", "Expired"),
            new("BAD2", "Invalid")
        };
        var result = new PromoCodeApplicationResult(200m, 185m, 15m, applied, rejected);
        result.AppliedCodes.Should().HaveCount(2);
        result.RejectedCodes.Should().HaveCount(2);
        result.OriginalAmount.Should().Be(200m);
        result.FinalAmount.Should().Be(185m);
    }

    [Fact]
    public void PromoStackingRuleDto_AllStrategies()
    {
        foreach (var strategy in Enum.GetValues<ConflictResolutionStrategy>())
        {
            var dto = new PromoStackingRuleDto(
                Guid.NewGuid(), $"Rule_{strategy}", null, true, 1, 5,
                false, 75m, 100m, strategy);
            dto.ConflictStrategy.Should().Be(strategy);
            dto.MaxStackableCount.Should().Be(5);
        }
    }

    [Fact]
    public void PricingCalculationResult_WithMultipleCodes()
    {
        var codes = new List<string> { "PROMO1", "PROMO2", "PROMO3" };
        var result = new PricingCalculationResult(
            200m, 150m, true, 30m, 120m, "EUR", codes);
        result.Currency.Should().Be("EUR");
        result.AppliedPromoCodes.Should().HaveCount(3);
        result.IsSaleActive.Should().BeTrue();
        result.PromoDiscount.Should().Be(30m);
    }

    // ── Entitlement edge cases ──────────────────────────────────────────
    [Fact]
    public void EntitlementInfo_WithSubscription()
    {
        var info = new EntitlementInfo(
            Guid.NewGuid(), "Sub Product", ProductAccessStatus.Active,
            ProductAcquisitionType.Subscription, DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1), true,
            EntitlementSubscriptionStatus.Active, 9.99m, "USD");
        info.IsSubscription.Should().BeTrue();
        info.SubscriptionStatus.Should().Be(EntitlementSubscriptionStatus.Active);
        info.AccessEndDate.Should().NotBeNull();
    }

    [Fact]
    public void EntitlementInfoDto_AllAcquisitionTypes()
    {
        var dto = new EntitlementInfoDto(
            Guid.NewGuid(), "Product", "Expired", "Trial",
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-1),
            true, "Cancelled", 0m, "USD");
        dto.Status.Should().Be("Expired");
        dto.AcquisitionType.Should().Be("Trial");
        dto.IsSubscription.Should().BeTrue();
        dto.SubscriptionStatus.Should().Be("Cancelled");
    }

    [Fact]
    public void ProductAccessCheckResult_NoAccess()
    {
        var r = new ProductAccessCheckResult(false);
        r.HasAccess.Should().BeFalse();
        r.AccessStatus.Should().BeNull();
        r.AcquisitionType.Should().BeNull();
    }

    [Fact]
    public void UserProductAccessDto_AllStatuses()
    {
        foreach (var status in Enum.GetValues<ProductAccessStatus>())
        {
            var dto = new UserProductAccessDto(
                Guid.NewGuid(), Guid.NewGuid(), status,
                ProductAcquisitionType.Grant, 0m, "USD",
                DateTime.UtcNow, null, DateTime.UtcNow);
            dto.AccessStatus.Should().Be(status);
        }
    }

    // ── Command variations ──────────────────────────────────────────────
    [Fact]
    public void CreateProductCommand_WithAllOptions()
    {
        var cmd = new CreateProductCommand(
            "Full Product", "Full description", "Short desc",
            "http://img.png", ProductType.Course, true,
            Guid.NewGuid(), new List<Guid> { Guid.NewGuid() },
            25m, 10m, 20m, Guid.NewGuid());
        cmd.Type.Should().Be(ProductType.Course);
        cmd.IsBundle.Should().BeTrue();
        cmd.BundleItems.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateProductCommand_WithAllFields()
    {
        var cmd = new UpdateProductCommand(
            Guid.NewGuid(), "Updated", "New desc", "New short",
            "http://new.png", ProductType.Workshop, false,
            new List<Guid>(), 15m, 5m, 10m, 42L);
        cmd.Name.Should().Be("Updated");
        cmd.ExpectedVersion.Should().Be(42L);
    }

    [Fact]
    public void DeleteProductCommand_HardDelete()
    {
        var cmd = new DeleteProductCommand(Guid.NewGuid(), false, "Cleanup", 5L);
        cmd.SoftDelete.Should().BeFalse();
        cmd.Reason.Should().Be("Cleanup");
    }

    [Fact]
    public void SetProductPricingCommand_WithSale()
    {
        var cmd = new SetProductPricingCommand(
            Guid.NewGuid(), "Sale Pricing", 100m, "EUR",
            80m, DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            true, Guid.NewGuid(), Guid.NewGuid());
        cmd.SalePrice.Should().Be(80m);
        cmd.IsDefault.Should().BeTrue();
        cmd.Currency.Should().Be("EUR");
    }

    [Fact]
    public void GrantProductAccessCommand_WithSubscription()
    {
        var cmd = new GrantProductAccessCommand(
            Guid.NewGuid(), Guid.NewGuid(),
            ProductAcquisitionType.Subscription, 9.99m, "EUR",
            DateTime.UtcNow.AddMonths(1), Guid.NewGuid());
        cmd.AcquisitionType.Should().Be(ProductAcquisitionType.Subscription);
        cmd.SubscriptionId.Should().NotBeNull();
    }

    [Fact]
    public void CreatePromoCodeCommand_FullyPopulated()
    {
        var cmd = new CreatePromoCodeCommand(
            "FULL20", "Full Promo", "All options",
            PromoCodeType.BuyOneGetOne, 20m, 10m, null, "GBP",
            50m, 100, 3, null, null, false,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            true, true, 5, "launch-2026", Guid.NewGuid(), Guid.NewGuid());
        cmd.Type.Should().Be(PromoCodeType.BuyOneGetOne);
        cmd.IsExclusive.Should().BeTrue();
        cmd.MaxUses.Should().Be(100);
    }

    [Fact]
    public void UpdatePromoCodeCommand_CanBeCreated()
    {
        var cmd = new UpdatePromoCodeCommand(Guid.NewGuid(), "Updated");
        cmd.Name.Should().Be("Updated");
    }

    [Fact]
    public void DeletePromoCodeCommand_CanBeCreated()
    {
        var cmd = new DeletePromoCodeCommand(Guid.NewGuid());
        cmd.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void AddOrderItemCommand_CanBeCreated()
    {
        var cmd = new AddOrderItemCommand(Guid.NewGuid(), Guid.NewGuid(), 3, "PROMO10");
        cmd.Quantity.Should().Be(3);
        cmd.PromoCode.Should().Be("PROMO10");
    }

    [Fact]
    public void CompleteOrderCommand_CanBeCreated()
    {
        var cmd = new CompleteOrderCommand(Guid.NewGuid(), "pi_123", "card");
        cmd.PaymentProviderReference.Should().Be("pi_123");
    }

    // ── Enum coverage ───────────────────────────────────────────────────
    [Fact]
    public void ProductType_AllValues()
    {
        var values = Enum.GetValues<ProductType>();
        values.Should().Contain(ProductType.Program);
        values.Should().Contain(ProductType.Other);
        values.Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public void ProductAcquisitionType_AllValues()
    {
        var values = Enum.GetValues<ProductAcquisitionType>();
        values.Should().Contain(ProductAcquisitionType.Purchase);
        values.Should().Contain(ProductAcquisitionType.Gift);
    }

    [Fact]
    public void PromoCodeType_AllValues()
    {
        var values = Enum.GetValues<PromoCodeType>();
        values.Should().Contain(PromoCodeType.PercentageOff);
        values.Should().Contain(PromoCodeType.FreeShipping);
    }

    [Fact]
    public void BundleValidationErrorCode_AllValues()
    {
        var values = Enum.GetValues<BundleValidationErrorCode>();
        values.Should().Contain(BundleValidationErrorCode.NotABundle);
        values.Should().Contain(BundleValidationErrorCode.BundleCannotContainBundles);
    }

    [Fact]
    public void EntitlementSubscriptionStatus_AllValues()
    {
        var values = Enum.GetValues<EntitlementSubscriptionStatus>();
        values.Should().Contain(EntitlementSubscriptionStatus.Active);
        values.Should().Contain(EntitlementSubscriptionStatus.Suspended);
    }

    // ── Additional validator tests ──────────────────────────────────────
    [Fact]
    public void AddOrderItemCommandValidator_CanBeCreated()
    {
        var v = new AddOrderItemCommandValidator();
        v.Should().NotBeNull();
    }

    [Fact]
    public void CompleteOrderCommandValidator_CanBeCreated()
    {
        var v = new CompleteOrderCommandValidator();
        v.Should().NotBeNull();
    }

    [Fact]
    public void GetProductByIdQueryValidator_CanBeCreated()
    {
        var v = new GetProductByIdQueryValidator();
        v.Should().NotBeNull();
    }

    [Fact]
    public void GetProductsPagedQueryValidator_CanBeCreated()
    {
        var v = new GetProductsPagedQueryValidator();
        v.Should().NotBeNull();
    }

    [Fact]
    public void GetProductPricingQueryValidator_CanBeCreated()
    {
        var v = new GetProductPricingQueryValidator();
        v.Should().NotBeNull();
    }

    [Fact]
    public void CalculateProductPriceQueryValidator_CanBeCreated()
    {
        var v = new CalculateProductPriceQueryValidator();
        v.Should().NotBeNull();
    }

    [Fact]
    public void CheckProductAccessQueryValidator_CanBeCreated()
    {
        var v = new CheckProductAccessQueryValidator();
        v.Should().NotBeNull();
    }

    [Fact]
    public void GetUserProductsQueryValidator_CanBeCreated()
    {
        var v = new GetUserProductsQueryValidator();
        v.Should().NotBeNull();
    }

    [Fact]
    public void GetPromoCodeByIdQueryValidator_CanBeCreated()
    {
        var v = new GetPromoCodeByIdQueryValidator();
        v.Should().NotBeNull();
    }

    [Fact]
    public void GetPromoCodesQueryValidator_CanBeCreated()
    {
        var v = new GetPromoCodesQueryValidator();
        v.Should().NotBeNull();
    }

    [Fact]
    public void GetActivePromoCodesQueryValidator_CanBeCreated()
    {
        var v = new GetActivePromoCodesQueryValidator();
        v.Should().NotBeNull();
    }
}
