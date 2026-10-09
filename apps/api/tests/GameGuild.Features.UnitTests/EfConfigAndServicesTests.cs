using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using GameGuild.Features;

namespace GameGuild.Features.UnitTests;

public class EfConfigAndServicesTests
{
    // ── EF Configuration Classes (5 configs) ────────────────────────────
    [Fact]
    public void CapabilityAuditLogConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new CapabilityAuditLogConfiguration();
        cfg.Configure(mb.Entity<CapabilityAuditLog>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new FeatureFlagConfiguration();
        cfg.Configure(mb.Entity<FeatureFlag>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagTargetConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new FeatureFlagTargetConfiguration();
        cfg.Configure(mb.Entity<FeatureFlagTarget>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagUsageConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new FeatureFlagUsageConfiguration();
        cfg.Configure(mb.Entity<FeatureFlagUsage>());
        mb.Model.Should().NotBeNull();
    }

    [Fact]
    public void TenantCapabilityConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new TenantCapabilityConfiguration();
        cfg.Configure(mb.Entity<TenantCapability>());
        mb.Model.Should().NotBeNull();
    }

    // ── FeaturesModelConfiguration ──────────────────────────────────────
    [Fact]
    public void FeaturesModelConfiguration_CanConfigure()
    {
        var mb = new ModelBuilder(new ConventionSet());
        var cfg = new FeaturesModelConfiguration();
        cfg.Configure(mb);
        mb.Model.Should().NotBeNull();
    }

    // ── Repository Constructors ─────────────────────────────────────────
    [Fact]
    public void FeatureFlagQueryRepository_CanBeCreated()
    {
        var repo = new FeatureFlagQueryRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagTargetingRepository_CanBeCreated()
    {
        var repo = new FeatureFlagTargetingRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagAnalyticsRepository_CanBeCreated()
    {
        var repo = new FeatureFlagAnalyticsRepository(Mock.Of<IApplicationDbContext>());
        repo.Should().NotBeNull();
    }

    // ── Core Service Constructors ───────────────────────────────────────
    [Fact]
    public void FeatureFlagEvaluationService_CanBeCreated()
    {
        var svc = new FeatureFlagEvaluationService(
            Mock.Of<IFeatureFlagQueryRepository>(),
            new List<IFeatureEvaluationStrategy>(),
            NullLogger<FeatureFlagEvaluationService>.Instance,
            Options.Create(new FeatureFlagOptions()));
        svc.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagAnalyticsService_CanBeCreated()
    {
        var svc = new FeatureFlagAnalyticsService(
            Mock.Of<IFeatureFlagQueryRepository>(),
            Mock.Of<IFeatureFlagAnalyticsRepository>(),
            NullLogger<FeatureFlagAnalyticsService>.Instance,
            Options.Create(new FeatureFlagOptions()));
        svc.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagConfigurationService_CanBeCreated()
    {
        var svc = new FeatureFlagConfigurationService(
            Mock.Of<IFeatureFlagQueryRepository>(),
            NullLogger<FeatureFlagConfigurationService>.Instance,
            Options.Create(new FeatureFlagOptions()),
            new MemoryCache(new MemoryCacheOptions()));
        svc.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagConfigurationService_WithoutCache()
    {
        var svc = new FeatureFlagConfigurationService(
            Mock.Of<IFeatureFlagQueryRepository>(),
            NullLogger<FeatureFlagConfigurationService>.Instance,
            Options.Create(new FeatureFlagOptions()));
        svc.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagManagementService_CanBeCreated()
    {
        var svc = new FeatureFlagManagementService(
            Mock.Of<IFeatureFlagQueryRepository>(),
            Mock.Of<IFeatureFlagTargetingRepository>(),
            NullLogger<FeatureFlagManagementService>.Instance,
            Options.Create(new FeatureFlagOptions()));
        svc.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagEncryptionService_CanBeCreated()
    {
        // 32 bytes encoded as base64
        var key = Convert.ToBase64String(new byte[32]);
        var svc = new FeatureFlagEncryptionService(key);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagDependencyValidator_CanBeCreated()
    {
        var svc = new FeatureFlagDependencyValidator(Mock.Of<IFeatureFlagQueryRepository>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagExperimentService_CanBeCreated()
    {
        var svc = new FeatureFlagExperimentService();
        svc.Should().NotBeNull();
    }

    [Fact]
    public void FeatureFlagSdkService_CanBeCreated()
    {
        var svc = new FeatureFlagSdkService(Mock.Of<IFeatureFlagQueryRepository>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void SubscriptionFeatureService_CanBeCreated()
    {
        var svc = new SubscriptionFeatureService(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<IFeatureFlagEvaluationService>(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<SubscriptionFeatureService>.Instance);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void CapabilityService_CanBeCreated()
    {
        var svc = new CapabilityService(
            Mock.Of<IApplicationDbContext>(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<CapabilityService>.Instance,
            Mock.Of<GameGuild.Identity.Context.Actors.IActorContextAccessor>());
        svc.Should().NotBeNull();
    }

    // ── Decorator Services ──────────────────────────────────────────────
    [Fact]
    public void CachedFeatureFlagService_CanBeCreated()
    {
        var svc = new CachedFeatureFlagService(
            Mock.Of<IFeatureFlagEvaluationService>(),
            Mock.Of<IDistributedCache>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void AnalyticsFeatureFlagService_CanBeCreated()
    {
        var svc = new AnalyticsFeatureFlagService(
            Mock.Of<IFeatureFlagEvaluationService>(),
            Mock.Of<IFeatureFlagAnalyticsService>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void LoggingFeatureFlagService_CanBeCreated()
    {
        var svc = new LoggingFeatureFlagService(
            Mock.Of<IFeatureFlagEvaluationService>(),
            NullLogger<LoggingFeatureFlagService>.Instance);
        svc.Should().NotBeNull();
    }

    // ── Strategy Classes ────────────────────────────────────────────────
    [Fact]
    public void SimpleToggleStrategy_CanBeCreated()
    {
        var s = new SimpleToggleStrategy();
        s.Should().NotBeNull();
    }

    [Fact]
    public void PercentageRolloutStrategy_CanBeCreated()
    {
        var s = new PercentageRolloutStrategy();
        s.Should().NotBeNull();
    }

    [Fact]
    public void TargetedEvaluationStrategy_CanBeCreated()
    {
        var s = new TargetedEvaluationStrategy(
            new List<ITargetingRuleHandler>());
        s.Should().NotBeNull();
    }

    // ── Targeting Rule Handlers ─────────────────────────────────────────
    [Fact]
    public void TenantTargetingHandler_CanBeCreated()
    {
        var h = new TenantTargetingHandler(
            NullLogger<TenantTargetingHandler>.Instance);
        h.Should().NotBeNull();
    }

    [Fact]
    public void UserTargetingHandler_CanBeCreated()
    {
        var h = new UserTargetingHandler();
        h.Should().NotBeNull();
    }

    [Fact]
    public void PlanTargetingHandler_CanBeCreated()
    {
        var h = new PlanTargetingHandler();
        h.Should().NotBeNull();
    }

    [Fact]
    public void CountryTargetingHandler_CanBeCreated()
    {
        var h = new CountryTargetingHandler();
        h.Should().NotBeNull();
    }

    [Fact]
    public void CustomTargetingHandler_CanBeCreated()
    {
        var h = new CustomTargetingHandler();
        h.Should().NotBeNull();
    }

    // ── DatabaseFeatureFlagProvider ─────────────────────────────────────
    [Fact]
    public void DatabaseFeatureFlagProvider_CanBeCreated()
    {
        var provider = new DatabaseFeatureFlagProvider(
            Mock.Of<IServiceProvider>(),
            NullLogger<DatabaseFeatureFlagProvider>.Instance);
        provider.Should().NotBeNull();
    }

    // ── OpenFeatureHostedInitializer ────────────────────────────────────
    [Fact]
    public void OpenFeatureHostedInitializer_CanBeCreated()
    {
        var svc = new OpenFeatureHostedInitializer(
            NullLogger<OpenFeatureHostedInitializer>.Instance);
        svc.Should().NotBeNull();
    }

    // ── DTO Records ─────────────────────────────────────────────────────
    [Fact]
    public void FeatureFlagDto_CanBeCreated()
    {
        var dto = new FeatureFlagDto
        {
            Id = Guid.NewGuid(),
            Key = "feature_key",
            Name = "Feature Name",
            Description = "Description",
            IsEnabled = true,
            Type = FeatureFlagType.Toggle,
            Environment = "production",
            DefaultValue = "true",
            CreatedAt = DateTime.UtcNow
        };
        dto.Key.Should().Be("feature_key");
        dto.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void FeatureFlagTargetDto_CanBeCreated()
    {
        var dto = new FeatureFlagTargetDto
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = Guid.NewGuid(),
            TargetType = "tenant",
            TargetIdentifier = "tenant-123",
            IsEnabled = true,
            RolloutPercentage = 100,
            CreatedAt = DateTime.UtcNow
        };
        dto.TargetType.Should().Be("tenant");
    }

    [Fact]
    public void FeatureFlagOptions_Defaults()
    {
        var opts = new FeatureFlagOptions();
        opts.CacheTtlMinutes.Should().Be(5);
        opts.EnableAnalytics.Should().BeTrue();
        opts.EnableCaching.Should().BeTrue();
    }

    [Fact]
    public void FeatureEvaluationResult_CanBeCreated()
    {
        var r = new FeatureEvaluationResult
        {
            FeatureKey = "test_flag",
            IsEnabled = true,
            Value = "variant_a",
            Reason = "targeted"
        };
        r.FeatureKey.Should().Be("test_flag");
    }

    [Fact]
    public void FeatureContext_CanBeCreated()
    {
        var ctx = new FeatureContext
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Environment = "staging",
            Country = "US"
        };
        ctx.Environment.Should().Be("staging");
    }

    [Fact]
    public void FeatureFlagConfig_CanBeCreated()
    {
        var cfg = new FeatureFlagConfig
        {
            Key = "new_feature",
            Name = "New Feature",
            IsEnabled = false,
            Type = FeatureFlagType.Percentage,
            RolloutPercentage = 50
        };
        cfg.RolloutPercentage.Should().Be(50);
    }

    [Fact]
    public void FeatureFlagAnalytics_CanBeCreated()
    {
        var a = new FeatureFlagAnalytics
        {
            FeatureKey = "analytics_flag",
            TotalAccesses = 1000,
            EnabledAccesses = 750,
            DisabledAccesses = 250,
            UniqueUsers = 100
        };
        a.TotalAccesses.Should().Be(1000);
    }

    [Fact]
    public void FeatureFlagStatistics_CanBeCreated()
    {
        var stats = new FeatureFlagStatistics
        {
            FeatureFlagId = Guid.NewGuid(),
            FeatureFlagKey = "stat_flag",
            TotalEvaluations = 500,
            EnabledEvaluations = 400,
            DisabledEvaluations = 100,
            EnabledPercentage = 80.0,
            UniqueUsers = 50,
            FirstEvaluationAt = DateTime.UtcNow.AddDays(-7),
            LastEvaluationAt = DateTime.UtcNow,
            PeriodStart = DateTime.UtcNow.AddDays(-7),
            PeriodEnd = DateTime.UtcNow
        };
        stats.TotalEvaluations.Should().Be(500);
    }

    [Fact]
    public void FeatureFlagUsageSummary_CanBeCreated()
    {
        var summary = new FeatureFlagUsageSummary
        {
            FeatureFlagId = Guid.NewGuid(),
            FeatureFlagKey = "usage_flag",
            Name = "Usage Flag",
            IsEnabled = true,
            TotalEvaluations = 200,
            UniqueUsers = 30,
            LastEvaluatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        summary.TotalEvaluations.Should().Be(200);
    }

    [Fact]
    public void ExperimentStatistics_CanBeCreated()
    {
        var stats = new ExperimentStatistics
        {
            VariantName = "control",
            Observations = 1000,
            Conversions = 50,
            IsControl = true
        };
        stats.Observations.Should().Be(1000);
    }

    [Fact]
    public void AbTestResult_CanBeCreated()
    {
        var result = new AbTestResult
        {
            ControlVariant = "control",
            TreatmentVariant = "treatment",
            PValue = 0.03,
            IsStatisticallySignificant = true,
            ConfidenceLevel = 0.95
        };
        result.IsStatisticallySignificant.Should().BeTrue();
    }

    [Fact]
    public void FeatureFlagUsageStats_CanBeCreated()
    {
        var stats = new FeatureFlagUsageStats
        {
            TotalAccessCount = 1000,
            EnabledCount = 800,
            DisabledCount = 200,
            UniqueUserCount = 150,
            UniqueTenantCount = 5
        };
        stats.EnabledPercentage.Should().Be(80.0);
    }

    [Fact]
    public void FeatureFlagDependency_CanBeCreated()
    {
        var dep = new FeatureFlagDependency
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = Guid.NewGuid(),
            DependsOnFeatureFlagId = Guid.NewGuid(),
            DependencyType = "requires",
            FeatureFlagKey = "feature_a",
            DependsOnFeatureFlagKey = "feature_b",
            CreatedAt = DateTime.UtcNow
        };
        dep.DependencyType.Should().Be("requires");
    }

    [Fact]
    public void FeatureFlagEvaluationHistory_CanBeCreated()
    {
        var hist = new FeatureFlagEvaluationHistory
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = Guid.NewGuid(),
            FeatureFlagKey = "eval_flag",
            UserId = Guid.NewGuid().ToString(),
            EvaluatedValue = "true",
            WasEnabled = true,
            Environment = "production",
            EvaluatedAt = DateTime.UtcNow
        };
        hist.WasEnabled.Should().BeTrue();
    }

    // ── Controller Request DTOs ─────────────────────────────────────────
    [Fact]
    public void SetCapabilityOverrideRequest_CanBeCreated()
    {
        var req = new SetCapabilityOverrideRequest("cap", true, "admin", "testing", DateTimeOffset.UtcNow.AddDays(7));
        req.Capability.Should().Be("cap");
        req.IsEnabled.Should().BeTrue();
        req.Source.Should().Be("admin");
        req.Reason.Should().Be("testing");
        req.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public void SubscriptionFeatureAccessResult_CanBeCreated()
    {
        var r = new SubscriptionFeatureAccessResult(true, "premium_feature", "Pro", null, null);
        r.IsAllowed.Should().BeTrue();
        r.FeatureKey.Should().Be("premium_feature");
        r.PlanName.Should().Be("Pro");
    }

    [Fact]
    public void SubscriptionFeatureAccessResult_Denied()
    {
        var r = new SubscriptionFeatureAccessResult(false, "locked", "Free", "Upgrade required", "https://upgrade.example.com");
        r.IsAllowed.Should().BeFalse();
        r.Reason.Should().Be("Upgrade required");
        r.UpgradeUrl.Should().Be("https://upgrade.example.com");
    }

    [Fact]
    public void FeatureEntitlementComparison_CanBeCreated()
    {
        var c = new FeatureEntitlementComparison(
            Guid.NewGuid(), "Free", Guid.NewGuid(), "Pro",
            new[] { "basic_feature" },
            new[] { "advanced_feature", "premium_support" },
            new[] { "trial_only" });
        c.CurrentPlanName.Should().Be("Free");
        c.TargetPlanName.Should().Be("Pro");
        c.NewFeatures.Should().HaveCount(2);
        c.LostFeatures.Should().HaveCount(1);
        c.SharedFeatures.Should().HaveCount(1);
    }

    // ── Additional Command Handler Constructors ─────────────────────────
    [Fact]
    public void CreateFeatureFlagCommandHandler_CanBeCreated()
    {
        var h = new CreateFeatureFlagCommandHandler(
            Mock.Of<IFeatureFlagQueryRepository>(),
            Mock.Of<ILogger<CreateFeatureFlagCommandHandler>>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void DisableFeatureFlagCommandHandler_CanBeCreated()
    {
        var h = new DisableFeatureFlagCommandHandler(
            Mock.Of<IFeatureFlagQueryRepository>(),
            Mock.Of<ILogger<DisableFeatureFlagCommandHandler>>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void EnableFeatureFlagCommandHandler_CanBeCreated()
    {
        var h = new EnableFeatureFlagCommandHandler(
            Mock.Of<IFeatureFlagQueryRepository>(),
            Mock.Of<ILogger<EnableFeatureFlagCommandHandler>>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void ToggleFeatureFlagCommandHandler_CanBeCreated()
    {
        var h = new ToggleFeatureFlagCommandHandler(
            Mock.Of<IFeatureFlagQueryRepository>(),
            Mock.Of<ILogger<ToggleFeatureFlagCommandHandler>>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void UpdateFeatureFlagCommandHandler_CanBeCreated()
    {
        var h = new UpdateFeatureFlagCommandHandler(
            Mock.Of<IFeatureFlagQueryRepository>(),
            Mock.Of<ILogger<UpdateFeatureFlagCommandHandler>>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void CreateFeatureCommandHandler_CanBeCreated()
    {
        var h = new CreateFeatureCommandHandler(
            Mock.Of<IFeatureFlagQueryRepository>(),
            Mock.Of<ILogger<CreateFeatureCommandHandler>>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void AddTargetingRuleCommandHandler_CanBeCreated()
    {
        var h = new AddTargetingRuleCommandHandler(
            Mock.Of<IFeatureFlagTargetingRepository>(),
            Mock.Of<ILogger<AddTargetingRuleCommandHandler>>());
        h.Should().NotBeNull();
    }

    // ── Additional Query Handler Constructors ───────────────────────────
    [Fact]
    public void GetFeatureFlagByIdQueryHandler_CanBeCreated()
    {
        var h = new GetFeatureFlagByIdQueryHandler(Mock.Of<IFeatureFlagQueryRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetFeatureFlagByKeyQueryHandler_CanBeCreated()
    {
        var h = new GetFeatureFlagByKeyQueryHandler(Mock.Of<IFeatureFlagQueryRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetAllFeatureFlagsQueryHandler_CanBeCreated()
    {
        var h = new GetAllFeatureFlagsQueryHandler(Mock.Of<IFeatureFlagQueryRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetFeatureFlagsByTenantQueryHandler_CanBeCreated()
    {
        var h = new GetFeatureFlagsByTenantQueryHandler(Mock.Of<IFeatureFlagQueryRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetFeatureFlagsByEnvironmentQueryHandler_CanBeCreated()
    {
        var h = new GetFeatureFlagsByEnvironmentQueryHandler(Mock.Of<IFeatureFlagQueryRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetFeatureFlagConfigsQueryHandler_CanBeCreated()
    {
        var h = new GetFeatureFlagConfigsQueryHandler(Mock.Of<IFeatureFlagQueryRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetFeatureFlagDependenciesQueryHandler_CanBeCreated()
    {
        var h = new GetFeatureFlagDependenciesQueryHandler(Mock.Of<IFeatureFlagQueryRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetFeatureFlagEvaluationHistoryQueryHandler_CanBeCreated()
    {
        var h = new GetFeatureFlagEvaluationHistoryQueryHandler(Mock.Of<IFeatureFlagQueryRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetFeatureFlagUsageSummaryQueryHandler_CanBeCreated()
    {
        var h = new GetFeatureFlagUsageSummaryQueryHandler(Mock.Of<IFeatureFlagQueryRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void EvaluateFeatureQueryHandler_CanBeCreated()
    {
        var h = new EvaluateFeatureQueryHandler(Mock.Of<IFeatureFlagEvaluationService>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void BulkEvaluateFeaturesQueryHandler_CanBeCreated()
    {
        var h = new BulkEvaluateFeaturesQueryHandler(Mock.Of<IFeatureFlagEvaluationService>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetTargetingRulesQueryHandler_CanBeCreated()
    {
        var h = new GetTargetingRulesQueryHandler(Mock.Of<IFeatureFlagQueryRepository>());
        h.Should().NotBeNull();
    }

    [Fact]
    public void GetTargetingRuleByIdQueryHandler_CanBeCreated()
    {
        var h = new GetTargetingRuleByIdQueryHandler(Mock.Of<IFeatureFlagQueryRepository>());
        h.Should().NotBeNull();
    }
}
