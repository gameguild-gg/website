using FluentAssertions;
using GameGuild.Commerce.Payments;
using GameGuild.Commerce.Payments.Commands.CloseWallet;
using GameGuild.Commerce.Payments.Commands.FreezeWallet;
using GameGuild.Commerce.Payments.Commands.PatchWallet;
using GameGuild.Commerce.Payments.Commands.UnfreezeWallet;
using GameGuild.Commerce.Payments.Queries.GetWalletAuditLog;
using GameGuild.Commerce.Payments.Queries.GetWalletById;
using GameGuild.Commerce.Payments.Queries.ListWallets;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests;

#region StripeAmountConverter Tests

public class StripeAmountConverterTests
{
    [Theory]
    [InlineData(10.50, "USD", 1050L)]
    [InlineData(99.99, "EUR", 9999L)]
    [InlineData(0.01, "GBP", 1L)]
    [InlineData(100.00, "USD", 10000L)]
    public void ToStripeAmount_RegularCurrency_MultipliesBy100(decimal amount, string currency, long expected)
    {
        var result = StripeAmountConverter.ToStripeAmount(amount, currency);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1000, "JPY", 1000L)]
    [InlineData(500, "KRW", 500L)]
    [InlineData(250, "VND", 250L)]
    [InlineData(100, "BIF", 100L)]
    [InlineData(100, "CLP", 100L)]
    [InlineData(100, "DJF", 100L)]
    [InlineData(100, "GNF", 100L)]
    [InlineData(100, "KMF", 100L)]
    [InlineData(100, "MGA", 100L)]
    [InlineData(100, "PYG", 100L)]
    [InlineData(100, "RWF", 100L)]
    [InlineData(100, "UGX", 100L)]
    [InlineData(100, "VUV", 100L)]
    [InlineData(100, "XAF", 100L)]
    [InlineData(100, "XOF", 100L)]
    [InlineData(100, "XPF", 100L)]
    public void ToStripeAmount_ZeroDecimalCurrency_NoMultiplication(decimal amount, string currency, long expected)
    {
        var result = StripeAmountConverter.ToStripeAmount(amount, currency);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1050L, "USD", 10.50)]
    [InlineData(9999L, "EUR", 99.99)]
    [InlineData(1L, "GBP", 0.01)]
    public void FromStripeAmount_RegularCurrency_DividesBy100(long amount, string currency, decimal expected)
    {
        var result = StripeAmountConverter.FromStripeAmount(amount, currency);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1000L, "JPY", 1000)]
    [InlineData(500L, "KRW", 500)]
    public void FromStripeAmount_ZeroDecimalCurrency_NoDivision(long amount, string currency, decimal expected)
    {
        var result = StripeAmountConverter.FromStripeAmount(amount, currency);
        result.Should().Be(expected);
    }

    [Fact]
    public void ToStripeAmount_CaseInsensitive()
    {
        var lower = StripeAmountConverter.ToStripeAmount(100, "jpy");
        var upper = StripeAmountConverter.ToStripeAmount(100, "JPY");
        lower.Should().Be(upper);
    }
}

#endregion

#region StripeStatusMapper Tests

public class StripeStatusMapperTests
{
    [Theory]
    [InlineData("succeeded", PaymentStatus.Succeeded)]
    [InlineData("processing", PaymentStatus.Processing)]
    [InlineData("requires_payment_method", PaymentStatus.Failed)]
    [InlineData("requires_confirmation", PaymentStatus.Pending)]
    [InlineData("requires_action", PaymentStatus.RequiresAction)]
    [InlineData("canceled", PaymentStatus.Cancelled)]
    public void MapPaymentStatus_KnownStatus_ReturnsCorrectMapping(string stripeStatus, PaymentStatus expected)
    {
        var result = StripeStatusMapper.MapPaymentStatus(stripeStatus);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("unknown_status")]
    [InlineData("")]
    [InlineData("something_else")]
    public void MapPaymentStatus_UnknownStatus_ReturnsPending(string stripeStatus)
    {
        var result = StripeStatusMapper.MapPaymentStatus(stripeStatus);
        result.Should().Be(PaymentStatus.Pending);
    }

    [Theory]
    [InlineData("duplicate", "duplicate")]
    [InlineData("fraudulent", "fraudulent")]
    [InlineData("requested_by_customer", "requested_by_customer")]
    [InlineData("customer_request", "requested_by_customer")]
    public void MapRefundReason_KnownReason_ReturnsMapping(string reason, string expected)
    {
        var result = StripeStatusMapper.MapRefundReason(reason);
        result.Should().Be(expected);
    }

    [Fact]
    public void MapRefundReason_Null_ReturnsNull()
    {
        var result = StripeStatusMapper.MapRefundReason(null);
        result.Should().BeNull();
    }

    [Fact]
    public void MapRefundReason_Empty_ReturnsNull()
    {
        var result = StripeStatusMapper.MapRefundReason("");
        result.Should().BeNull();
    }

    [Fact]
    public void MapRefundReason_UnknownReason_ReturnsNull()
    {
        var result = StripeStatusMapper.MapRefundReason("other_reason");
        result.Should().BeNull();
    }
}

#endregion

#region LedgerAccountExtensions Tests

public class LedgerAccountExtensionsTests
{
    [Theory]
    [InlineData(LedgerAccount.Cash, "1000")]
    [InlineData(LedgerAccount.AccountsReceivable, "1100")]
    [InlineData(LedgerAccount.AccountsPayable, "2000")]
    [InlineData(LedgerAccount.ProductRevenue, "4000")]
    [InlineData(LedgerAccount.PaymentProcessingFees, "5000")]
    [InlineData(LedgerAccount.SalesDiscounts, "6000")]
    public void ToAccountCode_ReturnsFormattedCode(LedgerAccount account, string expected)
    {
        account.ToAccountCode().Should().Be(expected);
    }

    [Theory]
    [InlineData(LedgerAccount.Cash, "Cash")]
    [InlineData(LedgerAccount.AccountsReceivable, "Accounts Receivable")]
    [InlineData(LedgerAccount.ProductRevenue, "Product Revenue")]
    [InlineData(LedgerAccount.PaymentProcessingFees, "Payment Processing Fees")]
    [InlineData(LedgerAccount.SalesDiscounts, "Sales Discounts")]
    [InlineData(LedgerAccount.ReturnsAndAllowances, "Returns and Allowances")]
    public void GetDescription_ReturnsDescriptionAttribute(LedgerAccount account, string expected)
    {
        account.GetDescription().Should().Be(expected);
    }

    [Theory]
    [InlineData(LedgerAccount.Cash, true)]
    [InlineData(LedgerAccount.AccountsReceivable, true)]
    [InlineData(LedgerAccount.PrepaidExpenses, true)]
    [InlineData(LedgerAccount.UserWalletDeposits, true)]
    [InlineData(LedgerAccount.PaymentGatewayPending, true)]
    [InlineData(LedgerAccount.AccountsPayable, false)]
    [InlineData(LedgerAccount.ProductRevenue, false)]
    public void IsAsset_ReturnsCorrectly(LedgerAccount account, bool expected)
    {
        account.IsAsset().Should().Be(expected);
    }

    [Theory]
    [InlineData(LedgerAccount.AccountsPayable, true)]
    [InlineData(LedgerAccount.DeferredRevenue, true)]
    [InlineData(LedgerAccount.TaxesPayable, true)]
    [InlineData(LedgerAccount.Cash, false)]
    [InlineData(LedgerAccount.ProductRevenue, false)]
    public void IsLiability_ReturnsCorrectly(LedgerAccount account, bool expected)
    {
        account.IsLiability().Should().Be(expected);
    }

    [Theory]
    [InlineData(LedgerAccount.ProductRevenue, true)]
    [InlineData(LedgerAccount.SubscriptionRevenue, true)]
    [InlineData(LedgerAccount.CourseRevenue, true)]
    [InlineData(LedgerAccount.Cash, false)]
    [InlineData(LedgerAccount.PaymentProcessingFees, false)]
    public void IsRevenue_ReturnsCorrectly(LedgerAccount account, bool expected)
    {
        account.IsRevenue().Should().Be(expected);
    }

    [Theory]
    [InlineData(LedgerAccount.PaymentProcessingFees, true)]
    [InlineData(LedgerAccount.CommissionExpense, true)]
    [InlineData(LedgerAccount.RefundsAndChargebacks, true)]
    [InlineData(LedgerAccount.BadDebtExpense, true)]
    [InlineData(LedgerAccount.Cash, false)]
    [InlineData(LedgerAccount.SalesDiscounts, false)]
    public void IsExpense_ReturnsCorrectly(LedgerAccount account, bool expected)
    {
        account.IsExpense().Should().Be(expected);
    }

    [Theory]
    [InlineData(LedgerAccount.SalesDiscounts, true)]
    [InlineData(LedgerAccount.ReturnsAndAllowances, true)]
    [InlineData(LedgerAccount.Cash, false)]
    [InlineData(LedgerAccount.PaymentProcessingFees, false)]
    public void IsContra_ReturnsCorrectly(LedgerAccount account, bool expected)
    {
        account.IsContra().Should().Be(expected);
    }
}

#endregion

#region NullPlanPricingResolver Tests

public class NullPlanPricingResolverTests
{
    private readonly NullPlanPricingResolver _resolver = new();

    [Fact]
    public async Task GetPlanMonthlyPriceAsync_ReturnsNull()
    {
        var result = await _resolver.GetPlanMonthlyPriceAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPlanPriceAsync_ReturnsNull()
    {
        var result = await _resolver.GetPlanPriceAsync(Guid.NewGuid(), BillingCycle.Monthly);
        result.Should().BeNull();
    }

    [Fact]
    public async Task PlanExistsAsync_ReturnsFalse()
    {
        var result = await _resolver.PlanExistsAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }
}

#endregion

#region StripePaymentGateway Tests

public class StripePaymentGatewayTests
{
    private readonly Mock<IStripePaymentService> _paymentService = new();
    private readonly Mock<IStripeCustomerService> _customerService = new();
    private readonly StripePaymentGateway _gateway;

    public StripePaymentGatewayTests()
    {
        var options = Options.Create(new StripeGatewayOptions { IsEnabled = true, UseSimulation = true });
        _gateway = new StripePaymentGateway(options, _paymentService.Object, _customerService.Object);
    }

    [Fact]
    public void ProviderId_ReturnsStripe()
    {
        _gateway.ProviderId.Should().Be("stripe");
    }

    [Fact]
    public void DisplayName_ReturnsStripe()
    {
        _gateway.DisplayName.Should().Be("Stripe");
    }

    [Fact]
    public void IsEnabled_ReturnsOptionValue()
    {
        _gateway.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessPaymentAsync_DelegatesToPaymentService()
    {
        var request = new GatewayPaymentRequest("idem_1", 100m, "USD", "cust_1", "pm_1", "Test payment");
        var expected = new GatewayPaymentResult(true, "tx_1", "ext_1", null, null, PaymentStatus.Succeeded, DateTime.UtcNow);
        _paymentService.Setup(x => x.ProcessPaymentAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _gateway.ProcessPaymentAsync(request);
        result.Should().Be(expected);
        _paymentService.Verify(x => x.ProcessPaymentAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRefundAsync_DelegatesToPaymentService()
    {
        var request = new GatewayRefundRequest("idem_1", "tx_1", null, "duplicate");
        var expected = new GatewayRefundResult(true, "ref_1", 100m, null, null, DateTime.UtcNow);
        _paymentService.Setup(x => x.ProcessRefundAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _gateway.ProcessRefundAsync(request);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ValidateWebhookSignatureAsync_DelegatesToPaymentService()
    {
        _paymentService.Setup(x => x.ValidateWebhookSignatureAsync("payload", "sig", "secret"))
            .ReturnsAsync(true);

        var result = await _gateway.ValidateWebhookSignatureAsync("payload", "sig", "secret");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CreateCustomerAsync_DelegatesToCustomerService()
    {
        var request = new GatewayCustomerRequest("test@test.com", "Test User", null);
        var expected = new GatewayCustomerResult(true, "cus_1", null, null);
        _customerService.Setup(x => x.CreateCustomerAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _gateway.CreateCustomerAsync(request);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task CreatePaymentMethodAsync_DelegatesToCustomerService()
    {
        var request = new GatewayPaymentMethodRequest("cus_1", "tok_1");
        var expected = new GatewayPaymentMethodResult(true, "pm_1", "4242", "visa", 12, 25, null, null);
        _customerService.Setup(x => x.CreatePaymentMethodAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _gateway.CreatePaymentMethodAsync(request);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_DelegatesToCustomerService()
    {
        var expected = new GatewayCancellationResult(true, null, null, DateTime.UtcNow);
        _customerService.Setup(x => x.CancelSubscriptionAsync("sub_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _gateway.CancelSubscriptionAsync("sub_1");
        result.Should().Be(expected);
    }

    [Fact]
    public void IsEnabled_WhenDisabled_ReturnsFalse()
    {
        var options = Options.Create(new StripeGatewayOptions { IsEnabled = false });
        var gateway = new StripePaymentGateway(options, _paymentService.Object, _customerService.Object);
        gateway.IsEnabled.Should().BeFalse();
    }
}

#endregion

#region StripePaymentService Tests

public class StripePaymentServiceTests
{
    private readonly StripePaymentService _service;
    private readonly Mock<ILogger<StripePaymentService>> _logger = new();

    public StripePaymentServiceTests()
    {
        var options = Options.Create(new StripeGatewayOptions { UseSimulation = true });
        _service = new StripePaymentService(options, _logger.Object);
    }

    [Fact]
    public async Task ProcessPaymentAsync_Simulated_ReturnsSuccess()
    {
        var request = new GatewayPaymentRequest("idem_1", 50m, "USD", "cust_1", "pm_1", "Test");
        var result = await _service.ProcessPaymentAsync(request);
        result.Success.Should().BeTrue();
        result.TransactionId.Should().NotBeNullOrEmpty();
        result.Status.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task ProcessRefundAsync_Simulated_ReturnsSuccess()
    {
        var request = new GatewayRefundRequest("idem_2", "tx_1", 25m, "duplicate");
        var result = await _service.ProcessRefundAsync(request);
        result.Success.Should().BeTrue();
        result.RefundId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateWebhookSignatureAsync_Simulated_ValidFormat_ReturnsTrue()
    {
        var result = await _service.ValidateWebhookSignatureAsync("payload", "t=123,v1=abc", "secret");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateWebhookSignatureAsync_Simulated_EmptySignature_ReturnsFalse()
    {
        var result = await _service.ValidateWebhookSignatureAsync("payload", "", "secret");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateWebhookSignatureAsync_Simulated_EmptySecret_ReturnsFalse()
    {
        var result = await _service.ValidateWebhookSignatureAsync("payload", "sig", "");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateWebhookSignatureAsync_Simulated_NonEmptySignature_ReturnsTrue()
    {
        // Even without valid format, non-empty signature with non-empty secret returns true
        var result = await _service.ValidateWebhookSignatureAsync("payload", "any_sig", "secret");
        result.Should().BeTrue();
    }
}

#endregion

#region TaxCalculationService Tests

public class TaxCalculationServiceAdditionalTests
{
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly TaxCalculationService _service;

    public TaxCalculationServiceAdditionalTests()
    {
        _service = new TaxCalculationService(_context.Object, NullLogger<TaxCalculationService>.Instance, _cache);
    }

    [Fact]
    public void Constructor_NullContext_Throws()
    {
        var act = () => new TaxCalculationService(null!, NullLogger<TaxCalculationService>.Instance, _cache);
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new TaxCalculationService(_context.Object, null!, _cache);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullCache_Throws()
    {
        var act = () => new TaxCalculationService(_context.Object, NullLogger<TaxCalculationService>.Instance, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Theory]
    [InlineData("", "DE", false)]
    [InlineData(" ", "DE", false)]
    [InlineData(null, "DE", false)]
    [InlineData("DE123456789", "DE", true)]  // 11 chars, starts with DE
    [InlineData("DE12345", "DE", false)]     // too short (7 chars)
    [InlineData("DE1234567890123", "DE", false)] // too long (15 chars)
    [InlineData("FR123456789", "FR", true)]
    [InlineData("GB12345678", "GB", true)]   // 10 chars
    [InlineData("DE123456789", "FR", false)] // starts with DE not FR
    public async Task ValidateVatNumberAsync_VariousInputs(string? vatNumber, string countryCode, bool expected)
    {
        var result = await _service.ValidateVatNumberAsync(vatNumber!, countryCode);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ValidateVatNumberAsync_WithSpaces_StripsAndValidates()
    {
        // "DE 12345 6789" → "DE123456789" (11 chars, starts with DE)
        var result = await _service.ValidateVatNumberAsync("DE 12345 6789", "DE");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateVatNumberAsync_LowerCase_ConvertsToUpper()
    {
        var result = await _service.ValidateVatNumberAsync("de123456789", "DE");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CalculateTaxAsync_ReturnsZeroTax_WhenJurisdictionDoesNotExist()
    {
        await using var context = CreateTaxDbContext();
        var service = CreateTaxService(context);

        var result = await service.CalculateTaxAsync(CreateTaxRequest("BR-SP", 125m));

        result.TaxAmount.Should().Be(0m);
        result.TotalAmount.Should().Be(125m);
        result.JurisdictionCode.Should().Be("BR-SP");
        result.JurisdictionName.Should().Be("Unknown");
        result.IsTaxExempt.Should().BeFalse();
        result.IsReverseCharge.Should().BeFalse();
    }

    [Fact]
    public async Task CalculateTaxAsync_ReturnsExemptResult_WhenApplicableExemptionsExist()
    {
        await using var context = CreateTaxDbContext(db =>
        {
            db.Add(new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "DE",
                Name = "Germany",
                Type = TaxJurisdictionType.Country,
                IsActive = true
            });
        });

        var service = CreateTaxService(context);
        var request = CreateTaxRequest("DE", 99m, applicableExemptions: new[] { "non-profit" });

        var result = await service.CalculateTaxAsync(request);

        result.TaxAmount.Should().Be(0m);
        result.TotalAmount.Should().Be(99m);
        result.IsTaxExempt.Should().BeTrue();
        result.ExemptionReason.Should().Be("Customer tax exemption");
        result.JurisdictionName.Should().Be("Germany");
    }

    [Fact]
    public async Task CalculateTaxAsync_ReturnsReverseChargeResult_ForEligibleB2BRequest()
    {
        await using var context = CreateTaxDbContext(db =>
        {
            db.Add(new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "DE",
                Name = "Germany",
                Type = TaxJurisdictionType.Country,
                IsActive = true,
                IsReverseChargeApplicable = true
            });
        });

        var service = CreateTaxService(context);
        var request = CreateTaxRequest("DE", 250m, CustomerType.B2B, "DE123456789");

        var result = await service.CalculateTaxAsync(request);

        result.TaxAmount.Should().Be(0m);
        result.TotalAmount.Should().Be(250m);
        result.IsReverseCharge.Should().BeTrue();
        result.IsTaxExempt.Should().BeFalse();
        result.ExemptionReason.Should().Be("EU B2B reverse charge mechanism");
        result.JurisdictionName.Should().Be("Germany");
    }

    [Fact]
    public async Task CalculateTaxAsync_ReturnsZeroTax_WhenNoApplicableRulesExist()
    {
        await using var context = CreateTaxDbContext(db =>
        {
            var jurisdiction = new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "DE",
                Name = "Germany",
                Type = TaxJurisdictionType.Country,
                IsActive = true
            };

            jurisdiction.TaxRules.Add(new TaxRule
            {
                Id = Guid.NewGuid(),
                Name = "Inactive rule",
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                RuleType = TaxRuleType.Standard,
                Priority = 10,
                IsActive = false,
                EffectiveFrom = SystemClock.UtcNow.AddDays(-30)
            });

            db.Add(jurisdiction);
        });

        var service = CreateTaxService(context);

        var result = await service.CalculateTaxAsync(CreateTaxRequest("DE", 150m));

        result.TaxAmount.Should().Be(0m);
        result.TotalAmount.Should().Be(150m);
        result.TaxDescription.Should().Be("No tax applicable");
        result.JurisdictionCode.Should().Be("DE");
    }

    [Fact]
    public async Task CalculateTaxAsync_ReturnsZeroTax_WhenNoTaxRateExists()
    {
        await using var context = CreateTaxDbContext(db =>
        {
            var jurisdiction = new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "DE",
                Name = "Germany",
                Type = TaxJurisdictionType.Country,
                IsActive = true
            };

            jurisdiction.TaxRules.Add(new TaxRule
            {
                Id = Guid.NewGuid(),
                Name = "Standard VAT rule",
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                RuleType = TaxRuleType.Standard,
                Priority = 10,
                IsActive = true,
                EffectiveFrom = SystemClock.UtcNow.AddDays(-30),
                CustomerTypeFilter = CustomerType.B2C
            });

            db.Add(jurisdiction);
        });

        var service = CreateTaxService(context);

        var result = await service.CalculateTaxAsync(CreateTaxRequest("DE", 100m));

        result.TaxAmount.Should().Be(0m);
        result.TotalAmount.Should().Be(100m);
        result.TaxDescription.Should().Be("No tax applicable");
    }

    [Fact]
    public async Task CalculateTaxAsync_ComputesExclusiveTax_WhenMatchingRuleAndRateExist()
    {
        await using var context = CreateTaxDbContext(db =>
        {
            var jurisdiction = new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "DE",
                Name = "Germany",
                Type = TaxJurisdictionType.Country,
                IsActive = true
            };

            var rate = new TaxRate
            {
                Id = Guid.NewGuid(),
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                TaxType = TaxType.VAT,
                Rate = 0.19m,
                ProductCategory = "saas",
                EffectiveFrom = SystemClock.UtcNow.AddDays(-30),
                IsActive = true,
                Description = "German VAT"
            };

            jurisdiction.TaxRules.Add(new TaxRule
            {
                Id = Guid.NewGuid(),
                Name = "Standard VAT rule",
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                RuleType = TaxRuleType.Standard,
                Priority = 10,
                IsActive = true,
                EffectiveFrom = SystemClock.UtcNow.AddDays(-30),
                CustomerTypeFilter = CustomerType.B2C,
                DefaultTaxRateId = rate.Id,
                DefaultTaxRate = rate
            });

            db.Add(jurisdiction);
            db.Add(rate);
        });

        var service = CreateTaxService(context);
        var request = CreateTaxRequest("DE", 100m, productCategory: "saas");

        var result = await service.CalculateTaxAsync(request);

        result.SubtotalAmount.Should().Be(100m);
        result.TaxAmount.Should().Be(19m);
        result.TotalAmount.Should().Be(119m);
        result.EffectiveTaxRate.Should().Be(0.19m);
        result.JurisdictionName.Should().Be("Germany");
        result.TaxBreakdowns.Should().ContainSingle();
        result.TaxBreakdowns[0].TaxAmount.Should().Be(19m);
    }

    [Fact]
    public async Task CalculateTaxAsync_ComputesInclusiveTax_WhenRuleIsTaxInclusive()
    {
        await using var context = CreateTaxDbContext(db =>
        {
            var jurisdiction = new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "DE",
                Name = "Germany",
                Type = TaxJurisdictionType.Country,
                IsActive = true
            };

            var rate = new TaxRate
            {
                Id = Guid.NewGuid(),
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                TaxType = TaxType.VAT,
                Rate = 0.19m,
                EffectiveFrom = SystemClock.UtcNow.AddDays(-30),
                IsActive = true,
                Description = "German VAT"
            };

            jurisdiction.TaxRules.Add(new TaxRule
            {
                Id = Guid.NewGuid(),
                Name = "Inclusive VAT rule",
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                RuleType = TaxRuleType.Standard,
                Priority = 10,
                IsActive = true,
                EffectiveFrom = SystemClock.UtcNow.AddDays(-30),
                IsTaxInclusive = true,
                DefaultTaxRateId = rate.Id,
                DefaultTaxRate = rate
            });

            db.Add(jurisdiction);
            db.Add(rate);
        });

        var service = CreateTaxService(context);

        var result = await service.CalculateTaxAsync(CreateTaxRequest("DE", 119m));

        result.SubtotalAmount.Should().Be(100m);
        result.TaxAmount.Should().Be(19m);
        result.TotalAmount.Should().Be(119m);
        result.TaxDescription.Should().Be("German VAT");
    }

    [Fact]
    public async Task GetTaxRateAsync_ReturnsSpecificCategoryMatch_AndUsesCache()
    {
        await using var context = CreateTaxDbContext(db =>
        {
            var jurisdiction = new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "DE",
                Name = "Germany",
                Type = TaxJurisdictionType.Country,
                IsActive = true
            };

            db.Add(jurisdiction);
            db.Add(new TaxRate
            {
                Id = Guid.NewGuid(),
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                TaxType = TaxType.VAT,
                Rate = 0.10m,
                EffectiveFrom = SystemClock.UtcNow.AddDays(-30),
                IsActive = true,
                Description = "Default VAT"
            });
            db.Add(new TaxRate
            {
                Id = Guid.NewGuid(),
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                TaxType = TaxType.VAT,
                Rate = 0.19m,
                ProductCategory = "saas",
                EffectiveFrom = SystemClock.UtcNow.AddDays(-30),
                IsActive = true,
                Description = "SaaS VAT"
            });
        });

        var service = CreateTaxService(context);
        var effectiveDate = SystemClock.UtcNow;

        var first = await service.GetTaxRateAsync("DE", TaxType.VAT, "saas", effectiveDate);

        context.RemoveRange(context.Set<TaxRate>());
        context.SaveChanges();

        var second = await service.GetTaxRateAsync("DE", TaxType.VAT, "saas", effectiveDate);

        first.Should().NotBeNull();
        first!.Rate.Should().Be(0.19m);
        second.Should().NotBeNull();
        second!.Rate.Should().Be(0.19m);
    }

    [Fact]
    public async Task GetTaxRateAsync_FallsBackToDefaultCategory_WhenSpecificCategoryMissing()
    {
        await using var context = CreateTaxDbContext(db =>
        {
            var jurisdiction = new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "DE",
                Name = "Germany",
                Type = TaxJurisdictionType.Country,
                IsActive = true
            };

            db.Add(jurisdiction);
            db.Add(new TaxRate
            {
                Id = Guid.NewGuid(),
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                TaxType = TaxType.VAT,
                Rate = 0.15m,
                EffectiveFrom = SystemClock.UtcNow.AddDays(-30),
                IsActive = true,
                Description = "Default VAT"
            });
        });

        var service = CreateTaxService(context);

        var result = await service.GetTaxRateAsync("DE", TaxType.VAT, "missing-category", SystemClock.UtcNow);

        result.Should().NotBeNull();
        result!.Rate.Should().Be(0.15m);
        result.ProductCategory.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTaxExemptionAsync_ReturnsTrue_ForVerifiedDirectExemption()
    {
        var customerId = Guid.NewGuid();

        await using var context = CreateTaxDbContext(db =>
        {
            var exemption = CustomerTaxExemption.Create(
                Guid.NewGuid(),
                customerId,
                "DE",
                TaxExemptionType.NonProfit,
                "CERT-123",
                SystemClock.UtcNow.AddDays(-5),
                SystemClock.UtcNow.AddDays(5));
            exemption.MarkVerified("system");
            db.Add(exemption);
        });

        var service = CreateTaxService(context);

        var result = await service.ValidateTaxExemptionAsync(customerId, "DE");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateTaxExemptionAsync_ReturnsTrue_ForVerifiedParentJurisdictionExemption()
    {
        var customerId = Guid.NewGuid();

        await using var context = CreateTaxDbContext(db =>
        {
            var exemption = CustomerTaxExemption.Create(
                Guid.NewGuid(),
                customerId,
                "US",
                TaxExemptionType.Reseller,
                "CERT-US",
                SystemClock.UtcNow.AddDays(-5),
                SystemClock.UtcNow.AddDays(5));
            exemption.MarkVerified("system");
            db.Add(exemption);
        });

        var service = CreateTaxService(context);

        var result = await service.ValidateTaxExemptionAsync(customerId, "US-CA");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetTaxJurisdictionsAsync_ReturnsActiveJurisdictionsInOrder_AndUsesCache()
    {
        await using var context = CreateTaxDbContext(db =>
        {
            db.Add(new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "BE",
                Name = "Belgium",
                Type = TaxJurisdictionType.Country,
                IsActive = true
            });
            db.Add(new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "DE",
                Name = "Germany",
                Type = TaxJurisdictionType.Country,
                IsActive = true
            });
            db.Add(new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "ZZ",
                Name = "Inactive",
                Type = TaxJurisdictionType.Country,
                IsActive = false
            });
        });

        var service = CreateTaxService(context);

        var first = (await service.GetTaxJurisdictionsAsync()).Select(j => j.Code).ToList();

        context.RemoveRange(context.Set<TaxJurisdiction>());
        context.SaveChanges();

        var second = (await service.GetTaxJurisdictionsAsync()).Select(j => j.Code).ToList();

        first.Should().Equal("BE", "DE");
        second.Should().Equal("BE", "DE");
    }

    [Fact]
    public async Task GetApplicableTaxRulesAsync_FiltersAndOrdersRules()
    {
        await using var context = CreateTaxDbContext(db =>
        {
            var jurisdiction = new TaxJurisdiction
            {
                Id = Guid.NewGuid(),
                Code = "DE",
                Name = "Germany",
                Type = TaxJurisdictionType.Country,
                IsActive = true
            };

            db.Add(jurisdiction);
            db.Add(new TaxRule
            {
                Id = Guid.NewGuid(),
                Name = "Low priority valid",
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                RuleType = TaxRuleType.Standard,
                Priority = 20,
                IsActive = true,
                EffectiveFrom = SystemClock.UtcNow.AddDays(-10),
                CustomerTypeFilter = CustomerType.B2C
            });
            db.Add(new TaxRule
            {
                Id = Guid.NewGuid(),
                Name = "High priority valid",
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                RuleType = TaxRuleType.Standard,
                Priority = 10,
                IsActive = true,
                EffectiveFrom = SystemClock.UtcNow.AddDays(-10),
                CustomerTypeFilter = CustomerType.B2C
            });
            db.Add(new TaxRule
            {
                Id = Guid.NewGuid(),
                Name = "Wrong customer type",
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                RuleType = TaxRuleType.Standard,
                Priority = 5,
                IsActive = true,
                EffectiveFrom = SystemClock.UtcNow.AddDays(-10),
                CustomerTypeFilter = CustomerType.B2B
            });
            db.Add(new TaxRule
            {
                Id = Guid.NewGuid(),
                Name = "Inactive",
                TaxJurisdictionId = jurisdiction.Id,
                TaxJurisdiction = jurisdiction,
                RuleType = TaxRuleType.Standard,
                Priority = 1,
                IsActive = false,
                EffectiveFrom = SystemClock.UtcNow.AddDays(-10)
            });
        });

        var service = CreateTaxService(context);

        var result = (await service.GetApplicableTaxRulesAsync("DE", CustomerType.B2C, SystemClock.UtcNow)).ToList();

        result.Select(rule => rule.Name).Should().Equal("High priority valid", "Low priority valid");
    }

    private static TaxCalculationService CreateTaxService(PaymentsTaxTestDbContext context)
    {
        return new TaxCalculationService(context, NullLogger<TaxCalculationService>.Instance, new MemoryCache(new MemoryCacheOptions()));
    }

    private static PaymentsTaxTestDbContext CreateTaxDbContext(Action<PaymentsTaxTestDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<PaymentsTaxTestDbContext>()
            .UseInMemoryDatabase($"payments-tax-{Guid.NewGuid()}")
            .Options;

        var context = new PaymentsTaxTestDbContext(options);
        seed?.Invoke(context);
        context.SaveChanges();
        return context;
    }

    private static TaxCalculationRequest CreateTaxRequest(
        string jurisdictionCode,
        decimal amount,
        CustomerType customerType = CustomerType.B2C,
        string? customerVatNumber = null,
        IEnumerable<string>? applicableExemptions = null,
        string? productCategory = null)
    {
        return new TaxCalculationRequest
        {
            JurisdictionCode = jurisdictionCode,
            Amount = amount,
            Currency = "EUR",
            CustomerType = customerType,
            CustomerVatNumber = customerVatNumber,
            ProductCategory = productCategory,
            TransactionDate = SystemClock.UtcNow,
            ApplicableExemptions = applicableExemptions?.ToList() ?? new List<string>()
        };
    }

    private sealed class PaymentsTaxTestDbContext(DbContextOptions<PaymentsTaxTestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Database.BeginTransactionAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaxJurisdiction).Assembly);
            modelBuilder.Entity<CustomerTaxExemption>();
            base.OnModelCreating(modelBuilder);
        }
    }
}

#endregion

#region PaymentsModule DI Tests

public class PaymentsModuleDiTests
{
    [Fact]
    public void AddPaymentsModule_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:IsEnabled"] = "true",
                ["Stripe:UseSimulation"] = "true"
            })
            .Build();

        services.AddPaymentsModule(config);

        // Verify repositories
        services.Should().Contain(sd => sd.ServiceType == typeof(IAuditTrailRepository));
        services.Should().Contain(sd => sd.ServiceType == typeof(IFinancialLedgerRepository));
        services.Should().Contain(sd => sd.ServiceType == typeof(IPaymentRepository));
        services.Should().Contain(sd => sd.ServiceType == typeof(IRevenueEventRepository));
        services.Should().Contain(sd => sd.ServiceType == typeof(IWalletRepository));

        // Verify services
        services.Should().Contain(sd => sd.ServiceType == typeof(IDisputeService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IRevenueAuditService));
        services.Should().Contain(sd => sd.ServiceType == typeof(ITaxCalculationService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IWalletService));

        // Verify gateway services
        services.Should().Contain(sd => sd.ServiceType == typeof(IStripePaymentService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IStripeCustomerService));
        services.Should().Contain(sd => sd.ServiceType == typeof(IPaymentGateway));
    }

    [Fact]
    public void UsePaymentsModule_ReturnsApp()
    {
        var app = new Mock<IApplicationBuilder>();
        var result = app.Object.UsePaymentsModule();
        result.Should().Be(app.Object);
    }
}

#endregion

#region Payment And Tax Handler Persistence Tests

public class PaymentAndTaxHandlerPersistenceTests
{
    [Fact]
    public async Task CreateTaxJurisdictionHandler_Handle_PersistsJurisdictionAndDefaultRate()
    {
        await using var context = CreateContext();
        var handler = new CreateTaxJurisdictionHandler(context);

        var id = await handler.Handle(
            new CreateTaxJurisdictionCommand("US-CA", "California", "US", "CA", "SalesTax", 7.25m),
            CancellationToken.None);

        var jurisdiction = await context.Set<TaxJurisdiction>().FindAsync(id);
        jurisdiction.Should().NotBeNull();
        jurisdiction!.Code.Should().Be("US-CA");
        jurisdiction.Type.Should().Be(TaxJurisdictionType.State);

        var rate = context.Set<TaxRate>().Single(item => item.TaxJurisdictionId == id);
        rate.TaxType.Should().Be(TaxType.SalesTax);
        rate.Rate.Should().Be(0.0725m);
    }

    [Fact]
    public async Task CreateTaxRuleHandler_Handle_PersistsRuleAndRate()
    {
        await using var context = CreateContext(seed =>
        {
            seed.Set<TaxJurisdiction>().Add(CreateJurisdiction("US"));
        });
        var handler = new CreateTaxRuleHandler(context);

        var id = await handler.Handle(
            new CreateTaxRuleCommand("US", "subscription", "B2B", 10m, DateTime.UtcNow.Date, null, "Business tax"),
            CancellationToken.None);

        var rule = context.Set<TaxRule>().Include(item => item.DefaultTaxRate).Single(item => item.Id == id);
        rule.CustomerTypeFilter.Should().Be(CustomerType.B2B);
        rule.DefaultTaxRate.Should().NotBeNull();
        rule.DefaultTaxRate!.Rate.Should().Be(0.10m);
        rule.ProductCategories.Should().Contain("subscription");
    }

    [Fact]
    public async Task PatchTaxRuleHandler_Handle_UpdatesRuleAndRate()
    {
        await using var context = CreateContext(seed =>
        {
            var jurisdiction = CreateJurisdiction("US");
            var rate = CreateRate(jurisdiction.Id, 0.05m);
            seed.Set<TaxJurisdiction>().Add(jurisdiction);
            seed.Set<TaxRate>().Add(rate);
            seed.Set<TaxRule>().Add(CreateRule(jurisdiction.Id, rate.Id));
        });
        var rule = context.Set<TaxRule>().Single();
        var handler = new PatchTaxRuleHandler(context);

        var result = await handler.Handle(
            new PatchTaxRuleCommand(rule.Id, 8.5m, DateTime.UtcNow.Date, null, "Updated", false),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        var updated = context.Set<TaxRule>().Include(item => item.DefaultTaxRate).Single(item => item.Id == rule.Id);
        updated.IsActive.Should().BeFalse();
        updated.Description.Should().Be("Updated");
        updated.DefaultTaxRate!.Rate.Should().Be(0.085m);
        updated.DefaultTaxRate.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task PatchTaxJurisdictionHandler_Handle_UpdatesJurisdictionAndDefaultRate()
    {
        await using var context = CreateContext(seed =>
        {
            var jurisdiction = CreateJurisdiction("US");
            seed.Set<TaxJurisdiction>().Add(jurisdiction);
            seed.Set<TaxRate>().Add(CreateRate(jurisdiction.Id, 0.05m));
        });
        var jurisdictionId = context.Set<TaxJurisdiction>().Single().Id;
        var handler = new PatchTaxJurisdictionHandler(context);

        var result = await handler.Handle(
            new PatchTaxJurisdictionCommand(jurisdictionId, "United States", "SalesTax", 6.25m, false),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        var jurisdiction = context.Set<TaxJurisdiction>().Single(item => item.Id == jurisdictionId);
        jurisdiction.Name.Should().Be("United States");
        jurisdiction.IsActive.Should().BeFalse();

        var rate = context.Set<TaxRate>().Single(item => item.TaxJurisdictionId == jurisdictionId);
        rate.TaxType.Should().Be(TaxType.SalesTax);
        rate.Rate.Should().Be(0.0625m);
        rate.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTaxRuleHandler_Handle_DeactivatesRuleAndRate()
    {
        await using var context = CreateContext(seed =>
        {
            var jurisdiction = CreateJurisdiction("US");
            var rate = CreateRate(jurisdiction.Id, 0.05m);
            seed.Set<TaxJurisdiction>().Add(jurisdiction);
            seed.Set<TaxRate>().Add(rate);
            seed.Set<TaxRule>().Add(CreateRule(jurisdiction.Id, rate.Id));
        });
        var ruleId = context.Set<TaxRule>().Single().Id;
        var handler = new DeleteTaxRuleHandler(context);

        var result = await handler.Handle(new DeleteTaxRuleCommand(ruleId), CancellationToken.None);

        result.Should().Be(Unit.Value);
        var rule = context.Set<TaxRule>().Include(item => item.DefaultTaxRate).Single(item => item.Id == ruleId);
        rule.IsActive.Should().BeFalse();
        rule.DefaultTaxRate!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTaxJurisdictionHandler_Handle_DeactivatesJurisdictionRulesAndRates()
    {
        await using var context = CreateContext(seed =>
        {
            var jurisdiction = CreateJurisdiction("US");
            var rate = CreateRate(jurisdiction.Id, 0.05m);
            seed.Set<TaxJurisdiction>().Add(jurisdiction);
            seed.Set<TaxRate>().Add(rate);
            seed.Set<TaxRule>().Add(CreateRule(jurisdiction.Id, rate.Id));
        });
        var jurisdictionId = context.Set<TaxJurisdiction>().Single().Id;
        var handler = new DeleteTaxJurisdictionHandler(context);

        var result = await handler.Handle(new DeleteTaxJurisdictionCommand(jurisdictionId), CancellationToken.None);

        result.Should().Be(Unit.Value);
        context.Set<TaxJurisdiction>().Single().IsActive.Should().BeFalse();
        context.Set<TaxRule>().Single().IsActive.Should().BeFalse();
        context.Set<TaxRate>().Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetPaymentByIdQueryHandler_Handle_ReturnsPersistedPayment()
    {
        var payment = CreateSucceededPayment(Guid.NewGuid(), 42m);
        await using var context = CreateContext(seed => seed.Set<Payment>().Add(payment));
        var handler = new GetPaymentByIdQueryHandler(context);

        var result = await handler.Handle(new GetPaymentByIdQuery(payment.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Amount!.Amount.Should().Be(42m);
    }

    [Fact]
    public async Task GetPaymentHistoryQueryHandler_Handle_ReturnsUserFilteredHistory()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var payment = CreateSucceededPayment(tenantId, 25m);
        payment.SetMetadata($$"""{"userId":"{{userId}}"}""");
        await using var context = CreateContext(seed => seed.Set<Payment>().Add(payment));
        var handler = new GetPaymentHistoryQueryHandler(context);

        var result = await handler.Handle(new GetPaymentHistoryQuery(userId, tenantId), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].UserId.Should().Be(userId);
        result[0].Amount.Should().Be(25m);
    }

    [Fact]
    public async Task GetScheduledPaymentsQueryHandler_Handle_ReturnsRetrySchedule()
    {
        var payment = CreateFailedPayment(Guid.NewGuid(), 31m);
        await using var context = CreateContext(seed => seed.Set<Payment>().Add(payment));
        var handler = new GetScheduledPaymentsQueryHandler(context);

        var result = await handler.Handle(new GetScheduledPaymentsQuery(), CancellationToken.None);

        result.Should().ContainSingle(item => item.Status == PaymentStatus.Failed);
    }

    [Fact]
    public async Task GetRefundedPaymentsQueryHandler_Handle_ReturnsRefundedPayment()
    {
        var payment = CreateSucceededPayment(Guid.NewGuid(), 12m);
        payment.ProcessRefund(12m, "re_123", "customer_request");
        await using var context = CreateContext(seed => seed.Set<Payment>().Add(payment));
        var handler = new GetRefundedPaymentsQueryHandler(context);

        var result = await handler.Handle(new GetRefundedPaymentsQuery(RefundReason: "customer"), CancellationToken.None);

        result.Should().ContainSingle(item => item.Status == PaymentStatus.Refunded);
    }

    [Fact]
    public async Task GetOverduePaymentsQueryHandler_Handle_ReturnsEmpty()
    {
        await using var context = CreateContext();
        var handler = new GetOverduePaymentsQueryHandler(context);
        var result = await handler.Handle(new GetOverduePaymentsQuery(), CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFailedPaymentsQueryHandler_Handle_ReturnsFailedPayments()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(seed => seed.Set<Payment>().Add(CreateFailedPayment(tenantId, 18m)));
        var handler = new GetFailedPaymentsQueryHandler(context);

        var result = await handler.Handle(new GetFailedPaymentsQuery(tenantId), CancellationToken.None);

        result.Should().ContainSingle(item => item.Status == PaymentStatus.Failed);
    }

    [Fact]
    public async Task GetCanceledPaymentsQueryHandler_Handle_ReturnsCanceledPayments()
    {
        var payment = Payment.Create(Guid.NewGuid(), 10m, "USD", Guid.NewGuid().ToString());
        payment.Cancel("duplicate");
        await using var context = CreateContext(seed => seed.Set<Payment>().Add(payment));
        var handler = new GetCanceledPaymentsQueryHandler(context);

        var result = await handler.Handle(new GetCanceledPaymentsQuery(CancellationReason: "duplicate"), CancellationToken.None);

        result.Should().ContainSingle(item => item.Status == PaymentStatus.Cancelled);
    }

    [Fact]
    public async Task GetAllPaymentsQueryHandler_Handle_ReturnsTenantPayments()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(seed =>
        {
            seed.Set<Payment>().Add(CreateSucceededPayment(tenantId, 40m));
            seed.Set<Payment>().Add(CreateSucceededPayment(Guid.NewGuid(), 55m));
        });
        var handler = new GetAllPaymentsQueryHandler(context);

        var result = await handler.Handle(new GetAllPaymentsQuery(tenantId), CancellationToken.None);

        result.Should().ContainSingle();
        result.Single().Amount!.Amount.Should().Be(40m);
    }

    [Fact]
    public async Task GetTaxRuleByIdHandler_Handle_ReturnsPersistedRule()
    {
        await using var context = CreateContext(seed =>
        {
            var jurisdiction = CreateJurisdiction("US");
            var rate = CreateRate(jurisdiction.Id, 0.05m);
            seed.Set<TaxJurisdiction>().Add(jurisdiction);
            seed.Set<TaxRate>().Add(rate);
            seed.Set<TaxRule>().Add(CreateRule(jurisdiction.Id, rate.Id));
        });
        var ruleId = context.Set<TaxRule>().Single().Id;
        var handler = new GetTaxRuleByIdHandler(context);

        var result = await handler.Handle(new GetTaxRuleByIdQuery(ruleId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.JurisdictionCode.Should().Be("US");
        result.Rate.Should().Be(0.05m);
    }

    [Fact]
    public async Task GetTaxJurisdictionByIdHandler_Handle_ReturnsPersistedJurisdiction()
    {
        await using var context = CreateContext(seed =>
        {
            var jurisdiction = CreateJurisdiction("US-CA", TaxJurisdictionType.State);
            seed.Set<TaxJurisdiction>().Add(jurisdiction);
            seed.Set<TaxRate>().Add(CreateRate(jurisdiction.Id, 0.0725m, TaxType.SalesTax));
        });
        var jurisdictionId = context.Set<TaxJurisdiction>().Single().Id;
        var handler = new GetTaxJurisdictionByIdHandler(context);

        var result = await handler.Handle(new GetTaxJurisdictionByIdQuery(jurisdictionId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Country.Should().Be("US");
        result.State.Should().Be("CA");
        result.DefaultRate.Should().Be(0.0725m);
    }

    private static PaymentsPersistenceTestDbContext CreateContext(Action<PaymentsPersistenceTestDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"payments-handler-{Guid.NewGuid()}")
            .Options;

        var context = new PaymentsPersistenceTestDbContext(options);
        seed?.Invoke(context);
        context.SaveChanges();
        return context;
    }

    private static TaxJurisdiction CreateJurisdiction(
        string code,
        TaxJurisdictionType type = TaxJurisdictionType.Country)
    {
        return new TaxJurisdiction
        {
            Code = code,
            Name = code,
            Type = type,
            IsActive = true
        };
    }

    private static TaxRate CreateRate(Guid jurisdictionId, decimal rate, TaxType taxType = TaxType.VAT)
    {
        return new TaxRate
        {
            TaxJurisdictionId = jurisdictionId,
            TaxType = taxType,
            Rate = rate,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            IsActive = true
        };
    }

    private static TaxRule CreateRule(Guid jurisdictionId, Guid taxRateId)
    {
        return new TaxRule
        {
            Name = "Default tax rule",
            TaxJurisdictionId = jurisdictionId,
            RuleType = TaxRuleType.Standard,
            Priority = 0,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            CustomerTypeFilter = CustomerType.B2C,
            DefaultTaxRateId = taxRateId
        };
    }

    private static Payment CreateSucceededPayment(Guid tenantId, decimal amount)
    {
        var payment = Payment.Create(tenantId, amount, "USD", Guid.NewGuid().ToString(), paymentMethodId: "pm_card");
        payment.MarkAsProcessing("txn_123");
        payment.MarkAsSucceeded("pi_123", "txn_123");
        return payment;
    }

    private static Payment CreateFailedPayment(Guid tenantId, decimal amount)
    {
        var payment = Payment.Create(tenantId, amount, "USD", Guid.NewGuid().ToString(), paymentMethodId: "pm_card");
        payment.MarkAsProcessing("txn_failed");
        payment.MarkAsFailed("card_declined", "card_declined");
        return payment;
    }
}

#endregion

#region Single-Dependency Handler Constructor Tests

public class WalletServiceHandlerConstructorTests
{
    private readonly Mock<IWalletService> _walletService = new();

    [Fact]
    public void AddFundsCommandHandler_CanBeConstructed()
    {
        var handler = new AddFundsCommandHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void DeductFundsCommandHandler_CanBeConstructed()
    {
        var handler = new DeductFundsCommandHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void CreateWalletCommandHandler_CanBeConstructed()
    {
        var handler = new CreateWalletCommandHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void CloseWalletHandler_CanBeConstructed()
    {
        var handler = new CloseWalletHandler(
            _walletService.Object,
            Mock.Of<IRevenueAuditService>(),
            Mock.Of<IActorContextAccessor>());
        handler.Should().NotBeNull();
    }

    [Fact]
    public void FreezeWalletHandler_CanBeConstructed()
    {
        var handler = new FreezeWalletHandler(
            _walletService.Object,
            Mock.Of<IRevenueAuditService>(),
            Mock.Of<IActorContextAccessor>());
        handler.Should().NotBeNull();
    }

    [Fact]
    public void UnfreezeWalletHandler_CanBeConstructed()
    {
        var handler = new UnfreezeWalletHandler(
            _walletService.Object,
            Mock.Of<IRevenueAuditService>(),
            Mock.Of<IActorContextAccessor>());
        handler.Should().NotBeNull();
    }

    [Fact]
    public void LockWalletCommandHandler_CanBeConstructed()
    {
        var handler = new LockWalletCommandHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void UnlockWalletCommandHandler_CanBeConstructed()
    {
        var handler = new UnlockWalletCommandHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void PatchWalletHandler_CanBeConstructed()
    {
        var handler = new PatchWalletHandler(
            _walletService.Object,
            Mock.Of<IRevenueAuditService>(),
            Mock.Of<IActorContextAccessor>());
        handler.Should().NotBeNull();
    }

    [Fact]
    public void TransferFundsCommandHandler_CanBeConstructed()
    {
        var handler = new TransferFundsCommandHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ListWalletsHandler_CanBeConstructed()
    {
        var handler = new ListWalletsHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetWalletByIdHandler_CanBeConstructed()
    {
        var handler = new GetWalletByIdHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetWalletByUserIdQueryHandler_CanBeConstructed()
    {
        var handler = new GetWalletByUserIdQueryHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetWalletBalanceQueryHandler_CanBeConstructed()
    {
        var handler = new GetWalletBalanceQueryHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetWalletAuditLogHandler_CanBeConstructed()
    {
        var handler = new GetWalletAuditLogHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetTransactionHistoryQueryHandler_CanBeConstructed()
    {
        var handler = new GetTransactionHistoryQueryHandler(_walletService.Object);
        handler.Should().NotBeNull();
    }
}

public class RevenueAuditServiceHandlerTests
{
    private readonly Mock<IRevenueAuditService> _revenueAuditService = new();

    [Fact]
    public void ReconcileLedgerCommandHandler_CanBeConstructed()
    {
        var handler = new ReconcileLedgerCommandHandler(_revenueAuditService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void CreateLedgerEntryCommandHandler_CanBeConstructed()
    {
        var handler = new CreateLedgerEntryCommandHandler(_revenueAuditService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void RecordRevenueEventCommandHandler_CanBeConstructed()
    {
        var handler = new RecordRevenueEventCommandHandler(_revenueAuditService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void RecordAuditTrailCommandHandler_CanBeConstructed()
    {
        var handler = new RecordAuditTrailCommandHandler(_revenueAuditService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetUnreconciledLedgerEntriesQueryHandler_CanBeConstructed()
    {
        var handler = new GetUnreconciledLedgerEntriesQueryHandler(_revenueAuditService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetLedgerEntriesByAccountQueryHandler_CanBeConstructed()
    {
        var handler = new GetLedgerEntriesByAccountQueryHandler(_revenueAuditService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetRevenueEventByIdQueryHandler_CanBeConstructed()
    {
        var handler = new GetRevenueEventByIdQueryHandler(_revenueAuditService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetRevenueEventsByDateRangeQueryHandler_CanBeConstructed()
    {
        var handler = new GetRevenueEventsByDateRangeQueryHandler(_revenueAuditService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetRevenueEventsByReferenceIdQueryHandler_CanBeConstructed()
    {
        var handler = new GetRevenueEventsByReferenceIdQueryHandler(_revenueAuditService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetAuditTrailQueryHandler_CanBeConstructed()
    {
        var handler = new GetAuditTrailQueryHandler(_revenueAuditService.Object);
        handler.Should().NotBeNull();
    }
}

public class DisputeServiceHandlerTests
{
    private readonly Mock<IDisputeService> _disputeService = new();

    [Fact]
    public void CreateDisputeCommandHandler_CanBeConstructed()
    {
        var handler = new CreateDisputeCommandHandler(_disputeService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void UpdateDisputeStatusCommandHandler_CanBeConstructed()
    {
        var handler = new UpdateDisputeStatusCommandHandler(_disputeService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ResolveDisputeCommandHandler_CanBeConstructed()
    {
        var handler = new ResolveDisputeCommandHandler(_disputeService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void CancelDisputeCommandHandler_CanBeConstructed()
    {
        var handler = new CancelDisputeCommandHandler(_disputeService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void AddDisputeEvidenceCommandHandler_CanBeConstructed()
    {
        var handler = new AddDisputeEvidenceCommandHandler(_disputeService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetDisputeByIdQueryHandler_CanBeConstructed()
    {
        var handler = new GetDisputeByIdQueryHandler(_disputeService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetDisputesByPaymentIdQueryHandler_CanBeConstructed()
    {
        var handler = new GetDisputesByPaymentIdQueryHandler(_disputeService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetDisputesByUserIdQueryHandler_CanBeConstructed()
    {
        var handler = new GetDisputesByUserIdQueryHandler(_disputeService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetDisputesByStatusQueryHandler_CanBeConstructed()
    {
        var handler = new GetDisputesByStatusQueryHandler(_disputeService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetDisputeEvidenceQueryHandler_CanBeConstructed()
    {
        var handler = new GetDisputeEvidenceQueryHandler(_disputeService.Object);
        handler.Should().NotBeNull();
    }
}

public class TaxCalculationHandlerTests
{
    private readonly Mock<ITaxCalculationService> _taxService = new();

    [Fact]
    public void CalculateTaxCommandHandler_CanBeConstructed()
    {
        var handler = new CalculateTaxCommandHandler(_taxService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetTaxJurisdictionsQueryHandler_CanBeConstructed()
    {
        var handler = new GetTaxJurisdictionsQueryHandler(_taxService.Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void GetApplicableTaxRulesQueryHandler_CanBeConstructed()
    {
        var handler = new GetApplicableTaxRulesQueryHandler(_taxService.Object);
        handler.Should().NotBeNull();
    }
}

public class PaymentCommandHandlerConstructorTests
{
    [Fact]
    public void ProcessPaymentCommandHandler_CanBeConstructed()
    {
        var handler = new ProcessPaymentCommandHandler(
            new Mock<IPaymentRepository>().Object,
            new Mock<IPaymentGateway>().Object,
            new Mock<IPaymentSubscriptionSyncService>().Object,
            new Mock<ISubscriptionPaymentContextService>().Object,
            Options.Create(new PaymentRetryOptions()),
            NullLogger<ProcessPaymentCommandHandler>.Instance);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void RetryPaymentCommandHandler_CanBeConstructed()
    {
        var handler = new RetryPaymentCommandHandler(
            new Mock<IPaymentRepository>().Object,
            new Mock<IPaymentGateway>().Object,
            new Mock<IPaymentSubscriptionSyncService>().Object,
            Options.Create(new PaymentRetryOptions()),
            new Mock<IRevenueAuditService>().Object,
            new Mock<IActorContextAccessor>().Object,
            NullLogger<RetryPaymentCommandHandler>.Instance);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void CancelPaymentCommandHandler_CanBeConstructed()
    {
        var handler = new CancelPaymentCommandHandler(
            new Mock<IPaymentRepository>().Object,
            new Mock<IPaymentGateway>().Object,
            NullLogger<CancelPaymentCommandHandler>.Instance);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void ProcessRefundCommandHandler_CanBeConstructed()
    {
        var handler = new ProcessRefundCommandHandler(
            new Mock<IPaymentRepository>().Object,
            new Mock<IPaymentGateway>().Object,
            new Mock<IRevenueAuditService>().Object,
            new Mock<IActorContextAccessor>().Object,
            NullLogger<ProcessRefundCommandHandler>.Instance);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void UpdatePaymentStatusCommandHandler_CanBeConstructed()
    {
        var handler = new UpdatePaymentStatusCommandHandler(
            new Mock<IPaymentRepository>().Object,
            new Mock<IPaymentSubscriptionSyncService>().Object,
            Options.Create(new PaymentRetryOptions()),
            NullLogger<UpdatePaymentStatusCommandHandler>.Instance);
        handler.Should().NotBeNull();
    }
}

public class CalculatePricingQueryHandlerTests
{
    [Fact]
    public void CanBeConstructed()
    {
        var handler = new CalculatePricingQueryHandler(new Mock<IPlanPricingResolver>().Object);
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullResolver_Throws()
    {
        var act = () => new CalculatePricingQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("pricingResolver");
    }

    [Fact]
    public async Task Handle_PlanNotExists_Throws()
    {
        var resolver = new Mock<IPlanPricingResolver>();
        resolver.Setup(x => x.PlanExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CalculatePricingQueryHandler(resolver.Object);

        var act = () => handler.Handle(new CalculatePricingQuery(Guid.NewGuid(), null), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_NullPrice_Throws()
    {
        var resolver = new Mock<IPlanPricingResolver>();
        resolver.Setup(x => x.PlanExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        resolver.Setup(x => x.GetPlanMonthlyPriceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Money?)null);

        var handler = new CalculatePricingQueryHandler(resolver.Object);

        var act = () => handler.Handle(new CalculatePricingQuery(Guid.NewGuid(), null), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*pricing*");
    }

    [Fact]
    public async Task Handle_ValidPlan_ReturnsPricing()
    {
        var planId = Guid.NewGuid();
        var resolver = new Mock<IPlanPricingResolver>();
        resolver.Setup(x => x.PlanExistsAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        resolver.Setup(x => x.GetPlanMonthlyPriceAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Money(9.99m, "USD"));

        var handler = new CalculatePricingQueryHandler(resolver.Object);
        var result = await handler.Handle(new CalculatePricingQuery(planId, null), CancellationToken.None);

        result.Should().NotBeNull();
        result.BasePrice.Amount.Should().Be(9.99m);
        result.TotalPrice.Amount.Should().Be(9.99m);
    }

    [Fact]
    public async Task Handle_DiscountCode_AppliesDeterministicDiscount()
    {
        var planId = Guid.NewGuid();
        var resolver = new Mock<IPlanPricingResolver>();
        resolver.Setup(x => x.PlanExistsAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        resolver.Setup(x => x.GetPlanMonthlyPriceAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Money(100m, "USD"));

        var handler = new CalculatePricingQueryHandler(resolver.Object);
        var result = await handler.Handle(new CalculatePricingQuery(planId, null, "SAVE20"), CancellationToken.None);

        result.Discount.Amount.Should().Be(20m);
        result.TotalPrice.Amount.Should().Be(80m);
        result.AppliedDiscounts.Should().ContainSingle(item => item.Code == "SAVE20");
    }
}

#endregion

#region Controller Constructor Tests

public class PaymentControllerConstructorTests
{
    [Fact]
    public void PaymentsController_CanBeConstructed()
    {
        var controller = new PaymentsController(
            new Mock<ISender>().Object,
            new Mock<IActorContextAccessor>().Object,
            new Mock<IStripeCustomerService>().Object,
            new Mock<ISubscriptionPaymentContextService>().Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void WalletsController_CanBeConstructed()
    {
        var controller = new WalletsController(
            new Mock<ISender>().Object,
            new Mock<IActorContextAccessor>().Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void TaxesController_CanBeConstructed()
    {
        var controller = new TaxesController(new Mock<ISender>().Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void TaxRulesController_CanBeConstructed()
    {
        var controller = new TaxRulesController(new Mock<ISender>().Object);
        controller.Should().NotBeNull();
    }

    [Fact]
    public void TaxJurisdictionsController_CanBeConstructed()
    {
        var controller = new TaxJurisdictionsController(new Mock<ISender>().Object);
        controller.Should().NotBeNull();
    }
}

#endregion

#region Validator Tests

public class AdditionalValidatorTests
{
    [Fact]
    public void GetWalletBalanceQueryValidator_InvalidUserId_HasError()
    {
        var validator = new GetWalletBalanceQueryValidator();
        var result = validator.Validate(new GetWalletBalanceQuery(Guid.Empty));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetWalletBalanceQueryValidator_ValidUserId_NoError()
    {
        var validator = new GetWalletBalanceQueryValidator();
        var result = validator.Validate(new GetWalletBalanceQuery(Guid.NewGuid()));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetPaymentByIdQueryValidator_InvalidId_HasError()
    {
        var validator = new GetPaymentByIdQueryValidator();
        var result = validator.Validate(new GetPaymentByIdQuery(Guid.Empty));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetPaymentByIdQueryValidator_ValidId_NoError()
    {
        var validator = new GetPaymentByIdQueryValidator();
        var result = validator.Validate(new GetPaymentByIdQuery(Guid.NewGuid()));
        result.IsValid.Should().BeTrue();
    }
}

#endregion

#region Entity Constructor Tests

public class EntityConstructorTests
{
    [Fact]
    public void AuditTrail_PartialConstructor()
    {
        var entity = new AuditTrail(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void TaxRule_PartialConstructor()
    {
        var entity = new TaxRule(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void TaxRate_PartialConstructor()
    {
        var entity = new TaxRate(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void TaxJurisdiction_PartialConstructor()
    {
        var entity = new TaxJurisdiction(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void RevenueEvent_PartialConstructor()
    {
        var entity = new RevenueEvent(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void PaymentDispute_PartialConstructor()
    {
        var entity = new PaymentDispute(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void FinancialLedgerEntry_PartialConstructor()
    {
        var entity = new FinancialLedgerEntry(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void DisputeEvidence_PartialConstructor()
    {
        var entity = new DisputeEvidence(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void UserWallet_PartialConstructor()
    {
        var entity = new UserWallet(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void WalletTransaction_PartialConstructor()
    {
        var entity = new WalletTransaction(new { });
        entity.Should().NotBeNull();
    }

    [Fact]
    public void PromoStackingRule_PartialConstructor()
    {
        var entity = new PromoStackingRule(new { });
        entity.Should().NotBeNull();
    }
}

#endregion

#region DisputeService Constructor Tests

public class DisputeServiceConstructorTests
{
    [Fact]
    public void DisputeService_CanBeConstructed()
    {
        var service = new DisputeService(
            new Mock<IApplicationDbContext>().Object,
            NullLogger<DisputeService>.Instance);
        service.Should().NotBeNull();
    }
}

#endregion

#region DisputeService Persistence Tests

public class DisputeServicePersistenceTests
{
    [Fact]
    public async Task CreateDisputeAndQueryMethods_ReturnExpectedDisputes()
    {
        await using var context = WalletRepositoryPersistenceTests_CreatePersistenceDbContext();
        var service = CreateDisputeService(context);
        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var secondPaymentId = Guid.NewGuid();

        var first = await service.CreateDisputeAsync(paymentId, userId, DisputeType.Fraudulent, 100m, "fraud");
        var second = await service.CreateDisputeAsync(paymentId, userId, DisputeType.Duplicate, 50m, "duplicate");
        await service.CreateDisputeAsync(secondPaymentId, userId, DisputeType.Other, 25m, "other");

        var byId = await service.GetDisputeByIdAsync(first.Id);
        var byPayment = await service.GetDisputesByPaymentIdAsync(paymentId);
        var byUser = await service.GetDisputesByUserIdAsync(userId);
        var byStatus = await service.GetDisputesByStatusAsync(DisputeStatus.Submitted);

        byId.Should().NotBeNull();
        byId!.PaymentId.Should().Be(paymentId);
        byId.DueDate.Should().NotBeNull();
        byPayment.Should().HaveCount(2);
        byUser.Should().HaveCount(3);
        byStatus.Should().HaveCount(3);
        byPayment.Select(dispute => dispute.Id).Should().Contain(new[] { first.Id, second.Id });
    }

    [Fact]
    public async Task UpdateDisputeStatusAsync_TransitionsSupportedStatuses()
    {
        await using var context = WalletRepositoryPersistenceTests_CreatePersistenceDbContext();
        var service = CreateDisputeService(context);
        var disputeForCustomer = await service.CreateDisputeAsync(Guid.NewGuid(), Guid.NewGuid(), DisputeType.ProductNotReceived, 40m, "missing");
        var disputeForMerchant = await service.CreateDisputeAsync(Guid.NewGuid(), Guid.NewGuid(), DisputeType.ProductNotAsDescribed, 55m, "incorrect");
        var customerDueDate = SystemClock.UtcNow.AddDays(3);
        var merchantDueDate = SystemClock.UtcNow.AddDays(4);

        await service.UpdateDisputeStatusAsync(disputeForCustomer.Id, DisputeStatus.UnderReview);
        await service.UpdateDisputeStatusAsync(disputeForCustomer.Id, DisputeStatus.PendingCustomerResponse, customerDueDate);
        await service.UpdateDisputeStatusAsync(disputeForMerchant.Id, DisputeStatus.UnderReview);
        await service.UpdateDisputeStatusAsync(disputeForMerchant.Id, DisputeStatus.PendingMerchantResponse, merchantDueDate);

        var updatedCustomer = await service.GetDisputeByIdAsync(disputeForCustomer.Id);
        var updatedMerchant = await service.GetDisputeByIdAsync(disputeForMerchant.Id);

        updatedCustomer!.Status.Should().Be(DisputeStatus.PendingCustomerResponse);
        updatedCustomer.DueDate.Should().Be(customerDueDate);
        updatedMerchant!.Status.Should().Be(DisputeStatus.PendingMerchantResponse);
        updatedMerchant.DueDate.Should().Be(merchantDueDate);
    }

    [Fact]
    public async Task ResolveCancelAddEvidenceAndGetEvidence_WorkAsExpected()
    {
        await using var context = WalletRepositoryPersistenceTests_CreatePersistenceDbContext();
        var service = CreateDisputeService(context);
        var resolvedBy = Guid.NewGuid();

        var won = await service.CreateDisputeAsync(Guid.NewGuid(), Guid.NewGuid(), DisputeType.Fraudulent, 90m, "won");
        var lost = await service.CreateDisputeAsync(Guid.NewGuid(), Guid.NewGuid(), DisputeType.Duplicate, 80m, "lost");
        var partial = await service.CreateDisputeAsync(Guid.NewGuid(), Guid.NewGuid(), DisputeType.Other, 70m, "partial");
        var cancelled = await service.CreateDisputeAsync(Guid.NewGuid(), Guid.NewGuid(), DisputeType.ServiceNotProvided, 60m, "cancel");

        await service.ResolveDisputeAsync(won.Id, DisputeResolution.Won, "customer won", resolvedBy);
        await service.ResolveDisputeAsync(lost.Id, DisputeResolution.Lost, "customer lost", resolvedBy);
        await service.ResolveDisputeAsync(partial.Id, DisputeResolution.PartialRefund, "partial refund", resolvedBy);
        await service.CancelDisputeAsync(cancelled.Id, "user withdrew dispute");

        var evidence = await service.AddEvidenceAsync(
            cancelled.Id,
            EvidenceType.Receipt,
            "Receipt",
            "Original receipt",
            Guid.NewGuid(),
            isFromMerchant: true,
            fileUrl: "https://example.test/receipt.pdf",
            fileName: "receipt.pdf",
            fileSize: 1024,
            mimeType: "application/pdf");

        var wonDispute = await service.GetDisputeByIdAsync(won.Id);
        var lostDispute = await service.GetDisputeByIdAsync(lost.Id);
        var partialDispute = await service.GetDisputeByIdAsync(partial.Id);
        var cancelledDispute = await service.GetDisputeByIdAsync(cancelled.Id);
        var evidenceList = await service.GetDisputeEvidenceAsync(cancelled.Id);

        wonDispute!.Status.Should().Be(DisputeStatus.Won);
        wonDispute.Resolution.Should().Be(DisputeResolution.Won);
        lostDispute!.Status.Should().Be(DisputeStatus.Lost);
        lostDispute.Resolution.Should().Be(DisputeResolution.Lost);
        partialDispute!.Status.Should().Be(DisputeStatus.Resolved);
        partialDispute.Resolution.Should().Be(DisputeResolution.PartialRefund);
        cancelledDispute!.Status.Should().Be(DisputeStatus.Cancelled);
        cancelledDispute.ResolutionNotes.Should().Be("user withdrew dispute");
        evidence.DisputeId.Should().Be(cancelled.Id);
        evidenceList.Should().ContainSingle();
        evidenceList[0].Title.Should().Be("Receipt");
    }

    private static PaymentsPersistenceTestDbContext WalletRepositoryPersistenceTests_CreatePersistenceDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"payments-disputes-{Guid.NewGuid()}")
            .Options;

        return new PaymentsPersistenceTestDbContext(options);
    }

    private static DisputeService CreateDisputeService(PaymentsPersistenceTestDbContext context)
    {
        return new DisputeService(context, NullLogger<DisputeService>.Instance);
    }
}

#endregion

#region WalletRepository Constructor Tests

public class WalletRepositoryConstructorTests
{
    [Fact]
    public void WalletRepository_CanBeConstructed()
    {
        var repository = new WalletRepository(
            new Mock<IApplicationDbContext>().Object,
            NullLogger<WalletRepository>.Instance);
        repository.Should().NotBeNull();
    }
}

#endregion

#region WalletRepository Persistence Tests

public class WalletRepositoryPersistenceTests
{
    [Fact]
    public async Task GetByIdAsync_ReturnsWalletWithTransactions()
    {
        var walletId = Guid.NewGuid();

        await using var context = CreatePersistenceDbContext(db =>
        {
            db.Add(CreateWallet(walletId, Guid.NewGuid(), "USD", 150m, isLocked: false, createdAt: SystemClock.UtcNow.AddMinutes(-10),
                CreateWalletTransaction(walletId, WalletTransactionType.Credit, TransactionStatus.Completed, 150m, SystemClock.UtcNow.AddMinutes(-9))));
        });

        var repository = CreateWalletRepository(context);

        var result = await repository.GetByIdAsync(walletId);

        result.Should().NotBeNull();
        result!.Transactions.Should().ContainSingle();
    }

    [Fact]
    public async Task UserWalletQueries_ReturnExpectedWallets()
    {
        var userId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var usdWalletId = Guid.NewGuid();
        var eurWalletId = Guid.NewGuid();

        await using var context = CreatePersistenceDbContext(db =>
        {
            db.Add(CreateWallet(usdWalletId, userId, "USD", 100m, false, SystemClock.UtcNow.AddMinutes(-5)));
            db.Add(CreateWallet(eurWalletId, userId, "EUR", 200m, false, SystemClock.UtcNow.AddMinutes(-4)));
            db.Add(CreateWallet(Guid.NewGuid(), secondUserId, "USD", 300m, false, SystemClock.UtcNow.AddMinutes(-3)));
        });

        var repository = CreateWalletRepository(context);

        var byUser = await repository.GetByUserIdAsync(userId);
        var allByUser = (await repository.GetAllByUserIdAsync(userId)).ToList();
        var byUserAndCurrency = await repository.GetByUserIdAndCurrencyAsync(userId, "EUR");

        byUser.Should().NotBeNull();
        allByUser.Should().HaveCount(2);
        byUserAndCurrency.Should().NotBeNull();
        byUserAndCurrency!.Id.Should().Be(eurWalletId);
    }

    [Fact]
    public async Task GetTransactionsAsync_AppliesFiltersAndCount()
    {
        var walletId = Guid.NewGuid();

        await using var context = CreatePersistenceDbContext(db =>
        {
            db.Add(CreateWallet(walletId, Guid.NewGuid(), "USD", 250m, false, SystemClock.UtcNow.AddMinutes(-10)));
            db.Add(CreateWalletTransaction(walletId, WalletTransactionType.Credit, TransactionStatus.Completed, 100m, SystemClock.UtcNow.AddMinutes(-3)));
            db.Add(CreateWalletTransaction(walletId, WalletTransactionType.Debit, TransactionStatus.Pending, 50m, SystemClock.UtcNow.AddMinutes(-2)));
            db.Add(CreateWalletTransaction(walletId, WalletTransactionType.Debit, TransactionStatus.Completed, 25m, SystemClock.UtcNow.AddMinutes(-1), deletedAt: SystemClock.UtcNow));
        });

        var repository = CreateWalletRepository(context);

        var filtered = await repository.GetTransactionsAsync(walletId, typeFilter: WalletTransactionType.Debit, statusFilter: TransactionStatus.Pending);
        var count = await repository.GetTransactionCountAsync(walletId);

        filtered.Should().ContainSingle();
        filtered[0].Status.Should().Be(TransactionStatus.Pending);
        count.Should().Be(2);
    }

    [Fact]
    public async Task AddUpdateAndSaveChangesAsync_PersistsWallet()
    {
        var wallet = CreateWallet(Guid.NewGuid(), Guid.NewGuid(), "USD", 25m, false, SystemClock.UtcNow.AddMinutes(-1));

        await using var context = CreatePersistenceDbContext();
        var repository = CreateWalletRepository(context);

        repository.Add(wallet);
        await repository.SaveChangesAsync();

        wallet.IsLocked = true;
        repository.Update(wallet);
        await repository.SaveChangesAsync();

        var persisted = await context.Set<UserWallet>().SingleAsync();
        persisted.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task ListWalletsAsync_FiltersByCurrencyAndFrozenStatus()
    {
        await using var context = CreatePersistenceDbContext(db =>
        {
            db.Add(CreateWallet(Guid.NewGuid(), Guid.NewGuid(), "USD", 100m, false, SystemClock.UtcNow.AddMinutes(-5)));
            db.Add(CreateWallet(Guid.NewGuid(), Guid.NewGuid(), "USD", 200m, true, SystemClock.UtcNow.AddMinutes(-4)));
            db.Add(CreateWallet(Guid.NewGuid(), Guid.NewGuid(), "EUR", 300m, true, SystemClock.UtcNow.AddMinutes(-3)));
            db.Add(CreateWallet(Guid.NewGuid(), Guid.NewGuid(), "USD", 400m, true, SystemClock.UtcNow.AddMinutes(-2), isActive: false));
        });

        var repository = CreateWalletRepository(context);

        var (wallets, totalCount) = await repository.ListWalletsAsync(1, 10, currency: "USD", isFrozen: true);

        totalCount.Should().Be(1);
        wallets.Should().ContainSingle();
        wallets[0].Currency.Should().Be("USD");
        wallets[0].IsLocked.Should().BeTrue();
    }

    private static WalletRepository CreateWalletRepository(PaymentsPersistenceTestDbContext context)
    {
        return new WalletRepository(context, NullLogger<WalletRepository>.Instance);
    }

    private static PaymentsPersistenceTestDbContext CreatePersistenceDbContext(Action<PaymentsPersistenceTestDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"payments-persistence-{Guid.NewGuid()}")
            .Options;

        var context = new PaymentsPersistenceTestDbContext(options);
        seed?.Invoke(context);
        context.SaveChanges();
        return context;
    }

    private static UserWallet CreateWallet(
        Guid walletId,
        Guid userId,
        string currency,
        decimal balance,
        bool isLocked,
        DateTime createdAt,
        params WalletTransaction[] transactions)
    {
        return new UserWallet
        {
            Id = walletId,
            UserId = userId,
            Currency = currency,
            Balance = balance,
            IsActive = true,
            IsLocked = isLocked,
            CreatedAt = createdAt,
            Transactions = transactions.ToList()
        };
    }

    private static UserWallet CreateWallet(
        Guid walletId,
        Guid userId,
        string currency,
        decimal balance,
        bool isLocked,
        DateTime createdAt,
        bool isActive)
    {
        return new UserWallet
        {
            Id = walletId,
            UserId = userId,
            Currency = currency,
            Balance = balance,
            IsActive = isActive,
            IsLocked = isLocked,
            CreatedAt = createdAt
        };
    }

    private static WalletTransaction CreateWalletTransaction(
        Guid walletId,
        WalletTransactionType type,
        TransactionStatus status,
        decimal amount,
        DateTime createdAt,
        DateTime? deletedAt = null)
    {
        return new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            Type = type,
            Status = status,
            Amount = amount,
            BalanceAfter = amount,
            Description = $"{type} transaction",
            CreatedAt = createdAt,
            DeletedAt = deletedAt
        };
    }
}

#endregion

#region Record/DTO Tests

public class RecordAndDtoTests
{
    [Fact]
    public void PatchTaxJurisdictionRequest_CanBeCreated()
    {
        var request = new PatchTaxJurisdictionRequest("Updated Name", "Sales", 5.5m, true);
        request.Name.Should().Be("Updated Name");
        request.TaxType.Should().Be("Sales");
        request.DefaultRate.Should().Be(5.5m);
        request.IsActive.Should().BeTrue();
    }

    [Fact]
    public void PatchTaxJurisdictionRequest_DefaultValues()
    {
        var request = new PatchTaxJurisdictionRequest();
        request.Name.Should().BeNull();
        request.TaxType.Should().BeNull();
        request.DefaultRate.Should().BeNull();
        request.IsActive.Should().BeNull();
    }

    [Fact]
    public void TaxJurisdictionDto_CanBeCreated()
    {
        var dto = new TaxJurisdictionDto(Guid.NewGuid(), "US", "United States", "US", null, "VAT", 10m, true);
        dto.Code.Should().Be("US");
        dto.Country.Should().Be("US");
        dto.State.Should().BeNull();
    }

    [Fact]
    public void CreateTaxJurisdictionRequest_CanBeCreated()
    {
        var request = new CreateTaxJurisdictionRequest("US-CA", "California", "US", "CA", "Sales", 7.25m);
        request.Code.Should().Be("US-CA");
        request.State.Should().Be("CA");
    }

    [Fact]
    public void StripeGatewayOptions_DefaultValues()
    {
        var options = new StripeGatewayOptions();
        options.IsEnabled.Should().BeFalse();
        options.UseSimulation.Should().BeFalse();
    }
}

#endregion
