using FluentAssertions;
using GameGuild.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests.Commands;

/// <summary>
/// Issue #395: CRUD round-trip for pricing rules at the command/query handler level,
/// including volume tier replacement, activation, and not-found behavior.
/// </summary>
public sealed class PricingRuleCommandHandlerTests
{
    [Fact]
    public async Task CrudRoundTrip_CreateReadUpdateDelete()
    {
        await using var db = CreateContext();
        var repository = new PricingRuleRepository(db);
        var productId = Guid.NewGuid();

        // Create with tiers
        var created = await new CreatePricingRuleCommandHandler(repository).Handle(
            new CreatePricingRuleCommand(
                productId,
                "Volume tiers",
                PricingRuleType.TieredPricing,
                Priority: 5,
                Tiers:
                [
                    new PricingRuleTierRequest(MinQuantity: 10, MaxQuantity: null, Price: null, DiscountPercentage: 10m),
                    new PricingRuleTierRequest(MinQuantity: 50, MaxQuantity: null, Price: 75m, DiscountPercentage: null)
                ]),
            CancellationToken.None);

        created.Id.Should().NotBeEmpty();
        created.Tiers.Should().HaveCount(2);
        created.Tiers[0].DiscountPercentage.Should().Be(10m);
        created.Tiers[1].Price.Should().Be(75m);

        // Read (query handler)
        var fetched = await new GetPricingRuleByIdQueryHandler(repository).Handle(
            new GetPricingRuleByIdQuery(created.Id), CancellationToken.None);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Volume tiers");
        fetched.Tiers.Should().HaveCount(2);
        fetched.Priority.Should().Be(5);

        // Update: rename + replace tiers
        var updated = await new UpdatePricingRuleCommandHandler(repository).Handle(
            new UpdatePricingRuleCommand(
                created.Id,
                productId,
                "Volume tiers v2",
                PricingRuleType.TieredPricing,
                Priority: 7,
                Tiers: [new PricingRuleTierRequest(MinQuantity: 20, MaxQuantity: null, Price: null, DiscountPercentage: 15m)]),
            CancellationToken.None);

        updated.Name.Should().Be("Volume tiers v2");
        updated.Priority.Should().Be(7);
        updated.Tiers.Should().ContainSingle().Which.DiscountPercentage.Should().Be(15m);

        // The replaced tier must be gone from persistence
        var refetched = await repository.GetByIdAsync(created.Id, CancellationToken.None);
        refetched!.PricingTiers.Should().ContainSingle();

        // Deactivate + activate
        var deactivated = await new DeactivatePricingRuleCommandHandler(repository).Handle(
            new DeactivatePricingRuleCommand(created.Id), CancellationToken.None);
        deactivated.IsActive.Should().BeFalse();

        var activated = await new ActivatePricingRuleCommandHandler(repository).Handle(
            new ActivatePricingRuleCommand(created.Id), CancellationToken.None);
        activated.IsActive.Should().BeTrue();

        // Delete (soft)
        await new DeletePricingRuleCommandHandler(repository).Handle(
            new DeletePricingRuleCommand(created.Id), CancellationToken.None);

        (await repository.GetByIdAsync(created.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Update_MissingRule_Throws()
    {
        await using var db = CreateContext();
        var repository = new PricingRuleRepository(db);

        var act = () => new UpdatePricingRuleCommandHandler(repository).Handle(
            new UpdatePricingRuleCommand(Guid.NewGuid(), null, "x", PricingRuleType.Percentage),
            CancellationToken.None);

        await act.Should().ThrowAsync<PricingRuleNotFoundException>();
    }

    [Fact]
    public async Task Delete_MissingRule_Throws()
    {
        await using var db = CreateContext();
        var repository = new PricingRuleRepository(db);

        var act = () => new DeletePricingRuleCommandHandler(repository).Handle(
            new DeletePricingRuleCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<PricingRuleNotFoundException>();
    }

    [Fact]
    public async Task List_Queries_FilterByProductAndIncludeGlobalRules()
    {
        await using var db = CreateContext();
        var repository = new PricingRuleRepository(db);
        var productId = Guid.NewGuid();

        await new CreatePricingRuleCommandHandler(repository).Handle(
            new CreatePricingRuleCommand(productId, "scoped", PricingRuleType.Percentage), CancellationToken.None);
        await new CreatePricingRuleCommandHandler(repository).Handle(
            new CreatePricingRuleCommand(null, "global", PricingRuleType.Percentage), CancellationToken.None);
        await new CreatePricingRuleCommandHandler(repository).Handle(
            new CreatePricingRuleCommand(Guid.NewGuid(), "other product", PricingRuleType.Percentage), CancellationToken.None);

        var result = await new GetPricingRulesQueryHandler(repository).Handle(
            new GetPricingRulesQuery(ProductId: productId), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Select(r => r.Name).Should().Contain(["scoped", "global"]);
        result.Items.Select(r => r.Name).Should().NotContain("other product");
    }

    [Fact]
    public void CreateValidator_RejectsTierWithoutPriceOrPercentage()
    {
        var validator = new CreatePricingRuleCommandValidator();
        var command = new CreatePricingRuleCommand(
            null,
            "bad tiers",
            PricingRuleType.TieredPricing,
            Tiers: [new PricingRuleTierRequest(MinQuantity: 10)]);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("fixed price or a discount percentage", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateValidator_RejectsInvalidPercentageAndDates()
    {
        var validator = new CreatePricingRuleCommandValidator();
        var command = new CreatePricingRuleCommand(
            null,
            "",
            PricingRuleType.Percentage,
            DiscountPercentage: 150m,
            StartDate: SystemClock.UtcNow.AddDays(2),
            EndDate: SystemClock.UtcNow.AddDays(1));

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
        result.Errors.Should().Contain(e => e.PropertyName == "DiscountPercentage");
        result.Errors.Should().Contain(e => e.PropertyName == "EndDate");
    }

    [Fact]
    public void UpdateValidator_RejectsEmptyRuleId()
    {
        var validator = new UpdatePricingRuleCommandValidator();
        var command = new UpdatePricingRuleCommand(Guid.Empty, null, "x", PricingRuleType.Percentage);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RuleId");
    }

    private static TestCommerceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestCommerceDbContext>()
            .UseInMemoryDatabase($"PricingRuleCommands_{Guid.NewGuid()}")
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
            // optimistic-concurrency Version bumped, which soft-delete (Version > 0) relies on.
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
