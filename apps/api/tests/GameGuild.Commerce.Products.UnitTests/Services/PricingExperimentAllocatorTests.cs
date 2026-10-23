using FluentAssertions;
using GameGuild.Commerce;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests.Services;

/// <summary>
/// Issue #395: deterministic bucket allocation for pricing A/B experiments —
/// stickiness, traffic holdout, split integrity and variant price application.
/// </summary>
public sealed class PricingExperimentAllocatorTests
{
    [Fact]
    public void BucketValue_IsDeterministic_ForTheSameKeyAndExperiment()
    {
        var experimentId = Guid.NewGuid();

        var first = PricingExperimentAllocator.BucketValue(experimentId, "user-1");
        var second = PricingExperimentAllocator.BucketValue(experimentId, "user-1");

        first.Should().Be(second, "the same key must always land in the same bucket");
        first.Should().BeInRange(0m, 1m);
    }

    [Fact]
    public void BucketValue_Differs_AcrossKeysAndExperiments()
    {
        var experimentA = Guid.NewGuid();
        var experimentB = Guid.NewGuid();

        var a1 = PricingExperimentAllocator.BucketValue(experimentA, "user-1");
        var a2 = PricingExperimentAllocator.BucketValue(experimentA, "user-2");
        var b1 = PricingExperimentAllocator.BucketValue(experimentB, "user-1");

        a1.Should().NotBe(a2);
        b1.Should().NotBe(a1, "the experiment id salts the hash");
    }

    [Fact]
    public void BucketValue_RejectsEmptyKeys()
    {
        var act = () => PricingExperimentAllocator.BucketValue(Guid.NewGuid(), "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Allocate_HoldsOutEverything_AtZeroTraffic()
    {
        var (experiment, variants) = CreateExperiment(trafficPercentage: 0);

        for (var i = 0; i < 100; i++)
        {
            PricingExperimentAllocator.Allocate(experiment, variants, $"user-{i}").Should().BeNull();
        }
    }

    [Fact]
    public void Allocate_EnrollsEverything_AtFullTraffic()
    {
        var (experiment, variants) = CreateExperiment(trafficPercentage: 100);

        var heldOut = 0;
        for (var i = 0; i < 200; i++)
        {
            if (PricingExperimentAllocator.Allocate(experiment, variants, $"user-{i}") is null)
            {
                heldOut++;
            }
        }

        heldOut.Should().Be(0, "a 100% traffic share must enroll every key");
    }

    [Fact]
    public void Allocate_SplitsEnrolledKeys_RoughlyByAllocationWeight()
    {
        var (experiment, variants) = CreateExperiment(trafficPercentage: 100);

        var treatmentCount = 0;
        const int samples = 10_000;
        for (var i = 0; i < samples; i++)
        {
            if (PricingExperimentAllocator.Allocate(experiment, variants, $"user-{i}")?.IsControl == false)
            {
                treatmentCount++;
            }
        }

        // 50/50 allocation over 10k deterministic samples lands comfortably inside 45–55%.
        var treatmentShare = (decimal)treatmentCount / samples;
        treatmentShare.Should().BeInRange(0.45m, 0.55m,
            $"the deterministic split should approximate the allocation weights (actual {treatmentShare:P1})");
    }

    [Fact]
    public void Allocate_HonorsPartialTrafficHoldout()
    {
        var (experiment, variants) = CreateExperiment(trafficPercentage: 20);

        var heldOut = 0;
        const int samples = 5_000;
        for (var i = 0; i < samples; i++)
        {
            if (PricingExperimentAllocator.Allocate(experiment, variants, $"user-{i}") is null)
            {
                heldOut++;
            }
        }

        var holdoutShare = (decimal)heldOut / samples;
        holdoutShare.Should().BeInRange(0.75m, 0.85m,
            $"an 80% holdout share is expected (actual {holdoutShare:P1})");
    }

    [Fact]
    public void Allocate_ReturnsNull_WithoutVariants()
    {
        var experiment = new PricingExperiment { Id = Guid.NewGuid(), TrafficPercentage = 100 };
        PricingExperimentAllocator.Allocate(experiment, [], "user-1").Should().BeNull();
    }

    [Fact]
    public void ApplyVariant_ControlLeavesPriceUntouched()
    {
        var control = new PricingExperimentVariant { IsControl = true };

        PricingExperimentAllocator.ApplyVariant(control, 100m).Should().Be(100m);
    }

    [Fact]
    public void ApplyVariant_DiscountPercentageShavesThePrice()
    {
        var treatment = new PricingExperimentVariant { DiscountPercentage = 15m };

        PricingExperimentAllocator.ApplyVariant(treatment, 100m).Should().Be(85m);
    }

    [Fact]
    public void ApplyVariant_FixedPricePinsThePrice_NeverRaisingIt()
    {
        var cheaper = new PricingExperimentVariant { FixedPrice = 70m };
        var pricier = new PricingExperimentVariant { FixedPrice = 500m };

        PricingExperimentAllocator.ApplyVariant(cheaper, 100m).Should().Be(70m);
        PricingExperimentAllocator.ApplyVariant(pricier, 100m).Should().Be(100m,
            "a misconfigured arm must not raise the effective price");
    }

    [Fact]
    public void ApplyVariant_TreatmentWithoutAdjustment_LeavesPriceUntouched()
    {
        var treatment = new PricingExperimentVariant { IsControl = false };

        PricingExperimentAllocator.ApplyVariant(treatment, 100m).Should().Be(100m);
    }

    private static (PricingExperiment Experiment, List<PricingExperimentVariant> Variants) CreateExperiment(
        int trafficPercentage,
        int controlWeight = 50,
        int treatmentWeight = 50)
    {
        var experiment = new PricingExperiment
        {
            Id = Guid.NewGuid(),
            Name = "split-test",
            TrafficPercentage = trafficPercentage
        };

        var variants = new List<PricingExperimentVariant>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ExperimentId = experiment.Id,
                Name = "control",
                IsControl = true,
                AllocationPercentage = controlWeight
            },
            new()
            {
                Id = Guid.NewGuid(),
                ExperimentId = experiment.Id,
                Name = "treatment-a",
                IsControl = false,
                AllocationPercentage = treatmentWeight,
                DiscountPercentage = 10m
            }
        };

        return (experiment, variants);
    }
}
