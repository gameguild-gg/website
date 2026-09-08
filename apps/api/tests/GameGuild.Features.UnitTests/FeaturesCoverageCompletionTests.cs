using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.Commerce.Subscriptions;
using Moq;
using Xunit;
using CommerceSubscriptionPlan = GameGuild.Commerce.Subscriptions.SubscriptionPlan;

namespace GameGuild.Features.UnitTests;

public class FeaturesCoverageCompletionTests
{
    [Theory]
    [MemberData(nameof(ConstructorGuardCases))]
    public void Constructors_WhenRequiredDependencyIsNull_ThrowArgumentNullException(string _, Action act)
    {
        act.Should().Throw<ArgumentNullException>();
    }

    public static IEnumerable<object[]> ConstructorGuardCases()
    {
        var queryRepository = Mock.Of<IFeatureFlagQueryRepository>();
        var targetingRepository = Mock.Of<IFeatureFlagTargetingRepository>();
        var analyticsRepository = Mock.Of<IFeatureFlagAnalyticsRepository>();
        var sdkService = Mock.Of<IFeatureFlagSdkService>();
        var evaluationService = Mock.Of<IFeatureFlagEvaluationService>();
        var strategies = Array.Empty<IFeatureEvaluationStrategy>();

        yield return Case("AddTargetingRule logger", () => new AddTargetingRuleCommandHandler(targetingRepository, null!));
        yield return Case("AddTargetingRule repo", () => new AddTargetingRuleCommandHandler(null!, NullLogger<AddTargetingRuleCommandHandler>.Instance));
        yield return Case("CreateFeature logger", () => new CreateFeatureCommandHandler(queryRepository, null!));
        yield return Case("CreateFeature repo", () => new CreateFeatureCommandHandler(null!, NullLogger<CreateFeatureCommandHandler>.Instance));
        yield return Case("UpdateFeatureFlag logger", () => new UpdateFeatureFlagCommandHandler(queryRepository, null!));
        yield return Case("UpdateFeatureFlag repo", () => new UpdateFeatureFlagCommandHandler(null!, NullLogger<UpdateFeatureFlagCommandHandler>.Instance));

        yield return Case("BulkEvaluateFeatures service", () => new BulkEvaluateFeaturesQueryHandler(null!));
        yield return Case("EvaluateFeature service", () => new EvaluateFeatureQueryHandler(null!));
        yield return Case("ExportAnalytics repo", () => new ExportAnalyticsQueryHandler(null!));
        yield return Case("FeatureFlagExists repo", () => new FeatureFlagExistsQueryHandler(null!));
        yield return Case("GetAllFeatureFlags repo", () => new GetAllFeatureFlagsQueryHandler(null!));
        yield return Case("GetFeatureFlagById repo", () => new GetFeatureFlagByIdQueryHandler(null!));
        yield return Case("GetFeatureFlagByKey repo", () => new GetFeatureFlagByKeyQueryHandler(null!));
        yield return Case("GetFeatureFlagConfigs repo", () => new GetFeatureFlagConfigsQueryHandler(null!));
        yield return Case("GetFeatureFlagDependencies repo", () => new GetFeatureFlagDependenciesQueryHandler(null!));
        yield return Case("GetFeatureFlagEvaluationHistory repo", () => new GetFeatureFlagEvaluationHistoryQueryHandler(null!));
        yield return Case("GetFeatureFlagUsageSummary repo", () => new GetFeatureFlagUsageSummaryQueryHandler(null!));
        yield return Case("GetSdkConfiguration service", () => new GetSdkConfigurationQueryHandler(null!));
        yield return Case("GetTargetingRuleById repo", () => new GetTargetingRuleByIdQueryHandler(null!));
        yield return Case("GetTargetingRules repo", () => new GetTargetingRulesQueryHandler(null!));
        yield return Case("ValidateFeatureFlagKey repo", () => new ValidateFeatureFlagKeyQueryHandler(null!));

        yield return Case("Analytics query repo", () => new FeatureFlagAnalyticsService(null!, analyticsRepository, NullLogger<FeatureFlagAnalyticsService>.Instance, Options.Create(new FeatureFlagOptions())));
        yield return Case("Analytics analytics repo", () => new FeatureFlagAnalyticsService(queryRepository, null!, NullLogger<FeatureFlagAnalyticsService>.Instance, Options.Create(new FeatureFlagOptions())));
        yield return Case("Analytics logger", () => new FeatureFlagAnalyticsService(queryRepository, analyticsRepository, null!, Options.Create(new FeatureFlagOptions())));
        yield return Case("Analytics options value", () => new FeatureFlagAnalyticsService(queryRepository, analyticsRepository, NullLogger<FeatureFlagAnalyticsService>.Instance, NullFeatureOptions()));

        yield return Case("Evaluation query repo", () => new FeatureFlagEvaluationService(null!, strategies, NullLogger<FeatureFlagEvaluationService>.Instance, Options.Create(new FeatureFlagOptions())));
        yield return Case("Evaluation strategies", () => new FeatureFlagEvaluationService(queryRepository, null!, NullLogger<FeatureFlagEvaluationService>.Instance, Options.Create(new FeatureFlagOptions())));
        yield return Case("Evaluation logger", () => new FeatureFlagEvaluationService(queryRepository, strategies, null!, Options.Create(new FeatureFlagOptions())));
        yield return Case("Evaluation options value", () => new FeatureFlagEvaluationService(queryRepository, strategies, NullLogger<FeatureFlagEvaluationService>.Instance, NullFeatureOptions()));

        yield return Case("Configuration query repo", () => new FeatureFlagConfigurationService(null!, NullLogger<FeatureFlagConfigurationService>.Instance, Options.Create(new FeatureFlagOptions())));
        yield return Case("Configuration logger", () => new FeatureFlagConfigurationService(queryRepository, null!, Options.Create(new FeatureFlagOptions())));
        yield return Case("Configuration options value", () => new FeatureFlagConfigurationService(queryRepository, NullLogger<FeatureFlagConfigurationService>.Instance, NullFeatureOptions()));

        yield return Case("Management query repo", () => new FeatureFlagManagementService(null!, targetingRepository, NullLogger<FeatureFlagManagementService>.Instance, Options.Create(new FeatureFlagOptions())));
        yield return Case("Management targeting repo", () => new FeatureFlagManagementService(queryRepository, null!, NullLogger<FeatureFlagManagementService>.Instance, Options.Create(new FeatureFlagOptions())));
        yield return Case("Management logger", () => new FeatureFlagManagementService(queryRepository, targetingRepository, null!, Options.Create(new FeatureFlagOptions())));
        yield return Case("Management options value", () => new FeatureFlagManagementService(queryRepository, targetingRepository, NullLogger<FeatureFlagManagementService>.Instance, NullFeatureOptions()));

        yield return Case("Database provider services", () => new DatabaseFeatureFlagProvider(null!, NullLogger<DatabaseFeatureFlagProvider>.Instance));
        yield return Case("Database provider logger", () => new DatabaseFeatureFlagProvider(new ServiceCollection().BuildServiceProvider(), null!));

        _ = evaluationService;
        _ = sdkService;
    }

    [Fact]
    public async Task FeatureFlagsController_Actions_InvokeEvaluationServiceAndHelpers()
    {
        var evaluation = new Mock<IFeatureFlagEvaluationService>();
        evaluation.Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureEvaluationResult { FeatureKey = "flag", IsEnabled = true, Value = "on" });
        evaluation.Setup(x => x.GetValueAsync("flag", It.IsAny<FeatureContext>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        evaluation.Setup(x => x.GetEnabledFeaturesAsync(It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "flag" });

        var controller = new FeatureFlagsController(evaluation.Object, NullLogger<FeatureFlagsController>.Instance, new CommandHandlerSender(evaluation.Object, Mock.Of<ICapabilityService>()))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Request.Headers.UserAgent = "tests";

        (await controller.EvaluateFeature(new FeatureEvaluationRequest { FeatureKey = "flag" }, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await controller.GetFeatureValue("flag"))
            .Should().BeOfType<OkObjectResult>();
        (await controller.GetEnabled())
            .Should().BeOfType<OkObjectResult>();

        var bulk = await controller.BulkEvaluateFeatures(new BulkEvaluationRequest
        {
            FeatureKeys = new List<string> { "flag" },
            Context = new FeatureContext()
        }, CancellationToken.None);

        bulk.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task FeatureFlagQueryRepository_GetDependenciesAsync_ReturnsEmptyPlaceholder()
    {
        var repository = new FeatureFlagQueryRepository(Mock.Of<IApplicationDbContext>());

        var result = await repository.GetDependenciesAsync(Guid.NewGuid(), includeInverse: true);

        result.Should().BeEmpty();
    }

    [Fact]
    public void UsageEnforcementMiddleware_CanConstructAndRegister()
    {
        var middleware = new UsageEnforcementMiddleware(
            _ => Task.CompletedTask,
            NullLogger<UsageEnforcementMiddleware>.Instance,
            new MemoryCache(new MemoryCacheOptions()));
        middleware.Should().NotBeNull();

        var builder = new Mock<IApplicationBuilder>();
        builder.Setup(x => x.ApplicationServices).Returns(new ServiceCollection().BuildServiceProvider());
        builder.Setup(x => x.Properties).Returns(new Dictionary<string, object?>());
        builder.Setup(x => x.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
            .Returns(builder.Object);

        builder.Object.UseUsageEnforcement().Should().BeSameAs(builder.Object);
    }

    [Fact]
    public async Task CapabilityService_CoversPlanFallbackOverridesSyncAndAuditFilters()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var plan = new CommerceSubscriptionPlan("Mystery", "mystery", 1000) { Id = Guid.NewGuid() };
        var subscription = CreateActiveSubscription(tenantId, plan);
        db.Set<CommerceSubscriptionPlan>().Add(plan);
        db.Set<Subscription>().Add(subscription);
        await db.SaveChangesAsync();

        var service = new CapabilityService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<CapabilityService>.Instance);

        (await service.IsCapabilityEnabledAsync(tenantId, "lms.courses.basic")).Should().BeTrue();
        (await service.IsCapabilityEnabledAsync(tenantId, "branding.custom")).Should().BeFalse();

        plan.Slug = "starter";
        await db.SaveChangesAsync();
        (await service.IsCapabilityEnabledAsync(tenantId, "lxp.discovery")).Should().BeTrue();

        await service.SetCapabilityOverrideAsync(tenantId, "branding.custom", true, "override:test", Guid.NewGuid(), "grant");
        (await service.IsCapabilityEnabledAsync(tenantId, "branding.custom")).Should().BeTrue();

        var capabilities = await service.GetTenantCapabilitiesAsync(tenantId);
        capabilities["branding.custom"].Should().BeTrue();

        await service.RemoveCapabilityOverrideAsync(tenantId, "branding.custom", Guid.NewGuid(), "remove");
        (await service.GetAuditLogAsync(
            tenantId,
            "branding.custom",
            SystemClock.UtcNow.AddMinutes(-5),
            SystemClock.UtcNow.AddMinutes(5))).Should().NotBeEmpty();

        plan.Slug = "mystery";
        await db.SaveChangesAsync();
        await service.SyncCapabilitiesFromPlanAsync(tenantId);

        plan.Slug = "enterprise";
        await db.SaveChangesAsync();
        await service.SyncCapabilitiesFromPlanAsync(tenantId);
        (await service.IsCapabilityEnabledAsync(tenantId, "branding.custom")).Should().BeTrue();
    }

    [Fact]
    public async Task SubscriptionFeatureService_CoversJsonCsvEmptyWildcardUpgradeAndComparisonPaths()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var currentPlan = new CommerceSubscriptionPlan("Starter", "starter", 1000)
        {
            Id = Guid.NewGuid(),
            Features = "[\"feature.a\"]"
        };
        var upgradePlan = new CommerceSubscriptionPlan("Pro", "pro", 2000)
        {
            Id = Guid.NewGuid(),
            Features = "feature.b,feature.c"
        };
        var wildcardPlan = new CommerceSubscriptionPlan("Enterprise", "enterprise", 3000)
        {
            Id = Guid.NewGuid(),
            Features = "[\"all\"]"
        };
        var nullFeaturesPlan = new CommerceSubscriptionPlan("Null Features", "null-features", 4000)
        {
            Id = Guid.NewGuid(),
            Features = "null"
        };
        db.Set<CommerceSubscriptionPlan>().AddRange(currentPlan, upgradePlan, wildcardPlan, nullFeaturesPlan);
        db.Set<Subscription>().Add(CreateActiveSubscription(tenantId, currentPlan));
        await db.SaveChangesAsync();

        var evaluation = new Mock<IFeatureFlagEvaluationService>();
        evaluation.Setup(x => x.IsEnabledAsync(It.IsAny<string>(), It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new SubscriptionFeatureService(
            db,
            evaluation.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<SubscriptionFeatureService>.Instance);

        (await service.GetAvailableFeaturesForTenantAsync(tenantId)).Should().Contain("feature.a");
        (await service.GetFeaturesUnlockedByPlanAsync(upgradePlan.Id)).Should().Contain(new[] { "feature.b", "feature.c" });
        (await service.GetFeaturesUnlockedByPlanAsync(nullFeaturesPlan.Id)).Should().BeEmpty();
        (await service.GetFeaturesUnlockedByPlanAsync(Guid.NewGuid())).Should().BeEmpty();

        var allowed = await service.ValidateFeatureAccessAsync(tenantId, "feature.a");
        allowed.Value.IsAllowed.Should().BeTrue();

        var denied = await service.ValidateFeatureAccessAsync(tenantId, "feature.b");
        denied.Value.IsAllowed.Should().BeFalse();
        denied.Value.UpgradeUrl.Should().Contain(upgradePlan.Slug);

        db.Set<Subscription>().RemoveRange(db.Set<Subscription>());
        db.Set<Subscription>().Add(CreateActiveSubscription(tenantId, wildcardPlan));
        await db.SaveChangesAsync();
        (await service.ValidateFeatureAccessAsync(tenantId, "anything")).Value.IsAllowed.Should().BeTrue();

        evaluation.Setup(x => x.IsEnabledAsync("disabled", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        (await service.ValidateFeatureAccessAsync(tenantId, "disabled")).Value.Reason.Should().Be("Feature is currently disabled");

        var comparison = await service.CompareFeatureEntitlementsAsync(currentPlan.Id, upgradePlan.Id);
        comparison.SharedFeatures.Should().BeEmpty();
        comparison.NewFeatures.Should().Contain("feature.b");

        var missingComparison = await service.CompareFeatureEntitlementsAsync(Guid.NewGuid(), Guid.NewGuid());
        missingComparison.CurrentPlanName.Should().Be("Unknown");
        missingComparison.TargetPlanName.Should().Be("Unknown");

        var noSubscription = await service.ValidateFeatureAccessAsync(Guid.NewGuid(), "feature.a");
        noSubscription.Value.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task FeatureAnalytics_DateValidation_CoversInvalidDateRanges()
    {
        var service = new FeatureFlagAnalyticsService(
            Mock.Of<IFeatureFlagQueryRepository>(),
            Mock.Of<IFeatureFlagAnalyticsRepository>(),
            NullLogger<FeatureFlagAnalyticsService>.Instance,
            Options.Create(new FeatureFlagOptions()));

        await service.Invoking(x => x.GetAnalyticsAsync("flag", SystemClock.UtcNow, SystemClock.UtcNow.AddDays(-1)))
            .Should().ThrowAsync<ArgumentException>();
        await service.Invoking(x => x.GetAnalyticsAsync("flag", SystemClock.UtcNow.AddDays(-400), SystemClock.UtcNow))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task FeatureAnalytics_WhenRepositoryThrows_ReturnsEmptyAnalytics()
    {
        var analyticsRepository = new Mock<IFeatureFlagAnalyticsRepository>();
        analyticsRepository
            .Setup(x => x.GetUsageAnalyticsAsync("flag", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("analytics unavailable"));

        var service = new FeatureFlagAnalyticsService(
            Mock.Of<IFeatureFlagQueryRepository>(),
            analyticsRepository.Object,
            NullLogger<FeatureFlagAnalyticsService>.Instance,
            Options.Create(new FeatureFlagOptions()));

        var result = await service.GetAnalyticsAsync("flag", SystemClock.UtcNow.AddDays(-1), SystemClock.UtcNow);

        result.FeatureKey.Should().Be("flag");
        result.TotalAccesses.Should().Be(0);
    }

    [Fact]
    public async Task FeatureEvaluation_EnvironmentMismatch_CoversNullActualEnvironment()
    {
        var repository = new Mock<IFeatureFlagQueryRepository>();
        repository.Setup(x => x.GetByKeyAsync("flag", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureFlag
            {
                Key = "flag",
                Name = "Flag",
                Environment = "production",
                DefaultValue = "off"
            });

        var service = new FeatureFlagEvaluationService(
            repository.Object,
            Array.Empty<IFeatureEvaluationStrategy>(),
            NullLogger<FeatureFlagEvaluationService>.Instance,
            Options.Create(new FeatureFlagOptions()));

        var result = await service.EvaluateAsync("flag", new FeatureContext { Environment = null! });

        result.IsEnabled.Should().BeFalse();
        result.Reason.Should().Contain("got 'null'");
    }

    [Fact]
    public async Task DatabaseFeatureFlagProvider_ContextConversion_CoversMissingInvalidAndNonStringValues()
    {
        var evaluation = new Mock<IFeatureFlagEvaluationService>();
        var capturedContexts = new List<FeatureContext>();
        evaluation
            .Setup(x => x.EvaluateAsync("flag", It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .Callback<string, FeatureContext, CancellationToken>((_, context, _) => capturedContexts.Add(context))
            .ReturnsAsync(new FeatureEvaluationResult { IsEnabled = true, Value = "true" });

        var services = new ServiceCollection();
        services.AddSingleton(evaluation.Object);
        var provider = new DatabaseFeatureFlagProvider(
            services.BuildServiceProvider(),
            NullLogger<DatabaseFeatureFlagProvider>.Instance);

        var missingStandardKeys = OpenFeature.Model.EvaluationContext.Builder()
            .Set("customKey", new OpenFeature.Model.Value("customValue"))
            .Set("customNull", new OpenFeature.Model.Value())
            .Build();
        await provider.ResolveBooleanValueAsync("flag", false, missingStandardKeys);

        var invalidGuids = OpenFeature.Model.EvaluationContext.Builder()
            .Set("userId", "not-a-guid")
            .Set("tenantId", "not-a-guid")
            .Set("permissions", "admin,read")
            .Build();
        await provider.ResolveBooleanValueAsync("flag", false, invalidGuids);

        var nonStringStandardValues = OpenFeature.Model.EvaluationContext.Builder()
            .Set("userId", new OpenFeature.Model.Value(1))
            .Set("tenantId", new OpenFeature.Model.Value(2))
            .Set("environment", new OpenFeature.Model.Value(3))
            .Set("ipAddress", new OpenFeature.Model.Value(true))
            .Set("userAgent", new OpenFeature.Model.Value(4L))
            .Set("country", new OpenFeature.Model.Value(5.5))
            .Set("subscriptionPlanId", new OpenFeature.Model.Value(false))
            .Set("permissions", new OpenFeature.Model.Value(6))
            .Build();
        await provider.ResolveBooleanValueAsync("flag", false, nonStringStandardValues);

        capturedContexts.Should().HaveCount(3);
        capturedContexts[0].CustomAttributes.Should().ContainKey("customKey");
        capturedContexts[0].CustomAttributes["customNull"].Should().Be(string.Empty);
        capturedContexts[1].UserId.Should().BeNull();
        capturedContexts[1].TenantId.Should().BeNull();
        capturedContexts[1].Permissions.Should().Contain(new[] { "admin", "read" });
        capturedContexts[2].Environment.Should().Be("production");
    }

    [Fact]
    public async Task TargetingHandlers_CoverDisabledNullAndValueOnlyBranches()
    {
        var planHandler = new PlanTargetingHandler();
        var disabledPlan = await planHandler.EvaluateAsync(
            TargetedFlag(Target(FeatureFlagConstants.TargetTypes.Plan, "pro", isEnabled: false, rollout: 50)),
            new FeatureContext { SubscriptionPlanId = "pro" });
        disabledPlan!.IsEnabled.Should().BeFalse();

        var nullUserPlan = await planHandler.EvaluateAsync(
            TargetedFlag(Target(FeatureFlagConstants.TargetTypes.Plan, "pro", rollout: 50)),
            new FeatureContext { SubscriptionPlanId = "pro", TenantId = Guid.NewGuid() });
        nullUserPlan.Should().NotBeNull();

        var tenantHandler = new TenantTargetingHandler(NullLogger<TenantTargetingHandler>.Instance);
        var tenantId = Guid.NewGuid();
        var disabledTenant = await tenantHandler.EvaluateAsync(
            TargetedFlag(Target(FeatureFlagConstants.TargetTypes.Tenant, tenantId.ToString(), isEnabled: false, rollout: 50)),
            new FeatureContext { TenantId = tenantId });
        disabledTenant!.IsEnabled.Should().BeFalse();

        var nullUserTenant = await tenantHandler.EvaluateAsync(
            TargetedFlag(Target(FeatureFlagConstants.TargetTypes.Tenant, tenantId.ToString(), rollout: 50)),
            new FeatureContext { TenantId = tenantId });
        nullUserTenant.Should().NotBeNull();

        var customHandler = new CustomTargetingHandler();
        var nullAttributes = await customHandler.EvaluateAsync(
            TargetedFlag(Target(FeatureFlagConstants.TargetTypes.Custom, "role=admin")),
            new FeatureContext { CustomAttributes = null! });
        nullAttributes.Should().BeNull();

        var nullIdentifier = await customHandler.EvaluateAsync(
            TargetedFlag(Target(FeatureFlagConstants.TargetTypes.Custom, null!)),
            new FeatureContext { CustomAttributes = new Dictionary<string, object> { ["role"] = "admin" } });
        nullIdentifier.Should().BeNull();

        var valueOnlyMatch = await customHandler.EvaluateAsync(
            TargetedFlag(Target(FeatureFlagConstants.TargetTypes.Custom, "admin")),
            new FeatureContext { CustomAttributes = new Dictionary<string, object> { ["role"] = "admin" } });
        valueOnlyMatch!.IsEnabled.Should().BeTrue();

        var disabledCustom = await customHandler.EvaluateAsync(
            TargetedFlag(Target(FeatureFlagConstants.TargetTypes.Custom, "role=admin", isEnabled: false, rollout: 50)),
            new FeatureContext { CustomAttributes = new Dictionary<string, object> { ["role"] = "admin" } });
        disabledCustom!.IsEnabled.Should().BeFalse();

        var nullUserCustom = await customHandler.EvaluateAsync(
            TargetedFlag(Target(FeatureFlagConstants.TargetTypes.Custom, "role=admin", rollout: 50)),
            new FeatureContext
            {
                TenantId = Guid.NewGuid(),
                CustomAttributes = new Dictionary<string, object> { ["role"] = "admin" }
            });
        nullUserCustom.Should().NotBeNull();
    }

    [Fact]
    public void ExperimentService_PrivateMathHelpers_CoverEdgeBranches()
    {
        var service = new FeatureFlagExperimentService();

        InvokePrivate<double>(service, "Erf", -1d).Should().BeLessThan(0);
        InvokePrivate<double>(service, "InverseStandardNormalCdf", 0.01d).Should().BeLessThan(0);
        InvokePrivate<double>(service, "InverseStandardNormalCdf", 0.99d).Should().BeGreaterThan(0);

        InvokingPrivate(service, "InverseStandardNormalCdf", 0d)
            .Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<ArgumentException>();
        InvokingPrivate(service, "InverseStandardNormalCdf", 1d)
            .Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<ArgumentException>();
    }

    [Fact]
    public void MapperAndContextFactory_CoverNullFallbackBranches()
    {
        var rule = EntityModelMapper.ToTargetingRule(new FeatureFlagTarget
        {
            TargetType = "custom",
            TargetIdentifier = "role=admin",
            Metadata = "null"
        });
        rule.Conditions.Should().BeEmpty();

        var context = new FeatureContext
        {
            CustomAttributes = new Dictionary<string, object>
            {
                ["nullStringObject"] = new NullToStringObject()
            }
        };

        var openFeatureContext = FeatureContextFactory.ToOpenFeatureContext(context);
        openFeatureContext.Should().NotBeNull();
    }

    private static object[] Case(string name, Action act) => new object[] { name, act };

    private static FeatureFlag TargetedFlag(params FeatureFlagTarget[] targets)
    {
        return new FeatureFlag
        {
            Key = "flag",
            Name = "Flag",
            DefaultValue = "off",
            EnabledValue = "on",
            Environment = "production",
            IsEnabled = true,
            Targets = targets.ToList()
        };
    }

    private static FeatureFlagTarget Target(string type, string identifier, bool isEnabled = true, int rollout = 100)
    {
        return new FeatureFlagTarget
        {
            TargetType = type,
            TargetIdentifier = identifier,
            IsEnabled = isEnabled,
            RolloutPercentage = rollout
        };
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        method.Should().NotBeNull();
        return (T)method!.Invoke(instance, args)!;
    }

    private static Action InvokingPrivate(object instance, string methodName, params object[] args)
    {
        return () => InvokePrivate<object>(instance, methodName, args);
    }

    private static IOptions<FeatureFlagOptions> NullFeatureOptions()
    {
        var options = new Mock<IOptions<FeatureFlagOptions>>();
        options.SetupGet(x => x.Value).Returns((FeatureFlagOptions)null!);
        return options.Object;
    }

    private static Subscription CreateActiveSubscription(Guid tenantId, CommerceSubscriptionPlan plan)
    {
        var subscription = new Subscription(
            tenantId,
            plan.Id,
            Guid.NewGuid(),
            BillingCycle.Monthly,
            new Money(plan.MonthlyPriceInCents / 100m),
            SystemClock.UtcNow.AddDays(-1))
        {
            Id = Guid.NewGuid(),
            Plan = plan
        };
        subscription.Activate();
        return subscription;
    }

    private static FeaturesCoverageDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FeaturesCoverageDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FeaturesCoverageDbContext(options);
    }

    private sealed class FeaturesCoverageDbContext(DbContextOptions<FeaturesCoverageDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantCapability>();
            modelBuilder.Entity<CapabilityAuditLog>();
            modelBuilder.Entity<FeatureFlag>();
            modelBuilder.Entity<FeatureFlagTarget>();
            modelBuilder.Entity<FeatureFlagUsage>();

            modelBuilder.Entity<CommerceSubscriptionPlan>();
            modelBuilder.Entity<Subscription>()
                .OwnsOne(s => s.Amount);
            modelBuilder.Entity<Subscription>()
                .HasOne(s => s.Plan)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey("PlanId");
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Transactions are not needed for coverage tests.");
    }

    private sealed class NullToStringObject
    {
        public override string? ToString() => null;
    }
}
