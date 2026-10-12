using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Commerce.Billing;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Queries;

/// <summary>
/// Unit tests for the stored-calculation validation surface (issue #417):
/// replay + invariants over the pricing-decision ledger.
/// </summary>
public sealed class PricingCalculationValidationTests
{
    private readonly Mock<IPricingDecisionLedgerRepository> _repository = new();

    [Fact]
    public async Task Validate_UnknownCalculation_ReturnsNull()
    {
        _repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PricingDecisionLedgerEntry?)null);

        var handler = new ValidatePricingCalculationQueryHandler(_repository.Object);

        var result = await handler.Handle(new ValidatePricingCalculationQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Validate_ConsistentCalculation_IsValid()
    {
        var breakdown = PricingDecisionBreakdown.Empty("USD") with
        {
            Version = 2,
            BaseAmount = 100m,
            PromoApplications = [new PromoApplicationBreakdown("X", null, "Percentage", 20m, 20m, 1)],
            PromoDiscountTotal = 20m,
            TaxAmount = 16m,
            FinalAmount = 96m
        };

        var entry = new PricingDecisionLedgerEntry
        {
            FinalAmount = 96m,
            BreakdownJson = breakdown.ToJson()
        };
        _repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var handler = new ValidatePricingCalculationQueryHandler(_repository.Object);

        var result = await handler.Handle(new ValidatePricingCalculationQuery(Guid.NewGuid()), CancellationToken.None);

        result!.Valid.Should().BeTrue();
        result.Drifted.Should().BeFalse();
        result.RecomputedFinalAmount.Should().Be(96m);
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_DriftedFinal_FlagsDriftWithViolation()
    {
        var breakdown = PricingDecisionBreakdown.Empty("USD") with
        {
            Version = 2,
            BaseAmount = 100m,
            TaxAmount = 16m,
            FinalAmount = 200m // stored final does not follow from the parts
        };

        var entry = new PricingDecisionLedgerEntry { FinalAmount = 200m, BreakdownJson = breakdown.ToJson() };
        _repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var handler = new ValidatePricingCalculationQueryHandler(_repository.Object);

        var result = await handler.Handle(new ValidatePricingCalculationQuery(Guid.NewGuid()), CancellationToken.None);

        result!.Drifted.Should().BeTrue();
        result.Valid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("diverges"));
    }

    [Fact]
    public async Task Validate_DiscountsExceedingBase_FlagsInvariant()
    {
        var breakdown = PricingDecisionBreakdown.Empty("USD") with
        {
            Version = 1,
            BaseAmount = 50m,
            PromoApplications = [new PromoApplicationBreakdown("BIG", null, "FixedAmount", null, 80m, 1)],
            PromoDiscountTotal = 80m,
            FinalAmount = 0m
        };

        var entry = new PricingDecisionLedgerEntry { FinalAmount = 0m, BreakdownJson = breakdown.ToJson() };
        _repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var handler = new ValidatePricingCalculationQueryHandler(_repository.Object);

        var result = await handler.Handle(new ValidatePricingCalculationQuery(Guid.NewGuid()), CancellationToken.None);

        result!.Valid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("exceeds the base amount"));
    }

    [Fact]
    public async Task Validate_UnparseableBreakdown_FailsClosed()
    {
        var entry = new PricingDecisionLedgerEntry { FinalAmount = 10m, BreakdownJson = "{not-json" };
        _repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var handler = new ValidatePricingCalculationQueryHandler(_repository.Object);

        var result = await handler.Handle(new ValidatePricingCalculationQuery(Guid.NewGuid()), CancellationToken.None);

        result!.Valid.Should().BeFalse();
        result.Violations.Should().ContainSingle(v => v.Contains("could not be parsed"));
    }

    [Fact]
    public async Task Validate_PromoPartsNotSummingToTotal_FlagsRoundingDrift()
    {
        var breakdown = PricingDecisionBreakdown.Empty("USD") with
        {
            Version = 1,
            BaseAmount = 100m,
            PromoApplications =
            [
                new PromoApplicationBreakdown("A", null, "Percentage", 10m, 10m, 1),
                new PromoApplicationBreakdown("B", null, "Percentage", 10m, 25m, 2)
            ],
            PromoDiscountTotal = 30m, // parts sum to 35 — drift inside the discount itself
            FinalAmount = 70m
        };

        var entry = new PricingDecisionLedgerEntry { FinalAmount = 70m, BreakdownJson = breakdown.ToJson() };
        _repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var handler = new ValidatePricingCalculationQueryHandler(_repository.Object);

        var result = await handler.Handle(new ValidatePricingCalculationQuery(Guid.NewGuid()), CancellationToken.None);

        result!.Violations.Should().Contain(v => v.Contains("sum to"));
    }
}
