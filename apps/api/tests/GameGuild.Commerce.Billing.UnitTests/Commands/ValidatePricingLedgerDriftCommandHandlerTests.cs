using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Commerce.Billing.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Commands;

/// <summary>
/// Issue #400 drift detection: the validation command recomputes each sampled entry's
/// final amount from its own breakdown, stamps Verified/Drifted, flags tampered rows and
/// skips unparseable breakdown documents as drifted.
/// </summary>
public sealed class ValidatePricingLedgerDriftCommandHandlerTests
{
    [Fact]
    public async Task Validate_MarksConsistentEntriesVerifiedAndPersistsStamps()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var repository = new PricingDecisionLedgerRepository(context);
        context.Set<PricingDecisionLedgerEntry>().Add(ConsistentEntry("ok-1"));
        context.Set<PricingDecisionLedgerEntry>().Add(ConsistentEntry("ok-2"));
        await context.SaveChangesAsync();

        var handler = new ValidatePricingLedgerDriftCommandHandler(
            repository,
            NullLogger<ValidatePricingLedgerDriftCommandHandler>.Instance);

        var result = await handler.Handle(new ValidatePricingLedgerDriftCommand(2), CancellationToken.None);

        result.SampleSize.Should().Be(2);
        result.VerifiedCount.Should().Be(2);
        result.DriftedCount.Should().Be(0);
        result.SkippedCount.Should().Be(0);

        var reloaded = await context.Set<PricingDecisionLedgerEntry>()
            .ToListAsync();
        reloaded.Should().OnlyContain(entry => entry.DriftStatus == PricingDecisionDriftStatus.Verified);
        reloaded.Should().OnlyContain(entry => entry.RecomputedFinalAmount!.Value == 72m);
        reloaded.Should().OnlyContain(entry => entry.ValidatedAtUtc.HasValue);
    }

    [Fact]
    public async Task Validate_FlagsTamperedFinalAmountAsDrifted()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var repository = new PricingDecisionLedgerRepository(context);

        var tampered = ConsistentEntry("tampered");
        tampered.FinalAmount = 10m; // breakdown arithmetic says 72.00

        context.Set<PricingDecisionLedgerEntry>().Add(tampered);
        await context.SaveChangesAsync();

        var handler = new ValidatePricingLedgerDriftCommandHandler(
            repository,
            NullLogger<ValidatePricingLedgerDriftCommandHandler>.Instance);

        var result = await handler.Handle(new ValidatePricingLedgerDriftCommand(1), CancellationToken.None);

        result.DriftedCount.Should().Be(1);
        result.VerifiedCount.Should().Be(0);
        result.Outcomes.Should().ContainSingle().Which.Drifted.Should().BeTrue();

        var stamped = await context.Set<PricingDecisionLedgerEntry>().SingleAsync();
        stamped.DriftStatus.Should().Be(PricingDecisionDriftStatus.Drifted);
        stamped.RecomputedFinalAmount.Should().Be(72m);
        stamped.DriftReason.Should().Contain("diverges");
    }

    [Fact]
    public async Task Validate_FlagsUnparseableBreakdownAsDriftedAndSkipped()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var repository = new PricingDecisionLedgerRepository(context);

        context.Set<PricingDecisionLedgerEntry>().Add(new PricingDecisionLedgerEntry
        {
            Id = Guid.NewGuid(),
            DecisionType = PricingDecisionType.CheckoutLineItem,
            Currency = "USD",
            Quantity = 1,
            BaseAmount = 50m,
            FinalAmount = 45m,
            BreakdownJson = "{ not valid json",
            IdempotencyKey = "broken"
        });
        await context.SaveChangesAsync();

        var handler = new ValidatePricingLedgerDriftCommandHandler(
            repository,
            NullLogger<ValidatePricingLedgerDriftCommandHandler>.Instance);

        var result = await handler.Handle(new ValidatePricingLedgerDriftCommand(1), CancellationToken.None);

        result.SkippedCount.Should().Be(1);
        result.DriftedCount.Should().Be(1, "an unverifiable entry cannot be trusted");
        result.Outcomes.Should().ContainSingle().Which.Drifted.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyLedger_ReturnsEmptySummaryWithoutSaving()
    {
        var repository = new Mock<IPricingDecisionLedgerRepository>();
        repository
            .Setup(mock => mock.SampleForValidationAsync(null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PricingDecisionLedgerEntry>());

        var handler = new ValidatePricingLedgerDriftCommandHandler(
            repository.Object,
            NullLogger<ValidatePricingLedgerDriftCommandHandler>.Instance);

        var result = await handler.Handle(new ValidatePricingLedgerDriftCommand(5), CancellationToken.None);

        result.SampleSize.Should().Be(0);
        result.VerifiedCount.Should().Be(0);
        repository.Verify(
            mock => mock.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "an empty sample must not issue a save");
    }

    /// <summary>An entry whose breakdown arithmetic is internally consistent (100 − 10 rule − 18 promo = 72).</summary>
    private static PricingDecisionLedgerEntry ConsistentEntry(string idempotencyKey)
    {
        var breakdown = PricingDecisionBreakdown.Empty("USD", 1)
            with
        {
            BaseUnitPrice = 100m,
            BaseAmount = 100m,
            RuleAdjustments = [new PricingRuleAdjustment(Guid.NewGuid(), "Volume 10%", "VolumeDiscount", 10m, 10m)],
            RuleAdjustmentTotal = 10m,
            PromoApplications = [new PromoApplicationBreakdown("WELCOME", null, "PercentageOff", 20m, 18m, 1)],
            PromoDiscountTotal = 18m,
            FinalAmount = 72m
        };

        return new PricingDecisionLedgerEntry
        {
            Id = Guid.NewGuid(),
            DecisionType = PricingDecisionType.CheckoutLineItem,
            Currency = "USD",
            Quantity = 1,
            BaseAmount = 100m,
            RuleAdjustmentTotal = 10m,
            PromoDiscountTotal = 18m,
            FinalAmount = 72m,
            BreakdownJson = breakdown.ToJson(),
            AppliedPromoCodes = ",WELCOME,",
            IdempotencyKey = idempotencyKey
        };
    }
}
