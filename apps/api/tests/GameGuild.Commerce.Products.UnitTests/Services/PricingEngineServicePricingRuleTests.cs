using FluentAssertions;
using GameGuild.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests.Services;

/// <summary>
/// Issue #395: pricing-rule resolution inside the pricing engine — volume tiers,
/// customer segments, priority, time windows, rule fallback, caching and invalidation.
/// </summary>
public sealed class PricingEngineServicePricingRuleTests
{
    private const string ProductName = "Rule Product";

    [Fact]
    public async Task VolumeTierPercentage_AppliesAtQualifiedQuantity()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.TieredPricing, tiers:
        [
            new PricingRuleTier { MinQuantity = 10, DiscountPercentage = 10m }
        ]);

        var result = await CalculateAsync(productId, db, quantity: 10);

        result.AppliedRuleId.Should().NotBeNull();
        result.RuleDiscount.Should().Be(10m); // 10% off the 100 base price
        result.FinalPrice.Should().Be(90m);
    }

    [Fact]
    public async Task VolumeTierPercentage_DoesNotApplyBelowTierThreshold()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.TieredPricing, tiers:
        [
            new PricingRuleTier { MinQuantity = 10, DiscountPercentage = 10m }
        ]);

        var result = await CalculateAsync(productId, db, quantity: 9);

        result.RuleDiscount.Should().Be(0m);
        result.FinalPrice.Should().Be(100m);
    }

    [Fact]
    public async Task VolumeTierFixedPrice_OverridesUnitPrice()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.TieredPricing, tiers:
        [
            new PricingRuleTier { MinQuantity = 5, Price = 80m }
        ]);

        var result = await CalculateAsync(productId, db, quantity: 6);

        result.RuleDiscount.Should().Be(20m);
        result.FinalPrice.Should().Be(80m);
    }

    [Fact]
    public async Task VolumeTierQuantityWindow_RespectsTierMaxQuantity()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.TieredPricing, tiers:
        [
            new PricingRuleTier { MinQuantity = 2, MaxQuantity = 4, DiscountPercentage = 15m },
            new PricingRuleTier { MinQuantity = 5, DiscountPercentage = 25m }
        ]);

        var small = await CalculateAsync(productId, db, quantity: 4);
        var large = await CalculateAsync(productId, db, quantity: 5);

        small.RuleDiscount.Should().Be(15m);
        large.RuleDiscount.Should().Be(25m);
    }

    [Fact]
    public async Task RuleLevelPercentage_AppliesWhenQuantityWindowMatches()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.VolumeDiscount, minQuantity: 5, discountPercentage: 10m);

        var below = await CalculateAsync(productId, db, quantity: 4);
        var at = await CalculateAsync(productId, db, quantity: 5);

        below.RuleDiscount.Should().Be(0m);
        at.RuleDiscount.Should().Be(10m);
    }

    [Fact]
    public async Task SegmentRule_AppliesOnlyToMatchingSegment()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.SegmentBased, discountPercentage: 20m, customerSegment: "vip");

        var withoutSegment = await CalculateAsync(productId, db, quantity: 1, customerSegment: null);
        var withOtherSegment = await CalculateAsync(productId, db, quantity: 1, customerSegment: "basic");
        var withMatchingSegment = await CalculateAsync(productId, db, quantity: 1, customerSegment: "VIP");

        withoutSegment.RuleDiscount.Should().Be(0m);
        withoutSegment.FinalPrice.Should().Be(100m);
        withOtherSegment.RuleDiscount.Should().Be(0m);
        withMatchingSegment.RuleDiscount.Should().Be(20m);
        withMatchingSegment.FinalPrice.Should().Be(80m);
    }

    [Fact]
    public async Task HighestPriorityRule_Wins()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.Percentage, name: "low", priority: 1, discountPercentage: 5m);
        await AddRuleAsync(db, productId, PricingRuleType.Percentage, name: "high", priority: 10, discountPercentage: 15m);

        var result = await CalculateAsync(productId, db, quantity: 1);

        result.RuleDiscount.Should().Be(15m);
        result.AppliedRuleName.Should().Be("high");
    }

    [Fact]
    public async Task ExpiredRule_IsIgnored_AndFallsBackToBasePrice()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.Percentage, discountPercentage: 30m, endDate: SystemClock.UtcNow.AddDays(-1));

        var result = await CalculateAsync(productId, db, quantity: 1);

        result.AppliedRuleId.Should().BeNull();
        result.RuleDiscount.Should().Be(0m);
        result.FinalPrice.Should().Be(100m);
    }

    [Fact]
    public async Task FutureRule_IsIgnored()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.Percentage, discountPercentage: 30m, startDate: SystemClock.UtcNow.AddDays(1));

        var result = await CalculateAsync(productId, db, quantity: 1);

        result.AppliedRuleId.Should().BeNull();
        result.FinalPrice.Should().Be(100m);
    }

    [Fact]
    public async Task NoRules_FallsBackToBaseAndActiveSale()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();

        var result = await CalculateAsync(productId, db, quantity: 1, salePrice: 80m);

        result.IsSaleActive.Should().BeTrue();
        result.FinalPrice.Should().Be(80m);
        result.RuleDiscount.Should().Be(0m);
        result.AppliedRuleId.Should().BeNull();
    }

    [Fact]
    public async Task RuleDiscount_AppliesOnTopOfActiveSale()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.Percentage, discountPercentage: 10m);

        var result = await CalculateAsync(productId, db, quantity: 1, salePrice: 80m);

        // 10% off the 80 sale price, not the 100 base price.
        result.RuleDiscount.Should().Be(8m);
        result.FinalPrice.Should().Be(72m);
    }

    [Fact]
    public async Task GlobalRule_AppliesToAnyProduct()
    {
        await using var db = CreateContext();
        var ruleProductId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        await AddRuleAsync(db, ruleProductId, PricingRuleType.Percentage, discountPercentage: 10m);
        await AddRuleAsync(db, null, PricingRuleType.Percentage, name: "global", priority: 5, discountPercentage: 20m);

        var result = await CalculateAsync(otherProductId, db, quantity: 1);

        result.AppliedRuleName.Should().Be("global");
        result.RuleDiscount.Should().Be(20m);
    }

    [Fact]
    public async Task CacheInvalidation_OnRuleUpdate()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        var rule = await AddRuleAsync(db, productId, PricingRuleType.Percentage, discountPercentage: 10m);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var engine = CreateEngine(db, cache);

        var before = await engine.CalculatePriceAsync(CreateProduct(productId), quantity: 1);
        before.FinalPrice.Should().Be(90m);

        // Update the rule to a bigger discount; the change stamp (version/updated ticks)
        // must produce a fresh cache key so the next calculation sees the new value.
        rule.DiscountPercentage = 25m;
        var repository = new PricingRuleRepository(db);
        await repository.UpdateAsync(rule);

        var after = await engine.CalculatePriceAsync(CreateProduct(productId), quantity: 1);
        after.FinalPrice.Should().Be(75m);
    }

    [Fact]
    public async Task CachedRules_RequireASingleLoad_UntilTheRuleChanges()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        var rule = await AddRuleAsync(db, productId, PricingRuleType.Percentage, discountPercentage: 10m);
        var counting = new CountingPricingRuleRepository(db);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var engine = new PricingEngineService(
            Mock.Of<IProductRepository>(),
            Mock.Of<IPromoCodeRepository>(),
            counting,
            cache);

        var first = await engine.CalculatePriceAsync(CreateProduct(productId), quantity: 1);
        first.FinalPrice.Should().Be(90m);
        counting.ActiveRuleLoads.Should().Be(1);

        // Same change stamp -> the cached rule set is reused, no extra load.
        var second = await engine.CalculatePriceAsync(CreateProduct(productId), quantity: 1);
        second.FinalPrice.Should().Be(90m);
        counting.ActiveRuleLoads.Should().Be(1, "an unchanged stamp must be served from the cache");

        // Updating the rule changes the stamp (Version/UpdatedAt) -> one extra load and the
        // new discount is applied immediately.
        rule.DiscountPercentage = 25m;
        await new PricingRuleRepository(db).UpdateAsync(rule);

        var third = await engine.CalculatePriceAsync(CreateProduct(productId), quantity: 1);
        third.FinalPrice.Should().Be(75m);
        counting.ActiveRuleLoads.Should().Be(2, "a changed stamp must trigger a fresh load");
    }

    /// <summary>
    /// Wraps the real repository and counts how often the engine loads the active rule set,
    /// so caching behavior can be asserted without relying on IMemoryCache internals.
    /// </summary>
    private sealed class CountingPricingRuleRepository(TestCommerceDbContext context)
        : IPricingRuleRepository
    {
        private readonly PricingRuleRepository _inner = new(context);

        public int ActiveRuleLoads { get; private set; }

        public Task<GameGuild.Commerce.PricingRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<GameGuild.Commerce.PricingRule>> GetActiveRulesForProductAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            ActiveRuleLoads++;
            return _inner.GetActiveRulesForProductAsync(productId, cancellationToken);
        }

        public Task<string> GetChangeStampAsync(Guid productId, CancellationToken cancellationToken = default)
            => _inner.GetChangeStampAsync(productId, cancellationToken);

        public Task<(IReadOnlyList<GameGuild.Commerce.PricingRule> Items, int TotalCount)> GetPagedAsync(
            bool? isActive = null,
            GameGuild.Commerce.PricingRuleType? ruleType = null,
            Guid? productId = null,
            string? searchTerm = null,
            int skip = 0,
            int take = 50,
            CancellationToken cancellationToken = default)
            => _inner.GetPagedAsync(isActive, ruleType, productId, searchTerm, skip, take, cancellationToken);

        public Task AddAsync(GameGuild.Commerce.PricingRule rule, CancellationToken cancellationToken = default)
            => _inner.AddAsync(rule, cancellationToken);

        public Task<GameGuild.Commerce.PricingRule> UpdateAsync(GameGuild.Commerce.PricingRule rule, CancellationToken cancellationToken = default)
            => _inner.UpdateAsync(rule, cancellationToken);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => _inner.DeleteAsync(id, cancellationToken);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _inner.SaveChangesAsync(cancellationToken);
    }

    [Fact]
    public async Task PromoCode_StacksAfterRuleDiscount()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.Percentage, discountPercentage: 10m);

        var promo = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "STACK10",
            Name = "Stack 10",
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = 10m,
            IsActive = true
        };

        var promoRepository = new Mock<IPromoCodeRepository>();
        promoRepository
            .Setup(r => r.GetByCodeAsync("STACK10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);

        var engine = new PricingEngineService(
            Mock.Of<IProductRepository>(),
            promoRepository.Object,
            new PricingRuleRepository(db),
            new MemoryCache(new MemoryCacheOptions()));

        var result = await engine.CalculatePriceAsync(
            CreateProduct(productId),
            promoCodes: ["STACK10"],
            quantity: 1);

        // 10% rule off 100 -> 90, then 10% promo off 90 -> 81.
        result.RuleDiscount.Should().Be(10m);
        result.PromoDiscount.Should().Be(9m);
        result.FinalPrice.Should().Be(81m);
    }

    [Fact]
    public async Task FixedPriceOverride_NeverRaisesThePrice()
    {
        var productId = Guid.NewGuid();
        await using var db = CreateContext();
        await AddRuleAsync(db, productId, PricingRuleType.FixedPriceOverride, fixedPrice: 500m);

        var result = await CalculateAsync(productId, db, quantity: 1);

        result.FinalPrice.Should().Be(100m, "a misconfigured override must not raise the effective price");
    }

    private static async Task<PricingRule> AddRuleAsync(
        TestCommerceDbContext db,
        Guid? productId,
        PricingRuleType ruleType,
        string name = "rule",
        int priority = 0,
        decimal? discountPercentage = null,
        decimal? fixedPrice = null,
        int? minQuantity = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? customerSegment = null,
        PricingRuleTier[]? tiers = null)
    {
        var rule = new PricingRule
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Name = name,
            RuleType = ruleType,
            Priority = priority,
            IsActive = true,
            StartDate = startDate,
            EndDate = endDate,
            MinQuantity = minQuantity,
            DiscountPercentage = discountPercentage,
            FixedPrice = fixedPrice,
            CustomerSegment = customerSegment
        };

        if (tiers is not null)
        {
            foreach (var tier in tiers)
            {
                tier.Id = Guid.NewGuid();
                tier.PricingRuleId = rule.Id;
                rule.PricingTiers.Add(tier);
            }
        }

        db.PricingRules.Add(rule);
        await db.SaveChangesAsync();
        return rule;
    }

    private static async Task<PricingCalculationResult> CalculateAsync(
        Guid productId,
        TestCommerceDbContext db,
        int quantity,
        decimal? salePrice = null,
        string? customerSegment = null)
    {
        var engine = CreateEngine(db);
        var product = CreateProduct(productId, salePrice);
        return await engine.CalculatePriceAsync(product, quantity: quantity, customerSegment: customerSegment);
    }

    private static PricingEngineService CreateEngine(TestCommerceDbContext db, IMemoryCache? cache = null)
    {
        return new PricingEngineService(
            Mock.Of<IProductRepository>(),
            Mock.Of<IPromoCodeRepository>(),
            new PricingRuleRepository(db),
            cache ?? new MemoryCache(new MemoryCacheOptions()));
    }

    private static Product CreateProduct(Guid productId, decimal? salePrice = null)
    {
        var product = Product.Create(ProductName, ProductType.Program);
        SetProductId(product, productId);

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

    private static void SetProductId(Product product, Guid productId)
    {
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(product, productId);
    }

    private static TestCommerceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestCommerceDbContext>()
            .UseInMemoryDatabase($"PricingRules_{Guid.NewGuid()}")
            .Options;

        return new TestCommerceDbContext(options);
    }

    private sealed class TestCommerceDbContext(DbContextOptions<TestCommerceDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<PricingRule> PricingRules { get; set; } = null!;

        public DbSet<PricingRuleTier> PricingRuleTiers { get; set; } = null!;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Mimic the production ApplicationDbContext: every Added/Modified entity gets its
            // optimistic-concurrency Version bumped, which the cache stamp and soft-delete rely on.
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
            modelBuilder.Entity<PricingRuleTier>(builder =>
            {
                builder.HasKey(t => t.Id);
                builder.HasOne(t => t.PricingRule)
                    .WithMany(r => r.PricingTiers)
                    .HasForeignKey(t => t.PricingRuleId);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Mock.Of<IDbContextTransaction>());
        }
    }
}
