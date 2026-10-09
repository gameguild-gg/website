using System.Reflection;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditRetentionPolicyTemplatesTests
{
    [Fact]
    public void CatalogIsValidWithExactlyOneBaselineAndResolvableInheritance()
    {
        AuditRetentionPolicyTemplates.ValidateCatalog();

        var all = AuditRetentionPolicyTemplates.All;
        Assert.True(all.Count >= 6);
        Assert.Equal(all.Count, all.Select(template => template.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Single(all, template => template.IsBaseline);
        var baseline = AuditRetentionPolicyTemplates.Baseline;
        Assert.Equal(AuditRetentionPolicyTemplates.BaselineId, baseline.Id);
        Assert.Null(baseline.BaseTemplateId);

        foreach (var template in all.Where(template => !template.IsBaseline))
        {
            Assert.Equal(baseline.Id, template.BaseTemplateId);
            Assert.NotEmpty(template.Obligations);
            Assert.All(template.Obligations, obligation =>
            {
                Assert.False(string.IsNullOrWhiteSpace(obligation.Source));
                Assert.InRange(obligation.MinimumRetentionDays, 1, 36500);
            });
        }

        var frameworks = all.Select(template => template.Framework).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("SOC 2", frameworks);
        Assert.Contains("ISO 27001", frameworks);
        Assert.Contains("GDPR", frameworks);
        Assert.Contains("HIPAA", frameworks);
        Assert.Contains("PCI DSS", frameworks);
        Assert.Contains("FedRAMP", frameworks);
    }

    [Fact]
    public void EveryTemplateMaterializesIntoAValidConfigurationRequest()
    {
        foreach (var template in AuditRetentionPolicyTemplates.All)
        {
            var exception = Record.Exception(() => AuditRetentionInputValidation.Validate(template.ToConfigurationRequest()));
            Assert.Null(exception);

            var request = template.ToConfigurationRequest();
            Assert.Equal("USD", request.Currency);
            Assert.Equal(4, request.TierPrices.Select(price => price.Tier).Distinct().Count());
            var baseline = request.Baseline;
            Assert.True(baseline.HotDays <= baseline.WarmUntilDays
                && baseline.WarmUntilDays <= baseline.ColdUntilDays
                && baseline.ColdUntilDays <= baseline.RetentionDays);
            Assert.Contains(request.Obligations, obligation => obligation.MinimumRetentionDays >= 400);
        }
    }

    [Fact]
    public void DerivedTemplatesNeverWeakenBaselineObligationFloors()
    {
        var baseline = AuditRetentionPolicyTemplates.Baseline.ToConfigurationRequest();

        foreach (var template in AuditRetentionPolicyTemplates.All.Where(template => !template.IsBaseline))
        {
            var request = template.ToConfigurationRequest();
            foreach (var baselineObligation in baseline.Obligations)
            {
                var derived = request.Obligations.SingleOrDefault(obligation =>
                    string.Equals(obligation.Name, baselineObligation.Name, StringComparison.OrdinalIgnoreCase));
                Assert.NotNull(derived);
                Assert.True(derived.MinimumRetentionDays >= baselineObligation.MinimumRetentionDays,
                    $"Template '{template.Id}' weakens baseline obligation '{baselineObligation.Name}'.");
            }
        }
    }

    [Fact]
    public void SensitivityRetentionIsMonotonicAndBounded()
    {
        foreach (var template in AuditRetentionPolicyTemplates.All)
        {
            var rules = template.SensitivityRules.OrderBy(rule => (int)rule.Sensitivity).ToArray();
            Assert.Equal(Enum.GetValues<SensitivityLevel>().Length, rules.Length);
            Assert.Equal(Enum.GetValues<SensitivityLevel>().OrderBy(level => (int)level),
                rules.Select(rule => rule.Sensitivity));

            var previousMinimum = 0;
            foreach (var rule in rules)
            {
                Assert.InRange(rule.MinimumRetentionDays, 1, 36500);
                Assert.False(string.IsNullOrWhiteSpace(rule.Rationale));
                if (rule.MaximumRetentionDays.HasValue)
                {
                    Assert.True(rule.MaximumRetentionDays.Value >= rule.MinimumRetentionDays);
                }
                Assert.True(rule.MinimumRetentionDays >= previousMinimum,
                    $"Template '{template.Id}' lowers retention for {rule.Sensitivity}.");
                previousMinimum = rule.MinimumRetentionDays;
            }
        }
    }

    [Fact]
    public async Task InheritedConfigurationIsServedOnlyWhenRequestedAndNoExplicitConfigurationExists()
    {
        var tenant = Guid.NewGuid();
        var actors = new Mock<IActorContextAccessor>();
        actors.SetupGet(item => item.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User, SubjectId = Guid.NewGuid().ToString(), TenantId = tenant,
            IsAuthenticated = true, Roles = new HashSet<string> { "TenantAdmin" }, Permissions = new HashSet<string>()
        });
        var repository = new Mock<IAuditRetentionSimulationRepository>();
        repository.Setup(item => item.GetConfigurationAsync(tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditRetentionConfiguration?)null);
        var service = new AuditRetentionSimulationService(actors.Object, repository.Object, new Mock<IAuditRetentionDataSource>().Object,
            new AuditRetentionSimulationEngine(), new Mock<IAuditService>().Object, TimeProvider.System);

        Assert.Null(await service.GetConfigurationAsync(false, default));

        var inherited = await service.GetConfigurationAsync(true, default);
        Assert.NotNull(inherited);
        Assert.Equal(tenant, inherited.TenantId);
        Assert.Equal(0, inherited.Revision);
        Assert.Equal(AuditRetentionPolicyTemplates.BaselineId, inherited.InheritedFromTemplateId);
        Assert.Equal(AuditRetentionPolicyTemplates.Baseline.PublishedAtUtc, inherited.UpdatedAtUtc);
        Assert.Equal(AuditRetentionPolicyTemplates.Baseline.BaselineScenario.Name, inherited.Configuration.Baseline.Name);
        Assert.Equal(AuditRetentionPolicyTemplates.Baseline.Obligations.Count, inherited.Configuration.Obligations.Count);
        repository.Verify(item => item.GetConfigurationAsync(tenant, It.IsAny<CancellationToken>()), Times.Exactly(2));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExplicitConfigurationWinsOverInheritedBaseline()
    {
        var tenant = Guid.NewGuid();
        var actors = new Mock<IActorContextAccessor>();
        actors.SetupGet(item => item.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User, SubjectId = Guid.NewGuid().ToString(), TenantId = tenant,
            IsAuthenticated = true, Roles = new HashSet<string> { "TenantAdmin" }, Permissions = new HashSet<string>()
        });
        var stored = new AuditRetentionConfiguration
        {
            TenantId = tenant,
            Revision = 4,
            ConfigurationJson = System.Text.Json.JsonSerializer.Serialize(
                AuditRetentionSimulationEngineTests.Configuration(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
        };
        var repository = new Mock<IAuditRetentionSimulationRepository>();
        repository.Setup(item => item.GetConfigurationAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        var service = new AuditRetentionSimulationService(actors.Object, repository.Object, new Mock<IAuditRetentionDataSource>().Object,
            new AuditRetentionSimulationEngine(), new Mock<IAuditService>().Object, TimeProvider.System);

        var response = await service.GetConfigurationAsync(true, default);
        Assert.NotNull(response);
        Assert.Equal(4, response.Revision);
        Assert.Null(response.InheritedFromTemplateId);
    }

    [Fact]
    public async Task TemplateListingReturnsCatalogUnchanged()
    {
        var actors = new Mock<IActorContextAccessor>();
        actors.SetupGet(item => item.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User, SubjectId = Guid.NewGuid().ToString(), TenantId = Guid.NewGuid(),
            IsAuthenticated = true, Roles = new HashSet<string> { "TenantAdmin" }, Permissions = new HashSet<string>()
        });
        var service = new AuditRetentionSimulationService(actors.Object, new Mock<IAuditRetentionSimulationRepository>().Object,
            new Mock<IAuditRetentionDataSource>().Object, new AuditRetentionSimulationEngine(), new Mock<IAuditService>().Object,
            TimeProvider.System);

        var templates = await service.GetPolicyTemplatesAsync(default);
        Assert.Same(AuditRetentionPolicyTemplates.All, templates);
        Assert.Equal(AuditRetentionPolicyTemplates.BaselineId, templates[0].Id);
    }

    [Fact]
    public void ControllerExposesTheRetentionPolicyRouteAliases()
    {
        var routes = typeof(AuditRetentionSimulationController)
            .GetCustomAttributes<RouteAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();

        Assert.Contains("v{version:apiVersion}/audit/retention-policies", routes);
        Assert.Contains("api/audit/retention-policies", routes);
        Assert.Contains("v{version:apiVersion}/audit/retention-simulation", routes);
        Assert.Contains("api/audit/retention-simulation", routes);

        var templates = typeof(AuditRetentionSimulationController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == nameof(AuditRetentionSimulationController.GetPolicyTemplates));
        Assert.Contains(templates.GetCustomAttributes<HttpGetAttribute>(), attribute => attribute.Template == "templates");
        Assert.Contains(templates.GetCustomAttributes<ProducesResponseTypeAttribute>(),
            attribute => attribute.StatusCode == StatusCodes.Status200OK);
    }
}
