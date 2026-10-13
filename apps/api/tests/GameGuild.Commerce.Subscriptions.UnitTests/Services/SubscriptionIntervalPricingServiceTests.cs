using FluentAssertions;
using GameGuild.Commerce.Subscriptions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Services;

/// <summary>
///     Interval-aware pricing (issues #391/#392): explicit interval price points win over the
///     derived monthly×months fallback, and the interval breakdown reports the configured flag,
///     monthly equivalent and discount versus paying monthly.
/// </summary>
public class SubscriptionIntervalPricingServiceTests
{
    private readonly Mock<ISubscriptionPlanRepository> _planRepository = new();
    private readonly SubscriptionIntervalPricingService _service;

    public SubscriptionIntervalPricingServiceTests()
    {
        _service = new SubscriptionIntervalPricingService(_planRepository.Object);
    }

    private static SubscriptionPlan CreatePlan(long monthlyCents = 1_000, long? annualCents = null) =>
        new("Studio", "studio", monthlyCents, "USD") { AnnualPriceInCents = annualCents };

    [Fact]
    public async Task ResolvePrice_UsesConfiguredIntervalPrice_WhenPresent()
    {
        var plan = CreatePlan();
        _planRepository
            .Setup(repository => repository.GetIntervalPriceAsync(plan.Id, BillingCycle.Quarterly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlanIntervalPrice(plan.Id, BillingCycle.Quarterly, 2_700));

        var price = await _service.ResolvePriceForCycleAsync(plan, BillingCycle.Quarterly);

        price.Amount.Should().Be(27m);
        price.Currency.Should().Be("USD");
    }

    [Theory]
    [InlineData(BillingCycle.Monthly, 1_000)]
    [InlineData(BillingCycle.Quarterly, 3_000)]
    [InlineData(BillingCycle.SemiAnnually, 6_000)]
    [InlineData(BillingCycle.Annually, 12_000)]
    public async Task ResolvePrice_DerivesFallback_WhenNoIntervalPriceExists(BillingCycle cycle, long expectedCents)
    {
        var plan = CreatePlan();

        var price = await _service.ResolvePriceForCycleAsync(plan, cycle);

        price.Amount.Should().Be(expectedCents / 100m);
    }

    [Fact]
    public async Task ResolvePrice_AnnualFallback_PrefersExplicitAnnualPriceOverTwelveMonthly()
    {
        var plan = CreatePlan(annualCents: 100_000);

        var price = await _service.ResolvePriceForCycleAsync(plan, BillingCycle.Annually);

        price.Amount.Should().Be(1_000m);
    }

    [Fact]
    public async Task GetIntervalPricing_ReportsConfiguredFlagsEquivalentsAndDiscounts()
    {
        var plan = CreatePlan();
        _planRepository
            .Setup(repository => repository.GetIntervalPricesAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SubscriptionPlanIntervalPrice(plan.Id, BillingCycle.Quarterly, 2_700),
                new SubscriptionPlanIntervalPrice(plan.Id, BillingCycle.Annually, 9_600)
            });

        var breakdown = await _service.GetIntervalPricingAsync(plan);

        breakdown.Should().HaveCount(4);

        var monthly = breakdown.Single(point => point.Interval == BillingCycle.Monthly);
        monthly.IsConfiguredPricePoint.Should().BeFalse();
        monthly.PriceInCents.Should().Be(1_000);
        monthly.MonthlyEquivalentInCents.Should().Be(1_000);
        monthly.DiscountPercentVersusMonthly.Should().BeNull();

        var quarterly = breakdown.Single(point => point.Interval == BillingCycle.Quarterly);
        quarterly.IsConfiguredPricePoint.Should().BeTrue();
        quarterly.MonthlyEquivalentInCents.Should().Be(900);
        // (3000 - 2700) * 100 / 3000 = 10% off versus paying monthly.
        quarterly.DiscountPercentVersusMonthly.Should().Be(10m);

        var semiAnnual = breakdown.Single(point => point.Interval == BillingCycle.SemiAnnually);
        semiAnnual.IsConfiguredPricePoint.Should().BeFalse();
        semiAnnual.PriceInCents.Should().Be(6_000);
        semiAnnual.DiscountPercentVersusMonthly.Should().Be(0m);

        var annual = breakdown.Single(point => point.Interval == BillingCycle.Annually);
        annual.IsConfiguredPricePoint.Should().BeTrue();
        annual.MonthlyEquivalentInCents.Should().Be(800);
        // (12000 - 9600) * 100 / 12000 = 20% off.
        annual.DiscountPercentVersusMonthly.Should().Be(20m);
    }

    [Fact]
    public void IntervalPriceEntity_GuardsConstruction()
    {
        var actEmptyPlan = () => new SubscriptionPlanIntervalPrice(Guid.Empty, BillingCycle.Monthly, 100);
        actEmptyPlan.Should().Throw<ArgumentException>();

        var actNegative = () => new SubscriptionPlanIntervalPrice(Guid.NewGuid(), BillingCycle.Monthly, -1);
        actNegative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IntervalPriceEntity_GetPrice_UsesPlanCurrency()
    {
        var price = new SubscriptionPlanIntervalPrice(Guid.NewGuid(), BillingCycle.Monthly, 1_234);

        var money = price.GetPrice("EUR");

        money.Amount.Should().Be(12.34m);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task SetIntervalPriceHandler_UpsertsInvalidatesCacheAndReturnsThePoint()
    {
        var plan = CreatePlan();
        var repository = new Mock<ISubscriptionPlanRepository>();
        repository
            .Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        repository
            .Setup(r => r.GetIntervalPricesAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new SubscriptionPlanIntervalPrice(plan.Id, BillingCycle.Quarterly, 2_700) });
        var cache = new Mock<ISubscriptionPlanListCache>();
        var handler = new SetSubscriptionPlanIntervalPriceCommandHandler(repository.Object, new SubscriptionIntervalPricingService(repository.Object), cache.Object);

        var result = await handler.Handle(new SetSubscriptionPlanIntervalPriceCommand(plan.Id, BillingCycle.Quarterly, 2_700), CancellationToken.None);

        result.Interval.Should().Be(BillingCycle.Quarterly);
        result.PriceInCents.Should().Be(2_700);
        result.IsConfiguredPricePoint.Should().BeTrue();
        cache.Verify(c => c.Invalidate(), Times.Once);
        repository.Verify(r => r.UpsertIntervalPriceAsync(It.Is<SubscriptionPlanIntervalPrice>(point => point.PlanId == plan.Id && point.Interval == BillingCycle.Quarterly && point.PriceInCents == 2_700), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(BillingCycle.Monthly, true)]
    [InlineData(BillingCycle.Quarterly, true)]
    [InlineData(BillingCycle.SemiAnnually, true)]
    [InlineData(BillingCycle.Annually, true)]
    [InlineData(BillingCycle.Weekly, false)]
    [InlineData(BillingCycle.Biannually, false)]
    public void IntervalPriceValidator_OnlyAcceptsSupportedIntervals(BillingCycle interval, bool expectedValid)
    {
        var validator = new SetSubscriptionPlanIntervalPriceCommandValidator();
        var command = new SetSubscriptionPlanIntervalPriceCommand(Guid.NewGuid(), interval, 1_000);

        var result = validator.Validate(command);

        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void IntervalPriceValidator_RejectsUnknownPlanAndNegativePrice()
    {
        var validator = new SetSubscriptionPlanIntervalPriceCommandValidator();

        validator.Validate(new SetSubscriptionPlanIntervalPriceCommand(Guid.Empty, BillingCycle.Monthly, 100)).IsValid.Should().BeFalse();
        validator.Validate(new SetSubscriptionPlanIntervalPriceCommand(Guid.NewGuid(), BillingCycle.Monthly, -1)).IsValid.Should().BeFalse();
    }
}
