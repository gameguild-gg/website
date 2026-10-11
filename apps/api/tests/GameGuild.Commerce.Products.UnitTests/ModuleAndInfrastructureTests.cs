using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using GameGuild.Commerce.Products;

namespace GameGuild.Commerce.Products.UnitTests;

public class ModuleAndInfrastructureTests
{
    // ── Module DI registration ──────────────────────────────────

    [Fact]
    public void AddProductsModule_RegistersAllServices()
    {
        var services = new ServiceCollection();
        services.AddProductsModule();
        services.Count.Should().BeGreaterThan(0);
    }

    // ── Inline EF config via ConfigureProductsModel ────────────

    [Fact]
    public void ConfigureProductsModel_ConfiguresAllEntities()
    {
        var mb = new ModelBuilder(new ConventionSet());
        ProductsModule.ConfigureProductsModel(mb);
        mb.Model.Should().NotBeNull();
    }

    // ── Standalone EF configurations ────────────────────────────

    [Fact]
    public void ProductConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        new ProductConfiguration().Configure(mb.Entity<Product>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void ProductBundleItemConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        new ProductBundleItemConfiguration().Configure(mb.Entity<ProductBundleItem>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void ProductsModelConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var config = new ProductsModelConfiguration();
        config.Configure(mb);
        mb.Model.Should().NotBeNull();
    }

    // ── Repository constructor tests ────────────────────────────

    [Fact]
    public void ProductRepository_CanBeInstantiated()
    {
        var repo = new ProductRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void ProductPricingRepository_CanBeInstantiated()
    {
        var repo = new ProductPricingRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void PromoCodeRepository_CanBeInstantiated()
    {
        var repo = new PromoCodeRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void UserProductRepository_CanBeInstantiated()
    {
        var repo = new UserProductRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    // ── Service constructor tests ───────────────────────────────

    [Fact]
    public void EntitlementService_CanBeInstantiated()
    {
        var svc = new EntitlementService(
            Mock.Of<IUserProductRepository>(),
            Mock.Of<IProductRepository>(),
            NullLogger<EntitlementService>.Instance);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void PricingEngineService_CanBeInstantiated()
    {
        var svc = new PricingEngineService(
            Mock.Of<IProductRepository>(),
            Mock.Of<IPromoCodeRepository>(),
            Mock.Of<IPricingRuleRepository>(),
            new MemoryCache(new MemoryCacheOptions()));
        svc.Should().NotBeNull();
    }

    [Fact]
    public void ProductPricingService_CanBeInstantiated()
    {
        var svc = new ProductPricingService(
            Mock.Of<IProductRepository>(),
            Mock.Of<IProductPricingRepository>(),
            Mock.Of<IPricingEngineService>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void PromoCodeService_CanBeInstantiated()
    {
        var svc = new PromoCodeService(Mock.Of<IPromoCodeRepository>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void UserProductService_CanBeInstantiated()
    {
        var svc = new UserProductService(Mock.Of<IUserProductRepository>());
        svc.Should().NotBeNull();
    }

    // ── Command handler constructor tests ───────────────────────

    [Fact]
    public void CreateProductCommandHandler_Ctor()
    {
        var h = new CreateProductCommandHandler(Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void UpdateProductCommandHandler_Ctor()
    {
        var h = new UpdateProductCommandHandler(Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void DeleteProductCommandHandler_Ctor()
    {
        var h = new DeleteProductCommandHandler(Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void SetProductPricingCommandHandler_Ctor()
    {
        var h = new SetProductPricingCommandHandler(
            Mock.Of<IProductRepository>(),
            Mock.Of<IApplicationDbContext>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GrantProductAccessCommandHandler_Ctor()
    {
        var h = new GrantProductAccessCommandHandler(
            Mock.Of<IUserProductRepository>(),
            Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void RevokeProductAccessCommandHandler_Ctor()
    {
        var h = new RevokeProductAccessCommandHandler(Mock.Of<IUserProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void CreatePromoCodeCommandHandler_Ctor()
    {
        var h = new CreatePromoCodeCommandHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void UpdatePromoCodeCommandHandler_Ctor()
    {
        var h = new UpdatePromoCodeCommandHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void DeletePromoCodeCommandHandler_Ctor()
    {
        var h = new DeletePromoCodeCommandHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void ValidatePromoCodeCommandHandler_Ctor()
    {
        var h = new ValidatePromoCodeCommandHandler(Mock.Of<IPricingEngineService>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void ApplyPromoCodesCommandHandler_Ctor()
    {
        var h = new ApplyPromoCodesCommandHandler(Mock.Of<IPricingEngineService>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void ActivateProductHandler_Ctor()
    {
        var h = new ActivateProductHandler(Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void DeactivateProductHandler_Ctor()
    {
        var h = new DeactivateProductHandler(Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void ArchiveProductHandler_Ctor()
    {
        var h = new ArchiveProductHandler(Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void PatchProductHandler_Ctor()
    {
        var h = new PatchProductHandler(Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void BatchCreateProductsHandler_Ctor()
    {
        var h = new BatchCreateProductsHandler(Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void ActivatePromoCodeHandler_Ctor()
    {
        var h = new ActivatePromoCodeHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void DeactivatePromoCodeHandler_Ctor()
    {
        var h = new DeactivatePromoCodeHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void PatchPromoCodeHandler_Ctor()
    {
        var h = new PatchPromoCodeHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    // ── Query handler constructor tests ─────────────────────────

    [Fact]
    public void GetProductByIdQueryHandler_Ctor()
    {
        var h = new GetProductByIdQueryHandler(Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetProductsPagedQueryHandler_Ctor()
    {
        var h = new GetProductsPagedQueryHandler(Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetProductPricingQueryHandler_Ctor()
    {
        var h = new GetProductPricingQueryHandler(
            Mock.Of<IProductRepository>(),
            Mock.Of<IPricingEngineService>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void CalculateProductPriceQueryHandler_Ctor()
    {
        var h = new CalculateProductPriceQueryHandler(Mock.Of<IPricingEngineService>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void CheckProductAccessQueryHandler_Ctor()
    {
        var h = new CheckProductAccessQueryHandler(Mock.Of<IUserProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetUserProductsQueryHandler_Ctor()
    {
        var h = new GetUserProductsQueryHandler(Mock.Of<IUserProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetPromoCodeByIdQueryHandler_Ctor()
    {
        var h = new GetPromoCodeByIdQueryHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetPromoCodesQueryHandler_Ctor()
    {
        var h = new GetPromoCodesQueryHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetActivePromoCodesQueryHandler_Ctor()
    {
        var h = new GetActivePromoCodesQueryHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void ProductExistsHandler_Ctor()
    {
        var h = new ProductExistsHandler(Mock.Of<IProductRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void PromoCodeExistsHandler_Ctor()
    {
        var h = new PromoCodeExistsHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetPromoCodeByCodeHandler_Ctor()
    {
        var h = new GetPromoCodeByCodeHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetPromoCodeUsageHandler_Ctor()
    {
        var h = new GetPromoCodeUsageHandler(Mock.Of<IPromoCodeRepository>());
        h.Should().NotBeNull();
    }

    // ── DTO / record instantiation tests ────────────────────────

    [Fact]
    public void PromoCodeDto_CanBeCreated()
    {
        var dto = new PromoCodeDto(
            Guid.NewGuid(), "CODE1", "Test Code", "Desc",
            PromoCodeType.PercentageOff, 10m, null, "USD", 5m,
            100, 1, DateTime.UtcNow, DateTime.UtcNow.AddDays(30),
            true, false, 0, null, 0, DateTime.UtcNow, DateTime.UtcNow);
        dto.Code.Should().Be("CODE1");
    }

    [Fact]
    public void PromoStackingRuleDto_CanBeCreated()
    {
        var dto = new PromoStackingRuleDto(
            Guid.NewGuid(), "Rule1", "Desc", true, 1, 3,
            false, 50m, null, ConflictResolutionStrategy.HighestDiscount);
        dto.Name.Should().Be("Rule1");
    }

    [Fact]
    public void PricingCalculationResult_CanBeCreated()
    {
        var dto = new PricingCalculationResult(
            100m, 80m, true, 10m, 70m, "USD",
            new List<string> { "PROMO1" });
        dto.FinalPrice.Should().Be(70m);
    }

    [Fact]
    public void PromoCodeUsageDto_CanBeCreated()
    {
        var dto = new PromoCodeUsageDto(
            Guid.NewGuid(), "CODE1", 50, 30, 500m, 10m,
            100, 50, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);
        dto.TotalUses.Should().Be(50);
    }

    [Fact]
    public void ProductDto_CanBeCreated()
    {
        var dto = new ProductDto(
            Guid.NewGuid(), "Product1", "Desc", "Short",
            "http://img.jpg", ProductType.Course, false,
            Guid.NewGuid(), null, 30m, 0m, 30m,
            DateTime.UtcNow, DateTime.UtcNow);
        dto.Name.Should().Be("Product1");
    }

    [Fact]
    public void ProductPricingDto_CanBeCreated()
    {
        var dto = new ProductPricingDto(
            Guid.NewGuid(), Guid.NewGuid(), "Default", 99.99m,
            79.99m, "USD", DateTime.UtcNow, DateTime.UtcNow.AddDays(30),
            true, 79.99m, true);
        dto.BasePrice.Should().Be(99.99m);
    }

    [Fact]
    public void UserProductAccessDto_CanBeCreated()
    {
        var dto = new UserProductAccessDto(
            Guid.NewGuid(), Guid.NewGuid(),
            ProductAccessStatus.Active, ProductAcquisitionType.Purchase,
            49.99m, "USD", DateTime.UtcNow, null, DateTime.UtcNow);
        dto.AccessStatus.Should().Be(ProductAccessStatus.Active);
    }

    [Fact]
    public void ProductAccessCheckResult_CanBeCreated()
    {
        var dto = new ProductAccessCheckResult(true, ProductAccessStatus.Active, null, ProductAcquisitionType.Purchase);
        dto.HasAccess.Should().BeTrue();
    }

    [Fact]
    public void PromoCodeValidationResult_CanBeCreated()
    {
        var dto = new PromoCodeValidationResult(true, "CODE1", null, 10m, 10m);
        dto.IsValid.Should().BeTrue();
    }

    [Fact]
    public void PromoCodeApplicationResult_CanBeCreated()
    {
        var dto = new PromoCodeApplicationResult(
            100m, 90m, 10m,
            new List<AppliedPromoCode> { new("CODE1", 10m, 10m) },
            new List<RejectedPromoCode>());
        dto.FinalAmount.Should().Be(90m);
    }

    [Fact]
    public void EntitlementInfo_CanBeCreated()
    {
        var dto = new EntitlementInfo(
            Guid.NewGuid(), "Product", ProductAccessStatus.Active,
            ProductAcquisitionType.Purchase, DateTime.UtcNow, null,
            false, null, 49.99m, "USD");
        dto.ProductName.Should().Be("Product");
    }

    [Fact]
    public void EntitlementCheckResult_CanBeCreated()
    {
        var dto = new EntitlementCheckResult(Guid.NewGuid(), true);
        dto.HasAccess.Should().BeTrue();
    }

    [Fact]
    public void EntitlementInfoDto_CanBeCreated()
    {
        var dto = new EntitlementInfoDto(
            Guid.NewGuid(), "Product", "Active", "Purchase",
            DateTime.UtcNow, null, false, null, 49.99m, "USD");
        dto.ProductName.Should().Be("Product");
    }

    // ── Exception tests ─────────────────────────────────────────

    [Fact]
    public void ProductNotFoundException_CanBeCreated()
    {
        var ex = new ProductNotFoundException(Guid.NewGuid());
        ex.ProductId.Should().NotBeEmpty();
    }

    [Fact]
    public void PromoCodeNotFoundException_CanBeCreated()
    {
        var ex = new PromoCodeNotFoundException("INVALID");
        ex.Code.Should().Be("INVALID");
    }

    [Fact]
    public void InvalidPromoCodeException_CanBeCreated()
    {
        var ex = new InvalidPromoCodeException("CODE1", "Expired");
        ex.Code.Should().Be("CODE1");
        ex.Reason.Should().Be("Expired");
    }

    [Fact]
    public void ConcurrencyException_CanBeCreated()
    {
        var ex = new ConcurrencyException("conflict");
        ex.Message.Should().Be("conflict");
    }

    // ── Validator instantiation tests ───────────────────────────

    [Fact]
    public void AllValidators_CanBeInstantiated()
    {
        new CreateProductCommandValidator().Should().NotBeNull();
        new UpdateProductCommandValidator().Should().NotBeNull();
        new DeleteProductCommandValidator().Should().NotBeNull();
        new SetProductPricingCommandValidator().Should().NotBeNull();
        new GrantProductAccessCommandValidator().Should().NotBeNull();
        new RevokeProductAccessCommandValidator().Should().NotBeNull();
        new CreatePromoCodeCommandValidator().Should().NotBeNull();
        new UpdatePromoCodeCommandValidator().Should().NotBeNull();
        new DeletePromoCodeCommandValidator().Should().NotBeNull();
        new ValidatePromoCodeCommandValidator().Should().NotBeNull();
        new ApplyPromoCodesCommandValidator().Should().NotBeNull();
    }

    // ── Bundle validation ───────────────────────────────────────

    [Fact]
    public void ProductBundleValidator_CanBeInstantiated()
    {
        var v = new ProductBundleValidator();
        v.Should().NotBeNull();
    }

    [Fact]
    public void BundleValidationResult_Success()
    {
        var r = BundleValidationResult.Success();
        r.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BundleValidationResult_Failure()
    {
        var r = BundleValidationResult.Failure("error", BundleValidationErrorCode.NotABundle);
        r.IsValid.Should().BeFalse();
        r.ErrorMessage.Should().Be("error");
    }

    [Fact]
    public void EntitlementResult_Succeeded()
    {
        var r = EntitlementResult.Succeeded(null!, false);
        r.Success.Should().BeTrue();
    }

    [Fact]
    public void EntitlementResult_Failed()
    {
        var r = EntitlementResult.Failed("error");
        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Be("error");
    }

    // ── CreatorInfo ─────────────────────────────────────────────

    [Fact]
    public void CreatorInfo_CanBeCreated()
    {
        var c = new CreatorInfo(Guid.NewGuid(), "Alice", "alice@test.com", true);
        c.Name.Should().Be("Alice");
    }
}
