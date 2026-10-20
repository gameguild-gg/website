using FluentAssertions;
using GameGuild.Commerce.Subscriptions;

using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.IntegrationTests;

/// <summary>
///     Integration tests for SubscriptionPlanPricingResolver
///     Tests the cross-module integration between Subscriptions and Payments modules
/// </summary>
public class SubscriptionPlanPricingResolverIntegrationTests
{
    private readonly Mock<ISubscriptionPlanService> _mockPlanService;
    private readonly Mock<ISubscriptionPlanRepository> _mockPlanRepository;
    private readonly SubscriptionPlanPricingResolver _resolver;

    public SubscriptionPlanPricingResolverIntegrationTests()
    {
        _mockPlanService = new Mock<ISubscriptionPlanService>();
        // The resolver delegates interval derivation to the real interval pricing service;
        // without configured interval price points every cycle uses its derived fallback
        // (including the plan-level annual discount, issue #416).
        _mockPlanRepository = new Mock<ISubscriptionPlanRepository>();
        _mockPlanRepository
            .Setup(repository => repository.GetIntervalPriceAsync(It.IsAny<Guid>(), It.IsAny<BillingCycle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlanIntervalPrice?)null);
        _mockPlanRepository
            .Setup(repository => repository.GetIntervalPricesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SubscriptionPlanIntervalPrice>());
        _resolver = new SubscriptionPlanPricingResolver(
            _mockPlanService.Object,
            new SubscriptionIntervalPricingService(_mockPlanRepository.Object));
    }

    #region GetPlanMonthlyPriceAsync Tests

    [Fact]
    public async Task GetPlanMonthlyPriceAsync_ShouldReturnMoney_WhenPlanExists()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan("Pro Plan", "pro-plan", 2999, "USD");

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _resolver.GetPlanMonthlyPriceAsync(planId);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(29.99m); // 2999 cents = $29.99
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task GetPlanMonthlyPriceAsync_ShouldReturnNull_WhenPlanNotFound()
    {
        // Arrange
        var planId = Guid.NewGuid();

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        // Act
        var result = await _resolver.GetPlanMonthlyPriceAsync(planId);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(999, "USD", 9.99)]
    [InlineData(4999, "USD", 49.99)]
    [InlineData(1000, "EUR", 10.00)]
    public async Task GetPlanMonthlyPriceAsync_ShouldConvertCentsToMoney_ForDifferentPrices(
        long priceInCents, string currency, decimal expectedAmount)
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan("Plan", "plan", priceInCents, currency);

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _resolver.GetPlanMonthlyPriceAsync(planId);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(expectedAmount);
        result.Currency.Should().Be(currency);
    }

    #endregion

    #region GetPlanPriceAsync Tests

    [Theory]
    [InlineData(BillingCycle.Monthly, 2999, 29.99)]
    [InlineData(BillingCycle.Quarterly, 2999, 89.97)] // 3 months
    [InlineData(BillingCycle.SemiAnnually, 2999, 179.94)] // 6 months
    public async Task GetPlanPriceAsync_ShouldCalculatePrice_ForDifferentBillingCycles(
        BillingCycle billingCycle, long monthlyPriceInCents, decimal expectedAmount)
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan("Plan", "plan", monthlyPriceInCents, "USD");

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _resolver.GetPlanPriceAsync(planId, billingCycle);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(expectedAmount);
    }

    [Fact]
    public async Task GetPlanPriceAsync_ShouldUseAnnualPrice_WhenProvided()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan("Plan", "plan", 2999, "USD");
        plan.UpdatePricing(2999, 29990); // Annual price with discount (normally 35988)

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _resolver.GetPlanPriceAsync(planId, BillingCycle.Annually);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(299.90m); // Uses explicit annual price
    }

    [Fact]
    public async Task GetPlanPriceAsync_ShouldFallbackToMonthlyMultiplier_WhenNoAnnualPrice()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan("Plan", "plan", 2999, "USD");
        // No annual price set

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _resolver.GetPlanPriceAsync(planId, BillingCycle.Annually);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(359.88m); // 29.99 * 12
    }

    [Fact]
    public async Task GetPlanPriceAsync_ShouldReturnNull_WhenPlanNotFound()
    {
        // Arrange
        var planId = Guid.NewGuid();

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        // Act
        var result = await _resolver.GetPlanPriceAsync(planId, BillingCycle.Monthly);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region PlanExistsAsync Tests

    [Fact]
    public async Task PlanExistsAsync_ShouldReturnTrue_WhenPlanExists()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan("Plan", "plan", 999);

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _resolver.PlanExistsAsync(planId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PlanExistsAsync_ShouldReturnFalse_WhenPlanNotFound()
    {
        // Arrange
        var planId = Guid.NewGuid();

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        // Act
        var result = await _resolver.PlanExistsAsync(planId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldThrow_WhenPlanServiceIsNull()
    {
        // Act
        var act = () => new SubscriptionPlanPricingResolver(null!,
            new SubscriptionIntervalPricingService(_mockPlanRepository.Object));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("planService");
    }

    [Fact]
    public async Task GetPlanPriceAsync_ShouldApplyPlanLevelAnnualDiscount_WhenNoExplicitAnnualPrice()
    {
        // Issue #416: the pricing preview surfaces the plan-level annual discount through the
        // shared interval pricing service.
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan("Plan", "plan", 2999, "USD");
        plan.UpdatePricing(2999, null, 20m); // 20% annual discount, no explicit annual price

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _resolver.GetPlanPriceAsync(planId, BillingCycle.Annually);

        result.Should().NotBeNull();
        result!.Amount.Should().Be(287.90m); // 29.99 x 12 discounted 20% = 287.904 -> 287.90
    }

    [Fact]
    public async Task GetPlanPriceAsync_ShouldFailClosed_WhenEnterprisePlanIsCustomQuotePending()
    {
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan("Enterprise", "enterprise", -1, "USD");
        plan.Reclassify(SubscriptionPlanType.Enterprise);

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var act = () => _resolver.GetPlanPriceAsync(planId, BillingCycle.Monthly);

        await act.Should().ThrowAsync<SubscriptionPlanCustomQuotePendingException>();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetPlanPriceAsync_ShouldHandleWeeklyBillingCycle()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan("Plan", "plan", 4000, "USD"); // $40/month

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _resolver.GetPlanPriceAsync(planId, BillingCycle.Weekly);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(10.00m); // 4000/4 = 1000 cents = $10
    }

    [Fact]
    public async Task GetPlanPriceAsync_ShouldHandleBiannuallyBillingCycle()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new SubscriptionPlan("Plan", "plan", 1000, "USD"); // $10/month

        _mockPlanService.Setup(s => s.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _resolver.GetPlanPriceAsync(planId, BillingCycle.Biannually);

        // Assert
        result.Should().NotBeNull();
        result!.Amount.Should().Be(240.00m); // $10 * 24 months
    }

    #endregion
}
