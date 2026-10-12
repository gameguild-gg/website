using System.Diagnostics;
using FluentAssertions;
using GameGuild.Commerce;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

/// <summary>
/// Unit tests for the unified pricing engine (issue #417): one calculation path for base
/// plan pricing, add-ons, progressive usage tiers, promo discounts, proration, tax,
/// credits, currency conversion, ledger recording, previews, and self-validation.
/// </summary>
public sealed class PricingEngineTests
{
    private readonly Mock<IPlanPricingResolver> _planResolver = new();
    private readonly Mock<IPlanProrationCalculator> _prorationCalculator = new();
    private readonly Mock<IPromoDiscountCalculator> _promoCalculator = new();
    private readonly Mock<ITaxCalculationService> _taxService = new();
    private readonly Mock<IWalletService> _walletService = new();
    private readonly Mock<IPricingDecisionLedgerWriter> _ledgerWriter = new();

    private readonly ManualExchangeRateProvider _rates = new([]);
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private PricingEngine CreateEngine(IExchangeRateProvider? rates = null, IMemoryCache? cache = null)
    {
        var conversion = new CurrencyConversionService(rates ?? _rates);
        return new PricingEngine(
            _planResolver.Object,
            _prorationCalculator.Object,
            _promoCalculator.Object,
            _taxService.Object,
            _walletService.Object,
            conversion,
            _ledgerWriter.Object,
            cache ?? _cache);
    }

    private void SetupPlanPrice(Guid planId, decimal monthly, decimal? annual = null, string currency = "USD")
    {
        _planResolver
            .Setup(x => x.GetPlanPriceAsync(planId, BillingCycle.Monthly, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Money(monthly, currency));
        _planResolver
            .Setup(x => x.GetPlanPriceAsync(planId, BillingCycle.Annually, It.IsAny<CancellationToken>()))
            .ReturnsAsync(annual.HasValue ? new Money(annual.Value, currency) : new Money(monthly * 12, currency));
    }

    // ---------- Base pricing ----------

    [Fact]
    public async Task CalculateAsync_PlanBasePrice_ComposesFullBreakdown()
    {
        var planId = Guid.NewGuid();
        SetupPlanPrice(planId, 100m);

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(PlanId: planId, IncludeTax: false, RecordDecision: false));

        result.BasePlanPrice.Amount.Should().Be(100m);
        result.Subtotal.Amount.Should().Be(100m);
        result.Total.Amount.Should().Be(100m);
        result.AmountDue.Amount.Should().Be(100m);
        result.LineItems.Should().ContainSingle(item => item.Type == "base_plan" && item.Amount.Amount == 100m);
        result.CalculationExplanation.Should().Contain("Base plan");
        result.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateAsync_CustomBaseAmount_IsUsedWithoutPlan()
    {
        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(BaseAmount: 55.5m, IncludeTax: false, RecordDecision: false));

        result.BasePlanPrice.Amount.Should().Be(55.5m);
        result.AmountDue.Amount.Should().Be(55.5m);
    }

    [Fact]
    public async Task CalculateAsync_AnnualCycle_UsesAnnualPlanPrice()
    {
        var planId = Guid.NewGuid();
        SetupPlanPrice(planId, 100m, annual: 1000m);

        var result = await CreateEngine().CalculateAsync(
            new PricingCalculationRequest(PlanId: planId, BillingCycle: BillingCycle.Annually, IncludeTax: false, RecordDecision: false));

        result.BasePlanPrice.Amount.Should().Be(1000m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CalculateAsync_WithoutPlanOrAmount_FailsClosed(bool planIdEmpty)
    {
        var request = planIdEmpty
            ? new PricingCalculationRequest(IncludeTax: false, RecordDecision: false)
            : new PricingCalculationRequest(PlanId: Guid.Empty, IncludeTax: false, RecordDecision: false);

        var act = () => CreateEngine().CalculateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CalculateAsync_UnknownPlan_FailsClosed()
    {
        _planResolver
            .Setup(x => x.GetPlanPriceAsync(It.IsAny<Guid>(), It.IsAny<BillingCycle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Money?)null);

        var act = () => CreateEngine().CalculateAsync(new PricingCalculationRequest(PlanId: Guid.NewGuid(), IncludeTax: false, RecordDecision: false));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    // ---------- Add-ons ----------

    [Fact]
    public async Task CalculateAsync_AddOns_PriceIndependentlyByQuantity()
    {
        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            AddOns:
            [
                new PricingAddOnInput("seats", "Extra seats", 3, new Money(10m, "USD")),
                new PricingAddOnInput("storage", "Storage", 1, new Money(7.5m, "USD"))
            ],
            IncludeTax: false,
            RecordDecision: false));

        result.AddOnsTotal.Amount.Should().Be(37.5m);
        result.Subtotal.Amount.Should().Be(137.5m);
        result.LineItems.Should().Contain(item => item.Type == "add_on" && item.Name == "Extra seats" && item.Amount.Amount == 30m);
    }

    // ---------- Usage tiers ----------

    [Fact]
    public async Task CalculateAsync_UsageFlatRate_WhenNoTiersSupplied()
    {
        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 10m,
            UsageCharges: [new PricingUsageChargeInput("api_calls", 1000, 0.01m)],
            IncludeTax: false,
            RecordDecision: false));

        result.UsageCharges.Amount.Should().Be(10m);
        result.Subtotal.Amount.Should().Be(20m);
    }

    [Fact]
    public async Task CalculateAsync_UsageProgressiveTiers_PriceEachBandAtItsOwnRate()
    {
        var tiers = new List<PricingUsageTier>
        {
            new(0, 100, 0.10m),
            new(100, null, 0.08m)
        };

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 0m,
            UsageCharges: [new PricingUsageChargeInput("gb", 150, 0, tiers)],
            IncludeTax: false,
            RecordDecision: false));

        // First 100 units @ 0.10 = 10; next 50 @ 0.08 = 4.
        result.UsageCharges.Amount.Should().Be(14m);
    }

    [Fact]
    public async Task CalculateAsync_UsageTiers_PriceOnlyFirstBandWhenQuantityInsideIt()
    {
        var tiers = new List<PricingUsageTier>
        {
            new(0, 100, 0.10m),
            new(100, null, 0.08m)
        };

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 0m,
            UsageCharges: [new PricingUsageChargeInput("gb", 50, 0, tiers)],
            IncludeTax: false,
            RecordDecision: false));

        result.UsageCharges.Amount.Should().Be(5m);
    }

    [Fact]
    public async Task CalculateAsync_UsageNegativeTierPrice_FailsClosed()
    {
        var act = () => CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 10m,
            UsageCharges: [new PricingUsageChargeInput("gb", 10, 0, [new PricingUsageTier(0, null, -1m)])],
            IncludeTax: false,
            RecordDecision: false));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---------- Discounts ----------

    [Fact]
    public async Task CalculateAsync_PromoCodes_ApplyThroughPromoEngineAndClampAtScope()
    {
        _promoCalculator
            .Setup(x => x.ApplyAsync(It.IsAny<PromoDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoDiscountResult(
                30m,
                [new AppliedPromoDiscount("WELCOME", "10% off", 30m, 10m, DiscountType.Percentage, 50, true, 1)],
                [],
                "DefaultStacking"));

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            AddOns: [new PricingAddOnInput("a", "Add-on", 1, new Money(50m, "USD"))],
            PromoCodes: ["WELCOME"],
            IncludeTax: false,
            RecordDecision: false));

        _promoCalculator.Verify(
            x => x.ApplyAsync(It.Is<PromoDiscountRequest>(r => r.OrderAmount == 150m), It.IsAny<CancellationToken>()),
            Times.Once);

        result.TotalDiscounts.Amount.Should().Be(30m);
        result.SubtotalAfterDiscounts.Amount.Should().Be(120m);
        result.AppliedDiscounts.Should().ContainSingle(d => d.Code == "WELCOME" && d.Type == DiscountType.Percentage);
    }

    [Fact]
    public async Task CalculateAsync_PlanOnlyScope_DiscountsApplyToBasePlanOnly()
    {
        _promoCalculator
            .Setup(x => x.ApplyAsync(It.IsAny<PromoDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoDiscountResult(20m, [new AppliedPromoDiscount("PLAN20", "20% off", 20m, 20m, DiscountType.Percentage, 50, true, 1)], [], null));

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            AddOns: [new PricingAddOnInput("a", "Add-on", 1, new Money(50m, "USD"))],
            PromoCodes: ["PLAN20"],
            DiscountScope: PricingDiscountScope.PlanOnly,
            IncludeTax: false,
            RecordDecision: false));

        _promoCalculator.Verify(
            x => x.ApplyAsync(It.Is<PromoDiscountRequest>(r => r.OrderAmount == 100m), It.IsAny<CancellationToken>()),
            Times.Once);

        result.TotalDiscounts.Amount.Should().Be(20m);
        result.SubtotalAfterDiscounts.Amount.Should().Be(130m);
    }

    [Fact]
    public async Task CalculateAsync_DiscountAboveDiscountableAmount_IsClamped()
    {
        _promoCalculator
            .Setup(x => x.ApplyAsync(It.IsAny<PromoDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoDiscountResult(500m, [new AppliedPromoDiscount("TOOBIG", "huge", 500m, null, DiscountType.FixedAmount, 50, true, 1)], [], null));

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            PromoCodes: ["TOOBIG"],
            IncludeTax: false,
            RecordDecision: false));

        result.TotalDiscounts.Amount.Should().Be(100m);
        result.SubtotalAfterDiscounts.Amount.Should().Be(0m);
    }

    // ---------- Proration ----------

    [Fact]
    public async Task CalculateAsync_Proration_AdjustsTotalAndEmitsLineItem()
    {
        var currentPlan = Guid.NewGuid();
        var newPlan = Guid.NewGuid();
        SetupPlanPrice(currentPlan, 100m);
        SetupPlanPrice(newPlan, 200m);

        _prorationCalculator
            .Setup(x => x.CalculateProrationAsync(It.IsAny<PlanProrationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanProrationResult(50m, 100m, 50m, SystemClock.UtcNow, 30, 15));

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            PlanId: newPlan,
            Proration: new PricingProrationInput(currentPlan, newPlan, DateTime.UtcNow.AddDays(-15), DateTime.UtcNow.AddDays(15), DateTime.UtcNow),
            IncludeTax: false,
            RecordDecision: false));

        result.Proration.Should().NotBeNull();
        result.Proration!.NetAdjustment.Should().Be(50m);
        result.ProrationAdjustment.Should().Be(50m);
        result.Total.Amount.Should().Be(200m + 50m);
        result.LineItems.Should().Contain(item => item.Type == "proration");

        _prorationCalculator.Verify(
            x => x.CalculateProrationAsync(
                It.Is<PlanProrationInput>(input => input.OldAmount.Amount == 100m && input.NewAmount.Amount == 200m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------- Tax ----------

    [Fact]
    public async Task CalculateAsync_Tax_AppliedAfterDiscountsThroughTaxService()
    {
        _promoCalculator
            .Setup(x => x.ApplyAsync(It.IsAny<PromoDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoDiscountResult(20m, [new AppliedPromoDiscount("T", "20 off", 20m, null, DiscountType.FixedAmount, 50, true, 1)], [], null));

        _taxService
            .Setup(x => x.CalculateTaxAsync(It.IsAny<TaxCalculationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxCalculationResult
            {
                SubtotalAmount = 80m,
                TaxAmount = 16m,
                TotalAmount = 96m,
                EffectiveTaxRate = 0.20m,
                JurisdictionCode = "DE",
                TaxBreakdowns = [new TaxBreakdown { TaxType = TaxType.VAT, Rate = 0.20m, TaxAmount = 16m, JurisdictionCode = "DE" }]
            });

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            PromoCodes: ["T"],
            TaxInfo: new PricingTaxInfo("DE"),
            IncludeTax: true,
            RecordDecision: false));

        // Tax input must be the post-discount amount, not the subtotal.
        _taxService.Verify(
            x => x.CalculateTaxAsync(
                It.Is<TaxCalculationRequest>(request => request.Amount == 80m && request.JurisdictionCode == "DE" && request.Currency == "USD"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        result.TaxAmount.Amount.Should().Be(16m);
        result.Total.Amount.Should().Be(96m);
        result.Tax!.TaxBreakdowns.Should().ContainSingle(b => b.JurisdictionCode == "DE");
    }

    [Fact]
    public async Task CalculateAsync_TaxInclusive_TotalEqualsTaxableAmount()
    {
        _taxService
            .Setup(x => x.CalculateTaxAsync(It.IsAny<TaxCalculationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxCalculationResult { SubtotalAmount = 100m, TaxAmount = 16m, TotalAmount = 100m, EffectiveTaxRate = 0.16m, JurisdictionCode = "DE" });

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            TaxInfo: new PricingTaxInfo("DE", IsTaxInclusive: true),
            IncludeTax: true,
            RecordDecision: false));

        result.Total.Amount.Should().Be(100m);
        result.TaxAmount.Amount.Should().Be(16m);
    }

    [Fact]
    public async Task CalculateAsync_IncludeTaxWithoutTaxInfo_FailsClosed()
    {
        var act = () => CreateEngine().CalculateAsync(new PricingCalculationRequest(BaseAmount: 100m, IncludeTax: true, RecordDecision: false));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*TaxInfo*");
    }

    [Fact]
    public async Task CalculateAsync_B2BExemptionCheck_RunsWhenUserSupplied()
    {
        _taxService
            .Setup(x => x.CalculateTaxAsync(It.IsAny<TaxCalculationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxCalculationResult { TaxAmount = 0m, TotalAmount = 100m, EffectiveTaxRate = 0m, JurisdictionCode = "FR", IsReverseCharge = true });

        var userId = Guid.NewGuid();
        _taxService
            .Setup(x => x.ValidateTaxExemptionAsync(userId, "FR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            UserId: userId,
            TaxInfo: new PricingTaxInfo("FR", CustomerType.B2B, CustomerVatNumber: "FR12345678901"),
            IncludeTax: true,
            RecordDecision: false));

        _taxService.Verify(x => x.ValidateTaxExemptionAsync(userId, "FR", It.IsAny<CancellationToken>()), Times.Once);
        _taxService.Verify(
            x => x.CalculateTaxAsync(It.Is<TaxCalculationRequest>(r => r.CustomerType == CustomerType.B2B && r.CustomerVatNumber == "FR12345678901"), It.IsAny<CancellationToken>()),
            Times.Once);
        result.Tax!.IsReverseCharge.Should().BeTrue();
    }

    // ---------- Credits ----------

    [Fact]
    public async Task CalculateAsync_Credits_AppliedAfterTaxAndCappedByBalance()
    {
        var userId = Guid.NewGuid();
        _walletService
            .Setup(x => x.GetWalletByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWallet(userId, 50m));

        _taxService
            .Setup(x => x.CalculateTaxAsync(It.IsAny<TaxCalculationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxCalculationResult { TaxAmount = 16m, TotalAmount = 116m, EffectiveTaxRate = 0.16m, JurisdictionCode = "DE" });

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            UserId: userId,
            TaxInfo: new PricingTaxInfo("DE"),
            IncludeTax: true,
            ApplyCredits: true,
            RecordDecision: false));

        result.Total.Amount.Should().Be(116m);
        result.CreditsApplied.Amount.Should().Be(50m);
        result.AmountDue.Amount.Should().Be(66m);
        result.Credits!.RemainingCredits.Should().Be(0m);

        // Quote by default: the wallet must not be debited.
        _walletService.Verify(x => x.DeductFundsAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CalculateAsync_Credits_RespectMaxUsageLimit()
    {
        var userId = Guid.NewGuid();
        _walletService
            .Setup(x => x.GetWalletByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWallet(userId, 50m));

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            UserId: userId,
            IncludeTax: false,
            ApplyCredits: true,
            MaxCreditUsage: 20m,
            RecordDecision: false));

        result.CreditsApplied.Amount.Should().Be(20m);
        result.AmountDue.Amount.Should().Be(80m);
    }

    [Fact]
    public async Task CalculateAsync_Credits_InsufficientBalanceHandledGracefully()
    {
        var userId = Guid.NewGuid();
        _walletService
            .Setup(x => x.GetWalletByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWallet(userId, 0m));

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            UserId: userId,
            IncludeTax: false,
            ApplyCredits: true,
            RecordDecision: false));

        result.CreditsApplied.Amount.Should().Be(0m);
        result.AmountDue.Amount.Should().Be(100m);
        result.Credits.Should().BeNull();
    }

    [Fact]
    public async Task CalculateAsync_Credits_CurrencyMismatchSkippedWithVisibleNote()
    {
        var userId = Guid.NewGuid();
        _walletService
            .Setup(x => x.GetWalletByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWallet(userId, 50m, "EUR"));

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            UserId: userId,
            IncludeTax: false,
            ApplyCredits: true,
            RecordDecision: false));

        result.CreditsApplied.Amount.Should().Be(0m);
        result.ValidationErrors.Should().Contain(e => e.Contains("EUR"));
    }

    [Fact]
    public async Task CalculateAsync_Credits_ConsumeCreditsDebitsWallet()
    {
        var userId = Guid.NewGuid();
        _walletService
            .Setup(x => x.GetWalletByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWallet(userId, 50m));

        await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            UserId: userId,
            IncludeTax: false,
            ApplyCredits: true,
            ConsumeCredits: true,
            RecordDecision: false));

        _walletService.Verify(
            x => x.DeductFundsAsync(userId, 50m, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------- Currency conversion ----------

    [Fact]
    public async Task CalculateAsync_SameCurrency_IsExactPassthrough()
    {
        var planId = Guid.NewGuid();
        SetupPlanPrice(planId, 100m);

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(PlanId: planId, IncludeTax: false, RecordDecision: false));

        result.Currency.Should().Be("USD");
        result.Total.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task CalculateAsync_CrossCurrency_ConvertsThroughFxCore()
    {
        var planId = Guid.NewGuid();
        SetupPlanPrice(planId, 100m);

        var rates = new ManualExchangeRateProvider(
        [
            new ExchangeRate("USD", "EUR", 0.9m, DateTimeOffset.UtcNow, "test")
        ]);

        var result = await CreateEngine(rates).CalculateAsync(new PricingCalculationRequest(
            PlanId: planId,
            Currency: "EUR",
            IncludeTax: false,
            RecordDecision: false));

        result.Currency.Should().Be("EUR");
        result.Total.Amount.Should().Be(90m);
    }

    [Fact]
    public async Task CalculateAsync_CrossCurrencyWithoutQuotation_FailsClosed()
    {
        var planId = Guid.NewGuid();
        SetupPlanPrice(planId, 100m);

        var act = () => CreateEngine().CalculateAsync(new PricingCalculationRequest(
            PlanId: planId,
            Currency: "EUR",
            IncludeTax: false,
            RecordDecision: false));

        await act.Should().ThrowAsync<BusinessRuleViolationException>()
            .Where(ex => ex.Rule == CurrencyConversionService.MissingRateRule);
    }

    [Fact]
    public async Task CalculateAsync_AddOnInDifferentCurrency_IsConvertedToCalculationCurrency()
    {
        var rates = new ManualExchangeRateProvider(
        [
            new ExchangeRate("EUR", "USD", 1.1m, DateTimeOffset.UtcNow, "test")
        ]);

        var result = await CreateEngine(rates).CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            AddOns: [new PricingAddOnInput("a", "Add-on", 2, new Money(10m, "EUR"))],
            IncludeTax: false,
            RecordDecision: false));

        result.AddOnsTotal.Amount.Should().Be(22m);
    }

    // ---------- Ledger recording ----------

    [Fact]
    public async Task CalculateAsync_RecordsCalculationQuoteInLedgerWithVersion2Breakdown()
    {
        PricingDecisionRecord? recorded = null;
        _ledgerWriter
            .Setup(x => x.RecordAsync(It.IsAny<PricingDecisionRecord>(), It.IsAny<CancellationToken>()))
            .Callback<PricingDecisionRecord, CancellationToken>((decision, _) => recorded = decision)
            .ReturnsAsync((PricingDecisionRecordingStatus.Recorded, new PricingDecisionLedgerEntry { Id = Guid.NewGuid() }));

        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(BaseAmount: 100m, IncludeTax: false, RecordDecision: true));

        recorded.Should().NotBeNull();
        recorded!.DecisionType.Should().Be(PricingDecisionType.CalculationQuote);
        recorded.IdempotencyKey.Should().Be($"pricing-quote:{result.CalculationId:N}");
        recorded.Breakdown.Version.Should().Be(2);
        recorded.Breakdown.BaseAmount.Should().Be(100m);
        recorded.Breakdown.FinalAmount.Should().Be(result.AmountDue.Amount);
        recorded.Breakdown.TaxAmount.Should().Be(0m);
        recorded.Breakdown.CreditsApplied.Should().Be(0m);
        recorded.Breakdown.ProrationAdjustment.Should().Be(0m);
        result.LedgerEntryId.Should().NotBeNull();
    }

    [Fact]
    public async Task CalculateAsync_RecordsVersion2BreakdownThatReplaysExactly()
    {
        PricingDecisionRecord? recorded = null;
        _promoCalculator
            .Setup(x => x.ApplyAsync(It.IsAny<PromoDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoDiscountResult(20m, [new AppliedPromoDiscount("X", "x", 20m, null, DiscountType.FixedAmount, 50, true, 1)], [], null));
        _taxService
            .Setup(x => x.CalculateTaxAsync(It.IsAny<TaxCalculationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxCalculationResult { TaxAmount = 16m, TotalAmount = 96m, EffectiveTaxRate = 0.2m, JurisdictionCode = "DE" });
        _ledgerWriter
            .Setup(x => x.RecordAsync(It.IsAny<PricingDecisionRecord>(), It.IsAny<CancellationToken>()))
            .Callback<PricingDecisionRecord, CancellationToken>((decision, _) => recorded = decision)
            .ReturnsAsync((PricingDecisionRecordingStatus.Recorded, new PricingDecisionLedgerEntry()));

        await CreateEngine().CalculateAsync(new PricingCalculationRequest(
            BaseAmount: 100m,
            PromoCodes: ["X"],
            TaxInfo: new PricingTaxInfo("DE"),
            IncludeTax: true,
            RecordDecision: true));

        // Replay: the drift validator arithmetic must reproduce the recorded final.
        recorded!.Breakdown.RecomputeFinalAmount().Should().Be(recorded.Breakdown.FinalAmount);
    }

    [Fact]
    public async Task CalculateAsync_RecordDecisionFalse_WritesNothing()
    {
        var result = await CreateEngine().CalculateAsync(new PricingCalculationRequest(BaseAmount: 100m, IncludeTax: false, RecordDecision: false));

        _ledgerWriter.Verify(x => x.RecordAsync(It.IsAny<PricingDecisionRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        result.LedgerEntryId.Should().BeNull();
    }

    // ---------- Preview ----------

    [Fact]
    public async Task PreviewAsync_ReturnsImmediateProratedChargeAndNextRenewal()
    {
        var currentPlan = Guid.NewGuid();
        var newPlan = Guid.NewGuid();
        SetupPlanPrice(currentPlan, 100m);
        SetupPlanPrice(newPlan, 200m);

        _prorationCalculator
            .Setup(x => x.CalculateProrationAsync(It.IsAny<PlanProrationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanProrationResult(50m, 100m, 50m, SystemClock.UtcNow, 30, 15));

        var now = SystemClock.UtcNow;
        var preview = await CreateEngine().PreviewAsync(new PricingPreviewRequest(
            currentPlan,
            newPlan,
            now.AddDays(-15),
            now.AddDays(15),
            IncludeTax: false));

        preview.ImmediateCharge.Proration.Should().NotBeNull();
        preview.ImmediateCharge.Total.Amount.Should().Be(200m + 50m);
        preview.NextRenewal.Proration.Should().BeNull();
        preview.NextRenewal.Total.Amount.Should().Be(200m);

        // Previews are quotes: no ledger writes.
        _ledgerWriter.Verify(x => x.RecordAsync(It.IsAny<PricingDecisionRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Caching ----------

    [Fact]
    public async Task GetPlanPriceAsync_CachesWithinTtl()
    {
        var planId = Guid.NewGuid();
        SetupPlanPrice(planId, 100m);
        var engine = CreateEngine();

        await engine.GetPlanPriceAsync(planId, BillingCycle.Monthly, "USD");
        await engine.GetPlanPriceAsync(planId, BillingCycle.Monthly, "USD");
        await engine.GetPlanPriceAsync(planId, BillingCycle.Monthly, "USD");

        _planResolver.Verify(x => x.GetPlanPriceAsync(planId, BillingCycle.Monthly, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- Performance (issue #417: < 100ms p99) ----------

    [Fact]
    public async Task CalculateAsync_CompletesWithin100MsAt99thPercentile()
    {
        var planId = Guid.NewGuid();
        SetupPlanPrice(planId, 100m);

        _promoCalculator
            .Setup(x => x.ApplyAsync(It.IsAny<PromoDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoDiscountResult(10m, [new AppliedPromoDiscount("X", "x", 10m, 10m, DiscountType.Percentage, 50, true, 1)], [], null));

        _taxService
            .Setup(x => x.CalculateTaxAsync(It.IsAny<TaxCalculationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxCalculationResult { TaxAmount = 16m, TotalAmount = 106m, EffectiveTaxRate = 0.16m, JurisdictionCode = "DE" });

        var userId = Guid.NewGuid();
        _walletService
            .Setup(x => x.GetWalletByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateWallet(userId, 20m));

        _ledgerWriter
            .Setup(x => x.RecordAsync(It.IsAny<PricingDecisionRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PricingDecisionRecordingStatus.Recorded, new PricingDecisionLedgerEntry()));

        var request = new PricingCalculationRequest(
            PlanId: planId,
            UserId: userId,
            AddOns: [new PricingAddOnInput("a", "Add-on", 2, new Money(10m, "USD"))],
            UsageCharges: [new PricingUsageChargeInput("api", 250, 0.01m, [new PricingUsageTier(0, 100, 0.02m), new PricingUsageTier(100, null, 0.01m)])],
            PromoCodes: ["X"],
            TaxInfo: new PricingTaxInfo("DE"),
            IncludeTax: true,
            ApplyCredits: true,
            RecordDecision: true);

        var engine = CreateEngine();

        // Warm-up (JIT + caches).
        for (var i = 0; i < 10; i++)
        {
            await engine.CalculateAsync(request);
        }

        const int iterations = 300;
        var samples = new List<long>(iterations);
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            stopwatch.Restart();
            var result = await engine.CalculateAsync(request);
            stopwatch.Stop();
            result.ValidationErrors.Should().BeEmpty();
            samples.Add(stopwatch.ElapsedMilliseconds);
        }

        samples.Sort();
        var p99 = samples[(int)Math.Floor(iterations * 0.99) - 1];
        p99.Should().BeLessThan(100, $"99th percentile calculation latency must stay under 100ms (measured p99 = {p99}ms over {iterations} iterations)");
    }

    private static UserWallet CreateWallet(Guid userId, decimal balance, string currency = "USD")
    {
        return new UserWallet { UserId = userId, Balance = balance, Currency = currency };
    }
}
