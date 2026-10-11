using FluentAssertions;
using GameGuild.Commerce.Products;
using Moq;
using Xunit;

namespace GameGuild.Tests.Commerce.Products.Unit.Services;

/// <summary>
/// End-to-end code normalization: create normalizes to uppercase, lookups are
/// case-insensitive, and the validator enforces the spec format
/// <c>^[A-Z0-9-]{3,50}$</c> with a 1-100% percentage range.
/// </summary>
public class PromoCodeNormalizationTests
{
    [Theory]
    [InlineData("summer-25", "SUMMER-25")]
    [InlineData("  summer25  ", "SUMMER25")]
    [InlineData("Summer-2026-x", "SUMMER-2026-X")]
    public void Normalize_TrimsAndUppercases(string input, string expected)
    {
        PromoCodeNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_NullOrWhitespace_ReturnsNull()
    {
        PromoCodeNormalizer.Normalize(null).Should().BeNull();
        PromoCodeNormalizer.Normalize("   ").Should().BeNull();
        PromoCodeNormalizer.Normalize("").Should().BeNull();
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("A-B-C-123")]
    [InlineData("SUMMER-2026")]
    [InlineData("X0123456789012345678901234567890123456789012345678")] // 50 chars
    public void IsValidFormat_AcceptsSpecFormat(string code)
    {
        PromoCodeNormalizer.IsValidFormat(PromoCodeNormalizer.Normalize(code)).Should().BeTrue();
    }

    [Theory]
    [InlineData("SUMMER_25")]   // underscores rejected
    [InlineData("AB")]          // too short
    [InlineData("abcd efgh")]   // spaces
    [InlineData("X01234567890123456789012345678901234567890123456789")] // 51 chars
    public void IsValidFormat_RejectsOutOfSpecFormat(string code)
    {
        PromoCodeNormalizer.IsValidFormat(PromoCodeNormalizer.Normalize(code)).Should().BeFalse();
    }

    [Fact]
    public void CreateValidator_AcceptsLowercaseInput_BecauseItValidatesTheNormalizedForm()
    {
        var validator = new CreatePromoCodeCommandValidator();
        var command = new CreatePromoCodeCommand(
            Code: "summer-25",
            Name: "Summer sale",
            Type: PromoCodeType.PercentageOff,
            DiscountPercentage: 25m);

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue("lowercase input is normalized before the format check");
    }

    [Theory]
    [InlineData("SUMMER_25")]
    [InlineData("SU")]
    [InlineData("WITH SPACE")]
    public void CreateValidator_RejectsInvalidFormat(string code)
    {
        var validator = new CreatePromoCodeCommandValidator();
        var command = new CreatePromoCodeCommand(
            Code: code,
            Name: "Summer sale",
            Type: PromoCodeType.PercentageOff,
            DiscountPercentage: 25m);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Code");
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.5)]
    public void CreateValidator_RejectsPercentageBelowOneOrAboveHundred(decimal percentage)
    {
        var validator = new CreatePromoCodeCommandValidator();
        var command = new CreatePromoCodeCommand(
            Code: "PCT",
            Name: "Pct",
            Type: PromoCodeType.PercentageOff,
            DiscountPercentage: percentage);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse("the spec range is 1-100");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void CreateValidator_AcceptsPercentageWithinOneToHundred(decimal percentage)
    {
        var validator = new CreatePromoCodeCommandValidator();
        var command = new CreatePromoCodeCommand(
            Code: "PCT",
            Name: "Pct",
            Type: PromoCodeType.PercentageOff,
            DiscountPercentage: percentage);

        validator.Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePromoCodeService_NormalizesBeforeUniquenessCheck()
    {
        var repository = new Mock<IPromoCodeRepository>();
        repository.Setup(r => r.CodeExistsAsync("SUMMER-25", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(r => r.AddAsync(It.IsAny<PromoCode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new PromoCodeService(repository.Object, Mock.Of<IPromoCodeUsageRecorder>());
        var promo = new PromoCode { Code = " summer-25 ", Name = "Summer", Type = PromoCodeType.PercentageOff, DiscountPercentage = 10m };

        await service.CreatePromoCodeAsync(promo);

        promo.Code.Should().Be("SUMMER-25");
        repository.Verify(r => r.CodeExistsAsync("SUMMER-25", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PricingEngine_LookupNormalizesInput()
    {
        var promo = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "SUMMER-25",
            Name = "Summer",
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = 25m,
            Currency = "USD",
            IsActive = true
        };

        var promoRepo = new Mock<IPromoCodeRepository>();
        promoRepo.Setup(r => r.GetByCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PromoCode> { promo });
        promoRepo.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);
        promoRepo.Setup(r => r.GetUsageCountAsync(promo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var ruleRepo = new Mock<IPromoStackingRuleRepository>();
        ruleRepo.Setup(r => r.GetActiveRuleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoStackingRule?)null);

        var engine = new PricingEngineService(Mock.Of<IProductRepository>(), promoRepo.Object, ruleRepo.Object, Mock.Of<IPricingRuleRepository>(), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

        // Lowercase + padded input must resolve the uppercase-stored code.
        var result = await engine.ApplyPromoCodesAsync(100m, new List<string> { "  summer-25  " });

        result.AppliedCodes.Should().ContainSingle(a => a.Code == "SUMMER-25");
        result.TotalDiscount.Should().Be(25m);
        promoRepo.Verify(r => r.GetByCodesAsync(
            It.Is<IEnumerable<string>>(codes => codes.Contains("SUMMER-25")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
