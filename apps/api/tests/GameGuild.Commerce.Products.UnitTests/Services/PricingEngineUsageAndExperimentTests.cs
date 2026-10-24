using FluentAssertions;
using GameGuild.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests.Services;

/// <summary>
/// Issue #395: dynamic usage-metric rule adjustments and A/B experiment arms inside the
/// pricing engine calculation path.
/// </summary>
public sealed class PricingEngineUsageAndExperimentTests
{
    [Fact]
    public async Task UsageGatedRule_Applies_WhenMetricIsInsideTheBand()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, usageMetricKey: "api_calls", usageMin: 1_000, usageMax: 10_000, discountPercentage: 20m);

        var result = await CalculateAsync(productId, db, usageMetrics: new Dictionary<string, decimal> { ["api_calls"] = 5_000m });

        result.AppliedRuleId.Should().NotBeNull();
        result.RuleDiscount.Should().Be(20m, "20% off the 100 base price at 5,000 api calls");
        result.FinalPrice.Should().Be(80m);
    }

    [Fact]
    public async Task UsageGatedRule_IsSkipped_WhenMetricIsUnreported()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, usageMetricKey: "api_calls", usageMin: 1_000, usageMax: 10_000, discountPercentage: 20m);

        var withoutMetrics = await CalculateAsync(productId, db);
        var withOtherMetric = await CalculateAsync(productId, db,
            usageMetrics: new Dictionary<string, decimal> { ["seats"] = 40m });

        withoutMetrics.RuleDiscount.Should().Be(0m, "an unreported metric fails the gate closed");
        withoutMetrics.FinalPrice.Should().Be(100m);
        withOtherMetric.RuleDiscount.Should().Be(0m);
    }

    [Fact]
    public async Task UsageGatedRule_IsSkipped_WhenMetricIsOutsideTheBand()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, usageMetricKey: "api_calls", usageMin: 1_000, usageMax: 10_000, discountPercentage: 20m);

        var below = await CalculateAsync(productId, db, usageMetrics: new Dictionary<string, decimal> { ["api_calls"] = 999m });
        var above = await CalculateAsync(productId, db, usageMetrics: new Dictionary<string, decimal> { ["api_calls"] = 10_001m });

        below.RuleDiscount.Should().Be(0m);
        above.RuleDiscount.Should().Be(0m);
    }

    [Fact]
    public async Task UsageGatedRule_FallsThrough_ToTheNextApplicableRule()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, name: "high-usage", priority: 10,
            usageMetricKey: "api_calls", usageMin: 10_000, discountPercentage: 30m);
        await AddRuleAsync(db, productId, name: "fallback", priority: 1, discountPercentage: 5m);

        var lowUsage = await CalculateAsync(productId, db,
            usageMetrics: new Dictionary<string, decimal> { ["api_calls"] = 500m });
        var highUsage = await CalculateAsync(productId, db,
            usageMetrics: new Dictionary<string, decimal> { ["api_calls"] = 50_000m });

        lowUsage.AppliedRuleName.Should().Be("fallback", "the gated rule steps aside for lower usage");
        lowUsage.RuleDiscount.Should().Be(5m);
        highUsage.AppliedRuleName.Should().Be("high-usage");
        highUsage.RuleDiscount.Should().Be(30m);
    }

    [Fact]
    public async Task UngatedRules_KeepWorking_WhenNoUsageMetricsAreSupplied()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, name: "plain", discountPercentage: 10m);

        var result = await CalculateAsync(productId, db);

        result.RuleDiscount.Should().Be(10m);
        result.FinalPrice.Should().Be(90m);
    }

    [Fact]
    public async Task ExperimentTreatmentArm_AdjustsThePriceThroughTheEngine()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        var (experiment, treatmentKey, controlKey) = await AddRunningExperimentWithKeysAsync(db, productId);

        var engine = CreateEngine(db);
        var product = CreateProduct(productId);

        var treatment = await engine.CalculatePriceAsync(product, experimentBucketKey: treatmentKey);
        var control = await engine.CalculatePriceAsync(product, experimentBucketKey: controlKey);

        treatment.AppliedExperimentId.Should().Be(experiment.Id);
        treatment.AppliedVariantName.Should().Be("treatment-a");
        treatment.ExperimentDiscount.Should().Be(10m, "the treatment arm carries a 10% discount");
        treatment.FinalPrice.Should().Be(90m);

        control.AppliedVariantName.Should().Be("control");
        control.ExperimentDiscount.Should().Be(0m);
        control.FinalPrice.Should().Be(100m);
    }

    [Fact]
    public async Task ExperimentAssignment_IsSticky_InsideTheEngine()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        var (_, treatmentKey, _) = await AddRunningExperimentWithKeysAsync(db, productId);

        var engine = CreateEngine(db);
        var product = CreateProduct(productId);

        var first = await engine.CalculatePriceAsync(product, experimentBucketKey: treatmentKey);
        var second = await engine.CalculatePriceAsync(product, experimentBucketKey: treatmentKey);

        first.AppliedVariantName.Should().Be(second.AppliedVariantName);
        first.FinalPrice.Should().Be(second.FinalPrice);
    }

    [Fact]
    public async Task WithoutABucketKey_NoExperimentIsApplied()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRunningExperimentWithKeysAsync(db, productId);

        var result = await CalculateAsync(productId, db);

        result.AppliedExperimentId.Should().BeNull();
        result.FinalPrice.Should().Be(100m);
    }

    [Fact]
    public async Task HeldOutKey_IsPricedWithoutExperimentAdjustment()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        var (experiment, treatmentKey, controlKey) = await AddRunningExperimentWithKeysAsync(db, productId);
        experiment.TrafficPercentage = 0;
        await db.SaveChangesAsync();

        // Find both arms before disabling traffic; neither key can be allocated once held out.
        var engine = CreateEngine(db);
        foreach (var key in new[] { treatmentKey, controlKey })
        {
            var result = await engine.CalculatePriceAsync(CreateProduct(productId), experimentBucketKey: key);

            result.AppliedExperimentId.Should().BeNull();
            result.AppliedVariantName.Should().BeNull();
            result.ExperimentDiscount.Should().Be(0m);
            result.FinalPrice.Should().Be(100m);
        }

        (await db.PricingExperimentAssignments.ToListAsync()).Should().BeEmpty();
    }

    private static async Task<PricingCalculationResult> CalculateAsync(
        Guid productId,
        TestPricingExperimentEngineDbContext db,
        IReadOnlyDictionary<string, decimal>? usageMetrics = null)
    {
        var engine = CreateEngine(db);
        return await engine.CalculatePriceAsync(CreateProduct(productId), usageMetrics: usageMetrics);
    }

    private static PricingEngineService CreateEngine(TestPricingExperimentEngineDbContext db)
    {
        return new PricingEngineService(
            Mock.Of<IProductRepository>(),
            Mock.Of<IPromoCodeRepository>(),
            Mock.Of<IPromoStackingRuleRepository>(),
            new PricingRuleRepository(db),
            new PricingExperimentService(new PricingExperimentRepository(db), new PricingRuleRepository(db)),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
    }

    private static async Task<PricingRule> AddRuleAsync(
        TestPricingExperimentEngineDbContext db,
        Guid productId,
        string name = "usage-rule",
        int priority = 0,
        string? usageMetricKey = null,
        decimal? usageMin = null,
        decimal? usageMax = null,
        decimal? discountPercentage = null)
    {
        var rule = new PricingRule
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Name = name,
            RuleType = PricingRuleType.UsageBased,
            Priority = priority,
            IsActive = true,
            UsageMetricKey = usageMetricKey,
            UsageMinValue = usageMin,
            UsageMaxValue = usageMax,
            DiscountPercentage = discountPercentage
        };

        db.PricingRules.Add(rule);
        await db.SaveChangesAsync();
        return rule;
    }

    /// <summary>
    /// Creates a Running 50/50 experiment (control / treatment-a at 10% off) for the product
    /// and finds deterministic bucket keys that land in each arm.
    /// </summary>
    private static async Task<(PricingExperiment Experiment, string TreatmentKey, string ControlKey)> AddRunningExperimentWithKeysAsync(
        TestPricingExperimentEngineDbContext db,
        Guid productId)
    {
        var experiment = new PricingExperiment
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Name = "price-test",
            Status = PricingExperimentStatus.Running,
            TrafficPercentage = 100
        };

        var control = new PricingExperimentVariant
        {
            Id = Guid.NewGuid(),
            ExperimentId = experiment.Id,
            Name = "control",
            IsControl = true,
            AllocationPercentage = 50
        };
        var treatment = new PricingExperimentVariant
        {
            Id = Guid.NewGuid(),
            ExperimentId = experiment.Id,
            Name = "treatment-a",
            IsControl = false,
            AllocationPercentage = 50,
            DiscountPercentage = 10m
        };
        experiment.Variants.Add(control);
        experiment.Variants.Add(treatment);

        db.PricingExperiments.Add(experiment);
        await db.SaveChangesAsync();

        string? treatmentKey = null;
        string? controlKey = null;
        for (var i = 0; i < 10_000 && (treatmentKey is null || controlKey is null); i++)
        {
            var key = $"user-{i}";
            var allocated = PricingExperimentAllocator.Allocate(experiment, [control, treatment], key);
            if (allocated is null)
            {
                continue;
            }

            if (allocated.IsControl && controlKey is null)
            {
                controlKey = key;
            }
            else if (!allocated.IsControl && treatmentKey is null)
            {
                treatmentKey = key;
            }
        }

        treatmentKey.Should().NotBeNull("a deterministic key landing in the treatment arm must exist");
        controlKey.Should().NotBeNull("a deterministic key landing in the control arm must exist");

        return (experiment, treatmentKey!, controlKey!);
    }

    private static Product CreateProduct(Guid productId, decimal? salePrice = null)
    {
        var product = Product.Create("Usage Product", ProductType.Program);
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(product, productId);

        var (pricing, _) = ProductPricing.CreateWithVersion(
            productId,
            "Standard",
            100m,
            "USD",
            salePrice,
            salePrice.HasValue ? SystemClock.UtcNow.AddDays(-1) : null,
            salePrice.HasValue ? SystemClock.UtcNow.AddDays(1) : null,
            isDefault: true);

        product.Pricing.Add(pricing);
        return product;
    }

    private static TestPricingExperimentEngineDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestPricingExperimentEngineDbContext>()
            .UseInMemoryDatabase($"PricingEngineUsage_{Guid.NewGuid()}")
            .Options;

        return new TestPricingExperimentEngineDbContext(options);
    }

    private sealed class TestPricingExperimentEngineDbContext(DbContextOptions<TestPricingExperimentEngineDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<PricingRule> PricingRules { get; set; } = null!;

        public DbSet<PricingExperiment> PricingExperiments { get; set; } = null!;

        public DbSet<PricingExperimentVariant> PricingExperimentVariants { get; set; } = null!;

        public DbSet<PricingExperimentAssignment> PricingExperimentAssignments { get; set; } = null!;

        public DbSet<PricingExperimentOutcome> PricingExperimentOutcomes { get; set; } = null!;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is GameGuild.EntityBase<Guid> entity &&
                    entry.State is EntityState.Added or EntityState.Modified)
                {
                    entry.Property(nameof(GameGuild.EntityBase<Guid>.Version)).CurrentValue =
                        (int)entry.Property(nameof(GameGuild.EntityBase<Guid>.Version)).CurrentValue! + 1;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PricingRule>(builder =>
            {
                builder.HasKey(r => r.Id);
                builder.Property(r => r.Name).IsRequired();
            });

            modelBuilder.Entity<PricingExperiment>(builder =>
            {
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Name).IsRequired();
                builder.HasMany(e => e.Variants)
                    .WithOne(v => v.Experiment)
                    .HasForeignKey(v => v.ExperimentId);
            });

            modelBuilder.Entity<PricingExperimentVariant>(builder =>
            {
                builder.HasKey(v => v.Id);
                builder.Property(v => v.Name).IsRequired();
            });

            modelBuilder.Entity<PricingExperimentAssignment>(builder =>
            {
                builder.HasKey(a => a.Id);
                builder.Property(a => a.BucketKey).IsRequired();
                builder.HasIndex(a => new { a.ExperimentId, a.BucketKey }).IsUnique();
                builder.HasOne(a => a.Variant)
                    .WithMany()
                    .HasForeignKey(a => a.VariantId);
            });

            modelBuilder.Entity<PricingExperimentOutcome>(builder => builder.HasKey(o => o.Id));
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Mock.Of<IDbContextTransaction>());
        }
    }
}
