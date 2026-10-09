using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.Commerce.Payments;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests;

[Collection(StripeProcessConfigurationCollection.Name)]
public sealed class PaymentsCoverageCompletionTests
{
    [Theory]
    [InlineData(" PERCENT25 ", 25, DiscountType.Percentage)]
    [InlineData("FIXED5", 5, DiscountType.FixedAmount)]
    [InlineData("OFF5", 5, DiscountType.FixedAmount)]
    public async Task CalculatePricing_ShouldApplyRemainingDiscountFormats(string code, decimal expectedDiscount, DiscountType type)
    {
        var handler = CreatePricingHandler(100m);

        var result = await handler.Handle(new CalculatePricingQuery(Guid.NewGuid(), null, code), CancellationToken.None);

        result.Discount.Amount.Should().Be(expectedDiscount);
        result.AppliedDiscounts.Should().ContainSingle(discount => discount.Type == type);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("SAVE0")]
    [InlineData("SAVE101")]
    [InlineData("FIXED0")]
    [InlineData("UNKNOWN10")]
    public async Task CalculatePricing_ShouldRejectInvalidDiscountFormats(string code)
    {
        var handler = CreatePricingHandler(100m);

        var act = () => handler.Handle(new CalculatePricingQuery(Guid.NewGuid(), null, code), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CalculatePricing_FixedDiscount_ShouldCapAtBasePriceAndHandleZeroBasePercentage()
    {
        var handler = CreatePricingHandler(0m);

        var result = await handler.Handle(new CalculatePricingQuery(Guid.NewGuid(), null, "FIXED5"), CancellationToken.None);

        result.Discount.Amount.Should().Be(0m);
        result.AppliedDiscounts.Single().Percentage.Should().Be(0m);
    }

    [Fact]
    public async Task StripePaymentService_ShouldValidateRealWebhookSignatureBranches()
    {
        var service = new StripePaymentService(
            Options.Create(new StripeGatewayOptions
            {
                UseSimulation = false,
                ApiKey = "sk_test_coverage",
                WebhookToleranceSeconds = 300
            }),
            NullLogger<StripePaymentService>.Instance);

        (await service.ValidateWebhookSignatureAsync("", "sig", "secret")).Should().BeFalse();
        (await service.ValidateWebhookSignatureAsync("{}", "bad-signature", "whsec_test")).Should().BeFalse();

        var payload = $$"""
            {
              "id": "evt_coverage",
              "object": "event",
              "api_version": "{{Stripe.StripeConfiguration.ApiVersion}}",
              "created": 1760000000,
              "data": {
                "object": {
                  "id": "pi_coverage",
                  "object": "payment_intent"
                }
              },
              "livemode": false,
              "pending_webhooks": 0,
              "request": {
                "id": null,
                "idempotency_key": null
              },
              "type": "payment_intent.succeeded"
            }
            """;
        var secret = "whsec_coverage";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = CreateStripeSignature(payload, secret, timestamp);

        (await service.ValidateWebhookSignatureAsync(payload, signature, secret)).Should().BeTrue();

        var invalidJsonSignature = CreateStripeSignature("not-json", secret, timestamp);
        (await service.ValidateWebhookSignatureAsync("not-json", invalidJsonSignature, secret)).Should().BeFalse();
    }

    [Fact]
    public void StripePaymentGateway_ShouldOnlySetApiKeyForRealConfiguredGateway()
    {
        var originalApiKey = Stripe.StripeConfiguration.ApiKey;
        try
        {
            StripePaymentGateway.EnsureApiKey(new StripeGatewayOptions { UseSimulation = true, ApiKey = "sk_ignored" });
            Stripe.StripeConfiguration.ApiKey.Should().Be(originalApiKey);
            StripePaymentGateway.EnsureApiKey(new StripeGatewayOptions { UseSimulation = false, ApiKey = string.Empty });
            Stripe.StripeConfiguration.ApiKey.Should().Be(originalApiKey);
            StripePaymentGateway.EnsureApiKey(new StripeGatewayOptions { UseSimulation = false, ApiKey = "sk_test_set" });
            Stripe.StripeConfiguration.ApiKey.Should().Be("sk_test_set");
        }
        finally
        {
            Stripe.StripeConfiguration.ApiKey = originalApiKey;
        }
    }

    [Fact]
    public void PaymentQueryMapper_ShouldCoverMetadataAndFallbackBranches()
    {
        var userId = Guid.NewGuid();
        PaymentQueryMapper.TryGetUserId(null).Should().BeNull();
        PaymentQueryMapper.TryGetUserId("not json").Should().BeNull();
        PaymentQueryMapper.TryGetUserId("""{"userId":"not-a-guid"}""").Should().BeNull();
        PaymentQueryMapper.TryGetUserId($$"""{"customerUserId":"{{userId}}"}""").Should().Be(userId);
        PaymentQueryMapper.TryGetUserId($$"""{"CustomerUserId":"{{userId}}"}""").Should().Be(userId);

        var payment = CreatePayment();
        Set(payment, nameof(Payment.Metadata), $$"""{"UserId":"{{userId}}"}""");
        Set(payment, nameof(Payment.ExternalPaymentId), "external-payment");
        Set(payment, nameof(Payment.ExternalTransactionId), "external-transaction");
        Set(payment, nameof(Payment.PaymentMethodId), null);
        Set(payment, nameof(Payment.Description), null);
        Set(payment, nameof(Payment.RefundedAt), DateTime.UtcNow);
        Set(payment, nameof(Payment.ProcessedAt), null);
        Set(payment, nameof(Payment.Status), PaymentStatus.Cancelled);
        Set(payment, nameof(Payment.CancellationReason), "requested");

        PaymentQueryMapper.ToHistoryResult(payment).UserId.Should().Be(userId);
        var result = PaymentQueryMapper.ToResult(payment);
        result.PaymentId.Should().Be("external-payment");
        result.FailureReason.Should().Be("requested");

        Set(payment, nameof(Payment.ExternalPaymentId), null);
        Set(payment, nameof(Payment.ExternalTransactionId), null);
        Set(payment, nameof(Payment.CancelledAt), DateTime.UtcNow);
        Set(payment, nameof(Payment.RefundedAt), null);
        PaymentQueryMapper.ToResult(payment).PaymentId.Should().Be(payment.Id.ToString("D"));
        PaymentQueryMapper.ToHistoryResult(payment).TransactionReference.Should().Be(payment.IdempotencyKey);

        var fallbackPayment = CreatePayment();
        Set(fallbackPayment, nameof(Payment.Metadata), null);
        Set(fallbackPayment, nameof(Payment.ProcessedAt), null);
        Set(fallbackPayment, nameof(Payment.RefundedAt), null);
        Set(fallbackPayment, nameof(Payment.CancelledAt), null);
        PaymentQueryMapper.ToResult(fallbackPayment).ProcessedAt.Should().Be(fallbackPayment.UpdatedAt);
        PaymentQueryMapper.ToHistoryResult(fallbackPayment).UserId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void TaxProjectionMapper_ShouldCoverFallbackAndValidationBranches()
    {
        TaxProjectionMapper.ParseTaxType("VAT").Should().Be(TaxType.VAT);
        TaxProjectionMapper.ParseTaxType("bad").Should().Be(TaxType.Other);
        TaxProjectionMapper.ParseCustomerType("B2B").Should().Be(CustomerType.B2B);
        TaxProjectionMapper.ParseCustomerType("bad").Should().BeNull();
        TaxProjectionMapper.NormalizeRate(19m).Should().Be(0.19m);
        TaxProjectionMapper.NormalizeRate(0.123456m).Should().Be(0.1235m);
        FluentActions.Invoking(() => TaxProjectionMapper.NormalizeRate(-1m)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => TaxProjectionMapper.NormalizeRate(101m)).Should().Throw<ArgumentOutOfRangeException>();
        TaxProjectionMapper.SerializeProductCategory(null).Should().BeNull();
        TaxProjectionMapper.SerializeProductCategory("  services  ").Should().Contain("services");

        var jurisdiction = new TaxJurisdiction { Id = Guid.NewGuid(), Code = "US-CA", Name = "California", IsActive = true };
        var rate = new TaxRate
        {
            Id = Guid.NewGuid(),
            Rate = 0.0825m,
            TaxType = TaxType.SalesTax,
            ProductCategory = "software",
            EffectiveFrom = new DateTime(2026, 1, 1),
            EffectiveTo = new DateTime(2026, 12, 31)
        };

        TaxProjectionMapper.ToJurisdictionDto(jurisdiction, rate).State.Should().Be("CA");
        TaxProjectionMapper.ToJurisdictionDto(new TaxJurisdiction { Code = "DE", Name = "Germany" }, null).State.Should().BeNull();
        TaxProjectionMapper.ToJurisdictionDto(new TaxJurisdiction { Code = "-", Name = "Fallback" }, null).State.Should().BeNull();
        TaxProjectionMapper.ToJurisdictionDto(new TaxJurisdiction { Code = string.Empty, Name = "Empty" }, null).Country.Should().BeEmpty();

        var ruleWithRate = new TaxRule
        {
            Id = Guid.NewGuid(),
            TaxJurisdiction = jurisdiction,
            DefaultTaxRate = rate,
            CustomerTypeFilter = CustomerType.B2B,
            Description = "rate rule",
            IsActive = true
        };

        var ruleWithCategories = new TaxRule
        {
            Id = Guid.NewGuid(),
            TaxJurisdiction = new TaxJurisdiction { Code = "DE", Name = "Germany" },
            ProductCategories = """["books"]""",
            Description = "category rule",
            EffectiveFrom = new DateTime(2026, 2, 1),
            IsActive = false
        };

        var ruleWithMalformedCategories = new TaxRule
        {
            Id = Guid.NewGuid(),
            TaxJurisdiction = new TaxJurisdiction { Code = "GB", Name = "United Kingdom" },
            ProductCategories = "legacy-category",
            Description = "legacy rule",
            IsActive = true
        };
        var ruleWithOwnEndDate = new TaxRule
        {
            Id = Guid.NewGuid(),
            TaxJurisdiction = jurisdiction,
            EffectiveTo = new DateTime(2026, 4, 1),
            Description = null,
            IsActive = true
        };
        var ruleWithNullCategoryList = new TaxRule
        {
            Id = Guid.NewGuid(),
            TaxJurisdiction = jurisdiction,
            ProductCategories = "null",
            DefaultTaxRate = new TaxRate
            {
                Rate = 0.05m,
                TaxType = TaxType.Other,
                EffectiveFrom = new DateTime(2026, 3, 1),
                EffectiveTo = null
            },
            IsActive = true
        };

        TaxProjectionMapper.ToRuleDto(ruleWithRate).ProductCategory.Should().Be("software");
        TaxProjectionMapper.ToRuleDto(ruleWithCategories).ProductCategory.Should().Be("books");
        TaxProjectionMapper.ToRuleDto(ruleWithMalformedCategories).ProductCategory.Should().Be("legacy-category");
        TaxProjectionMapper.ToRuleDto(ruleWithOwnEndDate).EffectiveTo.Should().Be(new DateTime(2026, 4, 1));
        TaxProjectionMapper.ToRuleDto(ruleWithNullCategoryList).EffectiveTo.Should().BeNull();
    }

    [Fact]
    public void TaxExemptionAndPromoModels_ShouldCoverRemainingBranches()
    {
        InvokePrivateStatic<bool>(typeof(ValidateTaxExemptionHandler), "IsVatFormatValid", "DE12345678", "DE")
            .Should().BeTrue();
        InvokePrivateStatic<bool>(typeof(ValidateTaxExemptionHandler), "IsVatFormatValid", "DE123", "DE")
            .Should().BeFalse();
        InvokePrivateStatic<bool>(typeof(ValidateTaxExemptionHandler), "IsVatFormatValid", "FR12", "DE")
            .Should().BeFalse();

        var exemption = CustomerTaxExemption.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "us-ca",
            TaxExemptionType.Reseller,
            "cert",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(1));
        exemption.MarkVerified("coverage");
        exemption.IsCurrentlyValid().Should().BeTrue();
        exemption.IsValidOn(DateTime.UtcNow.AddDays(2)).Should().BeFalse();
        var unboundedExemption = CustomerTaxExemption.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "DE",
            TaxExemptionType.Other,
            "cert-2",
            DateTime.UtcNow.AddDays(-1),
            null);
        unboundedExemption.MarkVerified("coverage");
        unboundedExemption.IsValidOn(DateTime.UtcNow).Should().BeTrue();

        var stackingRule = new PromoStackingRule
        {
            AllowedPromoCodeIds = "null",
            ExcludedPromoCodeIds = "null",
            PromoCodeTypes = "null"
        };

        stackingRule.GetAllowedPromoCodeIds().Should().BeEmpty();
        stackingRule.GetExcludedPromoCodeIds().Should().BeEmpty();
        stackingRule.GetPromoCodeTypes().Should().BeEmpty();

        LedgerAccount.Cash.GetDescription().Should().NotBeNullOrWhiteSpace();
        ((LedgerAccount)9999).GetDescription().Should().Be("9999");
        ((LedgerAccount)999).IsAsset().Should().BeFalse();
        LedgerAccount.Cash.IsAsset().Should().BeTrue();
        LedgerAccount.ProductRevenue.IsAsset().Should().BeFalse();

        var payment = CreatePayment();
        Set(payment, nameof(Payment.Status), (PaymentStatus)999);
        payment.CanTransitionTo(PaymentStatus.Succeeded).Should().BeFalse();
    }

    [Fact]
    public void TaxCalculationService_ShouldCoverPrivateTaxBranches()
    {
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Set<TaxRule>()).Returns((Microsoft.EntityFrameworkCore.DbSet<TaxRule>)null!);
        var service = new TaxCalculationService(
            context.Object,
            NullLogger<TaxCalculationService>.Instance,
            new MemoryCache(new MemoryCacheOptions()));

        typeof(TaxCalculationService).GetProperty("TaxRules", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(service)
            .Should().BeNull();

        var jurisdiction = new TaxJurisdiction { Code = "DE", Name = "Germany" };
        var inclusiveRule = new TaxRule { IsTaxInclusive = true };
        var exclusiveRule = new TaxRule { IsTaxInclusive = false };
        var rateWithDescription = new TaxRate { Rate = 0.19m, TaxType = TaxType.VAT, Description = "VAT" };
        var rateWithoutDescription = new TaxRate { Rate = 0.1m, TaxType = TaxType.SalesTax };
        var inclusiveRequest = new TaxCalculationRequest
        {
            JurisdictionCode = "DE",
            Amount = 119m,
            Currency = "EUR",
            CustomerType = CustomerType.B2C,
            TransactionDate = DateTime.UtcNow,
            IsTaxInclusive = true
        };
        var exclusiveRequest = new TaxCalculationRequest
        {
            JurisdictionCode = "DE",
            Amount = 100m,
            Currency = "USD",
            CustomerType = CustomerType.B2C,
            TransactionDate = DateTime.UtcNow,
            IsTaxInclusive = false
        };

        InvokePrivateInstance<TaxCalculationResult>(service, "CalculateTax", inclusiveRequest, jurisdiction, rateWithoutDescription, inclusiveRule)
            .TaxDescription.Should().BeEmpty();
        InvokePrivateInstance<TaxCalculationResult>(service, "CalculateTax", exclusiveRequest, jurisdiction, rateWithDescription, exclusiveRule)
            .TaxAmount.Should().Be(19m);
    }

    private static CalculatePricingQueryHandler CreatePricingHandler(decimal baseAmount)
    {
        var resolver = new Mock<IPlanPricingResolver>();
        resolver.Setup(x => x.PlanExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        resolver.Setup(x => x.GetPlanMonthlyPriceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Money(baseAmount, "USD"));
        return new CalculatePricingQueryHandler(resolver.Object);
    }

    private static Payment CreatePayment()
        => Payment.Create(Guid.NewGuid(), 25m, "USD", $"idem-{Guid.NewGuid():N}", description: "payment");

    private static string CreateStripeSignature(string payload, string secret, long timestamp)
    {
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var digest = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={timestamp},v1={digest}";
    }

    private static void Set(object instance, string propertyName, object? value)
        => instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(instance, value);

    private static T InvokePrivateStatic<T>(Type type, string methodName, params object?[] arguments)
        => (T)type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;

    private static T InvokePrivateInstance<T>(object instance, string methodName, params object?[] arguments)
        => (T)instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, arguments)!;
}
