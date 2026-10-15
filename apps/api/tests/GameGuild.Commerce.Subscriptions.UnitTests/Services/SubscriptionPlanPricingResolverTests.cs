using FluentAssertions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Services;

public class SubscriptionPlanPricingResolverTests
{
    private readonly Mock<ISubscriptionPlanService> _planService = new();
    private readonly Mock<ISubscriptionIntervalPricingService> _intervalPricing = new();
    private readonly SubscriptionPlanPricingResolver _resolver;

    public SubscriptionPlanPricingResolverTests()
    {
        _intervalPricing
            .Setup(service => service.ResolvePriceForCycleAsync(It.IsAny<SubscriptionPlan>(), It.IsAny<BillingCycle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan plan, BillingCycle cycle, CancellationToken _) =>
                new Money(cycle switch
                {
                    BillingCycle.Annually => 199.90m,
                    BillingCycle.Monthly => 19.99m,
                    _ => 19.99m
                }, plan.Currency));
        _resolver = new SubscriptionPlanPricingResolver(_planService.Object, _intervalPricing.Object);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPlanServiceIsNull()
    {
        var act = () => new SubscriptionPlanPricingResolver(null!, _intervalPricing.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("planService");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenIntervalPricingServiceIsNull()
    {
        var act = () => new SubscriptionPlanPricingResolver(_planService.Object, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("intervalPricingService");
    }

    [Fact]
    public async Task GetPlanMonthlyPriceAsync_ShouldReturnNull_WhenPlanDoesNotExist()
    {
        _planService.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var result = await _resolver.GetPlanMonthlyPriceAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPlanMonthlyPriceAsync_ShouldReturnMonthlyPrice_WhenPlanExists()
    {
        var plan = CreatePlan();
        _planService.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _resolver.GetPlanMonthlyPriceAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result!.Amount.Should().Be(19.99m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task GetPlanMonthlyPriceAsync_ShouldFailClosed_WhenEnterprisePlanIsCustomQuotePending()
    {
        var plan = CreatePlan(SubscriptionPlanType.Enterprise, SubscriptionPlanPricingRules.CustomQuotePriceInCents);
        _planService.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var act = () => _resolver.GetPlanMonthlyPriceAsync(plan.Id);

        await act.Should().ThrowAsync<SubscriptionPlanCustomQuotePendingException>();
    }

    [Fact]
    public async Task GetPlanPriceAsync_ShouldReturnNull_WhenPlanDoesNotExist()
    {
        _planService.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null);

        var result = await _resolver.GetPlanPriceAsync(Guid.NewGuid(), BillingCycle.Monthly);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPlanPriceAsync_ShouldDelegateToIntervalPricing_WithTheRequestedCycle()
    {
        var plan = CreatePlan();
        plan.AnnualPriceInCents = 19990;
        _planService.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var result = await _resolver.GetPlanPriceAsync(plan.Id, BillingCycle.Annually);

        result!.Amount.Should().Be(199.90m);
        _intervalPricing.Verify(service => service.ResolvePriceForCycleAsync(
            plan, BillingCycle.Annually, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPlanPriceAsync_ShouldFailClosed_WhenEnterprisePlanIsCustomQuotePending()
    {
        var plan = CreatePlan(SubscriptionPlanType.Enterprise, SubscriptionPlanPricingRules.CustomQuotePriceInCents);
        _planService.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var act = () => _resolver.GetPlanPriceAsync(plan.Id, BillingCycle.Monthly);

        await act.Should().ThrowAsync<SubscriptionPlanCustomQuotePendingException>();
        _intervalPricing.Verify(service => service.ResolvePriceForCycleAsync(
            It.IsAny<SubscriptionPlan>(), It.IsAny<BillingCycle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PlanExistsAsync_ShouldReturnExpectedExistence()
    {
        _planService.SetupSequence(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPlan?)null)
            .ReturnsAsync(CreatePlan());

        var missing = await _resolver.PlanExistsAsync(Guid.NewGuid());
        var existing = await _resolver.PlanExistsAsync(Guid.NewGuid());

        missing.Should().BeFalse();
        existing.Should().BeTrue();
    }

    private static SubscriptionPlan CreatePlan(SubscriptionPlanType type = SubscriptionPlanType.Paid, long monthlyPriceInCents = 1999)
    {
        var plan = new SubscriptionPlan("Pro", "pro", monthlyPriceInCents, "USD", "Professional plan")
        {
            Type = type
        };
        typeof(SubscriptionPlan).GetProperty(nameof(SubscriptionPlan.Id))!.SetValue(plan, Guid.NewGuid());
        return plan;
    }
}
