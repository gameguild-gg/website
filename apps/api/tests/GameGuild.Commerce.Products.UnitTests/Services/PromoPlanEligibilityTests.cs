using FluentAssertions;
using GameGuild.Commerce.Products;
using Moq;
using Xunit;

namespace GameGuild.Tests.Commerce.Products.Unit.Services;

/// <summary>
/// Issue #418 plan eligibility: a promo code scoped to a subscription plan (PlanId) must
/// validate only against that plan, mirroring the existing ProductId semantics.
/// </summary>
public class PromoPlanEligibilityTests
{
    private readonly Mock<IPromoCodeRepository> _promoCodeRepository = new();

    private PricingEngineService CreateService()
    {
        return new PricingEngineService(
            Mock.Of<IProductRepository>(),
            _promoCodeRepository.Object,
            Mock.Of<IPromoStackingRuleRepository>(),
            Mock.Of<IPricingRuleRepository>(),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
    }

    private PromoCode PlanScopedCode(Guid planId)
    {
        return new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "PLANSPECIFIC",
            Name = "Plan-specific code",
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = 15m,
            Currency = "USD",
            IsActive = true,
            PlanId = planId
        };
    }

    [Fact]
    public async Task Validate_PlanScopedCode_WithMatchingPlan_IsValid()
    {
        var planId = Guid.NewGuid();
        var code = PlanScopedCode(planId);
        _promoCodeRepository
            .Setup(r => r.GetByCodeAsync(code.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        var service = CreateService();

        var result = await service.ValidatePromoCodeAsync(code.Code, 100m, productId: null, userId: null, planId: planId);

        result.IsValid.Should().BeTrue();
        result.PromoCodeId.Should().Be(code.Id);
        result.Outcome.Should().Be(PromoCodeValidationOutcome.Valid);
    }

    [Fact]
    public async Task Validate_PlanScopedCode_WithDifferentPlan_IsRejectedWithPlanNotEligible()
    {
        var code = PlanScopedCode(Guid.NewGuid());
        _promoCodeRepository
            .Setup(r => r.GetByCodeAsync(code.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        var service = CreateService();

        var result = await service.ValidatePromoCodeAsync(code.Code, 100m, productId: null, userId: null, planId: Guid.NewGuid());

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Promo code is not valid for this subscription plan");
        result.Outcome.Should().Be(PromoCodeValidationOutcome.PlanNotEligible);
        result.PromoCodeId.Should().Be(code.Id);
    }

    [Fact]
    public async Task Validate_PlanScopedCode_WithoutPlanContext_RemainsValid_MirroringProductSemantics()
    {
        var code = PlanScopedCode(Guid.NewGuid());
        _promoCodeRepository
            .Setup(r => r.GetByCodeAsync(code.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        var service = CreateService();

        var result = await service.ValidatePromoCodeAsync(code.Code, 100m);

        // Consistent with ProductId semantics: scoping is only enforced when the caller
        // supplies the corresponding context (a plan id) to check against.
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_UnscopedCode_WithAnyPlan_IsValid()
    {
        var code = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "GENERIC10",
            Name = "Generic code",
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = 10m,
            Currency = "USD",
            IsActive = true,
            PlanId = null
        };
        _promoCodeRepository
            .Setup(r => r.GetByCodeAsync(code.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        var service = CreateService();

        var result = await service.ValidatePromoCodeAsync(code.Code, 100m, planId: Guid.NewGuid());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_FreeTrialExtensionCode_ExposesTrialExtensionDays()
    {
        var code = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "EXTRATRIAL",
            Name = "Extra trial",
            Type = PromoCodeType.FreeTrialExtension,
            FreeTrialExtensionDays = 14,
            Currency = "USD",
            IsActive = true
        };
        _promoCodeRepository
            .Setup(r => r.GetByCodeAsync(code.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        var service = CreateService();

        var result = await service.ValidatePromoCodeAsync(code.Code, 100m);

        result.IsValid.Should().BeTrue();
        result.TrialExtensionDays.Should().Be(14);
    }

    [Fact]
    public async Task Validate_FreeMonthsCode_ExposesGrantedMonths()
    {
        var code = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "FREEMONTHS",
            Name = "Free months",
            Type = PromoCodeType.FreeMonths,
            DurationMonths = 2,
            Currency = "USD",
            IsActive = true
        };
        _promoCodeRepository
            .Setup(r => r.GetByCodeAsync(code.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        var service = CreateService();

        var result = await service.ValidatePromoCodeAsync(code.Code, 100m);

        result.IsValid.Should().BeTrue();
        result.FreeMonthsGranted.Should().Be(2);
    }

    [Fact]
    public async Task Validate_UnknownCode_ReportsNotFoundOutcome()
    {
        _promoCodeRepository
            .Setup(r => r.GetByCodeAsync("NOPE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoCode?)null);
        var service = CreateService();

        var result = await service.ValidatePromoCodeAsync("NOPE", 100m);

        result.IsValid.Should().BeFalse();
        result.Outcome.Should().Be(PromoCodeValidationOutcome.NotFound);
        result.PromoCodeId.Should().BeNull();
    }

    [Fact]
    public async Task Validate_InactiveCode_ReportsInactiveOutcome()
    {
        var code = PlanScopedCode(Guid.NewGuid());
        code.IsActive = false;
        _promoCodeRepository
            .Setup(r => r.GetByCodeAsync(code.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        var service = CreateService();

        var result = await service.ValidatePromoCodeAsync(code.Code, 100m);

        result.IsValid.Should().BeFalse();
        result.Outcome.Should().Be(PromoCodeValidationOutcome.Inactive);
    }
}
