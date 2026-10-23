using FluentAssertions;
using GameGuild.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests.Commands;

/// <summary>
/// Issue #395: pricing experiment command handlers and validators — create round-trip,
/// lifecycle transitions, outcome recording and validation of the experiment contract.
/// </summary>
public sealed class PricingExperimentCommandTests
{
    [Fact]
    public async Task CreateExperiment_PersistsDraftWithVariants()
    {
        await using var db = CreateContext();
        var repository = new PricingExperimentRepository(db);

        var created = await new CreatePricingExperimentCommandHandler(repository).Handle(
            new CreatePricingExperimentCommand(
                ProductId: null,
                Name: "Checkout price test",
                Description: "10% off treatment vs control",
                TrafficPercentage: 80,
                Variants:
                [
                    new PricingExperimentVariantRequest("control", IsControl: true, AllocationPercentage: 50),
                    new PricingExperimentVariantRequest("treatment-a", IsControl: false, AllocationPercentage: 50, DiscountPercentage: 10m)
                ]),
            CancellationToken.None);

        created.Status.Should().Be(PricingExperimentStatus.Draft);
        created.TrafficPercentage.Should().Be(80);
        created.Variants.Should().HaveCount(2);
        created.Variants.Should().ContainSingle(v => v.IsControl).Which.Name.Should().Be("control");

        var fetched = await new GetPricingExperimentByIdQueryHandler(repository).Handle(
            new GetPricingExperimentByIdQuery(created.Id), CancellationToken.None);
        fetched.Should().NotBeNull();
        fetched!.Variants.Should().HaveCount(2);
    }

    [Fact]
    public async Task TransitionCommand_StartPauseComplete_EnforcesTheLifecycle()
    {
        await using var db = CreateContext();
        var repository = new PricingExperimentRepository(db);
        var created = await CreateExperimentAsync(repository);

        var started = await new TransitionPricingExperimentCommandHandler(repository).Handle(
            new TransitionPricingExperimentCommand(created.Id, PricingExperimentTransition.Start), CancellationToken.None);
        started.Status.Should().Be(PricingExperimentStatus.Running);
        started.StartDate.Should().NotBeNull("starting stamps the enrollment start when unset");

        var paused = await new TransitionPricingExperimentCommandHandler(repository).Handle(
            new TransitionPricingExperimentCommand(created.Id, PricingExperimentTransition.Pause), CancellationToken.None);
        paused.Status.Should().Be(PricingExperimentStatus.Paused);

        var completed = await new TransitionPricingExperimentCommandHandler(repository).Handle(
            new TransitionPricingExperimentCommand(created.Id, PricingExperimentTransition.Complete), CancellationToken.None);
        completed.Status.Should().Be(PricingExperimentStatus.Completed);

        var restart = () => new TransitionPricingExperimentCommandHandler(repository).Handle(
            new TransitionPricingExperimentCommand(created.Id, PricingExperimentTransition.Start), CancellationToken.None);
        await restart.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires status Draft*");
    }

    [Fact]
    public async Task CreateValidator_RejectsInvalidExperimentContracts()
    {
        var validator = new CreatePricingExperimentCommandValidator();

        var noControl = validator.Validate(new CreatePricingExperimentCommand(
            null, "broken",
            Variants:
            [
                new PricingExperimentVariantRequest("a", IsControl: false, AllocationPercentage: 50, DiscountPercentage: 5m),
                new PricingExperimentVariantRequest("b", IsControl: false, AllocationPercentage: 50, DiscountPercentage: 10m)
            ]));
        noControl.IsValid.Should().BeFalse("exactly one control arm is required");
        noControl.Errors.Should().Contain(e => e.ErrorMessage.Contains("exactly one control"));

        var treatmentWithoutAdjustment = validator.Validate(new CreatePricingExperimentCommand(
            null, "broken",
            Variants:
            [
                new PricingExperimentVariantRequest("control", IsControl: true, AllocationPercentage: 50),
                new PricingExperimentVariantRequest("b", IsControl: false, AllocationPercentage: 50)
            ]));
        treatmentWithoutAdjustment.IsValid.Should().BeFalse("a treatment arm needs an adjustment");
        treatmentWithoutAdjustment.Errors.Should().Contain(e => e.ErrorMessage.Contains("discount percentage or a fixed price"));

        var bothAdjustments = validator.Validate(new CreatePricingExperimentCommand(
            null, "broken",
            Variants:
            [
                new PricingExperimentVariantRequest("control", IsControl: true, AllocationPercentage: 50),
                new PricingExperimentVariantRequest("b", IsControl: false, AllocationPercentage: 50, DiscountPercentage: 10m, FixedPrice: 80m)
            ]));
        bothAdjustments.IsValid.Should().BeFalse("a variant cannot pin and discount at once");

        var singleArm = validator.Validate(new CreatePricingExperimentCommand(
            null, "broken",
            Variants: [new PricingExperimentVariantRequest("control", IsControl: true, AllocationPercentage: 100)]));
        singleArm.IsValid.Should().BeFalse("at least two arms are required");

        var trafficOutOfRange = validator.Validate(new CreatePricingExperimentCommand(
            null, "broken", TrafficPercentage: 150,
            Variants:
            [
                new PricingExperimentVariantRequest("control", IsControl: true, AllocationPercentage: 50),
                new PricingExperimentVariantRequest("b", IsControl: false, AllocationPercentage: 50, DiscountPercentage: 10m)
            ]));
        trafficOutOfRange.IsValid.Should().BeFalse("traffic percentage is bounded to 100");

        var valid = validator.Validate(new CreatePricingExperimentCommand(
            null, "healthy", TrafficPercentage: 100,
            Variants:
            [
                new PricingExperimentVariantRequest("control", IsControl: true, AllocationPercentage: 50),
                new PricingExperimentVariantRequest("b", IsControl: false, AllocationPercentage: 50, DiscountPercentage: 10m)
            ]));
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task RuleValidator_EnforcesUsageBandAndSchedulingContracts()
    {
        var validator = new CreatePricingRuleCommandValidator();

        var invertedBand = validator.Validate(new CreatePricingRuleCommand(
            null, "bad-band", PricingRuleType.UsageBased,
            UsageMetricKey: "api_calls", UsageMinValue: 500m, UsageMaxValue: 100m));
        invertedBand.IsValid.Should().BeFalse();
        invertedBand.Errors.Should().Contain(e => e.ErrorMessage.Contains("Usage max value cannot be lower"));

        var scheduledWithoutDates = validator.Validate(new CreatePricingRuleCommand(
            null, "bad-schedule", PricingRuleType.Percentage, DiscountPercentage: 10m, IsScheduled: true));
        scheduledWithoutDates.IsValid.Should().BeFalse();
        scheduledWithoutDates.Errors.Should().Contain(e => e.ErrorMessage.Contains("start or end date"));

        var validUsageRule = validator.Validate(new CreatePricingRuleCommand(
            null, "good-usage", PricingRuleType.UsageBased,
            UsageMetricKey: "api_calls", UsageMinValue: 100m, UsageMaxValue: 500m,
            DiscountPercentage: 10m, StartDate: SystemClock.UtcNow, IsScheduled: true));
        validUsageRule.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task RecordOutcomeCommand_DelegatesToTheExperimentService()
    {
        await using var db = CreateContext();
        var repository = new PricingExperimentRepository(db);
        var created = await CreateExperimentAsync(repository);
        await new TransitionPricingExperimentCommandHandler(repository).Handle(
            new TransitionPricingExperimentCommand(created.Id, PricingExperimentTransition.Start), CancellationToken.None);

        var service = new PricingExperimentService(repository, new PricingRuleRepository(db));
        var handler = new RecordPricingExperimentOutcomeCommandHandler(service);

        // Find a deterministic enrolled key and record a conversion under its arm.
        string? enrolledKey = null;
        for (var i = 0; i < 1_000 && enrolledKey is null; i++)
        {
            var assignment = await service.AssignAsync(created.Id, $"user-{i}");
            if (assignment.Variant is not null)
            {
                enrolledKey = $"user-{i}";
            }
        }

        enrolledKey.Should().NotBeNull();

        var result = await handler.Handle(
            new RecordPricingExperimentOutcomeCommand(created.Id, enrolledKey!, 90m, "USD"), CancellationToken.None);
        result.Should().BeTrue();

        var analytics = await service.GetAnalyticsAsync(created.Id);
        analytics.TotalConversions.Should().Be(1);
        analytics.TotalRevenue.Should().Be(90m);
    }

    private static async Task<PricingExperimentDto> CreateExperimentAsync(IPricingExperimentRepository repository)
    {
        return await new CreatePricingExperimentCommandHandler(repository).Handle(
            new CreatePricingExperimentCommand(
                ProductId: null,
                Name: $"experiment-{Guid.NewGuid():N}".Substring(0, 12),
                Variants:
                [
                    new PricingExperimentVariantRequest("control", IsControl: true, AllocationPercentage: 50),
                    new PricingExperimentVariantRequest("treatment-a", IsControl: false, AllocationPercentage: 50, DiscountPercentage: 10m)
                ]),
            CancellationToken.None);
    }

    private static TestPricingExperimentCommandDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestPricingExperimentCommandDbContext>()
            .UseInMemoryDatabase($"PricingExperimentCommands_{Guid.NewGuid()}")
            .Options;

        return new TestPricingExperimentCommandDbContext(options);
    }

    private sealed class TestPricingExperimentCommandDbContext(DbContextOptions<TestPricingExperimentCommandDbContext> options)
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
