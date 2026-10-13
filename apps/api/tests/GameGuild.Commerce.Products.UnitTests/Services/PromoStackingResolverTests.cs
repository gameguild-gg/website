using FluentAssertions;
using GameGuild.Commerce.Products;
using Xunit;

namespace GameGuild.Tests.Commerce.Products.Unit.Services;

/// <summary>
/// Stacking resolution matrix: priority ordering, single-code default, exclusivity
/// stop, stackable-count limits, same-type restriction, total caps, and conflict
/// strategies.
/// </summary>
public class PromoStackingResolverTests
{
    private static PromoCode Code(
        string code,
        PromoCodeType type = PromoCodeType.PercentageOff,
        decimal percentage = 0m,
        decimal amount = 0m,
        int priority = 0,
        bool exclusive = false)
    {
        return new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code,
            Type = type,
            DiscountPercentage = percentage == 0 ? null : percentage,
            DiscountAmount = amount == 0 ? null : amount,
            Currency = "USD",
            IsActive = true,
            IsExclusive = exclusive,
            StackingPriority = priority
        };
    }

    private static PromoStackingRule Rule(
        ConflictResolutionStrategy strategy = ConflictResolutionStrategy.Sequential,
        int maxStackable = 3,
        bool allowExclusive = false,
        bool allowSameType = false,
        decimal? maxTotalPercentage = null,
        decimal? maxTotalAmount = null)
    {
        return new PromoStackingRule
        {
            Id = Guid.NewGuid(),
            Name = "test-rule",
            IsActive = true,
            Priority = 50,
            MaxStackableCount = maxStackable,
            AllowExclusiveStacking = allowExclusive,
            AllowSameTypeStacking = allowSameType,
            MaxTotalDiscountPercentage = maxTotalPercentage,
            MaxTotalDiscountAmount = maxTotalAmount,
            ConflictStrategy = strategy
        };
    }

    [Fact]
    public void NoRule_SingleCodeDefault_AppliesOnlyHighestPriority()
    {
        var candidates = new List<PromoCode>
        {
            Code("LOW10", percentage: 10m, priority: 1),
            Code("HIGH5", percentage: 5m, priority: 90),
            Code("MID20", percentage: 20m, priority: 50)
        };

        var (applied, rejected, ruleName) = PromoStackingResolver.Resolve(100m, candidates, null);

        ruleName.Should().BeNull();
        applied.Should().ContainSingle(a => a.Code == "HIGH5");
        applied[0].DiscountAmount.Should().Be(5m);
        rejected.Should().HaveCount(2);
        rejected.Should().OnlyContain(r => r.Reason.Contains("single code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoRule_TieOnPriority_IsBrokenAlphabetically()
    {
        var candidates = new List<PromoCode>
        {
            Code("BETA", percentage: 10m, priority: 10),
            Code("ALPHA", percentage: 10m, priority: 10)
        };

        var (applied, _, _) = PromoStackingResolver.Resolve(100m, candidates, null);

        applied.Should().ContainSingle(a => a.Code == "ALPHA");
    }

    [Fact]
    public void SequentialRule_AppliesInPriorityOrder_PercentagesOnReducedAmount()
    {
        // Base 100: WELCOME20 (20%, priority 100) -> 80; SUMMER10 (10%, priority 50) -> 72;
        // LOYALTY5 ($5, priority 10) -> 67. Total discount 33.
        var candidates = new List<PromoCode>
        {
            Code("LOYALTY5", PromoCodeType.FixedAmountOff, amount: 5m, priority: 10),
            Code("SUMMER10", percentage: 10m, priority: 50),
            Code("WELCOME20", percentage: 20m, priority: 100)
        };

        var (applied, rejected, ruleName) = PromoStackingResolver.Resolve(100m, candidates, Rule(allowSameType: true));

        ruleName.Should().Be("test-rule");
        rejected.Should().BeEmpty();
        applied.Select(a => a.Code).Should().ContainInOrder("WELCOME20", "SUMMER10", "LOYALTY5");
        applied[0].DiscountAmount.Should().Be(20m);
        applied[1].DiscountAmount.Should().Be(8m, "10% applies to the reduced amount of 80");
        applied[2].DiscountAmount.Should().Be(5m);
        applied.Sum(a => a.DiscountAmount).Should().Be(33m);
    }

    [Fact]
    public void SequentialRule_StopsAtNonStackableExclusiveCode()
    {
        var candidates = new List<PromoCode>
        {
            Code("A-NORMAL", percentage: 10m, priority: 90),
            Code("B-EXCLUSIVE", percentage: 10m, priority: 50, exclusive: true),
            Code("C-LATER", percentage: 10m, priority: 10)
        };

        var (applied, rejected, _) = PromoStackingResolver.Resolve(100m, candidates, Rule());

        applied.Select(a => a.Code).Should().ContainSingle("A-NORMAL");
        rejected.Should().Contain(r => r.Code == "B-EXCLUSIVE" && r.Reason.Contains("non-stackable", StringComparison.OrdinalIgnoreCase));
        rejected.Should().Contain(r => r.Code == "C-LATER");
    }

    [Fact]
    public void SequentialRule_ExclusiveFirst_AppliesAlone()
    {
        var candidates = new List<PromoCode>
        {
            Code("B-EXCLUSIVE", percentage: 30m, priority: 100, exclusive: true),
            Code("A-LATER", percentage: 10m, priority: 10)
        };

        var (applied, rejected, _) = PromoStackingResolver.Resolve(100m, candidates, Rule());

        applied.Should().ContainSingle(a => a.Code == "B-EXCLUSIVE" && a.DiscountAmount == 30m);
        rejected.Should().Contain(r => r.Code == "A-LATER" && r.Reason.Contains("exclusive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SequentialRule_AllowExclusiveStacking_LetsExclusiveStack()
    {
        var candidates = new List<PromoCode>
        {
            Code("B-EXCLUSIVE", percentage: 30m, priority: 100, exclusive: true),
            Code("A-LATER", percentage: 10m, priority: 10)
        };

        var (applied, rejected, _) = PromoStackingResolver.Resolve(100m, candidates, Rule(allowExclusive: true, allowSameType: true));

        rejected.Should().BeEmpty();
        applied.Should().HaveCount(2);
        applied.Sum(a => a.DiscountAmount).Should().Be(37m, "30% off then 10% of the remaining 70");
    }

    [Fact]
    public void SequentialRule_RespectsMaxStackableCount()
    {
        var candidates = new List<PromoCode>
        {
            Code("A", percentage: 10m, priority: 30),
            Code("B", percentage: 10m, priority: 20),
            Code("C", percentage: 10m, priority: 10)
        };

        var (applied, rejected, _) = PromoStackingResolver.Resolve(100m, candidates, Rule(maxStackable: 2, allowSameType: true));

        applied.Should().HaveCount(2);
        applied.Select(a => a.Code).Should().ContainInOrder("A", "B");
        rejected.Should().Contain(r => r.Code == "C" && r.Reason.Contains("limit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SequentialRule_RejectsSameTypeStacking_WhenNotAllowed()
    {
        var candidates = new List<PromoCode>
        {
            Code("PCT-A", percentage: 10m, priority: 20),
            Code("PCT-B", percentage: 10m, priority: 10),
            Code("FIX-C", PromoCodeType.FixedAmountOff, amount: 5m, priority: 5)
        };

        var (applied, rejected, _) = PromoStackingResolver.Resolve(100m, candidates, Rule(allowSameType: false));

        applied.Select(a => a.Code).Should().BeEquivalentTo(["PCT-A", "FIX-C"]);
        rejected.Should().Contain(r => r.Code == "PCT-B" && r.Reason.Contains("same discount type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SequentialRule_AllowsSameTypeStacking_WhenAllowed()
    {
        var candidates = new List<PromoCode>
        {
            Code("PCT-A", percentage: 10m, priority: 20),
            Code("PCT-B", percentage: 10m, priority: 10)
        };

        var (applied, rejected, _) = PromoStackingResolver.Resolve(100m, candidates, Rule(allowSameType: true));

        rejected.Should().BeEmpty();
        applied.Should().HaveCount(2);
    }

    [Fact]
    public void SequentialRule_ClampsTotalToMaxTotalDiscountPercentage()
    {
        var candidates = new List<PromoCode>
        {
            Code("A", percentage: 30m, priority: 20),
            Code("B", percentage: 30m, priority: 10)
        };

        // Raw: 30 + 21 = 51; capped at 40% of 100 = 40.
        var (applied, rejected, _) = PromoStackingResolver.Resolve(100m, candidates, Rule(maxTotalPercentage: 40m, allowSameType: true));

        rejected.Should().BeEmpty();
        applied.Sum(a => a.DiscountAmount).Should().Be(40m);
        applied.Should().OnlyContain(a => a.DiscountAmount >= 0);
    }

    [Fact]
    public void SequentialRule_ClampsTotalToMaxTotalDiscountAmount()
    {
        var candidates = new List<PromoCode>
        {
            Code("A", PromoCodeType.FixedAmountOff, amount: 30m, priority: 20),
            Code("B", PromoCodeType.FixedAmountOff, amount: 25m, priority: 10)
        };

        var (applied, _, _) = PromoStackingResolver.Resolve(100m, candidates, Rule(maxTotalAmount: 40m, allowSameType: true));

        applied.Sum(a => a.DiscountAmount).Should().Be(40m);
    }

    [Fact]
    public void HighestDiscountStrategy_PicksSingleBestCode()
    {
        var candidates = new List<PromoCode>
        {
            Code("A", PromoCodeType.FixedAmountOff, amount: 5m, priority: 100),
            Code("B", percentage: 30m, priority: 10),
            Code("C", percentage: 10m, priority: 50)
        };

        var (applied, rejected, _) = PromoStackingResolver.Resolve(100m, candidates, Rule(strategy: ConflictResolutionStrategy.HighestDiscount));

        applied.Should().ContainSingle(a => a.Code == "B" && a.DiscountAmount == 30m);
        rejected.Should().HaveCount(2);
    }

    [Fact]
    public void LowestDiscountStrategy_PicksSingleSmallestCode()
    {
        var candidates = new List<PromoCode>
        {
            Code("A", PromoCodeType.FixedAmountOff, amount: 5m, priority: 100),
            Code("B", percentage: 30m, priority: 10)
        };

        var (applied, _, _) = PromoStackingResolver.Resolve(100m, candidates, Rule(strategy: ConflictResolutionStrategy.LowestDiscount));

        applied.Should().ContainSingle(a => a.Code == "A");
    }

    [Fact]
    public void FirstCodeOnlyStrategy_PicksHighestPriority()
    {
        var candidates = new List<PromoCode>
        {
            Code("A", percentage: 10m, priority: 10),
            Code("B", percentage: 50m, priority: 90)
        };

        var (applied, _, _) = PromoStackingResolver.Resolve(100m, candidates, Rule(strategy: ConflictResolutionStrategy.FirstCodeOnly));

        applied.Should().ContainSingle(a => a.Code == "B");
    }

    [Fact]
    public void LastCodeOnlyStrategy_PicksLowestPriority()
    {
        var candidates = new List<PromoCode>
        {
            Code("A", percentage: 10m, priority: 10),
            Code("B", percentage: 50m, priority: 90)
        };

        var (applied, _, _) = PromoStackingResolver.Resolve(100m, candidates, Rule(strategy: ConflictResolutionStrategy.LastCodeOnly));

        applied.Should().ContainSingle(a => a.Code == "A");
    }

    [Fact]
    public void AdditiveStrategy_ComputesEachDiscountOnOriginalAmount()
    {
        var candidates = new List<PromoCode>
        {
            Code("A", percentage: 20m, priority: 90),
            Code("B", percentage: 10m, priority: 10)
        };

        var (applied, _, _) = PromoStackingResolver.Resolve(100m, candidates, Rule(strategy: ConflictResolutionStrategy.Additive, allowSameType: true));

        applied.Sum(a => a.DiscountAmount).Should().Be(30m, "20% and 10% both computed on the original 100");
    }

    [Fact]
    public void SetPriceAndFreeMonths_CarryGrantMetadata()
    {
        var setPrice = Code("SETP", PromoCodeType.SetPrice, amount: 60m, priority: 20);
        setPrice.DiscountAmount = 60m;
        var freeMonths = Code("FREE2M", PromoCodeType.FreeMonths, priority: 10);
        freeMonths.DurationMonths = 2;

        var (applied, _, _) = PromoStackingResolver.Resolve(100m, new List<PromoCode> { setPrice, freeMonths }, Rule());

        var setPriceEntry = applied.Should().ContainSingle(a => a.Code == "SETP").Subject;
        setPriceEntry.DiscountAmount.Should().Be(40m);
        setPriceEntry.Type.Should().Be(PromoCodeType.SetPrice);

        var freeEntry = applied.Should().ContainSingle(a => a.Code == "FREE2M").Subject;
        freeEntry.FreeMonthsGranted.Should().Be(2);
        freeEntry.DiscountAmount.Should().Be(60m, "free months waive the remaining charge");
    }

    [Fact]
    public void FreeTrialExtension_CarriesTrialDaysMetadata()
    {
        var trial = Code("TRIAL14", PromoCodeType.FreeTrialExtension, priority: 10);
        trial.FreeTrialExtensionDays = 14;

        var (applied, _, _) = PromoStackingResolver.Resolve(100m, new List<PromoCode> { trial }, Rule());

        var entry = applied.Should().ContainSingle(a => a.Code == "TRIAL14").Subject;
        entry.TrialExtensionDays.Should().Be(14);
        entry.DiscountAmount.Should().Be(100m);
    }

    [Fact]
    public void EmptyCandidates_ProduceEmptyResult()
    {
        var (applied, rejected, _) = PromoStackingResolver.Resolve(100m, new List<PromoCode>(), Rule());

        applied.Should().BeEmpty();
        rejected.Should().BeEmpty();
    }
}
