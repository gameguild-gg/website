using FluentAssertions;
using GameGuild.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Commerce.Products.UnitTests.Services;

/// <summary>
/// Issue #395: pricing A/B experiment service — sticky assignments, conversion outcomes with
/// revenue-impact analytics, and the automated scheduling sweep for opt-in rules and
/// date-bounded experiments.
/// </summary>
public sealed class PricingExperimentServiceTests
{
    [Fact]
    public async Task AssignAsync_IsSticky_AcrossRepeatedResolutions()
    {
        await using var db = CreateContext();
        var experiment = await AddRunningExperimentAsync(db);

        var service = CreateService(db);

        var first = await service.AssignAsync(experiment.Id, "user-1");
        var second = await service.AssignAsync(experiment.Id, "user-1");

        first.Variant!.Name.Should().Be(second.Variant!.Name, "an enrolled key never changes arms");
        second.AssignmentId.Should().Be(first.AssignmentId, "the durable assignment row is reused");
    }

    [Fact]
    public async Task AssignAsync_Throws_ForMissingOrNotEnrollingExperiment()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var missing = () => service.AssignAsync(Guid.NewGuid(), "user-1");
        await missing.Should().ThrowAsync<InvalidOperationException>();

        var draft = await AddExperimentAsync(db, status: PricingExperimentStatus.Draft);
        var notEnrolling = () => service.AssignAsync(draft.Id, "user-1");
        await notEnrolling.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not enrolling*");
    }

    [Fact]
    public async Task RecordOutcomeAsync_RequiresAnAssignment_First()
    {
        await using var db = CreateContext();
        var experiment = await AddRunningExperimentAsync(db);
        var service = CreateService(db);

        var act = () => service.RecordOutcomeAsync(experiment.Id, "stranger", 50m, "USD");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not enrolled*");
    }

    [Fact]
    public async Task Analytics_ComputeRevenueImpact_VersusTheControlArm()
    {
        await using var db = CreateContext();
        var experiment = await AddRunningExperimentAsync(db);
        var service = CreateService(db);

        // Enroll deterministic keys and record one conversion per enrolled key: control keys
        // convert at 100, treatment keys at 90 (the arm's 10% discount).
        var controlAssignments = 0;
        var treatmentAssignments = 0;
        for (var i = 0; i < 200 && (controlAssignments < 10 || treatmentAssignments < 10); i++)
        {
            var assignment = await service.AssignAsync(experiment.Id, $"user-{i}");
            if (assignment.Variant is null)
            {
                continue;
            }

            if (assignment.Variant.IsControl)
            {
                controlAssignments++;
                await service.RecordOutcomeAsync(experiment.Id, $"user-{i}", 100m, "USD");
            }
            else
            {
                treatmentAssignments++;
                await service.RecordOutcomeAsync(experiment.Id, $"user-{i}", 90m, "USD");
            }
        }

        controlAssignments.Should().BeGreaterThanOrEqualTo(10);
        treatmentAssignments.Should().BeGreaterThanOrEqualTo(10);

        var analytics = await service.GetAnalyticsAsync(experiment.Id);

        analytics.TotalConversions.Should().Be(controlAssignments + treatmentAssignments);
        analytics.TotalRevenue.Should().Be(controlAssignments * 100m + treatmentAssignments * 90m);
        analytics.Currency.Should().Be("USD");

        var control = analytics.Variants.Single(v => v.IsControl);
        var treatment = analytics.Variants.Single(v => !v.IsControl);

        control.Assignments.Should().Be(controlAssignments);
        control.Conversions.Should().Be(controlAssignments);
        control.RevenuePerAssignment.Should().Be(100m);
        control.RevenueLiftPercentage.Should().BeNull("the control has no baseline to lift against");

        treatment.Assignments.Should().Be(treatmentAssignments);
        treatment.RevenuePerAssignment.Should().Be(90m);
        treatment.RevenueLiftPercentage.Should().Be(-10m,
            "10% lower revenue per assignment than the control arm");
    }

    [Fact]
    public async Task SchedulingSweep_ActivatesAndDeactivates_OptInScheduledRulesOnly()
    {
        await using var db = CreateContext();
        var now = SystemClock.UtcNow;

        var inWindow = await AddRuleAsync(db, name: "in-window", isScheduled: true,
            startDate: now.AddHours(-1), endDate: now.AddHours(1), isActive: false);
        var expired = await AddRuleAsync(db, name: "expired", isScheduled: true,
            endDate: now.AddHours(-1), isActive: true);
        var manual = await AddRuleAsync(db, name: "manual", isScheduled: false,
            endDate: now.AddHours(-1), isActive: true);

        var service = CreateService(db);
        var result = await service.RunSchedulingSweepAsync(now);

        result.RulesActivated.Should().Be(1);
        result.RulesDeactivated.Should().Be(1);
        inWindow.IsActive.Should().BeTrue("the window is open, so the scheduler activates the rule");
        expired.IsActive.Should().BeFalse("the window closed, so the scheduler deactivates the rule");
        manual.IsActive.Should().BeTrue("a rule not opted into scheduling is never touched");
    }

    [Fact]
    public async Task SchedulingSweep_TransitionsDateBoundedExperiments()
    {
        await using var db = CreateContext();
        var now = SystemClock.UtcNow;

        var dueToStart = await AddExperimentAsync(db, status: PricingExperimentStatus.Draft,
            startDate: now.AddMinutes(-5));
        var dueToEnd = await AddExperimentAsync(db, status: PricingExperimentStatus.Running,
            endDate: now.AddMinutes(-5));

        var service = CreateService(db);
        var result = await service.RunSchedulingSweepAsync(now);

        result.ExperimentsStarted.Should().Be(1);
        result.ExperimentsCompleted.Should().Be(1);
        dueToStart.Status.Should().Be(PricingExperimentStatus.Running);
        dueToEnd.Status.Should().Be(PricingExperimentStatus.Completed);
    }

    [Fact]
    public async Task SchedulingSweep_IsIdempotent_OnASecondPass()
    {
        await using var db = CreateContext();
        var now = SystemClock.UtcNow;
        await AddRuleAsync(db, name: "in-window", isScheduled: true,
            startDate: now.AddHours(-1), endDate: now.AddHours(1), isActive: false);

        var service = CreateService(db);
        var first = await service.RunSchedulingSweepAsync(now);
        var second = await service.RunSchedulingSweepAsync(now);

        first.RulesActivated.Should().Be(1);
        second.RulesActivated.Should().Be(0, "the rule is already in its desired state");
        second.RulesDeactivated.Should().Be(0);
    }

    private static PricingExperimentService CreateService(TestPricingExperimentDbContext db)
    {
        return new PricingExperimentService(
            new PricingExperimentRepository(db),
            new PricingRuleRepository(db));
    }

    private static async Task<PricingExperiment> AddRunningExperimentAsync(TestPricingExperimentDbContext db)
    {
        return await AddExperimentAsync(db, status: PricingExperimentStatus.Running);
    }

    private static async Task<PricingExperiment> AddExperimentAsync(
        TestPricingExperimentDbContext db,
        PricingExperimentStatus status,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var experiment = new PricingExperiment
        {
            Id = Guid.NewGuid(),
            Name = $"experiment-{Guid.NewGuid():N}".Substring(0, 12),
            Status = status,
            StartDate = startDate,
            EndDate = endDate,
            TrafficPercentage = 100
        };

        experiment.Variants.Add(new PricingExperimentVariant
        {
            Id = Guid.NewGuid(),
            ExperimentId = experiment.Id,
            Name = "control",
            IsControl = true,
            AllocationPercentage = 50
        });
        experiment.Variants.Add(new PricingExperimentVariant
        {
            Id = Guid.NewGuid(),
            ExperimentId = experiment.Id,
            Name = "treatment-a",
            IsControl = false,
            AllocationPercentage = 50,
            DiscountPercentage = 10m
        });

        db.PricingExperiments.Add(experiment);
        await db.SaveChangesAsync();
        return experiment;
    }

    private static async Task<PricingRule> AddRuleAsync(
        TestPricingExperimentDbContext db,
        string name,
        bool isScheduled,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool isActive = true)
    {
        var rule = new PricingRule
        {
            Id = Guid.NewGuid(),
            Name = name,
            RuleType = PricingRuleType.Percentage,
            DiscountPercentage = 10m,
            IsActive = isActive,
            IsScheduled = isScheduled,
            StartDate = startDate,
            EndDate = endDate
        };

        db.PricingRules.Add(rule);
        await db.SaveChangesAsync();
        return rule;
    }

    private static TestPricingExperimentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestPricingExperimentDbContext>()
            .UseInMemoryDatabase($"PricingExperiments_{Guid.NewGuid()}")
            .Options;

        return new TestPricingExperimentDbContext(options);
    }

    private sealed class TestPricingExperimentDbContext(DbContextOptions<TestPricingExperimentDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<PricingRule> PricingRules { get; set; } = null!;

        public DbSet<PricingExperiment> PricingExperiments { get; set; } = null!;

        public DbSet<PricingExperimentVariant> PricingExperimentVariants { get; set; } = null!;

        public DbSet<PricingExperimentAssignment> PricingExperimentAssignments { get; set; } = null!;

        public DbSet<PricingExperimentOutcome> PricingExperimentOutcomes { get; set; } = null!;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Mimic the production ApplicationDbContext: optimistic-concurrency Version bumps
            // drive the rule cache stamp and soft-delete semantics.
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
            return Task.FromResult(Moq.Mock.Of<IDbContextTransaction>());
        }
    }
}
